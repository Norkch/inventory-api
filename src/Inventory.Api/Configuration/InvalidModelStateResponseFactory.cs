using Microsoft.AspNetCore.Mvc;

namespace Inventory.Api.Configuration;

public static class InvalidModelStateResponseFactory
{
    private const string ProblemJsonContentType = "application/problem+json";
    private const string BadRequestType = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
    private const string BadRequestTitle = "La petición no es válida.";
    private const string InvalidJsonDetail = "El cuerpo de la petición no es un JSON válido.";
    private const string JsonBodyKeyPrefix = "$";

    public static IActionResult Create(ActionContext context)
    {
        var problem = new ProblemDetails
        {
            Type = BadRequestType,
            Title = BadRequestTitle,
            Status = StatusCodes.Status400BadRequest,
            Detail = DescribeErrors(context)
        };
        var result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
        result.ContentTypes.Add(ProblemJsonContentType);

        return result;
    }

    private static string DescribeErrors(ActionContext context)
    {
        var invalidKeys = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0).Select(entry => entry.Key).ToList();
        if (invalidKeys.Any(IsRequestBodyKey))
        {
            return InvalidJsonDetail;
        }

        return string.Join(" ", invalidKeys.Select(key => DescribeInvalidParameter(context, key)));
    }

    private static bool IsRequestBodyKey(string key)
    {
        return key.Length == 0 || key.StartsWith(JsonBodyKeyPrefix, StringComparison.Ordinal);
    }

    private static string DescribeInvalidParameter(ActionContext context, string key)
    {
        var parameterName = char.ToLowerInvariant(key[0]) + key.Substring(1);
        var parameterType = ActionParameterTypeResolver.Resolve(context.ActionDescriptor, key);

        return $"El parámetro '{parameterName}' {DescribeExpectedFormat(parameterType)}";
    }

    private static string DescribeExpectedFormat(Type? parameterType)
    {
        if (parameterType == typeof(int))
        {
            return "debe ser un número entero.";
        }

        return parameterType == typeof(Guid) ? "debe ser un identificador (GUID) válido." : "no tiene un formato válido.";
    }
}
