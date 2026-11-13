using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PennyPath.Api.Auth;
using PennyPath.Api.Data;
using PennyPath.Api.Spending;
using Xunit;

namespace PennyPath.Api.Tests;

[Trait("Category", "Spending")]
public class SpendingApiTests : IAsyncLifetime
{
    private const string Password = "reference passphrase";
    private readonly TestApplicationFactory factory = new();
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;
    private static readonly string[] GetRoutes =
        ["/api/spending/time-zones", "/api/spending/settings", "/api/expense-categories", "/api/spending/overview"];

    public async ValueTask InitializeAsync()
    {
        factory.Clock.SetUtcNow(DateTimeOffset.Parse("2026-11-13T12:00:00Z", CultureInfo.InvariantCulture));
        await factory.InitializeDatabaseAsync();
    }

    public async ValueTask DisposeAsync() => await factory.DisposeAsync();

    [Fact]
    public async Task AnonymousRequestsAreUnauthorizedWithoutRedirects()
    {
        using var browser = factory.CreateClockBrowser();
        foreach (var path in GetRoutes)
        {
            using var response = await browser.GetAsync(path, Cancellation);
            await ProblemAsync(response, 401, "unauthorized");
        }

        foreach (var path in new[] { "/api/spending/settings", "/api/expenses" })
        {
            using var response = await browser.PostAsJsonAsync(path, new { }, Cancellation);
            await ProblemAsync(response, 401, "unauthorized");
        }
    }

    [Fact]
    public async Task ReadsNeverInitializeAndFirstExplicitChoiceCreatesTheApprovedCatalog()
    {
        using var browser = factory.CreateClockBrowser();
        var account = await SignInAsync(browser);
        Assert.Null((await GetAsync<SpendingSettingsResponse>(browser, "/api/spending/settings")).TimeZoneId);
        var zones = await GetAsync<TimeZonesResponse>(browser, "/api/spending/time-zones");
        Assert.Contains("UTC", zones.TimeZoneIds);
        Assert.Contains("Europe/Warsaw", zones.TimeZoneIds);
        foreach (var path in new[] { "/api/expense-categories", "/api/spending/overview" })
        {
            using var response = await browser.GetAsync(path, Cancellation);
            await ProblemAsync(response, 409, "settings_required");
        }

        using (var response = await PostAsync(browser, "/api/expenses", ExpenseBody()))
            await ProblemAsync(response, 409, "settings_required");
        await InDatabaseAsync(async database =>
        {
            Assert.Empty(await database.SpendingProfiles.ToListAsync(Cancellation));
            Assert.Empty(await database.ExpenseCategories.ToListAsync(Cancellation));
            Assert.Empty(await database.ExpenseSubcategories.ToListAsync(Cancellation));
        });
        var saved = await SettingsAsync(browser, "Europe/Warsaw");
        Assert.Equal("Europe/Warsaw", saved.TimeZoneId);
        Assert.Equal(new DateOnly(2026, 11, 13), saved.Today);
        Assert.Equal(new SpendingPeriod(new DateOnly(2026, 11, 1), new DateOnly(2026, 12, 1)), saved.Period);
        var catalog = await GetAsync<ExpenseCategoriesResponse>(browser, "/api/expense-categories");
        Assert.Equal(new[] { "food", "transport", "home", "health", "entertainment", "miscellaneous" },
            catalog.Categories.Select(value => value.Id));
        Assert.Equal(new[] { "Jedzenie", "Transport", "Dom", "Zdrowie", "Rozrywka", "Pozostałe" },
            catalog.Categories.Select(value => value.Name));
        Assert.Equal(new[] { "groceries", "restaurants", "drinks", "other" }, catalog.Categories[0].Subcategories.Select(value => value.Id));
        Assert.Equal(17, catalog.Categories.Sum(value => value.Subcategories.Count));
        var empty = await OverviewAsync(browser);
        Assert.Equal("0.00", empty.Summary.Total);
        Assert.Equal(0, empty.Summary.ExpenseCount);
        Assert.Empty(empty.Summary.Categories);
        Assert.Empty(empty.Register.Items);
        Assert.Equal("current", empty.Register.Scope);
        Assert.Equal(1, empty.Register.Page);
        Assert.Equal(50, empty.Register.PageSize);
        await InDatabaseAsync(async database =>
            Assert.Equal(account.UserId, (await database.SpendingProfiles.SingleAsync(Cancellation)).OwnerId));
    }

