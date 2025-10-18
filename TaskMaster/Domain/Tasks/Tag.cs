using Domain.Common.Abstract;

namespace Domain.Tasks;

public sealed class Tag : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerUserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string NormalizedName { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<TaskTag> TaskTags { get; set; } = new List<TaskTag>();
}