namespace TaskMasterApi.Contracts.Auth;

public sealed class RegisterRequest
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}