    [Fact]
    public async Task PostsRequireFreshCsrfAndHttpsIncludingRejectedResponses()
    {
        using var browser = factory.CreateClockBrowser();
        var anonymous = await CsrfAsync(browser);
        await SignInAsync(browser);
        foreach (var path in new[] { "/api/spending/settings", "/api/expenses" })
        {
            foreach (var token in new string?[] { null, "invalid", anonymous })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
                if (token is not null) request.Headers.Add(AuthConfiguration.CsrfHeader, token);
                using var response = await browser.SendAsync(request, Cancellation);
                await ProblemAsync(response, 400, "csrf_invalid");
            }
        }

        using var login = await PostAsync(browser, "/api/auth/login", new { email = "owner@example.test", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var authCookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(AuthConfiguration.CookieName + "=", StringComparison.Ordinal)).Split(';')[0];
        using var http = factory.CreateBrowser(handleCookies: false);
        http.DefaultRequestHeaders.Add("Cookie", authCookie);
        foreach (var path in GetRoutes)
        {
            using var response = await http.GetAsync("http://localhost" + path, Cancellation);
            await ProblemAsync(response, 400, "https_required");
        }

        foreach (var path in new[] { "/api/spending/settings", "/api/expenses" })
        {
            using var response = await http.PostAsJsonAsync("http://localhost" + path, new { }, Cancellation);
            await ProblemAsync(response, 400, "https_required");
        }
    }

    [Fact]
    public async Task SettingsPersistOnAnotherDeviceAndZoneChangesPreserveCatalogAndDates()
    {
        factory.Clock.SetUtcNow(DateTimeOffset.Parse("2026-11-01T00:30:00Z", CultureInfo.InvariantCulture));
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        await SettingsAsync(browser, "Europe/Warsaw");
        var expense = await CreateAsync(browser, ExpenseBody(date: "2026-11-01"));
        await InDatabaseAsync(async database =>
        {
            var category = await database.ExpenseCategories.SingleAsync(value => value.CategoryId == "food", Cancellation);
            category.Name = "Zmieniona nazwa";
            await database.SaveChangesAsync(Cancellation);
        });
        var changed = await SettingsAsync(browser, "America/Los_Angeles");
        Assert.Equal(new DateOnly(2026, 10, 31), changed.Today);
        Assert.Equal(new SpendingPeriod(new DateOnly(2026, 10, 1), new DateOnly(2026, 11, 1)), changed.Period);
        await SettingsAsync(browser, "America/Los_Angeles");
        using var anotherDevice = factory.CreateClockBrowser();
        using (var login = await PostAsync(anotherDevice, "/api/auth/login", new { email = "owner@example.test", password = Password }))
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("America/Los_Angeles", (await GetAsync<SpendingSettingsResponse>(anotherDevice, "/api/spending/settings")).TimeZoneId);
        var catalog = await GetAsync<ExpenseCategoriesResponse>(anotherDevice, "/api/expense-categories");
        Assert.Equal(6, catalog.Categories.Count);
        Assert.Equal("Zmieniona nazwa", catalog.Categories[0].Name);
        Assert.Equal(17, catalog.Categories.Sum(value => value.Subcategories.Count));
        Assert.Empty((await OverviewAsync(anotherDevice)).Register.Items);
        var history = await OverviewAsync(anotherDevice, "?scope=all");
        Assert.Equal(expense.Id, Assert.Single(history.Register.Items).Id);
        Assert.Equal(new DateOnly(2026, 11, 1), history.Register.Items[0].PurchaseDate);
        Assert.Equal("0.00", history.Summary.Total);
        await SettingsAsync(browser, "Europe/Warsaw");
        Assert.Equal("1.00", (await OverviewAsync(browser)).Summary.Total);
    }

