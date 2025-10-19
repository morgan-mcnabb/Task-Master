namespace TaskMasterApi.Middleware;

public sealed class SecurityHeadersOptions
{
    public string XContentTypeOptions { get; init; } = "nosniff";
    public string XFrameOptions { get; init; } = "DENY";
    public string ReferrerPolicy { get; init; } = "no-referrer";

    // Requested additions
    public string CrossOriginOpenerPolicy { get; init; } = "same-origin";
    public string CrossOriginResourcePolicy { get; init; } = "same-site";
    public string PermissionsPolicy { get; init; } = "camera=(), microphone=(), geolocation=()";
}