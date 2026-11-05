using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PennyPath.Api.Data;

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
    .AddEntityFrameworkStores<PennyPathIdentityDbContext>();

if (migrate)
{
    await using var migrationApp = builder.Build();
    Environment.ExitCode = await IdentityMigrationRunner.RunAsync(migrationApp.Services);
    return;
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// The LAN deployment serves HTTP; TLS will be added with the future tunnel.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// API typos must not return the SPA's HTML (including paths with extensions).
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

app.Run();
