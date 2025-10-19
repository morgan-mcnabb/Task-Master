using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests.TestHelpers;

internal static class DbContextFactory
{
    public static ApplicationDbContext Create(string userId = "user-1", bool isAuthenticated = true)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"infra-tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        var currentUser = new FakeCurrentUser(userId, isAuthenticated);
        return new ApplicationDbContext(options, currentUser);
    }
}