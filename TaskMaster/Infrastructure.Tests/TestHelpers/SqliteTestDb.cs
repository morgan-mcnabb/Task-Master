using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Persistence;


namespace Infrastructure.Tests.TestHelpers;

public sealed class SqliteTestDb : IAsyncDisposable
{
    public ApplicationDbContext Db { get; }

    private readonly SqliteConnection _connection;

    private SqliteTestDb(ApplicationDbContext db, SqliteConnection connection)
    {
        Db = db;
        _connection = connection;
    }

    public static async Task<SqliteTestDb> CreateAsync(string userId = "u1", bool isAuthenticated = true)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var currentUser = new FakeCurrentUser(userId, isAuthenticated);
        var db = new ApplicationDbContext(options, currentUser);

        // Build schema (we want actual relational behavior)
        await db.Database.EnsureCreatedAsync();

        return new SqliteTestDb(db, connection);
    }

    public async ValueTask DisposeAsync()
    {
        await Db.DisposeAsync();
        await _connection.DisposeAsync();
    }
}