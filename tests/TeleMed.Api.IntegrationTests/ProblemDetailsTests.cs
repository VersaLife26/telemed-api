using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TeleMed.Api.ErrorHandling;
using TeleMed.Api.IntegrationTests.Infrastructure;
using TeleMed.Application.Common;
using TeleMed.Application.Common.Exceptions;

namespace TeleMed.Api.IntegrationTests;

public class ProblemDetailsTests(ApiFixture fixture) : IntegrationTest(fixture)
{
    [Fact]
    public async Task Unknown_api_route_returns_problem_details_404()
    {
        var response = await Factory.CreateClient().GetAsync("/api/v1/does-not-exist", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;
        body.GetProperty("status").GetInt32().ShouldBe(404);
        body.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Validation_exception_becomes_400_with_errors()
    {
        var failure = await new PageQueryValidator().ValidateAsync(new PageQuery { Page = 0, PageSize = 500 }, TestContext.Current.CancellationToken);

        var (status, body) = await HandleAsync(new ValidationException(failure.Errors));

        status.ShouldBe(400);
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.TryGetProperty("traceId", out _).ShouldBeTrue();
        var errors = body.GetProperty("errors");
        errors.GetProperty("page").GetArrayLength().ShouldBe(1);
        errors.GetProperty("pageSize").GetArrayLength().ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(DomainExceptions))]
    public async Task Application_exceptions_map_to_status_codes(Exception exception, int expected)
    {
        var (status, body) = await HandleAsync(exception);

        status.ShouldBe(expected);
        body.GetProperty("detail").GetString().ShouldBe(exception.Message);
    }

    [Fact]
    public async Task Conflict_includes_code()
    {
        var (_, body) = await HandleAsync(new ConflictException("slot_unavailable", "Taken."));

        body.GetProperty("code").GetString().ShouldBe("slot_unavailable");
    }

    [Fact]
    public async Task Unknown_exceptions_are_not_handled()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var handler = ActivatorUtilities.CreateInstance<GlobalExceptionHandler>(scope.ServiceProvider);

        var handled = await handler.TryHandleAsync(
            new DefaultHttpContext { RequestServices = scope.ServiceProvider },
            new InvalidOperationException(),
            TestContext.Current.CancellationToken);

        handled.ShouldBeFalse();
    }

    public static TheoryData<Exception, int> DomainExceptions() => new()
    {
        { new ForbiddenException("no"), 403 },
        { new NotFoundException("missing"), 404 },
        { new ConflictException("x", "clash"), 409 },
        { new TooManyRequestsException("slow down"), 429 },
    };

    private async Task<(int Status, JsonElement Body)> HandleAsync(Exception exception)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Response.Body = new MemoryStream();
        var handler = ActivatorUtilities.CreateInstance<GlobalExceptionHandler>(scope.ServiceProvider);

        (await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken)).ShouldBeTrue();

        context.Response.Body.Position = 0;
        return (context.Response.StatusCode, (await JsonDocument.ParseAsync(context.Response.Body)).RootElement);
    }
}
