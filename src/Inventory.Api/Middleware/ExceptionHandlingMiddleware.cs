using Inventory.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private const string ProblemJsonContentType = "application/problem+json";
    private const string UnexpectedErrorDetail = "Se produjo un error inesperado. Inténtalo de nuevo más tarde.";

    private static readonly IReadOnlyDictionary<int, (string Type, string Title)> ProblemTypes = new Dictionary<int, (string Type, string Title)>
    {
        [StatusCodes.Status400BadRequest] = ("https://tools.ietf.org/html/rfc7231#section-6.5.1", "La petición no es válida."),
        [StatusCodes.Status404NotFound] = ("https://tools.ietf.org/html/rfc7231#section-6.5.4", "Recurso no encontrado."),
        [StatusCodes.Status409Conflict] = ("https://tools.ietf.org/html/rfc7231#section-6.5.8", "Conflicto con el estado actual del recurso."),
        [StatusCodes.Status500InternalServerError] = ("https://tools.ietf.org/html/rfc7231#section-6.6.1", "Error interno del servidor.")
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception) when (!context.Response.HasStarted)
        {
            await WriteProblemDetailsAsync(context, exception);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception exception)
    {
        var problem = CreateProblemDetails(exception);
        LogIfUnexpected(problem, exception);
        context.Response.Clear();
        context.Response.StatusCode = problem.Status!.Value;
        await context.Response.WriteAsJsonAsync(problem, options: null, contentType: ProblemJsonContentType, context.RequestAborted);
    }

    private static ProblemDetails CreateProblemDetails(Exception exception)
    {
        var status = MapStatusCode(exception);
        var problemType = ProblemTypes[status];

        return new ProblemDetails
        {
            Type = problemType.Type,
            Title = problemType.Title,
            Status = status,
            Detail = status == StatusCodes.Status500InternalServerError ? UnexpectedErrorDetail : exception.Message
        };
    }

    private static int MapStatusCode(Exception exception)
    {
        return exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private void LogIfUnexpected(ProblemDetails problem, Exception exception)
    {
        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception while processing the request.");
        }
    }
}
