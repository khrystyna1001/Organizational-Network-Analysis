using Microsoft.AspNetCore.Mvc;
using ONA.Insights.Application.Ports;
using ONA.Insights.Domain.Exceptions;
using System.Text.Json;

namespace ONA.Insights.Presentation.Middleware;

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
            (int, string, string?) mapped = exception switch
            {
                RecommendationNotFoundException e =>
                    (StatusCodes.Status404NotFound, "The requested resource was not found.", e.Code),
                AnalysisRunNotFoundException e =>
                    (StatusCodes.Status404NotFound, "The requested resource was not found.", e.Code),
                DomainException e =>
                    (StatusCodes.Status422UnprocessableEntity, "A domain rule was violated.", e.Code),
                AnalysisIntegrationException =>
                    (StatusCodes.Status502BadGateway, "The Analysis service could not be reached or understood.", null),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", null)
            };

            var (status, title, code) = mapped;
            if (status >= 500)
            {
                logger.LogError(exception, "Request failed with {Status}.", status);
            }
            else
            {
                logger.LogInformation("Request rejected: {Message}", exception.Message);
            }

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
                Instance = context.Request.Path
            };
            if (code is not null)
            {
                problem.Extensions["code"] = code;
            }

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(problem, ProblemJsonOptions), context.RequestAborted);
        }
    }
}
