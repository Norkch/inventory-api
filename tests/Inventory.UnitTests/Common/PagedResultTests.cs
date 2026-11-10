using Inventory.Application.Common;

namespace Inventory.UnitTests.Common;

public sealed class PagedResultTests
{
    [Fact]
    public void BR18_TotalPages_IsZero_WhenThereAreNoItems()
    {
        var result = new PagedResult<string>(Array.Empty<string>(), PageRequest.Create(null, null), 0);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public void BR18_TotalPages_RoundsUp_WhenLastPageIsPartial()
    {
        var result = new PagedResult<string>(new[] { "Adhesivos" }, PageRequest.Create(1, 20), 25);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public void BR18_TotalPages_IsExact_WhenItemsFillTheLastPage()
    {
        var result = new PagedResult<string>(new[] { "Adhesivos" }, PageRequest.Create(1, 20), 40);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public void BR18_KeepsItemsPageAndPageSize_FromTheRequest()
    {
        var items = new[] { "Adhesivos", "Pinturas" };
        var result = new PagedResult<string>(items, PageRequest.Create(2, 10), 12);
        Assert.Equal(items, result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(12, result.TotalCount);
    }

    [Fact]
    public void BR18_KeepsRequestedPage_WhenPageIsOutOfRange()
    {
        var result = new PagedResult<string>(Array.Empty<string>(), PageRequest.Create(9, 20), 25);
        Assert.Empty(result.Items);
        Assert.Equal(9, result.Page);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }
}
