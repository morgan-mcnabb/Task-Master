using Domain.Common;
using Domain.Tasks;
using Domain.Tasks.Queries;
using TaskMasterApi.Contracts.Common;
using TaskMasterApi.Contracts.Tasks;
using TaskMasterApi.Contracts.Tags;

namespace TaskMasterApi.Mapping;

public interface ITaskMapper
{
    TaskQuery ToDomainQuery(TaskQueryRequest request);

    TaskDto ToTaskDto(TaskItem task);
    TagDto ToTagDto(Tag tag);
    PagedResponse<TaskDto> ToPagedResponse(PagedResult<TaskItem> page);
    
    string? ToETag(byte[]? rowVersion);
    byte[]? FromETag(string? etag);
}