    [Fact]
    public async Task ExactCentsNullAndOtherRemainSeparateAndFutureDatesUsePurchaseMonth()
    {
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        await SettingsAsync(browser, "UTC");
        var first = await CreateAsync(browser, ExpenseBody(amount: "0.10", subcategory: "other"));
        var second = await CreateAsync(browser, ExpenseBody(amount: "0.20", subcategory: "other"));
        var future = await CreateAsync(browser, ExpenseBody(amount: "0.01", date: "2026-11-30"));
        var nextMonth = await CreateAsync(browser, ExpenseBody(amount: "100.00", date: "2026-12-01"));
        var previousMonth = await CreateAsync(browser, ExpenseBody(amount: "200.00", date: "2026-10-31"));
        var overview = await OverviewAsync(browser);
        Assert.Equal("0.31", overview.Summary.Total);
        Assert.Equal(3, overview.Summary.ExpenseCount);
        Assert.Equal(new[] { first.Id, second.Id, future.Id }.Order(), overview.Register.Items.Select(value => value.Id).Order());
        Assert.Equal(future.Id, overview.Register.Items[0].Id);
        var category = Assert.Single(overview.Summary.Categories);
        Assert.Equal("0.31", category.Total);
        Assert.Equal(2, category.Subcategories.Count);
        Assert.Equal(new SubcategorySummaryDto(null, "Bez podkategorii", "0.01"), category.Subcategories.Single(value => value.Id is null));
        Assert.Equal("0.30", category.Subcategories.Single(value => value.Id == "other").Total);
        Assert.Null(future.SubcategoryId);
        Assert.Null(future.SubcategoryName);
        Assert.NotEqual(first.Id, second.Id);
        var history = await OverviewAsync(browser, "?scope=all");
        Assert.Equal("0.31", history.Summary.Total);
        Assert.Equal(5, history.Register.TotalCount);
        Assert.Equal(nextMonth.Id, history.Register.Items[0].Id);
        Assert.Equal(previousMonth.Id, history.Register.Items[^1].Id);
    }

