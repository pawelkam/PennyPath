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
[Route("api/expense-categories")]
[ServiceFilter(typeof(RequireHttpsForAuthFilter), Order = int.MinValue)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExpenseCategoriesController(UserManager<IdentityUser> users,
    PennyPathIdentityDbContext database) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return AuthProblems.Unauthorized();
        if (!await database.SpendingProfiles.AnyAsync(value => value.OwnerId == user.Id, cancellationToken))
        {
            return AuthProblems.SettingsRequired();
        }

        var categories = await database.ExpenseCategories.AsNoTracking().Where(value => value.OwnerId == user.Id)
            .OrderBy(value => value.DisplayOrder).ThenBy(value => value.CategoryId).ToListAsync(cancellationToken);
        var children = await database.ExpenseSubcategories.AsNoTracking().Where(value => value.OwnerId == user.Id)
            .OrderBy(value => value.DisplayOrder).ThenBy(value => value.SubcategoryId).ToListAsync(cancellationToken);
        return Ok(new ExpenseCategoriesResponse(categories.Select(category =>
            new ExpenseCategoryDto(category.CategoryId, category.Name,
                children.Where(child => child.CategoryId == category.CategoryId)
                    .Select(child => new ExpenseSubcategoryDto(child.SubcategoryId, child.Name)).ToArray())).ToArray()));
    }
}
