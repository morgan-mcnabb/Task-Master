namespace TaskMasterApi.Contracts.Auth;

public sealed class UpdateSettingsRequest
{
    public Dictionary<string, string>? Settings { get; set; }
}