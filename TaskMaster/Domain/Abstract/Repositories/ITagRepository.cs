using Domain.Tasks;

namespace Domain.Abstract.Repositories;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Tag?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct);
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct);

    /// <summary>
    /// Ensures the provided tag names exist for the current user, creating any that are missing,
    /// and returns the complete set (existing + created).
    /// </summary>
    Task<IReadOnlyList<Tag>> EnsureTagsExistAsync(IEnumerable<string> tagNames, CancellationToken ct);

    /// <summary>
    /// Typeahead/search for tags owned by the current user. If <paramref name="search"/> is null/whitespace,
    /// returns top tags ordered by name up to <paramref name="limit"/>.
    /// Search is case-insensitive and matches anywhere in the tag name.
    /// </summary>
    Task<IReadOnlyList<Tag>> SearchAsync(string? search, int limit, CancellationToken ct);
}