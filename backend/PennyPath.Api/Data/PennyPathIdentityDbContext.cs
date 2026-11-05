using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PennyPath.Api.Data;

public class PennyPathIdentityDbContext : IdentityDbContext<IdentityUser>
{
    public PennyPathIdentityDbContext(DbContextOptions<PennyPathIdentityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The database also enforces uniqueness when two registrations race.
        builder.Entity<IdentityUser>()
            .HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique();
    }
}
