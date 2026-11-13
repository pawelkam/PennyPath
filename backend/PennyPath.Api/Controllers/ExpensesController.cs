using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PennyPath.Api.Auth;
using PennyPath.Api.Data;
using PennyPath.Api.Spending;

namespace PennyPath.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/expenses")]
[ServiceFilter(typeof(RequireHttpsForAuthFilter), Order = int.MinValue)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExpensesController(UserManager<IdentityUser> users,
    PennyPathIdentityDbContext database, TimeProvider clock) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateExpenseRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return AuthProblems.Unauthorized();
        if (!await database.SpendingProfiles.AnyAsync(value => value.OwnerId == user.Id, cancellationToken))
        {
            return AuthProblems.SettingsRequired();
        }

        var errors = new Dictionary<string, string[]>();
        if (!ExpenseAmount.TryParse(request.Amount, out var amount)) errors["amount"] = ["invalid"];
        if (!DateOnly.TryParseExact(request.PurchaseDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var purchaseDate)) errors["purchaseDate"] = ["invalid"];
        var category = await database.ExpenseCategories.AsNoTracking().SingleOrDefaultAsync(
            value => value.OwnerId == user.Id && value.CategoryId == request.CategoryId, cancellationToken);
        if (category is null) errors["categoryId"] = ["invalid"];
        ExpenseSubcategory? subcategory = null;
        if (request.SubcategoryId is not null)
        {
            subcategory = await database.ExpenseSubcategories.AsNoTracking().SingleOrDefaultAsync(
                value => value.OwnerId == user.Id && value.CategoryId == request.CategoryId &&
                         value.SubcategoryId == request.SubcategoryId, cancellationToken);
            if (subcategory is null) errors["subcategoryId"] = ["invalid"];
        }

        if (errors.Count > 0) return AuthProblems.Validation(errors);
        var expense = new Expense
        {
            OwnerId = user.Id, Name = request.Name!, Amount = amount, PurchaseDate = purchaseDate,
            RecordedAtUtc = clock.GetUtcNow().UtcDateTime, CategoryId = category!.CategoryId,
            SubcategoryId = subcategory?.SubcategoryId
        };
        database.Expenses.Add(expense);
        await database.SaveChangesAsync(cancellationToken);
        return StatusCode(201, SpendingReadService.ToDto(expense, category.Name, subcategory?.Name));
    }
}
