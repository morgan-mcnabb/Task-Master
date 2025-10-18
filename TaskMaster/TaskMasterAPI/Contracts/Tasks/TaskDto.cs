using Domain.Tasks;
using TaskMasterApi.Contracts.Tags;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace TaskMasterApi.Contracts.Tasks;

public sealed class TaskDto
{
    public Guid Id { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }

    public TaskStatus Status { get; init; }
    public TaskPriority Priority { get; init; }
    public DateOnly? DueDate { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }

    public bool IsArchived { get; init; }

    public string? ETag { get; init; }

    public IReadOnlyList<TagDto> Tags { get; init; } = Array.Empty<TagDto>();
}