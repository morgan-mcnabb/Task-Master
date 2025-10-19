namespace TaskMasterApi.Services;

public interface ICorrelationIdAccessor
{
    /// <summary>
    /// Current request correlation id as a Guid, if available.
    /// </summary>
    Guid? CorrelationId { get; }
}