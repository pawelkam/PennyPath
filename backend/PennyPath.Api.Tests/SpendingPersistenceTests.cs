using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PennyPath.Api.Data;
using PennyPath.Api.Spending;
using Xunit;

namespace PennyPath.Api.Tests;

[Trait("Category", "SpendingPersistence")]
public class SpendingPersistenceTests : IDisposable
{
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("pennypath-spending-");

    [Fact]
    public async Task ExpensesAndSettingsSurviveReopeningWithoutChangingIdentity()
    {
        var categorized = CreateExpense(subcategoryId: "other", amount: 12.34m);
        var categoryOnly = CreateExpense(amount: 0.10m);
        categoryOnly.PurchaseDate = new DateOnly(2027, 1, 15);

        await using (var database = await CreateDatabaseAsync())
        {
            await SeedCategoriesAsync(database);
            database.SpendingProfiles.Add(new SpendingProfile { OwnerId = "owner-a", TimeZoneId = "Europe/Warsaw" });
            database.Expenses.AddRange(categorized, categoryOnly);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var reopened = CreateContext();
        var stored = await reopened.Expenses.SingleAsync(value => value.Id == categorized.Id,
            TestContext.Current.CancellationToken);
        var withoutSubcategory = await reopened.Expenses.SingleAsync(value => value.Id == categoryOnly.Id,
            TestContext.Current.CancellationToken);
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.NotEqual(stored.Id, withoutSubcategory.Id);
        Assert.Equal("owner-a", stored.OwnerId);
        Assert.Equal("Groceries", stored.Name);
        Assert.Equal(12.34m, stored.Amount);
        Assert.Equal(categorized.PurchaseDate, stored.PurchaseDate);
        Assert.Equal(categorized.RecordedAtUtc, stored.RecordedAtUtc);
        Assert.Equal("food", stored.CategoryId);
        Assert.Equal("other", stored.SubcategoryId);
        Assert.Null(withoutSubcategory.SubcategoryId);
        Assert.Equal(0.10m, withoutSubcategory.Amount);
        Assert.Equal(new DateOnly(2027, 1, 15), withoutSubcategory.PurchaseDate);
        Assert.Equal("Europe/Warsaw", (await reopened.SpendingProfiles.SingleAsync(
            TestContext.Current.CancellationToken)).TimeZoneId);
        var owner = await reopened.Users.SingleAsync(value => value.Id == "owner-a", TestContext.Current.CancellationToken);
        Assert.Equal("owner-a@example.test", owner.Email);
        Assert.Equal("original-stamp", owner.SecurityStamp);
        Assert.Equal(2, await reopened.Users.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreatingTheSchemaDoesNotGuessAProfileOrSeedCategoriesForExistingUsers()
    {
        await using var database = await CreateDatabaseAsync();

        Assert.Equal(2, await database.Users.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.SpendingProfiles.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.ExpenseCategories.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.ExpenseSubcategories.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CategoryAndSubcategoryIdsAreScopedByOwnerAndParent()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedCategoriesAsync(database);
        database.Expenses.AddRange(
            CreateExpense(subcategoryId: "other", amount: 15m),
            CreateExpense(categoryId: "transport", subcategoryId: "other", amount: 20m),
            CreateExpense(ownerId: "owner-b", subcategoryId: "other", amount: 5m));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var reopened = CreateContext();
        var rows = await reopened.Expenses.ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, rows.Count);
        Assert.Contains(rows, value => value.OwnerId == "owner-a" && value.CategoryId == "food" && value.Amount == 15m);
        Assert.Contains(rows, value => value.OwnerId == "owner-a" && value.CategoryId == "transport" && value.Amount == 20m);
        Assert.Contains(rows, value => value.OwnerId == "owner-b" && value.CategoryId == "food" && value.Amount == 5m);
    }

    [Theory]
    [InlineData("private-b", null)]
    [InlineData("food", "private-b-only")]
    [InlineData("food", "fuel")]
    public async Task ExpenseCannotReferenceAnotherOwnerOrCategory(string categoryId, string? subcategoryId)
    {
        await using var database = await CreateDatabaseAsync();
        await SeedCategoriesAsync(database);
        database.Expenses.Add(CreateExpense(categoryId: categoryId, subcategoryId: subcategoryId));

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(787, Assert.IsType<SqliteException>(failure.InnerException).SqliteExtendedErrorCode);
    }

    [Theory]
    [InlineData("0.00")]
    [InlineData("-0.01")]
    public async Task DatabaseRejectsZeroAndNegativeExpenseAmounts(string amount)
    {
        await using var database = await CreateDatabaseAsync();
        await SeedCategoriesAsync(database);
        database.Expenses.Add(CreateExpense(amount: decimal.Parse(amount, CultureInfo.InvariantCulture)));

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(275, Assert.IsType<SqliteException>(failure.InnerException).SqliteExtendedErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" padded ")]
    public async Task DatabaseRequiresANonemptyTrimmedExpenseName(string name)
    {
        await using var database = await CreateDatabaseAsync();
        await SeedCategoriesAsync(database);
        var expense = CreateExpense();
        expense.Name = name;
        database.Expenses.Add(expense);

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(275, Assert.IsType<SqliteException>(failure.InnerException).SqliteExtendedErrorCode);
    }

    [Fact]
    public async Task DatabaseRejectsExpenseNamesLongerThanTwoHundredCharacters()
    {
        await using var database = await CreateDatabaseAsync();
        await SeedCategoriesAsync(database);
        var expense = CreateExpense();
        expense.Name = new string('a', 201);
        database.Expenses.Add(expense);

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(275, Assert.IsType<SqliteException>(failure.InnerException).SqliteExtendedErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletingReferencedCategoryOrSubcategoryCannotDeleteTheExpense(bool deleteSubcategory)
    {
        var categoryId = deleteSubcategory ? "food" : "standalone";
        var expense = CreateExpense(categoryId: categoryId, subcategoryId: deleteSubcategory ? "other" : null);
        await using (var database = await CreateDatabaseAsync())
        {
            await SeedCategoriesAsync(database);
            database.ExpenseCategories.Add(new ExpenseCategory
            {
                OwnerId = "owner-a", CategoryId = "standalone", Name = "Standalone"
            });
            database.Expenses.Add(expense);
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Load only the principal: the database must protect rows without EF tracking them.
        await using (var database = CreateContext())
        {
            if (deleteSubcategory)
            {
                var subcategory = await database.ExpenseSubcategories.SingleAsync(
                    value => value.OwnerId == "owner-a" && value.CategoryId == "food" && value.SubcategoryId == "other",
                    TestContext.Current.CancellationToken);
                database.ExpenseSubcategories.Remove(subcategory);
            }
            else
            {
                var category = await database.ExpenseCategories.SingleAsync(
                    value => value.OwnerId == "owner-a" && value.CategoryId == "standalone",
                    TestContext.Current.CancellationToken);
                database.ExpenseCategories.Remove(category);
            }

            var failure = await Assert.ThrowsAsync<DbUpdateException>(() =>
                database.SaveChangesAsync(TestContext.Current.CancellationToken));
            // SQLite implements immediate RESTRICT through its foreign-key trigger.
            Assert.Equal(1811, Assert.IsType<SqliteException>(failure.InnerException).SqliteExtendedErrorCode);
        }

        await using var reopened = CreateContext();
        Assert.True(await reopened.Expenses.AnyAsync(value => value.Id == expense.Id,
            TestContext.Current.CancellationToken));
    }

    private PennyPathIdentityDbContext CreateContext()
    {
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory.FullName, "spending.db"),
            Pooling = false,
            ForeignKeys = true
        };
        return new PennyPathIdentityDbContext(new DbContextOptionsBuilder<PennyPathIdentityDbContext>()
            .UseSqlite(connection.ConnectionString).Options);
    }

    private async Task<PennyPathIdentityDbContext> CreateDatabaseAsync()
    {
        var database = CreateContext();
        try
        {
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            database.Users.AddRange(
                new IdentityUser("owner-a")
                {
                    Id = "owner-a", Email = "owner-a@example.test", NormalizedEmail = "OWNER-A@EXAMPLE.TEST",
                    SecurityStamp = "original-stamp"
                },
                new IdentityUser("owner-b")
                {
                    Id = "owner-b", Email = "owner-b@example.test", NormalizedEmail = "OWNER-B@EXAMPLE.TEST"
                });
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    private static async Task SeedCategoriesAsync(PennyPathIdentityDbContext database)
    {
        database.ExpenseCategories.AddRange(
            new ExpenseCategory { OwnerId = "owner-a", CategoryId = "food", Name = "Food A" },
            new ExpenseCategory { OwnerId = "owner-a", CategoryId = "transport", Name = "Transport A" },
            new ExpenseCategory { OwnerId = "owner-b", CategoryId = "food", Name = "Food B" },
            new ExpenseCategory { OwnerId = "owner-b", CategoryId = "private-b", Name = "Private B" });
        database.ExpenseSubcategories.AddRange(
            new ExpenseSubcategory { OwnerId = "owner-a", CategoryId = "food", SubcategoryId = "other", Name = "Other food A" },
            new ExpenseSubcategory { OwnerId = "owner-a", CategoryId = "transport", SubcategoryId = "other", Name = "Other transport A" },
            new ExpenseSubcategory { OwnerId = "owner-a", CategoryId = "transport", SubcategoryId = "fuel", Name = "Fuel A" },
            new ExpenseSubcategory { OwnerId = "owner-b", CategoryId = "food", SubcategoryId = "other", Name = "Other food B" },
            new ExpenseSubcategory { OwnerId = "owner-b", CategoryId = "food", SubcategoryId = "private-b-only", Name = "Private subcategory B" });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Expense CreateExpense(string ownerId = "owner-a", string categoryId = "food",
        string? subcategoryId = null, decimal amount = 12.34m) => new()
        {
            OwnerId = ownerId,
            Name = "Groceries",
            Amount = amount,
            PurchaseDate = new DateOnly(2026, 11, 1),
            RecordedAtUtc = new DateTime(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc),
            CategoryId = categoryId,
            SubcategoryId = subcategoryId
        };

    public void Dispose()
    {
        directory.Delete(recursive: true);
    }
}
