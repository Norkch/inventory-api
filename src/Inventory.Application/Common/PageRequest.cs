using Inventory.Domain.Exceptions;

namespace Inventory.Application.Common;

public sealed class PageRequest
{
    public const int DefaultPage = 1;
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    private PageRequest(int page, int pageSize)
    {
        Page = page;
        PageSize = pageSize;
    }

    public int Page { get; }

    public int PageSize { get; }

    public static PageRequest Create(int? page, int? pageSize)
    {
        var validPage = ValidatePage(page ?? DefaultPage);
        var validPageSize = ValidatePageSize(pageSize ?? DefaultPageSize);

        return new PageRequest(validPage, validPageSize);
    }

    private static int ValidatePage(int page)
    {
        if (page < 1)
        {
            throw new ValidationException("El parámetro 'page' debe ser un número entero mayor o igual que 1.");
        }

        return page;
    }

    private static int ValidatePageSize(int pageSize)
    {
        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ValidationException($"El parámetro 'pageSize' debe ser un número entero entre 1 y {MaxPageSize}.");
        }

        return pageSize;
    }
}
