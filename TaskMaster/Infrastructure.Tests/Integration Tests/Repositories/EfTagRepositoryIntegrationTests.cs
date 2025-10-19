using FluentAssertions;
using Domain.Tasks;
using Infrastructure.Repositories;
using Infrastructure.Tests.TestHelpers;

namespace Infrastructure.Tests.IntegrationTests.Repositories;

public sealed class EfTagRepositoryIntegrationTests
{
    [Fact]
    public async Task EnsureTagsExistAsync_CreatesMissing_TreatsNamesCaseInsensitively_AndReturnsFullSet()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;
        var repo = new EfTagRepository(db);

        // Mix of dupes and whitespace
        var input = new[] { "work", "Work", "  life  " };

        var ensured = await repo.EnsureTagsExistAsync(input, default);
        await db.SaveChangesAsync();

        ensured.Select(t => t.NormalizedName).Should().BeEquivalentTo(new[] { "WORK", "LIFE" });

        var all = await repo.GetAllAsync(CancellationToken.None);
        // Repo returns OrderBy(Name), so assert deterministic order:
        all.Select(t => t.Name).Should().Equal("life", "work");
        all.All(t => t.OwnerUserId == "u1").Should().BeTrue("owner is stamped on insert");

        // Calling again with existing + new should not duplicate
        var ensured2 = await repo.EnsureTagsExistAsync(new[] { "Work", "new" }, default);
        await db.SaveChangesAsync();

        var all2 = await repo.GetAllAsync(CancellationToken.None);
        all2.Select(t => t.NormalizedName).Should().BeEquivalentTo(new[] { "LIFE", "NEW", "WORK" });
        ensured2.Select(t => t.NormalizedName).Should().BeEquivalentTo(new[] { "WORK", "NEW" });
    }

    [Fact]
    public async Task SearchAsync_IsCaseInsensitive_ContainsMatch_AndHonorsLimit()
    {
        await using var test = await SqliteTestDb.CreateAsync(userId: "u1");
        var db = test.Db;
        var repo = new EfTagRepository(db);

        db.Tags.AddRange(
            new Tag { Name = "Home",      NormalizedName = "HOME" },
            new Tag { Name = "Homework",  NormalizedName = "HOMEWORK" },
            new Tag { Name = "Work",      NormalizedName = "WORK" },
            new Tag { Name = "Personal",  NormalizedName = "PERSONAL" }
        );
        await db.SaveChangesAsync();

        var results = await repo.SearchAsync("work", limit: 10, default);
        results.Select(t => t.Name).Should().Equal("Homework", "Work");

        var limited = await repo.SearchAsync("work", limit: 1, default);
        limited.Should().HaveCount(1);
        limited[0].Name.Should().Be("Homework"); // ordered by Name
    }
}
