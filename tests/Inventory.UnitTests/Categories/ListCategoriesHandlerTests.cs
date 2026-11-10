using Inventory.Application.Categories;
using Inventory.Domain.Exceptions;
using Inventory.UnitTests.Fakes;

namespace Inventory.UnitTests.Categories;

public sealed class ListCategoriesHandlerTests
{
    private readonly FakeCategoryReadRepository _readRepository = new();

    [Fact]
    public async Task BR18_Lists_FirstPageOfTwenty_WhenParametersAreMissing()
    {
        AddActiveCategories(25);
        var result = await CreateHandler().HandleAsync(new ListCategoriesQuery(null, null), CancellationToken.None);
        Assert.Equal(20, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    [Fact]
    public async Task BR18_PassesRequestedPageToReadPort()
    {
        await CreateHandler().HandleAsync(new ListCategoriesQuery(3, 10), CancellationToken.None);
        Assert.Equal(3, _readRepository.ReceivedPageRequest?.Page);
        Assert.Equal(10, _readRepository.ReceivedPageRequest?.PageSize);
    }

    [Fact]
    public async Task BR18_Rejects_InvalidParametersBeforeQueryingReadPort()
    {
        await Assert.ThrowsAsync<ValidationException>(() => CreateHandler().HandleAsync(new ListCategoriesQuery(0, 101), CancellationToken.None));
        Assert.Null(_readRepository.ReceivedPageRequest);
    }

    private void AddActiveCategories(int count)
    {
        for (var index = 1; index <= count; index++)
        {
            _readRepository.ActiveCategories.Add(new CategoryDto(Guid.NewGuid(), $"Categoría {index:D2}", null, FakeClock.FixedUtcNow));
        }
    }

    private ListCategoriesHandler CreateHandler()
    {
        return new ListCategoriesHandler(_readRepository);
    }
}
