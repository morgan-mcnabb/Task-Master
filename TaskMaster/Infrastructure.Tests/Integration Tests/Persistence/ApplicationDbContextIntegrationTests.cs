using Infrastructure.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Domain.Tasks;

namespace Infrastructure.Tests.Integration_Tests.Persistence;

public sealed class ApplicationDbContextIntegrationTests
{
    [Fact]
    public async Task GlobalQueryFilter_ShowsOnlyCurrentUsersTasks()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;

        db.Tasks.AddRange(
            new TaskItem { OwnerUserId = "u1", Title = "Mine #1" },
            new TaskItem { OwnerUserId = "u2", Title = "Not mine" }
        );

        await db.SaveChangesAsync();

        // Filtered by OwnerUserId == current user ("u1")
        var visible = await db.Tasks.AsNoTracking().Select(t => t.Title).ToListAsync();
        visible.Should().BeEquivalentTo(new[] { "Mine #1" });
    }

    [Fact]
    public async Task Audit_And_RowVersion_AreStamped_OnAdd_And_Modify_WhenAuthenticated()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1", isAuthenticated: true);
        var db = test.Db;

        var task = new TaskItem { OwnerUserId = "u1", Title = "T1" };

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        task.CreatedAtUtc.Should().NotBe(default);
        task.UpdatedAtUtc.Should().NotBe(default);
        task.RowVersion.Should().NotBeNullOrEmpty("RowVersion stamped on insert");

        var firstRowVersion = task.RowVersion.ToArray();
        var firstUpdatedAt = task.UpdatedAtUtc;

        // Modify
        task.Title = "T1-updated";
        await db.SaveChangesAsync();

        task.UpdatedAtUtc.Should().BeAfter(firstUpdatedAt);
        task.RowVersion.Should().NotBeNull().And.NotBeEmpty();
        task.RowVersion.SequenceEqual(firstRowVersion).Should().BeFalse("row version changes on update");
    }

    [Fact]
    public async Task CascadeDelete_FromTask_Removes_TaskTags_But_Leaves_Tag()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;

        var tag = new Tag { Name = "X", NormalizedName = "X" };        // owner set by stamp
        var task = new TaskItem { OwnerUserId = "u1", Title = "T1" };

        task.TaskTags.Add(new TaskTag { Task = task, Tag = tag });

        db.Add(task);
        await db.SaveChangesAsync();

        (await db.TaskTags.CountAsync()).Should().Be(1);

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();

        (await db.TaskTags.CountAsync()).Should().Be(0);
        (await db.Tags.CountAsync()).Should().Be(1, "deleting a task shouldn't delete the tag entity");
    }

    [Fact]
    public async Task CascadeDelete_FromTag_Removes_TaskTags_But_Leaves_Task()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;

        var tag = new Tag { Name = "X", NormalizedName = "X" };
        var task = new TaskItem { OwnerUserId = "u1", Title = "T1" };
        task.TaskTags.Add(new TaskTag { Task = task, Tag = tag });

        db.AddRange(task, tag);
        await db.SaveChangesAsync();

        (await db.TaskTags.CountAsync()).Should().Be(1);

        db.Tags.Remove(tag);
        await db.SaveChangesAsync();

        (await db.TaskTags.CountAsync()).Should().Be(0);
        (await db.Tasks.CountAsync()).Should().Be(1, "deleting a tag shouldn't delete the task entity");
    }

    [Fact]
    public async Task UniqueIndex_NormalizedName_PerOwner_IsEnforced()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;

        db.Tags.Add(new Tag { Name = "home", NormalizedName = "HOME" });
        await db.SaveChangesAsync();

        db.Tags.Add(new Tag { Name = "HOME", NormalizedName = "HOME" }); // same owner ("u1")
        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
