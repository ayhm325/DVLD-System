namespace Application.Common.Pagination;

public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } =
        Array.Empty<T>();

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public int TotalPages =>
        PageSize == 0
            ? 0
            : (int)Math.Ceiling(
                TotalCount / (double)PageSize);

    public bool HasPreviousPage =>
        PageNumber > 1;

    public bool HasNextPage =>
        PageNumber < TotalPages;

    public PagedResult<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return new PagedResult<TResult>
        {
            Items = Items.Select(mapper).ToList(),
            PageNumber = PageNumber,
            PageSize = PageSize,
            TotalCount = TotalCount
        };
    }
}