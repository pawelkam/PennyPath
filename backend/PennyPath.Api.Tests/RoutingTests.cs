using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace PennyPath.Api.Tests;

[Trait("Category", "Auth")]
public class RoutingTests
{
    [Theory]
    [InlineData("/api")]
    [InlineData("/api/unknown")]
    [InlineData("/api/unknown.json")]
    [InlineData("/API/unknown")]
    [InlineData("/assets/missing.js")]
    public async Task MissingApiAndAssetsReturn404WithoutAuthentication(string path)
    {
        await using var factory = new TestApplicationFactory();
        using var browser = factory.CreateBrowser();
        foreach (var scheme in new[] { "http", "https" })
        {
            using var response = await browser.GetAsync($"{scheme}://localhost{path}", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.DoesNotContain("<div id=\"app\">", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task SpaAndExistingAssetsRemainPublicAndAccountApiRemainsProtected()
    {
        var webRoot = Directory.CreateTempSubdirectory("pennypath-routing-");
        try
        {
            const string html = "<!doctype html><div id=\"app\"></div>";
            Directory.CreateDirectory(Path.Combine(webRoot.FullName, "assets"));
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "index.html"), html, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(webRoot.FullName, "assets", "app.js"), "console.log('app');", TestContext.Current.CancellationToken);
            await using var factory = new TestApplicationFactory();
            await using var application = factory.WithWebHostBuilder(builder => builder.UseWebRoot(webRoot.FullName));
            using var browser = application.CreateClient(new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://localhost"),
                AllowAutoRedirect = false
            });

            foreach (var path in new[] { "/", "/expenses/example" })
            {
                using var response = await browser.GetAsync(path, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
                Assert.Equal(html, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            }

            using var asset = await browser.GetAsync("/assets/app.js", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, asset.StatusCode);
            Assert.Equal("console.log('app');", await asset.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            using var missing = await browser.GetAsync("/assets/missing.js", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            using var account = await browser.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, account.StatusCode);
        }
        finally
        {
            webRoot.Delete(recursive: true);
        }
    }
}
