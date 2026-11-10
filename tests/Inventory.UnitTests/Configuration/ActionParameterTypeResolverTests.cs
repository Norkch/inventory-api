using Inventory.Api.Configuration;
using Inventory.Api.Contracts;
using Inventory.UnitTests.Fakes;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Inventory.UnitTests.Configuration;

public sealed class ActionParameterTypeResolverTests
{
    [Fact]
    public void Resolve_ReturnsTheUnderlyingTypeOfAPropertyOfAComplexParameter()
    {
        Assert.Equal(typeof(Guid), ActionParameterTypeResolver.Resolve(CreateAction(typeof(ListProductsRequest)), "categoryId"));
    }

    [Fact]
    public void Resolve_ReturnsTheTypeOfASimpleParameterByItsName()
    {
        Assert.Equal(typeof(int), ActionParameterTypeResolver.Resolve(CreateAction(typeof(int?), "pageSize"), "PageSize"));
    }

    [Fact]
    public void Resolve_IgnoresPropertiesOfSimpleParameters()
    {
        Assert.Null(ActionParameterTypeResolver.Resolve(CreateAction(typeof(string), "search"), "length"));
    }

    [Fact]
    public void Resolve_AcceptsAKeyPrefixedWithTheParameterName()
    {
        Assert.Equal(typeof(Guid), ActionParameterTypeResolver.Resolve(CreateAction(typeof(ListProductsRequest)), "request.categoryId"));
    }

    [Fact]
    public void Resolve_DoesNotThrow_WhenTwoPropertiesDifferOnlyInCase()
    {
        Assert.Equal(typeof(int), ActionParameterTypeResolver.Resolve(CreateAction(typeof(CaseCollidingRequest)), "value"));
    }

    private static ActionDescriptor CreateAction(Type parameterType, string parameterName = "request")
    {
        var parameter = new ControllerParameterDescriptor { Name = parameterName, ParameterType = parameterType };

        return new ActionDescriptor { Parameters = new List<ParameterDescriptor> { parameter } };
    }
}
