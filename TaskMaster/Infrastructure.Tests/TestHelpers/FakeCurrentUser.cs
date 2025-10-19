using Domain.Common.Abstract;

namespace Infrastructure.Tests.TestHelpers;

internal sealed class FakeCurrentUser(string? userId, bool isAuthenticated) : ICurrentUser
{
    public bool IsAuthenticated { get; } = isAuthenticated;
    public string? UserId { get; } = userId;
}