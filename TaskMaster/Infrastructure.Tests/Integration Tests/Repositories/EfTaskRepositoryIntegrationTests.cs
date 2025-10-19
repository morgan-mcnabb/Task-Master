using Infrastructure.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Domain.Tasks;
using Domain.Tasks.Queries;
using Infrastructure.Repositories;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace Infrastructure.Tests.Integration_Tests.Repositories;

public sealed class EfTaskRepositoryIntegrationTests
{
    private static Tag MakeTag(string name) => new() { Name = name, NormalizedName = name.ToUpperInvariant() };

    private static TaskItem MakeTask(string owner, string title, TaskPriority priority = TaskPriority.Medium, DateOnly? due = null, TaskStatus status = TaskStatus.Todo)
        => new()
        {
            OwnerUserId = owner,
            Title = title,
            Priority = priority,
            DueDate = due,
        };

    [Fact]
    public async Task GetByIdAsync_HonorsIncludeTags_AndTracking()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;
        var repo = new EfTaskRepository(db);

        var tag = MakeTag("alpha");
        var task = MakeTask("u1", "T1");
        task.TaskTags.Add(new TaskTag { Task = task, Tag = tag });

        db.Add(task);
        await db.SaveChangesAsync();

        var id = task.Id;

        // Notracking, no tags
        var t1 = await repo.GetByIdAsync(id, includeTags: false, asTracking: false, default);
        db.Entry(t1!).State.Should().Be(EntityState.Detached);
        t1!.TaskTags.Should().BeEmpty();

        // Tracking, with tags
        var t2 = await repo.GetByIdAsync(id, includeTags: true, asTracking: true, default);
        db.Entry(t2!).State.Should().Be(EntityState.Unchanged);
        t2!.TaskTags.Should().HaveCount(1);
        t2.TaskTags.First().Tag.Name.Should().Be("alpha");
    }

    [Fact]
    public async Task SearchAsync_Filters_Sorts_Pages_AndLoadsTags_WhenRequested()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;
        var repo = new EfTaskRepository(db);

        // Tags
        var red   = MakeTag("red");
        var blue  = MakeTag("blue");
        var green = MakeTag("green");

        // Tasks (all belong to u1 to pass query filter)
        var t1 = MakeTask("u1", "alpha", TaskPriority.High, new DateOnly(2025, 01, 01), TaskStatus.Todo);
        t1.Description = "contains-keyword";
        t1.TaskTags.Add(new TaskTag { Task = t1, Tag = red });
        t1.TaskTags.Add(new TaskTag { Task = t1, Tag = blue });

        var t2 = MakeTask("u1", "beta", TaskPriority.Medium, new DateOnly(2025, 01, 05), TaskStatus.InProgress);
        t2.TaskTags.Add(new TaskTag { Task = t2, Tag = red });

        var t3 = MakeTask("u1", "gamma", TaskPriority.Low, new DateOnly(2025, 01, 03), TaskStatus.Done);
        t3.TaskTags.Add(new TaskTag { Task = t3, Tag = blue });
        t3.TaskTags.Add(new TaskTag { Task = t3, Tag = green });

        var t4 = MakeTask("u1", "delta", TaskPriority.Medium, new DateOnly(2025, 01, 02), TaskStatus.Todo);

        db.AddRange(t1, t2, t3, t4);
        await db.SaveChangesAsync();

        // Filter: require tags red+blue AND search term in title/description
        var query = new TaskQuery
        {
            Statuses = new[] { TaskStatus.Todo, TaskStatus.Done, TaskStatus.InProgress },
            Priorities = new[] { TaskPriority.Low, TaskPriority.Medium, TaskPriority.High },
            DueOnOrAfter = new DateOnly(2024, 12, 30),
            DueOnOrBefore = new DateOnly(2025, 01, 10),
            Search = "keyword",
            Tags = new[] { "red", "blue" }, // requires BOTH
            SortBy = TaskSortBy.DueDate,
            SortDirection = SortDirection.Desc,
            PageNumber = 1,
            PageSize = 10,
            IncludeTags = true
        };

        var page = await repo.SearchAsync(query, default);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle();
        page.Items[0].Title.Should().Be("alpha");
        page.Items[0].TaskTags.Select(tt => tt.Tag.Name).Should().BeEquivalentTo(new[] { "red", "blue" });

        // Paging + sort check
        var allQuery = new TaskQuery
        {
            PageNumber = 2,
            PageSize = 2,
            SortBy = TaskSortBy.DueDate,
            SortDirection = SortDirection.Desc,
            IncludeTags = false
        };

        var page2 = await repo.SearchAsync(allQuery, default);
        page2.PageNumber.Should().Be(2);
        page2.PageSize.Should().Be(2);
        page2.TotalCount.Should().Be(4);
        // DueDate desc: 2025-01-05 (t2), 2025-01-03 (t3), 2025-01-02 (t4), 2025-01-01 (t1)
        page2.Items.Select(i => i.Title).Should().Equal("delta", "alpha");
    }

    [Fact]
    public async Task SearchAsync_TextSearch_Matches_Title_Or_Description()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;
        var repo = new EfTaskRepository(db);

        var a = MakeTask("u1", "Plan trip", TaskPriority.Medium, null);
        var b = MakeTask("u1", "Grocery list", TaskPriority.Low, null);
        b.Description = "buy milk and bread";

        db.AddRange(a, b);
        await db.SaveChangesAsync();

        (await repo.SearchAsync(new TaskQuery { Search = "trip" }, default))
            .Items.Select(x => x.Title).Should().Equal("Plan trip");

        (await repo.SearchAsync(new TaskQuery { Search = "milk" }, default))
            .Items.Select(x => x.Title).Should().Equal("Grocery list");
    }
}
