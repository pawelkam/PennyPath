using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PennyPath.Api.Data;
using Xunit;

namespace PennyPath.Api.Tests;

public sealed class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("pennypath-auth-");
    private readonly bool synchronizeRegistrations;
    private readonly bool synchronizeSpendingSettings;
    public TestTimeProvider Clock { get; } = new();

    public TestApplicationFactory(bool synchronizeRegistrations = false, bool synchronizeSpendingSettings = false)
    {
        this.synchronizeRegistrations = synchronizeRegistrations;
        this.synchronizeSpendingSettings = synchronizeSpendingSettings;
    }

    private string DatabaseFile => Path.Combine(directory.FullName, "identity.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PennyPathIdentityDbContext>();
            services.RemoveAll<DbContextOptions<PennyPathIdentityDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<PennyPathIdentityDbContext>>();
            var connection = new SqliteConnectionStringBuilder { DataSource = DatabaseFile, Pooling = false };
            services.AddDbContext<PennyPathIdentityDbContext>(options => options.UseSqlite(connection.ConnectionString));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            if (synchronizeRegistrations)
            {
                services.AddSingleton<IUserValidator<IdentityUser>, RegistrationBarrier>();
            }
            if (synchronizeSpendingSettings)
            {
                var barrier = new SpendingSettingsBarrier();
                services.Configure<MvcOptions>(options => options.Filters.Add(barrier));
            }
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>()
            .Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public HttpClient CreateBrowser(bool handleCookies = true) => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = handleCookies
    });

    public HttpClient CreateClockBrowser() =>
        CreateDefaultClient(new Uri("https://localhost"), new ClockCookieHandler(Clock));

    public void RemoveDatabase() => File.Delete(DatabaseFile);

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        directory.Delete(recursive: true);
    }

    private sealed class RegistrationBarrier : IUserValidator<IdentityUser>
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public async Task<IdentityResult> ValidateAsync(UserManager<IdentityUser> manager, IdentityUser user)
        {
            if (Interlocked.Increment(ref arrivals) >= 2)
            {
                ready.TrySetResult();
            }

            await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            return IdentityResult.Success;
        }
    }

    private sealed class SpendingSettingsBarrier : IAsyncActionFilter
    {
        private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.HttpContext.Request.Path == "/api/spending/settings" &&
                context.HttpContext.Request.Method == "POST")
            {
                if (Interlocked.Increment(ref arrivals) >= 2) ready.TrySetResult();
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(10), context.HttpContext.RequestAborted);
            }

            await next();
        }
    }
}
