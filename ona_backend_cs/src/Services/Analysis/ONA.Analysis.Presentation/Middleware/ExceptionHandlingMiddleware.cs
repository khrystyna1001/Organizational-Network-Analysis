using Microsoft.AspNetCore.Mvc;
using ONA.Analysis.Application.Ports;
using ONA.Analysis.Domain.Exceptions;
using System.Text.Json;

namespace ONA.Analysis.Presentation.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions ProblemJsonOptions =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            switch (exception)
            {
                case AnalysisRunNotFoundException or BoardNotFoundException:
                    await WriteAsync(context, StatusCodes.Status404NotFound,
                        "The requested resource was not found.", exception.Message, ((DomainException)exception).Code);
                    break;
                case DomainException domain:
                    await WriteAsync(context, StatusCodes.Status422UnprocessableEntity,
                        "A domain rule was violated.", domain.Message, domain.Code);
                    break;
                case GraphIntegrationException:
                    logger.LogWarning(exception, "Graph service integration failed.");
                    await WriteAsync(context, StatusCodes.Status502BadGateway,
                        "The Graph service is unavailable.", exception.Message, null);
                    break;
                default:
                    logger.LogError(exception, "An unhandled exception occurred while processing the request.");
                    await WriteAsync(context, StatusCodes.Status500InternalServerError,
                        "An unexpected error occurred.", null, null);
                    break;
            }
        }
    }

    private static Task WriteAsync(HttpContext context, int status, string title, string? detail, string? code)
    {
        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };
        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, ProblemJsonOptions), context.RequestAborted);
    }
}
