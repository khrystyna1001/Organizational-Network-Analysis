using Microsoft.AspNetCore.Mvc;
using ONA.Identity.Domain.Exceptions;
using System.Text.Json;

namespace ONA.Identity.Presentation.Middleware;

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
        catch (DomainException exception)
        {
            var status = exception switch
            {
                UserNotFoundException => StatusCodes.Status404NotFound,
                DuplicateEmailException => StatusCodes.Status409Conflict,
                InvalidCredentialsException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status422UnprocessableEntity
            };
            logger.LogInformation("Domain rule {Code} rejected the request: {Message}", exception.Code, exception.Message);
            if (context.Response.HasStarted)
            {
                throw;
            }

            var problem = new ProblemDetails
            {
                Status = status,
                Title = status == StatusCodes.Status422UnprocessableEntity ? "A domain rule was violated." : exception.Message,
                Detail = exception.Message,
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = exception.Code;
            await WriteAsync(context, problem);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unhandled exception occurred while processing the request.");
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteAsync(context, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Instance = context.Request.Path
            });
        }
    }

    private static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.Clear();
        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, ProblemJsonOptions), context.RequestAborted);
    }
}
