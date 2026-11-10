using Inventory.Api.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Inventory.UnitTests.Configuration;

public class InvalidModelStateResponseFactoryTests
{
    private const string ProblemJsonContentType = "application/problem+json";

    [Theory]
    [InlineData("$")]
    [InlineData("$.name")]
    [InlineData("")]
    public void Create_ReturnsBadRequestWithInvalidJsonDetail_WhenBodyCannotBeRead(string key)
    {
        var modelState = CreateModelStateWithError(key);
        modelState.AddModelError("request", "The request field is required.");
        var result = InvalidModelStateResponseFactory.Create(CreateActionContext(modelState));
        var problem = AssertBadRequestProblem(result);
        Assert.Equal("El cuerpo de la petición no es un JSON válido.", problem.Detail);
    }

    [Theory]
    [InlineData("pageSize")]
    [InlineData("PageSize")]
    public void Create_ReturnsBadRequestNamingTheParameter_WhenParameterIsNotAnInteger(string key)
    {
        var result = InvalidModelStateResponseFactory.Create(CreateActionContext(CreateModelStateWithError(key)));
        var problem = AssertBadRequestProblem(result);
        Assert.Equal("El parámetro 'pageSize' debe ser un número entero.", problem.Detail);
    }

    private static ProblemDetails AssertBadRequestProblem(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        Assert.Contains(ProblemJsonContentType, objectResult.ContentTypes);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("La petición no es válida.", problem.Title);

        return problem;
    }

    private static ModelStateDictionary CreateModelStateWithError(string key)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(key, "The value is not valid.");

        return modelState;
    }

    private static ActionContext CreateActionContext(ModelStateDictionary modelState)
    {
        return new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), modelState);
    }
}
