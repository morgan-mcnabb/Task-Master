using Domain.Tasks;

namespace Application.Tasks.Models;

public sealed class CreateTaskModel
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public TaskPriority Priority { get; init; } = TaskPriority.Medium;
    public DateOnly? DueDate { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = Array.Empty<string>();
}