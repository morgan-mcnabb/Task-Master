using Domain.Tasks;

namespace TaskMasterApi.Contracts.Tasks;

public sealed class CreateTaskRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public TaskPriority Priority { get; init; } = TaskPriority.Medium;
    public DateOnly? DueDate { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}