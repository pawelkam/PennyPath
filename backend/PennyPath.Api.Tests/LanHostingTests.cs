using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace PennyPath.Api.Tests;

public class LanHostingTests
{
    [Theory]
    [InlineData("172.30.91.2", 200)]
    [InlineData("172.30.91.3", 400)]
    [InlineData("127.0.0.1", 400)]
    [InlineData("::1", 400)]
    public async Task OnlyConfiguredProxyCanSupplyHttpsScheme(string remoteAddress, int expectedStatus)
    {
        await using var application = new TestApplicationFactory();
        await using var configured = application.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Proxy:KnownProxy"] = "172.30.91.2" })));

        using var client = configured.CreateClient();
        var response = await configured.Server.SendAsync(context =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
            context.Request.Scheme = "http";
            context.Request.Method = "GET";
            context.Request.Path = "/api/auth/csrf";
            context.Request.Headers.Host = "localhost";
            context.Request.Headers["X-Forwarded-Proto"] = "https";
            context.Request.Headers["X-Forwarded-For"] = "192.0.2.10";
        }, TestContext.Current.CancellationToken);

        Assert.Equal(expectedStatus, response.Response.StatusCode);
        Assert.Equal(expectedStatus == 200 ? "https" : "http", response.Request.Scheme);
        Assert.Equal(expectedStatus == 200 ? "192.0.2.10" : remoteAddress,
            response.Connection.RemoteIpAddress!.ToString());
    }
}
