using Domain.Common;
using Domain.Tasks;
using Domain.Tasks.Queries;

namespace Domain.Abstract.Repositories;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, bool includeTags, bool asTracking, CancellationToken ct);

    Task<PagedResult<TaskItem>> SearchAsync(
        TaskQuery criteria,
        CancellationToken ct);

    Task AddAsync(TaskItem entity, CancellationToken ct);
    void Update(TaskItem entity);
    void Remove(TaskItem entity);
}