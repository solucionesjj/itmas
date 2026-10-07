namespace Itmas3.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public sealed record PageRequest(int Page, int PageSize)
{
    public int Skip => (Page - 1) * PageSize;
}
