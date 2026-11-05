using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using PennyPath.Api.Auth;
using PennyPath.Api.Data;
using Xunit;

namespace PennyPath.Api.Tests;

[Trait("Category", "Auth")]
public class AuthApiTests : IAsyncLifetime
{
    private const string Email = "owner@example.test";
    private const string Password = "reference passphrase";
    private readonly TestApplicationFactory factory = new();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.InitializeDatabaseAsync();
    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task RegistrationRequiresSeparateLoginAndNoEmailConfirmation()
    {
        using var browser = factory.CreateBrowser();
        using var registered = await RegisterAsync(browser);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.True(registered.Headers.CacheControl?.NoStore);
        Assert.DoesNotContain(Cookies(registered), cookie => cookie.Name == AuthConfiguration.CookieName);
        await AssertProblemAsync(await browser.GetAsync("/api/auth/me", Cancellation), 401, "unauthorized");
        using var login = await LoginAsync(browser);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var account = await AccountAsync(login);
        Assert.False(string.IsNullOrWhiteSpace(account.UserId));
        Assert.Equal(Email, account.Email);
        Assert.True(login.Headers.CacheControl?.NoStore);
        Assert.Equal(account, await AccountAsync(await browser.GetAsync("/api/auth/me", Cancellation)));
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(Email);
        Assert.NotNull(user);
        Assert.False(user.EmailConfirmed);
    }

    [Fact]
    public async Task CookieIsPersistentSecureAndReusableInANewBrowserSession()
    {
        using var browser = factory.CreateBrowser();
        using var registered = await RegisterAsync(browser);
        using var login = await LoginAsync(browser);
        var cookie = AuthCookie(login);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
        Assert.Equal("/", cookie.Path.ToString());
        Assert.False(cookie.Domain.HasValue);
        Assert.Equal(factory.Clock.GetUtcNow().AddDays(7), cookie.Expires);
        var ticket = Ticket(cookie);
        Assert.True(ticket.Properties.IsPersistent);
        Assert.Equal(factory.Clock.GetUtcNow().AddDays(7), ticket.Properties.ExpiresUtc);

        using var reopened = factory.CreateBrowser(handleCookies: false);
        reopened.DefaultRequestHeaders.Add("Cookie", $"{cookie.Name}={cookie.Value}");
        Assert.Equal(await AccountAsync(login), await AccountAsync(await reopened.GetAsync("/api/auth/me", Cancellation)));
    }

    [Theory]
    [InlineData(-1, 200)]
    [InlineData(0, 401)]
    [InlineData(1, 401)]
    public async Task SessionHonorsTheExactSevenDayBoundary(long offsetTicks, int status)
    {
        using var browser = factory.CreateBrowser();
        using var registered = await RegisterAsync(browser);
        using var login = await LoginAsync(browser);
        factory.Clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromTicks(offsetTicks));
        using var response = await browser.GetAsync("/api/auth/me", Cancellation);
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task PrincipalRenewalNeverExtendsTheOriginalDeadline()
    {
        using var browser = factory.CreateBrowser();
        using var registered = await RegisterAsync(browser);
        using var login = await LoginAsync(browser);
        var deadline = Ticket(AuthCookie(login)).Properties.ExpiresUtc;
        for (var i = 0; i < 2; i++)
        {
            factory.Clock.Advance(TimeSpan.FromDays(3));
            using var response = await browser.GetAsync("/api/auth/me", Cancellation);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var renewed = AuthCookie(response);
            Assert.Equal(deadline, renewed.Expires);
            Assert.Equal(deadline, Ticket(renewed).Properties.ExpiresUtc);
        }

        factory.Clock.Advance(TimeSpan.FromDays(1));
        await AssertProblemAsync(await browser.GetAsync("/api/auth/me", Cancellation), 401, "unauthorized");
    }

    [Fact]
    public async Task StandardSecurityStampValidationStillRejectsChangedCredentials()
    {
        using var browser = factory.CreateBrowser();
        using var registered = await RegisterAsync(browser);
        using var login = await LoginAsync(browser);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = await manager.FindByEmailAsync(Email);
            Assert.True((await manager.UpdateSecurityStampAsync(user!)).Succeeded);
        }

