using Domain.Tasks;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace Application.Tasks.Models;

public sealed class UpdateTaskModel
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public TaskPriority? Priority { get; init; }
    public DateOnly? DueDate { get; init; }

    public TaskStatus? Status { get; init; }

    public IReadOnlyCollection<string>? Tags { get; init; }

    public byte[]? IfMatchRowVersion { get; init; }
}