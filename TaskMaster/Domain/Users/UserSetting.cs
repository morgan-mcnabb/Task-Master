namespace Domain.Users;

public sealed class UserSetting
{
    public string UserId { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Value { get; set; } = null!;
}