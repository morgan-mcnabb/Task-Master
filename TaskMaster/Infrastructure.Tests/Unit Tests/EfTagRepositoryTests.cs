using Domain.Tasks;
using FluentAssertions;
using Infrastructure.Persistence;
using Infrastructure.Repositories;
using Infrastructure.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Unit_Tests;

public sealed class EfTagRepositoryTests
{
    private static EfTagRepository Repo(ApplicationDbContext db) => new(db);

    [Fact]
    public async Task EnsureTagsExist_CreatesMissing_AndReturnsExistingPlusNew()
    {
        await using var db = DbContextFactory.Create();

        // pre-existing tag
        var existing = new Tag { OwnerUserId = "user-1", Name = "Home", NormalizedName = "HOME" };
        db.Tags.Add(existing);
        await db.SaveChangesAsync();

        var sut = Repo(db);

        var input = new[] { "home", "Work" };
        var result = await sut.EnsureTagsExistAsync(input, CancellationToken.None);

        // Should return existing + new
        result.Should().HaveCount(2);
        result.Select(t => t.NormalizedName).Should().BeEquivalentTo(new[] { "HOME", "WORK" });

        // Persist newly created ones
        await db.SaveChangesAsync();

        (await db.Tags.CountAsync()).Should().Be(2);
        (await db.Tags.Select(t => t.Name).ToListAsync()).Should().BeEquivalentTo(new[] { "Home", "Work" });
    }

    [Fact]
    public async Task EnsureTagsExist_Trims_Normalizes_And_Deduplicates_IgnoringBlanks()
    {
        await using var db = DbContextFactory.Create();
        var sut = Repo(db);

        var input = new[] { "  Home  ", "", "HOME", "Work", "work", "   " };
        var result = await sut.EnsureTagsExistAsync(input, CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(t => t.NormalizedName).Should().BeEquivalentTo(new[] { "HOME", "WORK" });

        await db.SaveChangesAsync();

        var tags = await db.Tags.OrderBy(t => t.Name).ToListAsync();
        tags.Should().HaveCount(2);
        tags[0].Name.Should().Be("Home");
        tags[1].Name.Should().Be("Work");
    }

    [Fact]
    public async Task Search_FiltersByContains_CaseInsensitive_SortsByName_And_Limits()
    {
        await using var db = DbContextFactory.Create();

        db.Tags.AddRange(
            new Tag { OwnerUserId = "user-1", Name = "Alpha", NormalizedName = "ALPHA" },
            new Tag { OwnerUserId = "user-1", Name = "Alpine", NormalizedName = "ALPINE" },
            new Tag { OwnerUserId = "user-1", Name = "Beta", NormalizedName = "BETA" },
            new Tag { OwnerUserId = "user-1", Name = "Delta", NormalizedName = "DELTA" }
        );
        await db.SaveChangesAsync();

        var sut = Repo(db);

        var all = await sut.SearchAsync(null, 10, CancellationToken.None);
        all.Select(t => t.Name).Should().ContainInOrder("Alpha", "Alpine", "Beta", "Delta");

        var match = await sut.SearchAsync("alp", 10, CancellationToken.None);
        match.Select(t => t.Name).Should().BeEquivalentTo(new[] { "Alpha", "Alpine" }, options => options.WithStrictOrdering());

        var limited = await sut.SearchAsync(null, 2, CancellationToken.None);
        limited.Should().HaveCount(2);
    }

    [Fact]
    public async Task Getters_Work_ById_And_ByNormalizedName()
    {
        await using var db = DbContextFactory.Create();

        var tag = new Tag { OwnerUserId = "user-1", Name = "Gamma", NormalizedName = "GAMMA" };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();

        var sut = Repo(db);

        var byId = await sut.GetByIdAsync(tag.Id, CancellationToken.None);
        byId!.Name.Should().Be("Gamma");

        var byNorm = await sut.GetByNormalizedNameAsync("GAMMA", CancellationToken.None);
        byNorm!.Id.Should().Be(tag.Id);
    }
}
