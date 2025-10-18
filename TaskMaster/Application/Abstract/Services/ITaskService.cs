using Domain.Common;
using Domain.Tasks;
using Domain.Tasks.Queries;
using Application.Tasks.Models;

namespace Application.Abstract.Services;

public interface ITaskService
{
    Task<PagedResult<TaskItem>> SearchAsync(TaskQuery criteria, CancellationToken cancellationToken);

    Task<TaskItem?> GetByIdAsync(Guid taskId, bool includeTags, CancellationToken cancellationToken);

    Task<TaskItem> CreateAsync(CreateTaskModel model, CancellationToken cancellationToken);

    Task<TaskItem> UpdateAsync(Guid taskId, UpdateTaskModel model, CancellationToken cancellationToken);

    Task DeleteAsync(Guid taskId, byte[]? ifMatchRowVersion, CancellationToken cancellationToken);
}