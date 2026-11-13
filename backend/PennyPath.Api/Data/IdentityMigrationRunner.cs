using Microsoft.EntityFrameworkCore;

namespace PennyPath.Api.Data;

public static class IdentityMigrationRunner
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        try
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PennyPathIdentityDbContext>();
            await database.Database.MigrateAsync();
            Console.WriteLine("Database migrations applied successfully.");
            return 0;
        }
        catch (Exception)
        {
            // Provider exceptions can contain configuration or connection details.
            Console.Error.WriteLine("Database migration failed. Check database configuration and availability.");
            return 1;
        }
    }
}
