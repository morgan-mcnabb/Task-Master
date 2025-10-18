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
        IQueryable<TaskItem> q = db.Tasks;

        if (includeTags)
            q = q.Include(t => t.TaskTags).ThenInclude(tt => tt.Tag);

        if (!asTracking)
            q = q.AsNoTracking();

        return await q.FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    public async Task<PagedResult<TaskItem>> SearchAsync(TaskQuery query, CancellationToken ct)
    {
        var q = db.Tasks.AsQueryable();

        if (query.IncludeTags)
            q = q.Include(t => t.TaskTags).ThenInclude(tt => tt.Tag);

        // Filters
        if (query.Statuses is { Length: > 0 })
            q = q.Where(t => query.Statuses!.Contains(t.Status));

        if (query.Priorities is { Length: > 0 })
            q = q.Where(t => query.Priorities!.Contains(t.Priority));

        if (query.DueOnOrAfter.HasValue)
            q = q.Where(t => t.DueDate >= query.DueOnOrAfter);

        if (query.DueOnOrBefore.HasValue)
            q = q.Where(t => t.DueDate <= query.DueOnOrBefore);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(t => t.Title.Contains(term) ||
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
                q = q.Where(t => t.TaskTags.Count(tt => normalized.Contains(tt.Tag.NormalizedName)) == normalized.Length);
            }
        }

        q = (query.SortBy, query.SortDirection) switch
        {
            (TaskSortBy.DueDate, SortDirection.Asc)      => q.OrderBy(t => t.DueDate).ThenByDescending(t => t.CreatedAtUtc),
            (TaskSortBy.DueDate, SortDirection.Desc)     => q.OrderByDescending(t => t.DueDate).ThenByDescending(t => t.CreatedAtUtc),

            (TaskSortBy.Priority, SortDirection.Asc)     => q.OrderBy(t => t.Priority).ThenByDescending(t => t.CreatedAtUtc),
            (TaskSortBy.Priority, SortDirection.Desc)    => q.OrderByDescending(t => t.Priority).ThenByDescending(t => t.CreatedAtUtc),

            (TaskSortBy.Title, SortDirection.Asc)        => q.OrderBy(t => t.Title).ThenByDescending(t => t.CreatedAtUtc),
            (TaskSortBy.Title, SortDirection.Desc)       => q.OrderByDescending(t => t.Title).ThenByDescending(t => t.CreatedAtUtc),

            (TaskSortBy.CreatedAt, SortDirection.Asc)    => q.OrderBy(t => t.CreatedAtUtc),
            _                                            => q.OrderByDescending(t => t.CreatedAtUtc),
        };

        // Paging
        var page = Math.Max(query.PageNumber, 1);
        var size = Math.Clamp(query.PageSize, 1, 100);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size)
                           .Take(size)
                           .AsNoTracking()
                           .ToListAsync(ct);

        return new PagedResult<TaskItem>(items, total, page, size);
    }

    public Task AddAsync(TaskItem entity, CancellationToken ct)
    {
        db.Tasks.Add(entity);
        return Task.CompletedTask;
    }

    public void Update(TaskItem entity) => db.Tasks.Update(entity);

    public void Remove(TaskItem entity) => db.Tasks.Remove(entity);
}
