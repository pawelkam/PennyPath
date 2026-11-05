using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PennyPath.Api.Data;
using Xunit;

namespace PennyPath.Api.Tests;

[Trait("Category", "Persistence")]
public class PersistenceTests : IDisposable
{
    private const string Password = "Reference password 123!";
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("pennypath-identity-");

    [Fact]
    public async Task AccountStoresASaltedHashAndAcceptsTheCorrectPassword()
    {
        await using var services = await CreateDatabaseAsync();
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var first = await CreateUserAsync(manager, "first@example.test");
        var second = await CreateUserAsync(manager, "second@example.test");
        var database = scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>();
        database.ChangeTracker.Clear();
        var stored = await database.Users.SingleAsync(user => user.Id == first.Id, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrEmpty(stored.PasswordHash));
        Assert.NotEqual(Password, stored.PasswordHash);
        Assert.NotEqual(first.PasswordHash, second.PasswordHash);
        Assert.True(await manager.CheckPasswordAsync(stored, Password));
    }

    [Fact]
    public async Task IncorrectPasswordIsRejected()
    {
        await using var services = await CreateDatabaseAsync();
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await CreateUserAsync(manager, "owner@example.test");

        Assert.False(await manager.CheckPasswordAsync(user, "Another password 123!"));
    }

    [Fact]
    public async Task EmailNormalizationFindsTheSameAccountAndRejectsDuplicates()
    {
        await using var services = await CreateDatabaseAsync();
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await CreateUserAsync(manager, "Owner@example.test");

        var found = await manager.FindByEmailAsync("OWNER@EXAMPLE.TEST");
        Assert.Equal(user.Id, found?.Id);
        Assert.Equal(manager.NormalizeEmail(user.Email), user.NormalizedEmail);

        var duplicate = new IdentityUser("another-user") { Email = "owner@EXAMPLE.test" };
        var result = await manager.CreateAsync(duplicate, Password);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, error => error.Code == "DuplicateEmail");
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateNormalizedEmailsEvenWhenValidationIsBypassed()
    {
        await using var services = await CreateDatabaseAsync();
        await using (var scope = services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            await CreateUserAsync(manager, "owner@example.test");
        }

        // A separate context bypasses UserManager's preflight duplicate check.
        await using var contender = services.CreateAsyncScope();
        var database = contender.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>();
        database.Users.Add(new IdentityUser("different-user")
        {
            Email = "OWNER@example.test",
            NormalizedEmail = "OWNER@EXAMPLE.TEST",
            NormalizedUserName = "DIFFERENT-USER"
        });

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(19, Assert.IsType<SqliteException>(exception.InnerException).SqliteErrorCode);
    }

    [Fact]
    public async Task DistinctIdsSurviveReopeningTheDatabaseAndChangingEmail()
    {
        string firstId;
        string secondId;
        await using (var services = await CreateDatabaseAsync())
        {
            await using var scope = services.CreateAsyncScope();
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var first = await CreateUserAsync(manager, "first@example.test");
            var second = await CreateUserAsync(manager, "second@example.test");
            firstId = first.Id;
            secondId = second.Id;
            Assert.False(string.IsNullOrWhiteSpace(firstId));
            Assert.False(string.IsNullOrWhiteSpace(secondId));
            Assert.NotEqual(firstId, secondId);
            Assert.NotEqual(first.Email, firstId);
            Assert.True((await manager.SetEmailAsync(first, "renamed@example.test")).Succeeded);
            Assert.Equal(firstId, first.Id);
        }

        await using var reopened = CreateServices();
        await using var reopenedScope = reopened.CreateAsyncScope();
        var reopenedManager = reopenedScope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.Equal(firstId, (await reopenedManager.FindByEmailAsync("renamed@example.test"))?.Id);
        Assert.Equal(secondId, (await reopenedManager.FindByEmailAsync("second@example.test"))?.Id);
        Assert.Null(await reopenedManager.FindByEmailAsync("first@example.test"));
    }

    private ServiceProvider CreateServices()
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory.FullName, "identity.db"),
            Pooling = false
        }.ConnectionString;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PennyPathIdentityDbContext>(options => options.UseSqlite(connectionString));
        services.AddIdentityCore<IdentityUser>(options => options.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<PennyPathIdentityDbContext>();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private async Task<ServiceProvider> CreateDatabaseAsync()
    {
        var services = CreateServices();
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>();
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        return services;
    }

    private static async Task<IdentityUser> CreateUserAsync(UserManager<IdentityUser> manager, string email)
    {
        var user = new IdentityUser(email) { Email = email };
        var result = await manager.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));
        return user;
    }

    public void Dispose()
    {
        directory.Delete(recursive: true);
    }
}
