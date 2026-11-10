using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
            Detail = DescribeErrors(context.ModelState)
        };
        var result = new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
        result.ContentTypes.Add(ProblemJsonContentType);

        return result;
    }

    private static string DescribeErrors(ModelStateDictionary modelState)
    {
        var invalidKeys = modelState.Where(entry => entry.Value?.Errors.Count > 0).Select(entry => entry.Key).ToList();
        if (invalidKeys.Any(IsRequestBodyKey))
        {
            return InvalidJsonDetail;
        }

        return string.Join(" ", invalidKeys.Select(DescribeInvalidParameter));
    }

    private static bool IsRequestBodyKey(string key)
    {
        return key.Length == 0 || key.StartsWith(JsonBodyKeyPrefix, StringComparison.Ordinal);
    }

    private static string DescribeInvalidParameter(string key)
    {
        var parameterName = char.ToLowerInvariant(key[0]) + key.Substring(1);

        return $"El parámetro '{parameterName}' debe ser un número entero.";
    }
}
