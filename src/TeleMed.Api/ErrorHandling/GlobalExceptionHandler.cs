using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TeleMed.Application.Common.Exceptions;

namespace TeleMed.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            ValidationException ex => new HttpValidationProblemDetails(ToErrors(ex)) { Status = StatusCodes.Status400BadRequest },
            BadRequestException ex => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = ex.Message,
                Extensions = { ["code"] = ex.Code },
            },
            UnauthorizedException ex => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Detail = ex.Message,
                Extensions = { ["code"] = ex.Code },
            },
            ForbiddenException { Code: { } code } ex => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Detail = ex.Message,
                Extensions = { ["code"] = code },
            },
            ForbiddenException ex => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Detail = ex.Message },
            NotFoundException ex => new ProblemDetails { Status = StatusCodes.Status404NotFound, Detail = ex.Message },
            ConflictException ex => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Detail = ex.Message,
                Extensions = new Dictionary<string, object?>(ex.Extensions) { ["code"] = ex.Code },
            },
            TooManyRequestsException ex => new ProblemDetails { Status = StatusCodes.Status429TooManyRequests, Detail = ex.Message },
            ServiceUnavailableException ex => new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable, Detail = ex.Message },
            _ => null,
        };
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static Dictionary<string, string[]> ToErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(e => CamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

    private static string CamelCase(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
