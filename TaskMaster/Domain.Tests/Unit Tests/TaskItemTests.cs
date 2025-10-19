using Domain.Common.Abstract;
using Domain.Tasks;
using FluentAssertions;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace Domain.Tests.Unit_Tests;

public sealed class TaskItemTests
{
    [Fact]
    public void Implements_IAuditableEntity()
    {
        var t = new TaskItem();
        (t is IAuditableEntity).Should().BeTrue();
    }

    [Fact]
    public void Create_Sets_Defaults_And_Fields()
    {
        var now = new DateTime(2025, 01, 01, 12, 00, 00, DateTimeKind.Utc);

        var task = TaskItem.Create(
            ownerUserId: "user-123",
            title: "Title",
            description: "desc",
            priority: TaskPriority.High,
            dueDate: new DateOnly(2025, 2, 1),
            utcNow: now);

        task.Id.Should().NotBeEmpty();
        task.OwnerUserId.Should().Be("user-123");
        task.Title.Should().Be("Title");
        task.Description.Should().Be("desc");
        task.Priority.Should().Be(TaskPriority.High);
        task.DueDate.Should().Be(new DateOnly(2025, 2, 1));

        task.Status.Should().Be(TaskStatus.Todo);
        task.CompletedAtUtc.Should().BeNull();

        task.CreatedAtUtc.Should().Be(now);
        task.UpdatedAtUtc.Should().Be(now);

        task.RowVersion.Should().NotBeNull();
        task.RowVersion.Length.Should().Be(0); // domain default; infra stamps later

        task.TaskTags.Should().NotBeNull();
        task.TaskTags.Should().BeEmpty();
    }

    [Fact]
    public void UpdateCoreDetails_Overwrites_All_Fields()
    {
        var task = TaskItem.Create("u", "Old", "old", TaskPriority.Low, new DateOnly(2025, 1, 1), DateTime.UtcNow);

        task.UpdateCoreDetails("New", "new", TaskPriority.High, new DateOnly(2025, 12, 31));

        task.Title.Should().Be("New");
        task.Description.Should().Be("new");
        task.Priority.Should().Be(TaskPriority.High);
        task.DueDate.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void MarkTodo_Sets_Status_And_Clears_CompletedAt()
    {
        var task = TaskItem.Create("u", "x", null, TaskPriority.Medium, null, DateTime.UtcNow);
        task.MarkDone(new DateTime(2024, 12, 31, 1, 2, 3, DateTimeKind.Utc));

        task.MarkTodo();

        task.Status.Should().Be(TaskStatus.Todo);
        task.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkInProgress_Sets_Status_And_Clears_CompletedAt()
    {
        var task = TaskItem.Create("u", "x", null, TaskPriority.Medium, null, DateTime.UtcNow);
        task.MarkDone(new DateTime(2024, 12, 31, 1, 2, 3, DateTimeKind.Utc));

        task.MarkInProgress();

        task.Status.Should().Be(TaskStatus.InProgress);
        task.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void MarkDone_Sets_Status_And_Timestamp()
    {
        var task = TaskItem.Create("u", "x", null, TaskPriority.Medium, null, DateTime.UtcNow);

        var finishedAt = new DateTime(2024, 12, 25, 08, 30, 00, DateTimeKind.Utc);
        task.MarkDone(finishedAt);

        task.Status.Should().Be(TaskStatus.Done);
        task.CompletedAtUtc.Should().Be(finishedAt);

        // Calling again moves the timestamp to the most recent value (idempotence not required)
        var later = finishedAt.AddHours(2);
        task.MarkDone(later);
        task.CompletedAtUtc.Should().Be(later);
    }

    [Fact]
    public void Archive_Sets_Archived_And_Clears_CompletedAt()
    {
        var task = TaskItem.Create("u", "x", null, TaskPriority.Medium, null, DateTime.UtcNow);
        task.MarkDone(DateTime.UtcNow);

        task.Archive();

        task.Status.Should().Be(TaskStatus.Archived);
        task.IsArchived.Should().BeTrue();
        task.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void Unarchive_Goes_Back_To_Todo_And_Clears_CompletedAt()
    {
        var task = TaskItem.Create("u", "x", null, TaskPriority.Medium, null, DateTime.UtcNow);
        task.Archive();
        task.MarkDone(DateTime.UtcNow); // ensure it clears again

        task.Unarchive();

        task.Status.Should().Be(TaskStatus.Todo);
        task.IsArchived.Should().BeFalse();
        task.CompletedAtUtc.Should().BeNull();
    }

    [Fact]
    public void TaskTags_Can_Be_Linked()
    {
        var task = TaskItem.Create("u", "x", null, TaskPriority.Medium, null, DateTime.UtcNow);
        var tag = new Tag { Name = "work", NormalizedName = "WORK", OwnerUserId = "u" };

        task.TaskTags.Add(new TaskTag { TaskId = task.Id, Task = task, TagId = tag.Id, Tag = tag });

        task.TaskTags.Should().HaveCount(1);
        task.TaskTags.Single().Tag.Should().BeSameAs(tag);
    }
}
