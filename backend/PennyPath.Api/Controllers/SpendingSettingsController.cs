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
[Route("api/spending")]
[ServiceFilter(typeof(RequireHttpsForAuthFilter), Order = int.MinValue)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SpendingSettingsController(UserManager<IdentityUser> users,
    PennyPathIdentityDbContext database, SpendingSettingsService settings) : ControllerBase
{
    [HttpGet("time-zones")]
    public async Task<IActionResult> TimeZones()
    {
        if (await users.GetUserAsync(User) is null) return AuthProblems.Unauthorized();
        return Ok(new TimeZonesResponse(SpendingPeriod.TimeZoneIds()));
    }

    [HttpGet("settings")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return AuthProblems.Unauthorized();
        var profile = await database.SpendingProfiles.AsNoTracking()
            .SingleOrDefaultAsync(value => value.OwnerId == user.Id, cancellationToken);
        return Ok(new SpendingSettingsResponse(profile?.TimeZoneId));
    }

    [HttpPost("settings")]
    public async Task<IActionResult> Save(SpendingSettingsRequest request, CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(User);
        if (user is null) return AuthProblems.Unauthorized();
        if (!SpendingPeriod.TryFindTimeZone(request.TimeZoneId, out _))
        {
            return AuthProblems.Validation(new() { ["timeZoneId"] = ["invalid"] });
        }

        return Ok(await settings.SaveAsync(user.Id, request.TimeZoneId!, cancellationToken));
    }
}
