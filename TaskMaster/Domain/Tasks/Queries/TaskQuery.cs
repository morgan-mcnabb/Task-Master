namespace Domain.Tasks.Queries;

using Domain.Tasks;

public sealed class TaskQuery
{
    // Paging
    public int PageNumber { get; init; } = 1;
    public int PageSize   { get; init; } = 20;

    // Filters
    public TaskStatus[]? Statuses { get; init; }
    public TaskPriority[]? Priorities { get; init; }
    public DateOnly? DueOnOrAfter { get; init; }
    public DateOnly? DueOnOrBefore { get; init; }
    public string? Search { get; init; }  
    public IReadOnlyCollection<string>? Tags { get; init; }
    public TaskSortBy SortBy { get; init; } = TaskSortBy.CreatedAt;
    public SortDirection SortDirection { get; init; } = SortDirection.Desc;
    public bool IncludeTags { get; init; } = true;
}