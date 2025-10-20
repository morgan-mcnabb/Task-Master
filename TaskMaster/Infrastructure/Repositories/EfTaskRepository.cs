using Domain.Abstract.Repositories;
using Domain.Common;
using Domain.Tasks;
using Domain.Tasks.Queries;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class EfTaskRepository(ApplicationDbContext db) : ITaskRepository
{
    public async Task<TaskItem?> GetByIdAsync(Guid id, bool includeTags, bool asTracking, CancellationToken ct)
    {
        IQueryable<TaskItem> queryable = db.Tasks;

        if (includeTags)
            queryable = queryable.Include(t => t.TaskTags).ThenInclude(tt => tt.Tag);

        if (!asTracking)
            queryable = queryable.AsNoTracking();

        return await queryable.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<PagedResult<TaskItem>> SearchAsync(TaskQuery query, CancellationToken ct)
    {
        var queryable = db.Tasks.AsQueryable();

        if (query.IncludeTags)
            queryable = queryable.Include(t => t.TaskTags).ThenInclude(tt => tt.Tag);

        // Filters
        if (query.Statuses is { Length: > 0 })
            queryable = queryable.Where(t => query.Statuses!.Contains(t.Status));

        if (query.Priorities is { Length: > 0 })
            queryable = queryable.Where(t => query.Priorities!.Contains(t.Priority));

        if (query.DueOnOrAfter.HasValue)
            queryable = queryable.Where(t => t.DueDate >= query.DueOnOrAfter);

        if (query.DueOnOrBefore.HasValue)
            queryable = queryable.Where(t => t.DueDate <= query.DueOnOrBefore);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            queryable = queryable.Where(t =>
                t.Title.Contains(term) ||
                (t.Description != null && t.Description.Contains(term)));
        }

        if (query.Tags is { Count: > 0 })
        {
            var normalized = query.Tags
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Select(n => n.Trim().ToUpperInvariant())
                .Distinct()
                .ToArray();

            if (normalized.Length > 0)
            {
                // Require ALL specified tags
                queryable = queryable.Where(t =>
                    t.TaskTags.Count(tt => normalized.Contains(tt.Tag.NormalizedName)) == normalized.Length);
            }
        }

        // Ordering (simple, numeric for enums)
        queryable = (query.SortBy, query.SortDirection) switch
        {
            (TaskSortBy.DueDate, SortDirection.Asc)   => queryable.OrderBy(t => t.DueDate).ThenByDescending(t => t.CreatedAtUtc),
            (TaskSortBy.DueDate, SortDirection.Desc)  => queryable.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.CreatedAtUtc),

            (TaskSortBy.Priority, SortDirection.Asc)  => queryable.OrderBy(t => t.Priority).ThenByDescending(t => t.CreatedAtUtc),
            (TaskSortBy.Priority, SortDirection.Desc) => queryable.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAtUtc),

            (TaskSortBy.Title, SortDirection.Asc)     => queryable.OrderBy(t => t.Title).ThenByDescending(t => t.CreatedAtUtc),
            (TaskSortBy.Title, SortDirection.Desc)    => queryable.OrderByDescending(t => t.Title).ThenByDescending(t => t.CreatedAtUtc),

            (TaskSortBy.Status, SortDirection.Asc)    => queryable.OrderBy(t => t.Status).ThenByDescending(t => t.CreatedAtUtc),
            (TaskSortBy.Status, SortDirection.Desc)   => queryable.OrderByDescending(t => t.Status).ThenByDescending(t => t.CreatedAtUtc),

            (TaskSortBy.CreatedAt, SortDirection.Asc) => queryable.OrderBy(t => t.CreatedAtUtc),
            _                                          => queryable.OrderByDescending(t => t.CreatedAtUtc),
        };

        // Paging
        var pageNumber = Math.Max(query.PageNumber, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await queryable.CountAsync(ct);
        var items = await queryable
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(ct);

        return new PagedResult<TaskItem>(items, totalCount, pageNumber, pageSize);
    }

    public Task AddAsync(TaskItem entity, CancellationToken ct)
    {
        db.Tasks.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TaskItem entity) => db.Tasks.Update(entity);

    public void Remove(TaskItem entity) => db.Tasks.Remove(entity);
}
