using Microsoft.AspNetCore.Mvc;
using EchoAPI.Graph.Domain.Exceptions;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace EchoAPI.Graph.Presentation.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions ProblemJsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BoardNotFoundException exception)
        {
            await WriteDomainProblemAsync(
                context,
                exception,
                StatusCodes.Status404NotFound,
                logger);
        }
        catch (NodeNotFoundException exception)
        {
            await WriteDomainProblemAsync(
                context,
                exception,
                StatusCodes.Status404NotFound,
                logger);
        }
        catch (DomainException exception)
        {
            await WriteDomainProblemAsync(
                context,
                exception,
                StatusCodes.Status422UnprocessableEntity,
                logger);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An unhandled exception occurred while processing the request.");
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await WriteProblemAsync(
                context,
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Instance = context.Request.Path
                });
        }
    }

    private static async Task WriteDomainProblemAsync(
        HttpContext context,
        DomainException exception,
        int statusCode,
        ILogger logger)
    {
        logger.LogInformation(
            "Domain rule {DomainErrorCode} rejected the request: {DomainErrorMessage}",
            exception.Code,
            exception.Message);
        if (context.Response.HasStarted)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status404NotFound
                ? "The requested resource was not found."
                : "A domain rule was violated.",
            Detail = exception.Message,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = exception.Code;
        await WriteProblemAsync(context, problem);
    }

    private static Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.ContentType = "application/problem+json";
        var json = JsonSerializer.Serialize(problem, ProblemJsonOptions);
        return context.Response.WriteAsync(json, context.RequestAborted);
    }
}
