using System.Text.Json;
using Inventory.Api.Middleware;
using Inventory.Application.Movements;
using Inventory.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.UnitTests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private const string ProblemJsonContentType = "application/problem+json";

    public static TheoryData<Exception, int, string> KnownExceptions => new()
    {
        { new ValidationException("El nombre de la categoría es obligatorio."), StatusCodes.Status400BadRequest, "La petición no es válida." },
        { new NotFoundException("No existe una categoría activa con el id indicado."), StatusCodes.Status404NotFound, "Recurso no encontrado." },
        { new ConflictException("Ya existe una categoría con el nombre 'Tornillería'."), StatusCodes.Status409Conflict, "Conflicto con el estado actual del recurso." },
        { new InsufficientStockException(MovementMessages.InsufficientStock), StatusCodes.Status409Conflict, "Conflicto con el estado actual del recurso." }
    };

    [Theory]
    [MemberData(nameof(KnownExceptions))]
    public async Task InvokeAsync_WritesProblemDetailsWithTheExceptionMessage_WhenKnownExceptionIsThrown(Exception exception, int expectedStatus, string expectedTitle)
    {
        var context = CreateHttpContext();
        await CreateMiddleware(_ => throw exception).InvokeAsync(context);
        var problem = ReadProblem(context);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.StartsWith(ProblemJsonContentType, context.Response.ContentType);
        Assert.Equal(expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
        Assert.Equal(exception.Message, problem.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task InvokeAsync_WritesGenericProblemDetailsWithoutTheExceptionMessage_WhenUnexpectedExceptionIsThrown()
    {
        var exception = new InvalidOperationException("Login failed for user 'test-user'. Server=test-server;Password=not-a-real-password");
        var context = CreateHttpContext();
        await CreateMiddleware(_ => throw exception).InvokeAsync(context);
        var body = ReadBody(context);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.StartsWith(ProblemJsonContentType, context.Response.ContentType);
        Assert.Equal(StatusCodes.Status500InternalServerError, problem.GetProperty("status").GetInt32());
        Assert.Equal("Error interno del servidor.", problem.GetProperty("title").GetString());
        Assert.Equal("Se produjo un error inesperado. Inténtalo de nuevo más tarde.", problem.GetProperty("detail").GetString());
        Assert.DoesNotContain(exception.Message, body);
        Assert.DoesNotContain("Password", body);
    }

    [Fact]
    public async Task InvokeAsync_LeavesTheResponseUntouched_WhenNoExceptionIsThrown()
    {
        var context = CreateHttpContext();
        await CreateMiddleware(WriteCreatedResponseAsync).InvokeAsync(context);
        Assert.Equal(StatusCodes.Status201Created, context.Response.StatusCode);
        Assert.Equal("text/plain", context.Response.ContentType);
        Assert.Equal("creado", ReadBody(context));
    }

    private static async Task WriteCreatedResponseAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status201Created;
        context.Response.ContentType = "text/plain";
        await context.Response.WriteAsync("creado");
    }

    private static ExceptionHandlingMiddleware CreateMiddleware(RequestDelegate next)
    {
        return new ExceptionHandlingMiddleware(next, NullLogger<ExceptionHandlingMiddleware>.Instance);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        return context;
    }

    private static JsonElement ReadProblem(HttpContext context)
    {
        return JsonDocument.Parse(ReadBody(context)).RootElement;
    }

    private static string ReadBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);

        return reader.ReadToEnd();
    }
}
