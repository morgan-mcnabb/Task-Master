namespace Domain.Common.Abstract;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    string? UserId { get; }
}