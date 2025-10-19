using Domain.Tasks;
using Domain.Tasks.Queries;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace Infrastructure.Tests.Unit_Tests;

public sealed class EfTaskRepositoryTests
{
    private static EfTaskRepository Repo(ApplicationDbContext db) => new(db);

    private static async Task<(TaskItem t1, TaskItem t2, TaskItem t3, TaskItem t4, Tag red, Tag blue, Tag green)>
        SeedAsync(ApplicationDbContext db)
    {
        var red   = new Tag { OwnerUserId = "user-1", Name = "Red",   NormalizedName = "RED" };
        var blue  = new Tag { OwnerUserId = "user-1", Name = "Blue",  NormalizedName = "BLUE" };
        var green = new Tag { OwnerUserId = "user-1", Name = "Green", NormalizedName = "GREEN" };

        var now = DateTime.UtcNow;

        var t1 = new TaskItem
        {
            OwnerUserId = "user-1",
            Title = "Fix bug",
            Description = "red hot",
            Status = TaskStatus.Todo,
            Priority = TaskPriority.High,
            DueDate = new DateOnly(2025, 01, 01),
            CreatedAtUtc = now.AddHours(-4),
            UpdatedAtUtc = now.AddHours(-4)
        };
        t1.TaskTags.Add(new TaskTag { Task = t1, Tag = red });

        var t2 = new TaskItem
        {
            OwnerUserId = "user-1",
            Title = "Write docs",
            Status = TaskStatus.InProgress,
            Priority = TaskPriority.Low,
            CreatedAtUtc = now.AddHours(-3),
            UpdatedAtUtc = now.AddHours(-3)
        };
        t2.TaskTags.Add(new TaskTag { Task = t2, Tag = blue });

        var t3 = new TaskItem
        {
            OwnerUserId = "user-1",
            Title = "Plan",
            Description = "roadmap",
            Status = TaskStatus.Done,
            Priority = TaskPriority.Medium,
            DueDate = new DateOnly(2025, 02, 01),
            CreatedAtUtc = now.AddHours(-2),
            UpdatedAtUtc = now.AddHours(-2)
        };
        t3.TaskTags.Add(new TaskTag { Task = t3, Tag = red });
        t3.TaskTags.Add(new TaskTag { Task = t3, Tag = blue });

        var t4 = new TaskItem
        {
            OwnerUserId = "user-1",
            Title = "Archive me",
            Status = TaskStatus.Archived,
            Priority = TaskPriority.Medium,
            CreatedAtUtc = now.AddHours(-1),
            UpdatedAtUtc = now.AddHours(-1)
        };
        t4.TaskTags.Add(new TaskTag { Task = t4, Tag = green });

        // Another user's record (should be hidden by filter)
        var otherUser = new TaskItem { OwnerUserId = "user-2", Title = "Other", CreatedAtUtc = now, UpdatedAtUtc = now };

        db.AddRange(red, blue, green, t1, t2, t3, t4, otherUser);
        await db.SaveChangesAsync();

        return (t1, t2, t3, t4, red, blue, green);
    }

    [Fact]
    public async Task GetById_Respects_IncludeTags_And_AsNoTracking()
    {
        await using var db = DbContextFactory.Create();
        var (t1, _, _, _, _, _, _) = await SeedAsync(db);

        var sut = Repo(db);

        var withTags = await sut.GetByIdAsync(t1.Id, includeTags: true, asTracking: false, CancellationToken.None);
        withTags.Should().NotBeNull();
        withTags!.TaskTags.Should().NotBeEmpty();
        db.Entry(withTags).State.Should().Be(EntityState.Detached);

        var withoutTags = await sut.GetByIdAsync(t1.Id, includeTags: false, asTracking: false, CancellationToken.None);
        withoutTags.Should().NotBeNull();
        withoutTags!.TaskTags.Should().BeEmpty(); // no Include => not loaded
    }

    [Fact]
    public async Task Search_Filters_Sort_Paging_And_TagAllMatch_Work()
    {
        await using var db = DbContextFactory.Create();
        var (t1, t2, t3, t4, red, blue, green) = await SeedAsync(db);
        var sut = Repo(db);

        // Status filter
        var byStatus = await sut.SearchAsync(new TaskQuery { Statuses = new[] { TaskStatus.Done } }, CancellationToken.None);
        byStatus.Items.Should().ContainSingle(i => i.Id == t3.Id);

        // Priority filter
        var byPriority = await sut.SearchAsync(new TaskQuery { Priorities = new[] { TaskPriority.High } }, CancellationToken.None);
        byPriority.Items.Should().ContainSingle(i => i.Id == t1.Id);

        // Due date range
        var byDueRange = await sut.SearchAsync(new TaskQuery
        {
            DueOnOrAfter = new DateOnly(2025, 01, 15),
            DueOnOrBefore = new DateOnly(2025, 02, 15)
        }, CancellationToken.None);
        byDueRange.Items.Should().ContainSingle(i => i.Id == t3.Id);

        // Search in title/description
        var search = await sut.SearchAsync(new TaskQuery { Search = "bug" }, CancellationToken.None);
        search.Items.Should().ContainSingle(i => i.Id == t1.Id);

        // Tags require ALL
        var byTagsAll = await sut.SearchAsync(new TaskQuery { Tags = new[] { "red", "blue" }, IncludeTags = true }, CancellationToken.None);
        byTagsAll.Items.Should().ContainSingle(i => i.Id == t3.Id);

        var byTagsNone = await sut.SearchAsync(new TaskQuery { Tags = new[] { "red", "green" } }, CancellationToken.None);
        byTagsNone.Items.Should().BeEmpty();

        // Sort by Title Asc
        var sortTitleAsc = await sut.SearchAsync(new TaskQuery { SortBy = TaskSortBy.Title, SortDirection = SortDirection.Asc }, CancellationToken.None);
        sortTitleAsc.Items.First().Id.Should().Be(t4.Id); // "Archive me"

        // Sort by Priority Desc
        var sortPriorityDesc = await sut.SearchAsync(new TaskQuery { SortBy = TaskSortBy.Priority, SortDirection = SortDirection.Desc }, CancellationToken.None);
        sortPriorityDesc.Items.First().Id.Should().Be(t1.Id); // High first

        // Paging
        var paging = await sut.SearchAsync(new TaskQuery { PageNumber = 2, PageSize = 2, SortBy = TaskSortBy.CreatedAt, SortDirection = SortDirection.Desc }, CancellationToken.None);
        paging.TotalCount.Should().Be(4); // other user's task hidden by filter
        paging.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Search_IncludeTags_False_DoesNotLoad_Tags()
    {
        await using var db = DbContextFactory.Create();
        await SeedAsync(db);
        var sut = Repo(db);

        var page = await sut.SearchAsync(new TaskQuery { IncludeTags = false }, CancellationToken.None);
        page.Items.Should().NotBeEmpty();
        page.Items.All(t => t.TaskTags.Count == 0).Should().BeTrue();
    }
}
