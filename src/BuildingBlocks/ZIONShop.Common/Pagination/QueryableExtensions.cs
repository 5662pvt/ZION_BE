using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;

namespace ZIONShop.Common.Pagination;

public static class QueryableExtensions
{
    public static IQueryable<T> ApplySorting<T>(this IQueryable<T> source, string? sort)
    {
        if (string.IsNullOrWhiteSpace(sort)) return source;
        try
        {
            return source.OrderBy(sort);
        }
        catch
        {
            return source;
        }
    }

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> source, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var total = await source.CountAsync(cancellationToken);
        var items = await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return PagedResult<T>.Create(items, page, pageSize, total);
    }
}
