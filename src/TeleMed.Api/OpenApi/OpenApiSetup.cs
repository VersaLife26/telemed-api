using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;
using TeleMed.Api.Auth;

namespace TeleMed.Api.OpenApi;

// Error responses are added by rule rather than declared per action: every error leaves through GlobalExceptionHandler
// or the auth/rate-limit middleware as application/problem+json, so the rules below mirror where each status can arise.
internal static class OpenApiSetup
{
    private const string ProblemSchema = "ProblemDetails";
    private const string UserBearer = "userBearer";
    private const string AdminCloudflare = "adminCloudflareAccess";
    private const string AdminLocal = "adminLocalJwt";
    private const string TestSecret = "testSecret";
    private const string ProblemContentType = "application/problem+json";

    private static readonly string[] RedundantRequestTypes = ["text/json", "application/*+json"];
    private static readonly string[] RedundantResponseTypes = ["text/json", "text/plain"];

    public static IServiceCollection AddTeleMedOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(o =>
        {
            o.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "TeleMed API",
                    Version = "v1",
                    Description = "camelCase JSON, enums as camelCase strings, money as integer cents plus an ISO currency. "
                        + "Lists return {items, page, pageSize, total}. Errors are RFC 9457 problem details with an optional `code` "
                        + "and, for validation failures, `errors` keyed by camelCase field path.",
                };
                document.Servers = [];
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    [UserBearer] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Patient or doctor access token from /api/v1/auth/*.",
                    },
                    [AdminCloudflare] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header,
                        Name = AdminAuthentication.CloudflareHeader,
                        Description = "Cloudflare Access JWT (header or CF_Authorization cookie); its email must match an active admin user.",
                    },
                    [AdminLocal] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "Local admin JWT (HS256, iss telemed-admin-local, aud telemed-admin, email claim); only when AdminAuth:LocalJwt:Enabled.",
                    },
                    [TestSecret] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.ApiKey,
                        In = ParameterLocation.Header,
                        Name = TestEndpointFilter.SecretHeader,
                        Description = "Testing:SharedSecret; test endpoints are 404 unless their switch is on.",
                    },
                };
                document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
                document.Components.Schemas[ProblemSchema] = Problem();
                return Task.CompletedTask;
            });

            o.AddOperationTransformer((operation, context, _) =>
            {
                var action = (ControllerActionDescriptor)context.Description.ActionDescriptor;
                var metadata = action.EndpointMetadata;
                operation.OperationId = $"{action.ControllerName}_{action.ActionName}";

                // Query binding is case-insensitive, so camelCase names are accurate; computed read-only properties
                // such as PageQuery.Skip are listed by ApiExplorer but can never be bound.
                var readOnly = context.Description.ParameterDescriptions
                    .Where(p => p.Source == BindingSource.Query && p.ModelMetadata is { MetadataKind: ModelMetadataKind.Property, IsReadOnly: true })
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.Ordinal);
                if (operation.Parameters is { } parameters)
                {
                    foreach (var parameter in parameters.Where(p => p.In == ParameterLocation.Query && readOnly.Contains(p.Name!)).ToList())
                    {
                        parameters.Remove(parameter);
                    }

                    foreach (var parameter in parameters.OfType<OpenApiParameter>().Where(p => p.In == ParameterLocation.Query))
                    {
                        parameter.Name = JsonNamingPolicy.CamelCase.ConvertName(parameter.Name!);
                    }
                }

                Trim(operation.RequestBody?.Content, RedundantRequestTypes);
                foreach (var response in operation.Responses?.Values ?? Enumerable.Empty<IOpenApiResponse>())
                {
                    Trim(response.Content, RedundantResponseTypes);
                }

                var document = context.Document!;
                var test = metadata.OfType<TestEndpointAttribute>().FirstOrDefault();
                var admin = metadata.OfType<AuthorizeAttribute>().Any(a => a.AuthenticationSchemes == AdminAuthentication.Scheme);
                var anonymous = metadata.OfType<IAllowAnonymous>().Any();
                operation.Security = [];
                if (test is not null)
                {
                    var requirement = new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(TestSecret, document)] = [] };
                    if (Equals(test.Arguments?.FirstOrDefault(), TestFeature.InstantMeetings))
                    {
                        requirement[new OpenApiSecuritySchemeReference(UserBearer, document)] = [];
                    }

                    operation.Security.Add(requirement);
                }
                else if (admin)
                {
                    operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(AdminCloudflare, document)] = [] });
                    operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(AdminLocal, document)] = [] });
                }
                else if (!anonymous)
                {
                    operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(UserBearer, document)] = [] });
                }

                operation.Responses ??= [];
                var method = context.Description.HttpMethod ?? "GET";
                var hasInput = operation.RequestBody is not null || (operation.Parameters?.Any(p => p.In == ParameterLocation.Query) ?? false);
                AddProblem(operation, document, StatusCodes.Status400BadRequest, "Validation failed or the request is malformed.", hasInput);
                AddProblem(operation, document, StatusCodes.Status401Unauthorized, "Missing, expired or revoked credentials.", test is not null || admin || !anonymous);
                AddProblem(operation, document, StatusCodes.Status403Forbidden, "The caller may not do this.", admin || !anonymous);
                AddProblem(operation, document, StatusCodes.Status404NotFound, "Not found, or not visible to the caller.", context.Description.RelativePath?.Contains('{') == true || test is not null);
                AddProblem(operation, document, StatusCodes.Status409Conflict, "The current state does not allow this; see `code`.", !HttpMethods.IsGet(method));
                AddProblem(operation, document, StatusCodes.Status429TooManyRequests, "Rate limited.", metadata.OfType<EnableRateLimitingAttribute>().Any());
                return Task.CompletedTask;
            });

            // System.Text.Json's web defaults also accept numbers as strings, which the schema generator reports as a
            // string-or-integer with a digits pattern; clients should send plain numbers, so the schema says just that.
            // Enums are camelCase strings. A nullable enum property already says so with oneOf [null, $ref], but the generator
            // also leaks that null into the shared enum component.
            o.AddSchemaTransformer((schema, context, _) =>
            {
                var clrType = context.JsonTypeInfo.Type;
                if ((Nullable.GetUnderlyingType(clrType) ?? clrType).IsEnum && schema.Enum is { Count: > 0 } values)
                {
                    schema.Type = JsonSchemaType.String;
                    schema.Enum = values.Where(v => v is not null).ToList();
                }

                return Task.CompletedTask;
            });

            o.AddSchemaTransformer((schema, _, _) =>
            {
                if (schema.Type is { } type
                    && type.HasFlag(JsonSchemaType.String)
                    && (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number)))
                {
                    schema.Type = type & ~JsonSchemaType.String;
                    schema.Pattern = null;
                }

                return Task.CompletedTask;
            });
        });

    private static void Trim(IDictionary<string, OpenApiMediaType>? content, string[] redundant)
    {
        if (content is null || !content.ContainsKey("application/json"))
        {
            return;
        }

        foreach (var type in redundant)
        {
            content.Remove(type);
        }
    }

    private static void AddProblem(OpenApiOperation operation, OpenApiDocument document, int status, string description, bool applies)
    {
        var key = status.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!applies || operation.Responses!.ContainsKey(key))
        {
            return;
        }

        operation.Responses[key] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [ProblemContentType] = new() { Schema = new OpenApiSchemaReference(ProblemSchema, document) },
            },
        };
    }

    private static OpenApiSchema Problem() => new()
    {
        Type = JsonSchemaType.Object,
        Description = "RFC 9457 problem details. `code` is a stable machine-readable reason (e.g. slot_unavailable); "
            + "`errors` maps camelCase field paths to messages on validation failures; other extension members may appear.",
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["type"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            ["title"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer | JsonSchemaType.Null, Format = "int32" },
            ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String | JsonSchemaType.Null },
            ["code"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["errors"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
            },
            ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String },
        },
        AdditionalProperties = new OpenApiSchema(),
    };
}
