using Application.Common.Exceptions;
using Application.Services;
using Application.Tasks.Models;
using Domain.Abstract.Persistence;
using Domain.Abstract.Repositories;
using Domain.Common;
using Domain.Common.Abstract;
using Domain.Tasks;
using FluentAssertions;
using Moq;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace Application.Tests.UnitTests;

public sealed class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepo = new();
    private readonly Mock<ITagRepository> _tagRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _currentUser = new();

    private readonly TimeProvider _timeProvider;

    private readonly DateTimeOffset _fixedNowUtc = new DateTimeOffset(2024, 12, 25, 10, 30, 00, TimeSpan.Zero);

    public TaskServiceTests()
    {
        _timeProvider = new FixedTimeProvider(_fixedNowUtc);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }

    private TaskService CreateSut() =>
        new TaskService(_taskRepo.Object, _tagRepo.Object, _uow.Object, _currentUser.Object, _timeProvider);

    private void SetAuthenticatedUser(string userId = "user-123")
    {
        _currentUser.SetupGet(c => c.IsAuthenticated).Returns(true);
        _currentUser.SetupGet(c => c.UserId).Returns(userId);
    }

    private void SetUnauthenticatedUser()
    {
        _currentUser.SetupGet(c => c.IsAuthenticated).Returns(false);
        _currentUser.SetupGet(c => c.UserId).Returns((string?)null);
    }

    // ---------------------------
    // Search & Get
    // ---------------------------

    [Fact]
    public async Task SearchAsync_ForwardsToRepository()
    {
        SetAuthenticatedUser();
        var sut = CreateSut();

        var pageItems = new List<TaskItem> { new() { Title = "T1", OwnerUserId = "user-123" } };
        var page = new PagedResult<TaskItem>(pageItems, 1, 1, 20);

        _taskRepo.Setup(r => r.SearchAsync(It.IsAny<Domain.Tasks.Queries.TaskQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(page);

        var result = await sut.SearchAsync(new Domain.Tasks.Queries.TaskQuery(), CancellationToken.None);

        result.Should().BeSameAs(page);
        _taskRepo.Verify(r => r.SearchAsync(It.IsAny<Domain.Tasks.Queries.TaskQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_UsesAsTrackingFalseAndIncludeTagsPassthrough()
    {
        SetAuthenticatedUser();
        var sut = CreateSut();

        var id = Guid.NewGuid();
        var entity = new TaskItem { Id = id, Title = "T", OwnerUserId = "user-123" };

        // We verify the exact call params by capturing them in the setup predicate.
        _taskRepo.Setup(r => r.GetByIdAsync(
                It.Is<Guid>(g => g == id),
                It.Is<bool>(include => include == true),
                It.Is<bool>(track => track == false),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);

        var result = await sut.GetByIdAsync(id, includeTags: true, CancellationToken.None);

        result.Should().BeSameAs(entity);
        _taskRepo.VerifyAll();
    }

    // ---------------------------
    // Create
    // ---------------------------

    [Fact]
    public async Task CreateAsync_Throws_WhenUnauthenticated()
    {
        SetUnauthenticatedUser();
        var sut = CreateSut();

        var model = new CreateTaskModel { Title = "X" };

        Func<Task> act = async () => await sut.CreateAsync(model, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Authentication is required*");
        _taskRepo.VerifyNoOtherCalls();
        _tagRepo.VerifyNoOtherCalls();
        _uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateAsync_PersistsTask_TrimsTitle_NullsWhitespaceDescription_SetsFields_AndSaves()
    {
        SetAuthenticatedUser("alice");
        var sut = CreateSut();

        var model = new CreateTaskModel
        {
            Title = "  Buy milk  ",
            Description = "   ", // becomes null
            Priority = TaskPriority.High,
            DueDate = new DateOnly(2025, 1, 15),
            Tags = Array.Empty<string>()
        };

        TaskItem? captured = null;
        _taskRepo.Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
                 .Callback<TaskItem, CancellationToken>((t, _) => captured = t)
                 .Returns(Task.CompletedTask);

        var result = await sut.CreateAsync(model, CancellationToken.None);

        // Verify properties on the resulting TaskItem
        captured.Should().NotBeNull();
        captured!.OwnerUserId.Should().Be("alice");
        captured.Title.Should().Be("Buy milk"); // trimmed
        captured.Description.Should().BeNull(); // whitespace -> null
        captured.Priority.Should().Be(TaskPriority.High);
        captured.DueDate.Should().Be(new DateOnly(2025, 1, 15));
        captured.Status.Should().Be(TaskStatus.Todo);
        captured.CreatedAtUtc.Should().Be(_fixedNowUtc.UtcDateTime);
        captured.UpdatedAtUtc.Should().Be(_fixedNowUtc.UtcDateTime);
        captured.CompletedAtUtc.Should().BeNull();

        // Save was called
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Return value is the same instance we added
        result.Should().BeSameAs(captured);
    }

    [Fact]
    public async Task CreateAsync_WithTags_EnsuresTagsAndLinks_AllowsDuplicatesInInput()
    {
        SetAuthenticatedUser("user-1");
        var sut = CreateSut();

        var model = new CreateTaskModel
        {
            Title = "Task with tags",
            Tags = new[] { "work", "Work", "urgent" } // input may duplicate (repo de-dupes)
        };

        var work = new Tag { Id = Guid.NewGuid(), Name = "work", NormalizedName = "WORK" };
        var urgent = new Tag { Id = Guid.NewGuid(), Name = "urgent", NormalizedName = "URGENT" };

        _tagRepo.Setup(r => r.EnsureTagsExistAsync(
                It.Is<IEnumerable<string>>(seq => seq.SequenceEqual(model.Tags)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tag> { work, urgent });

        TaskItem? captured = null;
        _taskRepo.Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .Callback<TaskItem, CancellationToken>((t, _) => captured = t)
            .Returns(Task.CompletedTask);

        await sut.CreateAsync(model, CancellationToken.None);

        captured.Should().NotBeNull();
        var tagIds = captured!.TaskTags.Select(tt => tt.TagId).ToArray();
        tagIds.Should().BeEquivalentTo(new[] { work.Id, urgent.Id });
    }

    // ---------------------------
    // Update
    // ---------------------------

    [Fact]
    public async Task UpdateAsync_Throws_WhenUnauthenticated()
    {
        SetUnauthenticatedUser();
        var sut = CreateSut();
        var model = new UpdateTaskModel { Title = "X" };

        Func<Task> act = async () => await sut.UpdateAsync(Guid.NewGuid(), model, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Throws_NotFound_WhenEntityMissing()
    {
        SetAuthenticatedUser();
        var sut = CreateSut();
        _taskRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), true, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((TaskItem?)null);

        Func<Task> act = async () => await sut.UpdateAsync(Guid.NewGuid(), new UpdateTaskModel(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_Throws_Concurrency_WhenIfMatchMismatch()
    {
        SetAuthenticatedUser();
        var sut = CreateSut();

        var existing = NewTask(ownerId: "user-123", title: "Old");
        existing.RowVersion = new byte[] { 1, 2, 3, 4 };

        _taskRepo.Setup(r => r.GetByIdAsync(existing.Id, true, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

        var model = new UpdateTaskModel
        {
            Title = "New",
            IfMatchRowVersion = new byte[] { 9, 9, 9, 9 }
        };

        Func<Task> act = async () => await sut.UpdateAsync(existing.Id, model, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
        _taskRepo.Verify(r => r.Update(It.IsAny<TaskItem>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCoreFields_StatusAndTags_Saves()
    {
        SetAuthenticatedUser("bob");
        var sut = CreateSut();

        var tagOld = new Tag { Id = Guid.NewGuid(), Name = "old", NormalizedName = "OLD" };
        var tagKeep = new Tag { Id = Guid.NewGuid(), Name = "keep", NormalizedName = "KEEP" };
        var existing = NewTask(ownerId: "bob", title: "Old title", description: "Old desc", priority: TaskPriority.Low, due: new DateOnly(2025, 1, 1));
        existing.RowVersion = new byte[] { 7, 7, 7, 7 };
        existing.TaskTags.Add(new TaskTag { TaskId = existing.Id, Task = existing, TagId = tagOld.Id, Tag = tagOld });
        existing.TaskTags.Add(new TaskTag { TaskId = existing.Id, Task = existing, TagId = tagKeep.Id, Tag = tagKeep });

        _taskRepo.Setup(r => r.GetByIdAsync(existing.Id, true, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

        var tagNew = new Tag { Id = Guid.NewGuid(), Name = "new", NormalizedName = "NEW" };
        // Request to keep "keep" and add "new" (removing "old")
        var incomingTagNames = new[] { "keep", "new" };

        _tagRepo.Setup(r => r.EnsureTagsExistAsync(
                It.Is<IEnumerable<string>>(seq => seq.SequenceEqual(incomingTagNames)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Tag> { tagKeep, tagNew });

        var model = new UpdateTaskModel
        {
            Title = "  New title  ",
            Description = "New desc",
            Priority = TaskPriority.High,
            DueDate = new DateOnly(2025, 2, 2),
            Status = TaskStatus.Done, // should stamp CompletedAtUtc with fixed time
            Tags = incomingTagNames,
            IfMatchRowVersion = existing.RowVersion
        };

        await sut.UpdateAsync(existing.Id, model, CancellationToken.None);

        existing.Title.Should().Be("  New title  "); // UpdateCoreDetails does not trim (controller validation trims; we keep as-is)
        existing.Description.Should().Be("New desc");
        existing.Priority.Should().Be(TaskPriority.High);
        existing.DueDate.Should().Be(new DateOnly(2025, 2, 2));
        existing.Status.Should().Be(TaskStatus.Done);
        existing.CompletedAtUtc.Should().Be(_fixedNowUtc.UtcDateTime);

        // Tags synced: {keep, new}
        var tagIds = existing.TaskTags.Select(t => t.TagId).OrderBy(x => x).ToArray();
        tagIds.Should().BeEquivalentTo(new[] { tagKeep.Id, tagNew.Id }.OrderBy(x => x));

        _taskRepo.Verify(r => r.Update(existing), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotTouchTags_WhenTagsIsNull()
    {
        SetAuthenticatedUser("bob");
        var sut = CreateSut();

        var t1 = NewTask("bob", "Old");
        var tagA = new Tag { Id = Guid.NewGuid(), Name = "A", NormalizedName = "A" };
        var tagB = new Tag { Id = Guid.NewGuid(), Name = "B", NormalizedName = "B" };
        t1.TaskTags.Add(new TaskTag { TaskId = t1.Id, Task = t1, TagId = tagA.Id, Tag = tagA });
        t1.TaskTags.Add(new TaskTag { TaskId = t1.Id, Task = t1, TagId = tagB.Id, Tag = tagB });
        t1.RowVersion = new byte[] { 1 };

        _taskRepo.Setup(r => r.GetByIdAsync(t1.Id, true, true, It.IsAny<CancellationToken>())).ReturnsAsync(t1);

        var model = new UpdateTaskModel
        {
            Title = "Changed only title",
            Tags = null, // explicitly null => no tag changes
            IfMatchRowVersion = t1.RowVersion
        };

        await sut.UpdateAsync(t1.Id, model, CancellationToken.None);

        t1.TaskTags.Select(x => x.TagId).Should().BeEquivalentTo(new[] { tagA.Id, tagB.Id });
        _tagRepo.Verify(r => r.EnsureTagsExistAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------------------------
    // Delete
    // ---------------------------

    [Fact]
    public async Task DeleteAsync_Throws_WhenUnauthenticated()
    {
        SetUnauthenticatedUser();
        var sut = CreateSut();

        Func<Task> act = async () => await sut.DeleteAsync(Guid.NewGuid(), null, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _taskRepo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DeleteAsync_Throws_NotFound_WhenMissing()
    {
        SetAuthenticatedUser();
        var sut = CreateSut();
        _taskRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), false, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((TaskItem?)null);

        Func<Task> act = async () => await sut.DeleteAsync(Guid.NewGuid(), null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_Throws_Concurrency_WhenIfMatchMismatch()
    {
        SetAuthenticatedUser();
        var sut = CreateSut();

        var existing = NewTask("user-1", "to delete");
        existing.RowVersion = new byte[] { 5, 5, 5, 5 };

        _taskRepo.Setup(r => r.GetByIdAsync(existing.Id, false, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

        var ifMatch = new byte[] { 1, 2, 3, 4 };

        Func<Task> act = async () => await sut.DeleteAsync(existing.Id, ifMatch, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
        _taskRepo.Verify(r => r.Remove(It.IsAny<TaskItem>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAndSaves_WhenMatchOrNoIfMatch()
    {
        SetAuthenticatedUser();
        var sut = CreateSut();

        var existing = NewTask("user-1", "to delete");
        existing.RowVersion = new byte[] { 8, 8, 8 };

        _taskRepo.Setup(r => r.GetByIdAsync(existing.Id, false, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

        // Case A: matching etag
        await sut.DeleteAsync(existing.Id, ifMatchRowVersion: existing.RowVersion, CancellationToken.None);
        _taskRepo.Verify(r => r.Remove(existing), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // Reset verifications for Case B
        _taskRepo.Invocations.Clear();
        _uow.Invocations.Clear();

        _taskRepo.Setup(r => r.GetByIdAsync(existing.Id, false, true, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(existing);

        // Case B: no etag (service allows it, controller enforces header)
        await sut.DeleteAsync(existing.Id, ifMatchRowVersion: null, CancellationToken.None);
        _taskRepo.Verify(r => r.Remove(existing), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------------------------
    // Helpers
    // ---------------------------

    private TaskItem NewTask(string ownerId, string title, string? description = null, TaskPriority priority = TaskPriority.Medium, DateOnly? due = null)
    {
        return TaskItem.Create(ownerId, title, description, priority, due, _fixedNowUtc.UtcDateTime);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
