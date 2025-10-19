using Domain.Common.Abstract;

namespace Domain.Tasks;

public sealed class TaskItem : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerUserId { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateOnly? DueDate { get; set; }

    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();

    public bool IsArchived => Status == TaskStatus.Archived;

    public void MarkTodo()
    {
        Status = TaskStatus.Todo;
        CompletedAtUtc = null;
    }

    public void MarkInProgress()
    {
        Status = TaskStatus.InProgress;
        CompletedAtUtc = null;
    }

    public void MarkDone(DateTime utcNow)
    {
        Status = TaskStatus.Done;
        CompletedAtUtc = utcNow;
    }

    public void Archive()
    {
        Status = TaskStatus.Archived;
        CompletedAtUtc = null;
    }

    public void Unarchive()
    {
        Status = TaskStatus.Todo;
        CompletedAtUtc = null;
    }

    public void UpdateCoreDetails(string newTitle, string? newDescription, TaskPriority newPriority, DateOnly? newDueDate)
    {
        Title = newTitle;
        Description = newDescription;
        Priority = newPriority;
        DueDate = newDueDate;
    }

    public static TaskItem Create(string ownerUserId, string title, string? description, TaskPriority priority, DateOnly? dueDate, DateTime utcNow)
    {
        return new TaskItem
        {
            OwnerUserId = ownerUserId,
            Title = title,
            Description = description,
            Priority = priority,
            DueDate = dueDate,
            Status = TaskStatus.Todo,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };
    }
}
