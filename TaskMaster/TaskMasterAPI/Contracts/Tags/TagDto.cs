namespace TaskMasterApi.Contracts.Tags;

public sealed class TagDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
}