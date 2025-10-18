namespace Domain.Tasks.Queries;

public enum TaskSortBy
{
    CreatedAt = 0,
    DueDate = 1,
    Priority = 2,
    Title = 3
}

public enum SortDirection
{
    Asc = 0,
    Desc = 1
}