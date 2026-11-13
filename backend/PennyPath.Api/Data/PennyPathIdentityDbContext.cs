using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PennyPath.Api.Spending;

namespace PennyPath.Api.Data;

public class PennyPathIdentityDbContext : IdentityDbContext<IdentityUser>
{
    public PennyPathIdentityDbContext(DbContextOptions<PennyPathIdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<SpendingProfile> SpendingProfiles => Set<SpendingProfile>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<ExpenseSubcategory> ExpenseSubcategories => Set<ExpenseSubcategory>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // The database also enforces uniqueness when two registrations race.
        builder.Entity<IdentityUser>()
            .HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName("EmailIndex")
            .IsUnique();

        builder.Entity<SpendingProfile>(profile =>
        {
            profile.HasKey(value => value.OwnerId);
            profile.Property(value => value.TimeZoneId).HasMaxLength(128).IsRequired();
            profile.HasOne<IdentityUser>().WithOne().HasForeignKey<SpendingProfile>(value => value.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExpenseCategory>(category =>
        {
            category.HasKey(value => new { value.OwnerId, value.CategoryId });
            category.Property(value => value.CategoryId).HasMaxLength(64);
            category.Property(value => value.Name).HasMaxLength(100).IsRequired();
            category.HasOne<IdentityUser>().WithMany().HasForeignKey(value => value.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ExpenseSubcategory>(subcategory =>
        {
            subcategory.HasKey(value => new { value.OwnerId, value.CategoryId, value.SubcategoryId });
            subcategory.Property(value => value.CategoryId).HasMaxLength(64);
            subcategory.Property(value => value.SubcategoryId).HasMaxLength(64);
            subcategory.Property(value => value.Name).HasMaxLength(100).IsRequired();
            subcategory.HasOne<ExpenseCategory>().WithMany()
                .HasForeignKey(value => new { value.OwnerId, value.CategoryId })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Expense>(expense =>
        {
            expense.ToTable("Expenses", table =>
            {
                table.HasCheckConstraint("CK_Expenses_PositiveAmount", "\"Amount\" > 0");
                table.HasCheckConstraint("CK_Expenses_Name",
                    "length(trim(\"Name\")) BETWEEN 1 AND 200 AND \"Name\" = trim(\"Name\")");
            });
            expense.HasKey(value => value.Id);
            expense.Property(value => value.Id).ValueGeneratedNever();
            expense.Property(value => value.Name).HasMaxLength(200).IsRequired();
            expense.Property(value => value.Amount).HasPrecision(14, 2).HasColumnType("numeric(14,2)");
            expense.Property(value => value.PurchaseDate).HasColumnType("date");
            expense.Property(value => value.RecordedAtUtc).HasColumnType("timestamp with time zone");
            expense.Property(value => value.CategoryId).HasMaxLength(64).IsRequired();
            expense.Property(value => value.SubcategoryId).HasMaxLength(64);
            expense.HasOne<IdentityUser>().WithMany().HasForeignKey(value => value.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
            expense.HasOne<ExpenseCategory>().WithMany()
                .HasForeignKey(value => new { value.OwnerId, value.CategoryId })
                .OnDelete(DeleteBehavior.Restrict);
            // A null subcategory bypasses this FK; the category FK still validates ownership.
            expense.HasOne<ExpenseSubcategory>().WithMany()
                .HasForeignKey(value => new { value.OwnerId, value.CategoryId, value.SubcategoryId })
                .OnDelete(DeleteBehavior.Restrict);
            expense.HasIndex(value => new { value.OwnerId, value.PurchaseDate, value.RecordedAtUtc, value.Id })
                .IsDescending(false, true, true, true);
        });
    }
}
