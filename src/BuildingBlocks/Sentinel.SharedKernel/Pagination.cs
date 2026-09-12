namespace Sentinel.SharedKernel;

public sealed record PageRequest
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public int Skip => Math.Max(0, (Page - 1) * PageSize);

    public PageRequest Normalize() => new()
    {
        Page = Page < 1 ? 1 : Page,
        PageSize = PageSize is < 1 or > 200 ? 25 : PageSize
    };
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
