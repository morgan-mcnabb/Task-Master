namespace TaskMasterApi.Services;

public static class CorrelationIds
{
    public const string HeaderName = "X-Correlation-Id";
    public const string HttpContextItemKey = "CorrelationId"; 
}