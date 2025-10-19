using Domain.Tasks;
using Domain.Tasks.Queries;
using FluentAssertions;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace Domain.Tests.Unit_Tests;

public sealed class TaskQueryTests
{
    [Fact]
    public void Defaults_Are_As_Expected()
    {
        var q = new TaskQuery();

        q.PageNumber.Should().Be(1);
        q.PageSize.Should().Be(20);

        q.Statuses.Should().BeNull();
        q.Priorities.Should().BeNull();
        q.DueOnOrAfter.Should().BeNull();
        q.DueOnOrBefore.Should().BeNull();
        q.Search.Should().BeNull();
        q.Tags.Should().BeNull();

        q.SortBy.Should().Be(TaskSortBy.CreatedAt);
        q.SortDirection.Should().Be(SortDirection.Desc);
        q.IncludeTags.Should().BeTrue();
    }

    [Fact]
    public void Can_Set_All_Fields()
    {
        var q = new TaskQuery
        {
            PageNumber = 3,
            PageSize = 50,
            Statuses = new[] { TaskStatus.Todo, TaskStatus.Done },
            Priorities = new[] { TaskPriority.High },
            DueOnOrAfter = new DateOnly(2025, 1, 1),
            DueOnOrBefore = new DateOnly(2025, 12, 31),
            Search = "report",
            Tags = new[] { "work", "urgent" },
            SortBy = TaskSortBy.Priority,
            SortDirection = SortDirection.Asc,
            IncludeTags = false
        };

        q.PageNumber.Should().Be(3);
        q.PageSize.Should().Be(50);
        q.Statuses.Should().BeEquivalentTo([TaskStatus.Todo, TaskStatus.Done]);
        q.Priorities.Should().BeEquivalentTo([TaskPriority.High]);
        q.DueOnOrAfter.Should().Be(new DateOnly(2025, 1, 1));
        q.DueOnOrBefore.Should().Be(new DateOnly(2025, 12, 31));
        q.Search.Should().Be("report");
        q.Tags.Should().BeEquivalentTo("work", "urgent");
        q.SortBy.Should().Be(TaskSortBy.Priority);
        q.SortDirection.Should().Be(SortDirection.Asc);
        q.IncludeTags.Should().BeFalse();
    }
}