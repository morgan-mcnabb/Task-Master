using System.Text;
using Domain.Common;
using Domain.Tasks;
using Domain.Tasks.Queries;
using FluentAssertions;
using TaskMasterApi.Contracts.Tasks;
using TaskMasterApi.Mapping;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace TaskMasterApi.Tests.Integration_Tests;

public sealed class TaskMapperTests
{
    private readonly TaskMapper _sut = new();

    [Fact]
    public void ToDomainQuery_TrimAndDistinctTags_CaseInsensitive()
    {
        var req = new TaskQueryRequest
        {
            PageNumber = 1,
            PageSize = 50,
            Tags = [" Work ", "work", "life", " LIFE "],
            Search = "  hello  "
        };

        var q = _sut.ToDomainQuery(req);

        q.Should().BeEquivalentTo(new TaskQuery
        {
            PageNumber = 1,
            PageSize = 50,
            Tags = new[] { "Work", "life", "LIFE" }.Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            Search = "hello",
            SortBy = TaskSortBy.CreatedAt,
            SortDirection = SortDirection.Desc,
            IncludeTags = true
        }, opt => opt
            .Excluding(x => x.Statuses)
            .Excluding(x => x.Priorities)
            .Excluding(x => x.DueOnOrAfter)
            .Excluding(x => x.DueOnOrBefore));
    }

    [Fact]
    public void ToTaskDto_Maps_All_Fields_Including_ETag_And_Tags()
    {
        var tag = new Tag { Id = Guid.NewGuid(), Name = "work", NormalizedName = "WORK", OwnerUserId = "u1" };
        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            OwnerUserId = "u1",
            Title = "T",
            Description = "D",
            Priority = TaskPriority.High,
            Status = TaskStatus.InProgress,
            DueDate = new DateOnly(2025, 01, 01),
            CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
            UpdatedAtUtc = DateTime.UtcNow,
            CompletedAtUtc = null,
            RowVersion = Encoding.ASCII.GetBytes("abcdefgh")
        };
        task.TaskTags.Add(new TaskTag { Task = task, Tag = tag, TagId = tag.Id, TaskId = task.Id });

        var dto = _sut.ToTaskDto(task);

        dto.Id.Should().Be(task.Id);
        dto.Title.Should().Be("T");
        dto.Description.Should().Be("D");
        dto.Priority.Should().Be(TaskPriority.High);
        dto.Status.Should().Be(TaskStatus.InProgress);
        dto.DueDate.Should().Be(new DateOnly(2025, 01, 01));
        dto.IsArchived.Should().BeFalse();
        dto.ETag.Should().Be(Convert.ToBase64String(task.RowVersion));
        dto.Tags.Should().HaveCount(1);
        dto.Tags[0].Name.Should().Be("work");
    }

    [Fact]
    public void ToPagedResponse_Maps_Paging_And_Items()
    {
        var t = new TaskItem { Id = Guid.NewGuid(), Title = "A", OwnerUserId = "u1", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var page = new PagedResult<TaskItem>([t], 11, 2, 5);

        var resp = _sut.ToPagedResponse(page);

        resp.TotalCount.Should().Be(11);
        resp.PageNumber.Should().Be(2);
        resp.PageSize.Should().Be(5);
        resp.Items.Should().HaveCount(1);
        resp.Items[0].Id.Should().Be(t.Id);
    }

    [Fact]
    public void ETag_RoundTrip_Valid_And_Invalid()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var base64 = _sut.ToETag(bytes);
        base64.Should().Be(Convert.ToBase64String(bytes));

        _sut.FromETag(base64).Should().BeEquivalentTo(bytes);

        _sut.FromETag("not-base64").Should().BeNull();
        _sut.FromETag(null).Should().BeNull();
        _sut.ToETag(Array.Empty<byte>()).Should().BeNull();
    }
}
