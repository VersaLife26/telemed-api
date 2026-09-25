using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Scalar.AspNetCore;
using TeleMed.Api;
using TeleMed.Api.Auth;
using TeleMed.Api.ErrorHandling;
using TeleMed.Api.Hubs;
using TeleMed.Api.OpenApi;
using TeleMed.Api.RateLimiting;
using TeleMed.Api.Validation;
using TeleMed.Application;
using TeleMed.Application.Abstractions;
using TeleMed.Application.Consultations;
using TeleMed.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers(o =>
    {
        o.Conventions.Add(new RoutePrefixConvention("api/v1"));
        o.Filters.Add<ValidationFilter>();
    })
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));

builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddSingleton<ConsultationRooms>();
builder.Services.AddSingleton<IConsultationPresence>(sp => sp.GetRequiredService<ConsultationRooms>());
builder.Services.AddSingleton<IConsultationRealtime, ConsultationRealtime>();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions.TryAdd("traceId", ctx.HttpContext.TraceIdentifier));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddTeleMedOpenApi();

builder.Services.AddJwtAuthentication();
builder.Services.AddAdminAuthentication();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();
builder.Services.AddTeleMedCors(builder.Configuration);
builder.Services.AddClientIpRateLimiting(builder.Configuration);

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    if (builder.Configuration.GetValue<bool>("ForwardedHeaders:TrustCloudflare"))
    {
        // Only safe when the origin is reachable solely through Cloudflare (e.g. a tunnel): any peer may set this header.
        o.ForwardedForHeaderName = "CF-Connecting-IP";
        o.ForwardLimit = 1;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    }
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments(AdminAuthentication.PathPrefix),
    admin => admin.UseMiddleware<AdminIpAllowlistMiddleware>().UseMiddleware<AdminOriginCheckMiddleware>());
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.MapControllers().RequireAuthorization();
app.MapHub<ConsultationHub>(ConsultationService.HubPath);

app.Run();

public partial class Program;
