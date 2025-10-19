using Domain.Tasks;
using FluentAssertions;
using Infrastructure.Repositories;
using Infrastructure.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.Unit_Tests;

public sealed class EfUnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_PersistsChanges()
    {
        await using var db = DbContextFactory.Create();
        var uow = new EfUnitOfWork(db);

        db.Tasks.Add(new TaskItem { OwnerUserId = "user-1", Title = "Persist me" });
        var before = await db.Tasks.CountAsync();

        before.Should().Be(0);

        await uow.SaveChangesAsync();

        var after = await db.Tasks.CountAsync();
        after.Should().Be(1);
    }
}