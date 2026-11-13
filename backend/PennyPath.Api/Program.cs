using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PennyPath.Api.Auth;
using PennyPath.Api.Data;
using PennyPath.Api.Spending;

var migrate = args.Contains("--migrate", StringComparer.Ordinal);
var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--migrate").ToArray());

// The one-shot command reports only a sanitized result, including on DB errors.
if (migrate)
{
    builder.Logging.ClearProviders();
}

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<PennyPathIdentityDbContext>(options =>
    options.UseNpgsql(DatabaseConfiguration.CreateConnectionString(builder.Configuration)));
builder.Services.AddIdentityCore<IdentityUser>(options => options.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<PennyPathIdentityDbContext>()
    .AddSignInManager();
builder.Services.AddPrivateAccountAccess();
builder.Services.AddScoped<SpendingSettingsService>();
builder.Services.AddScoped<SpendingReadService>();
builder.Services.AddLanHosting(builder.Configuration, builder.Environment);

if (migrate)
{
    await using var migrationApp = builder.Build();
    Environment.ExitCode = await IdentityMigrationRunner.RunAsync(migrationApp.Services);
    return;
}

var app = builder.Build();

// Only the configured Caddy address may supply the original HTTPS scheme/IP.
app.UseForwardedHeaders();
app.UseExceptionHandler(errorApp => errorApp.Run(context =>
    AuthProblems.WriteAsync(context, 500, "server_error", "The request could not be completed.")));

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Caddy terminates LAN TLS; loopback HTTP remains available for healthchecks.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();

// API typos must not return the SPA's HTML (including paths with extensions).
app.Map("/api/{**path}", () => Results.NotFound()).AllowAnonymous();
app.MapFallbackToFile("index.html").AllowAnonymous();

// Missing files bypass the SPA's :nonfile fallback and must remain public 404s.
app.MapFallback("{*path:file}", context =>
{
    context.Response.StatusCode = StatusCodes.Status404NotFound;
    return Task.CompletedTask;
}).AllowAnonymous();

app.Run();

public partial class Program { }
