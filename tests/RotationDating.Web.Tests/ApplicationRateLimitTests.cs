using System.Net;
using Microsoft.AspNetCore.Http;
using RotationDating.Web.Services.ApplicationForm;

namespace RotationDating.Web.Tests;

public class ApplicationRateLimitTests
{
    private static HttpContext Request(string remoteIp, string? cfConnectingIp = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
        if (cfConnectingIp is not null)
            context.Request.Headers["CF-Connecting-IP"] = cfConnectingIp;
        return context;
    }

    [Fact]
    public void Visitors_behind_the_same_cloudflare_edge_get_separate_counters()
    {
        var a = ApplicationFormEndpoints.ClientKey(Request("172.70.1.1", "211.234.10.1"));
        var b = ApplicationFormEndpoints.ClientKey(Request("172.70.1.1", "175.223.20.2"));

        Assert.NotEqual(a, b);
        Assert.Equal("211.234.10.1", a);
    }

    [Fact]
    public void Falls_back_to_connection_address_without_cloudflare()
    {
        Assert.Equal("10.0.0.5", ApplicationFormEndpoints.ClientKey(Request("10.0.0.5")));
    }

    [Fact]
    public void Ignores_a_malformed_header()
    {
        Assert.Equal("10.0.0.5", ApplicationFormEndpoints.ClientKey(Request("10.0.0.5", "not-an-ip")));
    }

    [Fact]
    public void Limit_allows_a_busy_shared_network()
    {
        Assert.True(ApplicationFormEndpoints.SubmitsPerWindow >= 30);
    }
}
