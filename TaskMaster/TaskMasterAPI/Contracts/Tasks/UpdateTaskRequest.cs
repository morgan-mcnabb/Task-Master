using Domain.Tasks;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace TaskMasterApi.Contracts.Tasks;

public sealed class UpdateTaskRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public TaskPriority? Priority { get; init; }
    public DateOnly? DueDate { get; init; }
    public TaskStatus? Status { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public string? ETag { get; init; }
}