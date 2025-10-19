using Microsoft.AspNetCore.Http;

namespace TaskMasterApi.Services;

public sealed class HttpCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    public Guid? CorrelationId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null) return null;

            if (context.Items.TryGetValue(CorrelationIds.HttpContextItemKey, out var value) && value is Guid g)
                return g;

            return null; // middleware should have set it; null is a safe fallback
        }
    }
}