using Microsoft.AspNetCore.Http;

namespace TaskMasterApi.Tests.TestHelpers;

internal static class HttpContextHelper
{
    public static DefaultHttpContext NewContext()
    {
        var ctx = new DefaultHttpContext
        {
            Response =
            {
                Body = new MemoryStream()
            }
        };
        return ctx;
    }
}