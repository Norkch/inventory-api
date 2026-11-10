using Inventory.Application.Common;
using Inventory.Domain.Exceptions;

namespace Inventory.UnitTests.Common;

public sealed class PageRequestTests
{
    [Fact]
    public void BR18_Create_UsesFirstPageAndTwentyItems_WhenParametersAreMissing()
    {
        var pageRequest = PageRequest.Create(null, null);
        Assert.Equal(1, pageRequest.Page);
        Assert.Equal(20, pageRequest.PageSize);
    }

    [Fact]
    public void BR18_Create_KeepsReceivedParameters_WhenTheyAreInRange()
    {
        var pageRequest = PageRequest.Create(9, 20);
        Assert.Equal(9, pageRequest.Page);
        Assert.Equal(20, pageRequest.PageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BR18_Create_Rejects_PageBelowOne(int page)
    {
        var exception = Assert.Throws<ValidationException>(() => PageRequest.Create(page, null));
        Assert.Equal("El parámetro 'page' debe ser un número entero mayor o igual que 1.", exception.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void BR18_Create_Rejects_PageSizeOutOfRange(int pageSize)
    {
        var exception = Assert.Throws<ValidationException>(() => PageRequest.Create(null, pageSize));
        Assert.Equal("El parámetro 'pageSize' debe ser un número entero entre 1 y 100.", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void BR18_Create_Accepts_PageSizeAtRangeLimits(int pageSize)
    {
        var pageRequest = PageRequest.Create(null, pageSize);
        Assert.Equal(pageSize, pageRequest.PageSize);
    }
}
