using Domain.Tasks;
using Domain.Users;
using FluentAssertions;
using Infrastructure.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Unit_Tests;

public sealed class ApplicationDbContextTests
{
    [Fact]
    public async Task QueryFilters_ReturnOnlyCurrentUsersData()
    {
        await using var db = DbContextFactory.Create(userId: "u1", isAuthenticated: true);

        var t1 = new TaskItem { OwnerUserId = "u1", Title = "mine" };
        var t2 = new TaskItem { OwnerUserId = "u2", Title = "theirs" };
        db.Tasks.AddRange(t1, t2);

        var tag1 = new Tag { OwnerUserId = "u1", Name = "Home", NormalizedName = "HOME" };
        var tag2 = new Tag { OwnerUserId = "u2", Name = "Work", NormalizedName = "WORK" };
        db.Tags.AddRange(tag1, tag2);

        var s1 = new UserSetting { UserId = "u1", Key = "theme", Value = "dark" };
        var s2 = new UserSetting { UserId = "u2", Key = "theme", Value = "light" };
        db.UserSettings.AddRange(s1, s2);

        await db.SaveChangesAsync();

        (await db.Tasks.Select(x => x.Title).ToListAsync()).Should().BeEquivalentTo(new[] { "mine" });
        (await db.Tags.Select(x => x.Name).ToListAsync()).Should().BeEquivalentTo(new[] { "Home" });
        (await db.UserSettings.Select(x => x.Value).ToListAsync()).Should().BeEquivalentTo(new[] { "dark" });
    }

    [Fact]
    public async Task Auditing_Stamps_CreatedAndUpdated_OnAdd_And_Updates_Updated_OnModify()
    {
        await using var db = DbContextFactory.Create();

        var task = new TaskItem { OwnerUserId = "u1", Title = "t" };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        task.CreatedAtUtc.Should().NotBe(default);
        task.UpdatedAtUtc.Should().NotBe(default);

        var createdAt = task.CreatedAtUtc;
        var updatedAt = task.UpdatedAtUtc;

        task.Title = "t2";
        await db.SaveChangesAsync();

        task.CreatedAtUtc.Should().Be(createdAt);
        task.UpdatedAtUtc.Should().BeAfter(updatedAt);
    }

    [Fact]
    public async Task RowVersion_IsAssignedOnAdd_AndChangesOnModify_WhenAuthenticated()
    {
        await using var db = DbContextFactory.Create(userId: "u1", isAuthenticated: true);

        var task = new TaskItem { OwnerUserId = "u1", Title = "rv" };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        task.RowVersion.Should().NotBeNull();
        task.RowVersion.Length.Should().Be(8);

        var original = task.RowVersion.ToArray();
        task.Title = "rv2";
        await db.SaveChangesAsync();

        task.RowVersion.Should().NotBeNull();
        task.RowVersion.Length.Should().Be(8);
        task.RowVersion.SequenceEqual(original).Should().BeFalse("row version should change on modify");
    }

    [Fact]
    public async Task RowVersion_IsNotStamped_WhenUnauthenticated()
    {
        await using var db = DbContextFactory.Create(userId: null!, isAuthenticated: false);

        var task = new TaskItem { OwnerUserId = "someone", Title = "rv" };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        task.RowVersion.Should().NotBeNull();
        task.RowVersion.Length.Should().Be(0);
    }

    [Fact]
    public async Task Ownership_IsStamped_ForTag_And_UserSetting_WhenAuthenticated_IfMissing()
    {
        await using var db = DbContextFactory.Create(userId: "u9", isAuthenticated: true);

        var tag = new Tag { OwnerUserId = "", Name = "Work", NormalizedName = "WORK" };
        var setting = new UserSetting { UserId = "", Key = "lang", Value = "en" };

        db.Tags.Add(tag);
        db.UserSettings.Add(setting);
        await db.SaveChangesAsync();

        tag.OwnerUserId.Should().Be("u9");
        setting.UserId.Should().Be("u9");
    }

    [Fact]
    public async Task CascadeDelete_FromTask_Removes_TaskTags_AndKeeps_Tag()
    {
        await using var db = DbContextFactory.Create(userId: "u1", isAuthenticated: true);

        var tag = new Tag { OwnerUserId = "u1", Name = "X", NormalizedName = "X" };
        var task = new TaskItem { OwnerUserId = "u1", Title = "T1" };
        task.TaskTags.Add(new TaskTag { Task = task, Tag = tag });

        db.Add(task);
        await db.SaveChangesAsync();

        (await db.TaskTags.CountAsync()).Should().Be(1);

        db.Tasks.Remove(task);
        await db.SaveChangesAsync();

        (await db.TaskTags.CountAsync()).Should().Be(0);
        (await db.Tags.CountAsync()).Should().Be(1, "deleting a Task should not delete Tag entity");
    }
}
