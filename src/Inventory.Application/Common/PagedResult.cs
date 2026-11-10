namespace Inventory.Application.Common;

public sealed class PagedResult<T>
{
    public PagedResult(IReadOnlyList<T> items, PageRequest pageRequest, int totalCount)
    {
        Items = items;
        Page = pageRequest.Page;
        PageSize = pageRequest.PageSize;
        TotalCount = totalCount;
        TotalPages = CountPagesRoundingUp(totalCount, pageRequest.PageSize);
    }

    public IReadOnlyList<T> Items { get; }

    public int Page { get; }

    public int PageSize { get; }

    public int TotalCount { get; }

    public int TotalPages { get; }

    private static int CountPagesRoundingUp(int totalCount, int pageSize)
    {
        return (totalCount + pageSize - 1) / pageSize;
    }
}
