using Itmas3.Application.Common;

namespace Itmas3.Api.Controllers.Authorization;

internal static class PagingDefaults
{
    public const int MaxPageSize = 100;

    public static PageRequest Page(int page, int pageSize) => new(page, pageSize);
}