        factory.Clock.Advance(TimeSpan.FromMinutes(31));
        await AssertProblemAsync(await browser.GetAsync("/api/auth/me", Cancellation), 401, "unauthorized");
    }

    [Theory]
    [InlineData("register")]
    [InlineData("login")]
    public async Task AnonymousPostsRejectMissingAndInvalidCsrf(string action)
    {
        using var browser = factory.CreateBrowser();
        using var missing = await PostAsync(browser, action, null);
        await AssertProblemAsync(missing, 400, "csrf_invalid");
        using var invalid = await PostAsync(browser, action, "invalid-token");
        await AssertProblemAsync(invalid, 400, "csrf_invalid");
    }

    [Fact]
    public async Task CsrfTokenMustMatchTheCurrentIdentityAndLogoutDeletesAuthentication()
    {
        using var browser = factory.CreateBrowser();
        var anonymousToken = await CsrfAsync(browser);
        using var registered = await PostAsync(browser, "register", anonymousToken);
        using var login = await PostAsync(browser, "login", anonymousToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var stale = await PostAsync(browser, "logout", anonymousToken);
        await AssertProblemAsync(stale, 400, "csrf_invalid");
        using var missing = await PostAsync(browser, "logout", null);
        await AssertProblemAsync(missing, 400, "csrf_invalid");
        var authenticatedToken = await CsrfAsync(browser);
        using var logout = await PostAsync(browser, "logout", authenticatedToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        var deleted = AuthCookie(logout);
        Assert.True(deleted.Expires < DateTimeOffset.UtcNow);
        await AssertProblemAsync(await browser.GetAsync("/api/auth/me", Cancellation), 401, "unauthorized");
        using var expired = await PostAsync(browser, "logout", authenticatedToken);
        await AssertProblemAsync(expired, 401, "unauthorized");
    }

    [Fact]
    public async Task AccountsHaveSeparatePrincipalsAndRejectAnotherBrowsersCsrf()
    {
        using var first = factory.CreateBrowser();
        using var second = factory.CreateBrowser();
        using var firstRegistration = await RegisterAsync(first);
        using var secondRegistration = await RegisterAsync(second, "second@example.test");
        using var firstLogin = await LoginAsync(first);
        using var secondLogin = await LoginAsync(second, "second@example.test");
        var firstAccount = await AccountAsync(firstLogin);
        var secondAccount = await AccountAsync(secondLogin);
        Assert.NotEqual(firstAccount.UserId, secondAccount.UserId);
        Assert.Equal(firstAccount, await AccountAsync(await first.GetAsync(
            "/api/auth/me?userId=" + secondAccount.UserId, Cancellation)));
        Assert.Equal(secondAccount, await AccountAsync(await second.GetAsync("/api/auth/me", Cancellation)));
        using var wrongCsrf = await PostAsync(second, "logout", await CsrfAsync(first));
        await AssertProblemAsync(wrongCsrf, 400, "csrf_invalid");
    }

    [Fact]
    public async Task EmailIsTrimmedAndNormalizedButPasswordIsNotTrimmed()
    {
        const string password = "  spaced passphrase  ";
        using var browser = factory.CreateBrowser();
        using var registered = await RegisterAsync(browser, "  Owner@example.test  ", password);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);
        Assert.Equal("Owner@example.test", (await AccountAsync(registered)).Email);
        using var wrong = await LoginAsync(browser, "OWNER@EXAMPLE.TEST", password.Trim());
        await AssertProblemAsync(wrong, 401, "invalid_credentials");
        using var correct = await LoginAsync(browser, " OWNER@EXAMPLE.TEST ", password);
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
    }

    [Theory]
    [InlineData(11, 400)]
    [InlineData(12, 201)]
    [InlineData(128, 201)]
    [InlineData(129, 400)]
    public async Task PasswordLengthIsEnforcedWithoutCharacterClassRequirements(int length, int status)
    {
        using var browser = factory.CreateBrowser();
        using var response = await RegisterAsync(browser, password: new string('a', length));
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 400)
        {
            await AssertProblemAsync(response, 400, "validation_failed");
            var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
            Assert.True(body.GetProperty("errors").TryGetProperty("password", out _));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task InvalidEmailGivesFieldValidation(string email)
    {
        using var browser = factory.CreateBrowser();
        using var response = await RegisterAsync(browser, email);
        await AssertProblemAsync(response, 400, "validation_failed");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.True(body.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task CaseInsensitiveDuplicateEmailIsAConflict()
    {
        using var browser = factory.CreateBrowser();
        using var first = await RegisterAsync(browser);
        using var duplicate = await RegisterAsync(browser, " OWNER@EXAMPLE.TEST ");
        await AssertProblemAsync(duplicate, 409, "duplicate_email");
    }

    [Fact]
    public async Task ConcurrentRegistrationsReconcileTheDatabaseUniquenessRace()
    {
        await using var racing = new TestApplicationFactory(synchronizeRegistrations: true);
        await racing.InitializeDatabaseAsync();
        using var first = racing.CreateBrowser();
        using var second = racing.CreateBrowser();
        var firstToken = await CsrfAsync(first);
        var secondToken = await CsrfAsync(second);
        var responses = await Task.WhenAll(PostAsync(first, "register", firstToken), PostAsync(second, "register", secondToken));
        try
        {
            Assert.Equal(new[] { 201, 409 }, responses.Select(response => (int)response.StatusCode).Order().ToArray());
            await AssertProblemAsync(responses.Single(response => response.StatusCode == HttpStatusCode.Conflict), 409, "duplicate_email");
            await using var scope = racing.Services.CreateAsyncScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>()
                .Users.CountAsync(Cancellation));
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task UnknownEmailAndWrongPasswordHaveTheSameGenericFailure()
    {
        using var browser = factory.CreateBrowser();
        using var registered = await RegisterAsync(browser);
        using var wrong = await LoginAsync(browser, password: "wrong passphrase");
        using var unknown = await LoginAsync(browser, "unknown@example.test");
        await AssertProblemAsync(wrong, 401, "invalid_credentials");
        await AssertProblemAsync(unknown, 401, "invalid_credentials");
        Assert.Equal(await wrong.Content.ReadAsStringAsync(Cancellation), await unknown.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task FiveBadPasswordsLockTheAccountForFiveMinutes()
    {
        using var browser = factory.CreateBrowser();
        using var registered = await RegisterAsync(browser);
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await LoginAsync(browser, password: "wrong passphrase");
            await AssertProblemAsync(wrong, 401, "invalid_credentials");
        }

        using var blocked = await LoginAsync(browser);
        await AssertProblemAsync(blocked, 401, "invalid_credentials");
        await using var scope = factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await manager.FindByEmailAsync(Email);
        Assert.True(await manager.IsLockedOutAsync(user!));
        Assert.InRange((user!.LockoutEnd!.Value - DateTimeOffset.UtcNow).TotalSeconds, 240, 300);
    }

    [Fact]
    public async Task LoginAndRegistrationShareTenAttemptsPerMinuteAndReturnRetryAfter()
    {
        using var browser = factory.CreateBrowser();
        var token = await CsrfAsync(browser);
        for (var i = 0; i < 10; i++)
        {
            using var response = await PostAsync(browser, i % 2 == 0 ? "register" : "login", token,
                email: $"attempt{i}@example.test");
            Assert.Equal(i % 2 == 0 ? 201 : 401, (int)response.StatusCode);
        }

        using var limited = await PostAsync(browser, "register", token, email: "limited@example.test");
        await AssertProblemAsync(limited, 429, "rate_limited");
        Assert.True(limited.Headers.RetryAfter?.Delta > TimeSpan.Zero);
        using var http = await PostAsync(browser, "login", token, http: true);
        await AssertProblemAsync(http, 400, "https_required");
    }

    [Fact]
    public async Task HttpRejectsTokenAndCredentialRequestsBeforeWritingData()
    {
        using var browser = factory.CreateBrowser();
        await AssertProblemAsync(await browser.GetAsync("http://localhost/api/auth/csrf", Cancellation), 400, "https_required");
        foreach (var action in new[] { "register", "login" })
        {
            using var response = await PostAsync(browser, action, null, http: true);
            await AssertProblemAsync(response, 400, "https_required");
        }

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>().Users.CountAsync(Cancellation));
    }

    [Fact]
    public async Task CsrfCookieIsSecureAndTokenResponsesAreNotCacheable()
    {
        using var browser = factory.CreateBrowser();
        using var response = await browser.GetAsync("/api/auth/csrf", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var cookie = Assert.Single(Cookies(response));
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.False(string.IsNullOrWhiteSpace((await response.Content.ReadFromJsonAsync<CsrfResponse>(Cancellation))?.RequestToken));
    }

    [Fact]
    public async Task DatabaseFailuresDoNotExposeProviderOrSchemaDetails()
    {
        using var browser = factory.CreateBrowser();
        var token = await CsrfAsync(browser);
        factory.RemoveDatabase();
        using var response = await PostAsync(browser, "register", token);
        await AssertProblemAsync(response, 500, "server_error");
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        foreach (var secret in new[] { "SQLite", "AspNetUsers", Password, "StackTrace", ".db" })
        {
            Assert.DoesNotContain(secret, body);
        }
    }

    [Fact]
    public async Task MalformedJsonHasAStableErrorWithoutEchoingSubmittedSecrets()
    {
        using var browser = factory.CreateBrowser();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register");
        request.Headers.Add(AuthConfiguration.CsrfHeader, await CsrfAsync(browser));
        request.Content = new StringContent("{\"password\":\"synthetic secret\",\"email\":", Encoding.UTF8, "application/json");
        using var response = await browser.SendAsync(request, Cancellation);
        await AssertProblemAsync(response, 400, "validation_failed");
        Assert.DoesNotContain("synthetic secret", await response.Content.ReadAsStringAsync(Cancellation));
    }

    [Fact]
    public async Task StarterRoutesRemainPublicAndExtraIdentityEndpointsAreAbsent()
    {
        using var browser = factory.CreateBrowser();
        foreach (var path in new[] { "/health", "/weatherforecast" })
        {
            using var response = await browser.GetAsync(path, Cancellation);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        foreach (var path in new[] { "/api/unknown", "/api/auth/refresh", "/api/auth/manage/info", "/api/auth/forgotPassword" })
        {
            using var response = await browser.GetAsync(path, Cancellation);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    private static async Task<string> CsrfAsync(HttpClient browser)
    {
        using var response = await browser.GetAsync("/api/auth/csrf", Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CsrfResponse>(Cancellation))!.RequestToken;
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient browser, string email = Email, string password = Password) =>
        await PostAsync(browser, "register", await CsrfAsync(browser), email, password);

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient browser, string email = Email, string password = Password) =>
        await PostAsync(browser, "login", await CsrfAsync(browser), email, password);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient browser, string action, string? token,
        string email = Email, string password = Password, bool http = false)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, (http ? "http://localhost" : "") + "/api/auth/" + action);
        if (token is not null) request.Headers.Add(AuthConfiguration.CsrfHeader, token);
        if (action != "logout") request.Content = JsonContent.Create(new { email, password });
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<AccountResponse> AccountAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<AccountResponse>(Cancellation))!;

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    private static IList<SetCookieHeaderValue> Cookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList()) : [];

    private static SetCookieHeaderValue AuthCookie(HttpResponseMessage response) =>
        Assert.Single(Cookies(response), cookie => cookie.Name == AuthConfiguration.CookieName);

    private AuthenticationTicket Ticket(SetCookieHeaderValue cookie) => factory.Services
        .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme)
        .TicketDataFormat.Unprotect(cookie.Value.ToString())!;
}
