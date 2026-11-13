using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PennyPath.Api.Auth;
using PennyPath.Api.Spending;

namespace PennyPath.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/spending/overview")]
[ServiceFilter(typeof(RequireHttpsForAuthFilter), Order = int.MinValue)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SpendingOverviewController(UserManager<IdentityUser> users,
    SpendingReadService spending) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] SpendingOverviewQuery query, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return AuthProblems.Unauthorized();
        var errors = new Dictionary<string, string[]>();
        if (query.Scope is not ("current" or "all")) errors["scope"] = ["invalid"];
        if (query.Page < 1) errors["page"] = ["invalid"];
        if (query.PageSize is < 1 or > 100) errors["pageSize"] = ["invalid"];
        if (errors.Count > 0) return AuthProblems.Validation(errors);
        var result = await spending.ReadAsync(user.Id, query, cancellationToken);
        return result is null ? AuthProblems.SettingsRequired() : Ok(result);
    }
}
