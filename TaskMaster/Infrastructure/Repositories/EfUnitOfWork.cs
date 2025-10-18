using Domain.Abstract.Persistence;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

public sealed class EfUnitOfWork(ApplicationDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}