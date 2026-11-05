using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PennyPath.Api.Auth;
using PennyPath.Api.Data;

namespace PennyPath.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
[ServiceFilter(typeof(RequireHttpsForAuthFilter), Order = int.MinValue)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> users;
    private readonly SignInManager<IdentityUser> signIn;
    private readonly PennyPathIdentityDbContext database;

    public AuthController(UserManager<IdentityUser> users, SignInManager<IdentityUser> signIn,
        PennyPathIdentityDbContext database)
    {
        this.users = users;
        this.signIn = signIn;
        this.database = database;
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    public ActionResult<CsrfResponse> Csrf([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfResponse(tokens.RequestToken!));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [EnableRateLimiting(AuthConfiguration.AttemptsPolicy)]
    public async Task<IActionResult> Register(AuthCredentials credentials)
    {
        var user = new IdentityUser(credentials.Email!) { Email = credentials.Email! };
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, credentials.Password!);
        }
        catch (DbUpdateException)
        {
            // Reconcile a uniqueness race without depending on provider-specific errors.
            database.ChangeTracker.Clear();
            if (await users.FindByEmailAsync(credentials.Email!) is not null)
            {
                return DuplicateEmail();
            }

            throw;
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return DuplicateEmail();
            }

            var errors = result.Errors.GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal)
                ? "password" : "email").ToDictionary(group => group.Key, group => group.Select(error => error.Code).ToArray());
            return AuthProblems.Result(400, "validation_failed", "Check the submitted fields.", errors);
        }

        return StatusCode(201, new AccountResponse(user.Id, user.Email!));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [EnableRateLimiting(AuthConfiguration.AttemptsPolicy)]
    public async Task<IActionResult> Login(AuthCredentials credentials)
    {
        var user = await users.FindByEmailAsync(credentials.Email!);
        if (user is null || !(await signIn.PasswordSignInAsync(user, credentials.Password!,
                isPersistent: true, lockoutOnFailure: true)).Succeeded)
        {
            return AuthProblems.Result(401, "invalid_credentials", "The email or password is incorrect.");
        }

        return Ok(new AccountResponse(user.Id, user.Email!));
    }

    [HttpGet("me")]
    public async Task<ActionResult<AccountResponse>> Me()
    {
        var user = await users.GetUserAsync(User);
        return user is null
            ? AuthProblems.Result(401, "unauthorized", "Authentication is required.")
            : Ok(new AccountResponse(user.Id, user.Email!));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return NoContent();
    }

    private static ObjectResult DuplicateEmail() =>
        AuthProblems.Result(409, "duplicate_email", "This email is unavailable.");
}
