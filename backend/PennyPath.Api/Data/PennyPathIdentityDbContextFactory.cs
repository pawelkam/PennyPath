using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PennyPath.Api.Data;

public class PennyPathIdentityDbContextFactory : IDesignTimeDbContextFactory<PennyPathIdentityDbContext>
{
    public PennyPathIdentityDbContext CreateDbContext(string[] args)
    {
        // SQL generation needs a provider, not a live server or runtime credentials.
        var options = new DbContextOptionsBuilder<PennyPathIdentityDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=pennypath;Username=pennypath")
            .Options;

        return new PennyPathIdentityDbContext(options);
    }
}
