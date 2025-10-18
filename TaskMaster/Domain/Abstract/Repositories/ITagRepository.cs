using Domain.Tasks;

namespace Domain.Abstract.Repositories;

public interface ITagRepository
{
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Tag?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct);
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Tag>> EnsureTagsExistAsync(IEnumerable<string> tagNames, CancellationToken ct);
}