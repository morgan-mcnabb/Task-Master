using Domain.Abstract.Repositories;
using Domain.Tasks;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public sealed class EfTagRepository(ApplicationDbContext db) : ITagRepository
{
    public Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct)
        => db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct)!;

    public Task<Tag?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct)
        => db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == normalizedName, ct)!;

    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct)
        => await db.Tags.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Tag>> EnsureTagsExistAsync(IEnumerable<string> tagNames, CancellationToken ct)
    {
        var normalized = tagNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalized.Length == 0) return Array.Empty<Tag>();

        var existing = await db.Tags
            .Where(t => normalized.Contains(t.NormalizedName))
            .ToListAsync(ct);

        var existingSet = existing.Select(t => t.NormalizedName).ToHashSet(StringComparer.Ordinal);

        foreach (var n in normalized)
        {
            if (!existingSet.Contains(n))
            {
                db.Tags.Add(new Tag
                {
                    Name = n,
                    NormalizedName = n
                });
            }
        }

        var final = await db.Tags
            .Where(t => normalized.Contains(t.NormalizedName))
            .ToListAsync(ct);

        return final;
    }
}