    [Theory]
    [InlineData("2024-02-15T12:00:00Z", "2024-01-31", "2024-02-01", "2024-02-29", "2024-03-01")]
    [InlineData("2026-03-29T01:00:00Z", "2026-02-28", "2026-03-01", "2026-03-31", "2026-04-01")]
    [InlineData("2025-12-15T12:00:00Z", "2025-11-30", "2025-12-01", "2025-12-31", "2026-01-01")]
    public async Task PersistedDatesIncludeBothMonthEndsAndExcludeAdjacentMonths(string instant,
        string before, string start, string last, string end)
    {
        factory.Clock.SetUtcNow(DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture));
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        await SettingsAsync(browser, "Europe/Warsaw");
        await CreateAsync(browser, ExpenseBody(amount: "100.00", date: before));
        var first = await CreateAsync(browser, ExpenseBody(amount: "0.10", date: start));
        var final = await CreateAsync(browser, ExpenseBody(amount: "0.20", date: last));
        await CreateAsync(browser, ExpenseBody(amount: "200.00", date: end));
        var overview = await OverviewAsync(browser);
        Assert.Equal("0.30", overview.Summary.Total);
        Assert.Equal(2, overview.Summary.ExpenseCount);
        Assert.Equal(new[] { final.Id, first.Id }, overview.Register.Items.Select(value => value.Id));
        Assert.Equal(DateOnly.Parse(start, CultureInfo.InvariantCulture), overview.Period.StartInclusive);
        Assert.Equal(DateOnly.Parse(end, CultureInfo.InvariantCulture), overview.Period.EndExclusive);
    }

    [Theory]
    [InlineData("12.349", "12.34")]
    [InlineData("12.999", "12.99")]
    [InlineData("0.019", "0.01")]
    [InlineData("999999999999.999", "999999999999.99")]
    public async Task ConfirmedPostContainsCanonicalAmountAndPersistsIt(string input, string expected)
    {
        using var browser = factory.CreateClockBrowser();
        var owner = await SignInAsync(browser);
        await SettingsAsync(browser, "UTC");
        var expense = await CreateAsync(browser, new
        {
            name = "  Zakup  ", amount = input, purchaseDate = "2026-11-13", categoryId = "food", subcategoryId = (string?)null,
            ownerId = "injected-owner", recordedAtUtc = "1900-01-01T00:00:00Z", id = Guid.Empty, categoryName = "injected-name"
        });
        Assert.Equal(expected, expense.Amount);
        Assert.Equal("Zakup", expense.Name);
        Assert.Equal("Jedzenie", expense.CategoryName);
        Assert.NotEqual(Guid.Empty, expense.Id);
        Assert.Equal(factory.Clock.GetUtcNow().UtcDateTime, expense.RecordedAtUtc);
        Assert.Equal(DateTimeKind.Utc, expense.RecordedAtUtc.Kind);
        await InDatabaseAsync(async database =>
        {
            var stored = await database.Expenses.SingleAsync(Cancellation);
            Assert.Equal(owner.UserId, stored.OwnerId);
            Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), stored.Amount);
        });
        Assert.Equal(expected, (await OverviewAsync(browser)).Summary.Total);
    }

    [Theory]
    [InlineData("name", "")]
    [InlineData("name", "   ")]
    [InlineData("amount", "0.009")]
    [InlineData("amount", "0")]
    [InlineData("amount", "-1")]
    [InlineData("amount", "1,00")]
    [InlineData("amount", "1e3")]
    [InlineData("amount", "1000000000000.00")]
    [InlineData("purchaseDate", "2026-02-30")]
    [InlineData("purchaseDate", "2026-11-13T12:00:00Z")]
    [InlineData("categoryId", "missing")]
    [InlineData("subcategoryId", "fuel")]
    [InlineData("subcategoryId", "")]
    public async Task InvalidValuesHaveSafeFieldErrorsAndWriteNothing(string field, string value)
    {
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        await SettingsAsync(browser, "UTC");
        var body = ExpenseBody();
        body[field] = value;
        using var response = await PostAsync(browser, "/api/expenses", body);
        await ProblemAsync(response, 400, "validation_failed", field);
        Assert.Empty((await OverviewAsync(browser)).Register.Items);
    }

    [Fact]
    public async Task MissingOversizedAndWrongJsonTypesMapToStableFields()
    {
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        await SettingsAsync(browser, "UTC");
        foreach (var field in new[] { "name", "amount", "purchaseDate", "categoryId" })
        {
            var body = ExpenseBody();
            body.Remove(field);
            using var response = await PostAsync(browser, "/api/expenses", body);
            await ProblemAsync(response, 400, "validation_failed", field);
        }

        foreach (var (field, size) in new[] { ("name", 201), ("amount", 65), ("categoryId", 65), ("subcategoryId", 65) })
        {
            var body = ExpenseBody();
            body[field] = new string('1', size);
            using var response = await PostAsync(browser, "/api/expenses", body);
            await ProblemAsync(response, 400, "validation_failed", field);
        }

        var typed = ExpenseBody();
        typed["amount"] = 12.34;
        using (var response = await PostAsync(browser, "/api/expenses", typed))
            await ProblemAsync(response, 400, "validation_failed", "amount");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/expenses");
        request.Headers.Add(AuthConfiguration.CsrfHeader, await CsrfAsync(browser));
        request.Content = new StringContent("{\"unknownSecret\":\"do-not-echo\",", Encoding.UTF8, "application/json");
        using var malformed = await browser.SendAsync(request, Cancellation);
        await ProblemAsync(malformed, 400, "validation_failed", "body");
        Assert.DoesNotContain("do-not-echo", await malformed.Content.ReadAsStringAsync(Cancellation));
    }

    [Theory]
    [InlineData("scope=unknown", "scope")]
    [InlineData("page=0", "page")]
    [InlineData("page=-1", "page")]
    [InlineData("page=invalid", "page")]
    [InlineData("page=999999999999", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("pageSize=no", "pageSize")]
    public async Task InvalidRegisterQueryHasSafeFieldError(string query, string field)
    {
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        await SettingsAsync(browser, "UTC");
        using var response = await browser.GetAsync("/api/spending/overview?" + query, Cancellation);
        await ProblemAsync(response, 400, "validation_failed", field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Wrong/Zone")]
    [InlineData(" Europe/Warsaw")]
    public async Task InvalidTimeZoneDoesNotCreateSettingsOrCatalog(string zone)
    {
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        using var response = await PostAsync(browser, "/api/spending/settings", new { timeZoneId = zone });
        await ProblemAsync(response, 400, "validation_failed", "timeZoneId");
        Assert.Null((await GetAsync<SpendingSettingsResponse>(browser, "/api/spending/settings")).TimeZoneId);
        await InDatabaseAsync(async database => Assert.Empty(await database.ExpenseCategories.ToListAsync(Cancellation)));
    }

    [Fact]
    public async Task TwoAccountsCannotReadOrAssignEachOthersCatalogAndExpenses()
    {
        factory.Clock.SetUtcNow(DateTimeOffset.Parse("2026-11-01T00:30:00Z", CultureInfo.InvariantCulture));
        using var first = factory.CreateClockBrowser();
        using var second = factory.CreateClockBrowser();
        var ownerA = await SignInAsync(first);
        var ownerB = await SignInAsync(second, "second@example.test");
        await SettingsAsync(first, "Europe/Warsaw");
        await SettingsAsync(second, "America/Los_Angeles");
        await InDatabaseAsync(async database =>
        {
            database.ExpenseCategories.Add(new ExpenseCategory { OwnerId = ownerB.UserId, CategoryId = "private", Name = "Secret category" });
            database.ExpenseSubcategories.Add(new ExpenseSubcategory
            {
                OwnerId = ownerB.UserId, CategoryId = "food", SubcategoryId = "private", Name = "Secret child"
            });
            await database.SaveChangesAsync(Cancellation);
        });
        await CreateAsync(first, ExpenseBody(amount: "1.00", date: "2026-11-01"));
        var bExpense = await CreateAsync(second, ExpenseBody(amount: "2.00", date: "2026-10-31"));
        var a = await OverviewAsync(first, "?scope=all&ownerId=" + ownerB.UserId);
        var b = await OverviewAsync(second, "?scope=all&ownerId=" + ownerA.UserId);
        Assert.Equal("1.00", a.Summary.Total);
        Assert.Equal("2.00", b.Summary.Total);
        Assert.DoesNotContain(a.Register.Items, value => value.Id == bExpense.Id);
        Assert.Equal(1, a.Register.TotalCount);
        Assert.Equal(1, b.Register.TotalCount);
        var catalog = await GetAsync<ExpenseCategoriesResponse>(first, "/api/expense-categories?ownerId=" + ownerB.UserId);
        Assert.DoesNotContain(catalog.Categories, value => value.Id == "private");
        Assert.DoesNotContain(catalog.Categories.SelectMany(value => value.Subcategories), value => value.Id == "private");
        var foreignCategory = ExpenseBody();
        foreignCategory["categoryId"] = "private";
        using (var response = await PostAsync(first, "/api/expenses", foreignCategory))
            await ProblemAsync(response, 400, "validation_failed", "categoryId");
        using (var response = await PostAsync(first, "/api/expenses", ExpenseBody(subcategory: "private")))
        {
            await ProblemAsync(response, 400, "validation_failed", "subcategoryId");
            Assert.DoesNotContain("Secret", await response.Content.ReadAsStringAsync(Cancellation));
        }
    }

    [Fact]
    public async Task SummaryCoversEveryPageAndOrderingIsStableForMatchingDates()
    {
        using var browser = factory.CreateClockBrowser();
        var owner = await SignInAsync(browser);
        await SettingsAsync(browser, "UTC");
        var ids = new List<Guid>();
        await InDatabaseAsync(async database =>
        {
            for (var index = 0; index < 55; index++)
            {
                var id = Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}");
                ids.Add(id);
                database.Expenses.Add(new Expense
                {
                    Id = id, OwnerId = owner.UserId, Name = "Same purchase", Amount = 0.01m,
                    PurchaseDate = new DateOnly(2026, 11, 13), RecordedAtUtc = factory.Clock.GetUtcNow().UtcDateTime,
                    CategoryId = "food"
                });
            }

            await database.SaveChangesAsync(Cancellation);
        });
        var first = await OverviewAsync(browser);
        var second = await OverviewAsync(browser, "?page=2");
        Assert.Equal("0.55", first.Summary.Total);
        Assert.Equal("0.55", second.Summary.Total);
        Assert.Equal(55, first.Summary.ExpenseCount);
        Assert.Equal(55, first.Register.TotalCount);
        Assert.Equal(50, first.Register.Items.Count);
        Assert.Equal(5, second.Register.Items.Count);
        Assert.Equal(ids.OrderDescending(), first.Register.Items.Concat(second.Register.Items).Select(value => value.Id));
        var distant = await OverviewAsync(browser, "?page=2147483647&pageSize=100");
        Assert.Empty(distant.Register.Items);
        Assert.Equal("0.55", distant.Summary.Total);
    }

    [Fact]
    public async Task TwoFirstSettingsRequestsInitializeExactlyOnce()
    {
        await using var racing = new TestApplicationFactory(synchronizeSpendingSettings: true);
        await racing.InitializeDatabaseAsync();
        using var browser = racing.CreateClockBrowser();
        var owner = await SignInAsync(browser);
        var token = await CsrfAsync(browser);
        async Task<HttpResponseMessage> SendAsync(string zone)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/spending/settings")
            {
                Content = JsonContent.Create(new { timeZoneId = zone })
            };
            request.Headers.Add(AuthConfiguration.CsrfHeader, token);
            return await browser.SendAsync(request, Cancellation);
        }

        var responses = await Task.WhenAll(SendAsync("Europe/Warsaw"), SendAsync("America/Los_Angeles"));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            await using var scope = racing.Services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>();
            Assert.Equal(owner.UserId, (await database.SpendingProfiles.SingleAsync(Cancellation)).OwnerId);
            Assert.Equal(6, await database.ExpenseCategories.CountAsync(Cancellation));
            Assert.Equal(17, await database.ExpenseSubcategories.CountAsync(Cancellation));
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }

    [Fact]
    public async Task FailedCatalogInitializationRollsBackProfileAndCanBeExplicitlyRetried()
    {
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        await InDatabaseAsync(database => database.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER fail_catalog BEFORE INSERT ON "ExpenseSubcategories"
            BEGIN SELECT RAISE(ABORT, 'synthetic catalog failure'); END;
            """, Cancellation));
        using (var response = await PostAsync(browser, "/api/spending/settings", new { timeZoneId = "UTC" }))
        {
            await ProblemAsync(response, 500, "server_error");
            Assert.DoesNotContain("synthetic catalog failure", await response.Content.ReadAsStringAsync(Cancellation));
        }

        await InDatabaseAsync(async database =>
        {
            Assert.Empty(await database.SpendingProfiles.ToListAsync(Cancellation));
            Assert.Empty(await database.ExpenseCategories.ToListAsync(Cancellation));
            Assert.Empty(await database.ExpenseSubcategories.ToListAsync(Cancellation));
            await database.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_catalog", Cancellation);
        });
        await SettingsAsync(browser, "UTC");
        Assert.Equal(6, (await GetAsync<ExpenseCategoriesResponse>(browser, "/api/expense-categories")).Categories.Count);
    }

    [Fact]
    public async Task DeletedAccountIsRejectedOnEverySpendingRoute()
    {
        using var browser = factory.CreateClockBrowser();
        var owner = await SignInAsync(browser);
        var token = await CsrfAsync(browser);
        await InDatabaseAsync(database => database.Users.Where(value => value.Id == owner.UserId).ExecuteDeleteAsync(Cancellation));
        foreach (var path in GetRoutes)
        {
            using var response = await browser.GetAsync(path, Cancellation);
            await ProblemAsync(response, 401, "unauthorized");
        }

        foreach (var path in new[] { "/api/spending/settings", "/api/expenses" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = path.EndsWith("settings", StringComparison.Ordinal)
                    ? JsonContent.Create(new { timeZoneId = "UTC" }) : JsonContent.Create(ExpenseBody())
            };
            request.Headers.Add(AuthConfiguration.CsrfHeader, token);
            using var response = await browser.SendAsync(request, Cancellation);
            await ProblemAsync(response, 401, "unauthorized");
        }
    }

    [Fact]
    public async Task HistoricalBrowserSessionStillExpiresAtTheSevenDayBoundary()
    {
        factory.Clock.SetUtcNow(DateTimeOffset.Parse("2024-02-15T12:00:00Z", CultureInfo.InvariantCulture));
        using var browser = factory.CreateClockBrowser();
        await SignInAsync(browser);
        await SettingsAsync(browser, "UTC");
        factory.Clock.Advance(TimeSpan.FromDays(7) - TimeSpan.FromTicks(1));
        Assert.Equal("UTC", (await GetAsync<SpendingSettingsResponse>(browser, "/api/spending/settings")).TimeZoneId);
        factory.Clock.Advance(TimeSpan.FromTicks(1));
        using var expired = await browser.GetAsync("/api/spending/settings", Cancellation);
        await ProblemAsync(expired, 401, "unauthorized");
    }

    private static Dictionary<string, object?> ExpenseBody(string amount = "1.00", string date = "2026-11-13", string? subcategory = null) =>
        new() { ["name"] = "Zakup", ["amount"] = amount, ["purchaseDate"] = date, ["categoryId"] = "food", ["subcategoryId"] = subcategory };

    private async Task InDatabaseAsync(Func<PennyPathIdentityDbContext, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>());
    }

    private static async Task<AccountResponse> SignInAsync(HttpClient browser, string email = "owner@example.test")
    {
        using var registration = await PostAsync(browser, "/api/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        using var login = await PostAsync(browser, "/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<AccountResponse>(Cancellation))!;
    }

    private static async Task<string> CsrfAsync(HttpClient browser) =>
        (await GetAsync<CsrfResponse>(browser, "/api/auth/csrf")).RequestToken;

    private static async Task<HttpResponseMessage> PostAsync(HttpClient browser, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add(AuthConfiguration.CsrfHeader, await CsrfAsync(browser));
        return await browser.SendAsync(request, Cancellation);
    }

    private static async Task<T> GetAsync<T>(HttpClient browser, string path)
    {
        using var response = await browser.GetAsync(path, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<T>(Cancellation))!;
    }

    private static async Task<SavedSpendingSettingsResponse> SettingsAsync(HttpClient browser, string zone)
    {
        using var response = await PostAsync(browser, "/api/spending/settings", new { timeZoneId = zone });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<SavedSpendingSettingsResponse>(Cancellation))!;
    }

    private static Task<SpendingOverviewDto> OverviewAsync(HttpClient browser, string query = "") =>
        GetAsync<SpendingOverviewDto>(browser, "/api/spending/overview" + query);

    private static async Task<ExpenseDto> CreateAsync(HttpClient browser, object body)
    {
        using var response = await PostAsync(browser, "/api/expenses", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        return (await response.Content.ReadFromJsonAsync<ExpenseDto>(Cancellation))!;
    }

    private static async Task ProblemAsync(HttpResponseMessage response, int status, string code, string? field = null)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        Assert.Equal(status, body.GetProperty("status").GetInt32());
        Assert.Equal(code, body.GetProperty("code").GetString());
        if (field is not null) Assert.True(body.GetProperty("errors").TryGetProperty(field, out _));
    }
}
