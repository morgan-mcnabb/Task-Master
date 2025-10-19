using System.Text;
using Domain.Common;
using Domain.Tasks;
using Domain.Tasks.Queries;
using TaskMasterApi.Contracts.Common;
using TaskMasterApi.Contracts.Tasks;
using TaskMasterApi.Contracts.Tags;
using TaskStatus = Domain.Tasks.TaskStatus;

namespace TaskMasterApi.Mapping;

public sealed class TaskMapper : ITaskMapper
{
    public TaskQuery ToDomainQuery(TaskQueryRequest request)
    {
        var pageNumber = Math.Max(request.PageNumber, 1);
        var pageSize   = Math.Clamp(request.PageSize, 1, 100);

        return new TaskQuery
        {
            PageNumber = pageNumber,
            PageSize   = pageSize,

            Statuses   = request.Statuses,
            Priorities = request.Priorities,
            DueOnOrAfter = request.DueOnOrAfter,
            DueOnOrBefore = request.DueOnOrBefore,
            Search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            Tags = request.Tags?.Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),

            SortBy = request.SortBy,
            SortDirection = request.SortDirection,

            IncludeTags = request.IncludeTags
        };
    }

    public TaskDto ToTaskDto(TaskItem task)
    {
        var tagDtos = task.TaskTags.Select(link => ToTagDto(link.Tag)).ToArray();

        return new TaskDto
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            Status = task.Status,
            Priority = task.Priority,
            DueDate = task.DueDate,
            CreatedAtUtc = task.CreatedAtUtc,
            UpdatedAtUtc = task.UpdatedAtUtc,
            CompletedAtUtc = task.CompletedAtUtc,
            IsArchived = task.IsArchived,
            ETag = ToETag(task.RowVersion),
            Tags = tagDtos
        };
    }

    public TagDto ToTagDto(Tag tag) =>
        new TagDto
        {
            Id = tag.Id,
            Name = tag.Name
        };

    public PagedResponse<TaskDto> ToPagedResponse(PagedResult<TaskItem> page)
    {
        var taskDtos = page.Items.Select(ToTaskDto).ToArray();
        return new PagedResponse<TaskDto>(taskDtos, page.TotalCount, page.PageNumber, page.PageSize);
    }

    public string? ToETag(byte[]? rowVersion)
        => rowVersion is null || rowVersion.Length == 0 ? null : Convert.ToBase64String(rowVersion);

    public byte[]? FromETag(string? etag)
    {
        if (string.IsNullOrWhiteSpace(etag)) return null;

        try
        {
            return Convert.FromBase64String(etag);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
