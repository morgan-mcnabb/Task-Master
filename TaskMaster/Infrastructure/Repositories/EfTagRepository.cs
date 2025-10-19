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
        ArgumentNullException.ThrowIfNull(tagNames);

        var normalizedPairs = tagNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Select(n => new { Original = n, Normalized = n.ToUpperInvariant() })
            .GroupBy(x => x.Normalized, StringComparer.Ordinal)
            .Select(g => g.First()) // choose a representative original
            .ToArray();

        if (normalizedPairs.Length == 0)
            return Array.Empty<Tag>();

        var keys = normalizedPairs.Select(p => p.Normalized).ToArray();

        var existing = await db.Tags
            .Where(t => keys.Contains(t.NormalizedName))
            .ToListAsync(ct);

        var existingSet = existing
            .Select(t => t.NormalizedName)
            .ToHashSet(StringComparer.Ordinal);

        var newTags = normalizedPairs
            .Where(p => !existingSet.Contains(p.Normalized))
            .Select(p => new Tag
            {
                Name = p.Original,
                NormalizedName = p.Normalized 
            })
            .ToList();

        if (newTags.Count > 0)
            db.Tags.AddRange(newTags);

        return existing.Concat(newTags).ToList();
    }

    public async Task<IReadOnlyList<Tag>> SearchAsync(string? search, int limit, CancellationToken ct)
    {
        var normalizedLimit = Math.Clamp(limit, 1, 50);

        var query = db.Tags.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim().ToUpperInvariant();
            query = query.Where(t => t.NormalizedName.Contains(needle));
        }

        return await query
            .OrderBy(t => t.Name)
            .Take(normalizedLimit)
            .ToListAsync(ct);
    }
}
