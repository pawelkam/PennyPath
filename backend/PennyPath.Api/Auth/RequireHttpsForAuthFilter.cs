using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PennyPath.Api.Auth;

public sealed class RequireHttpsForAuthFilter : IAsyncAuthorizationFilter, IOrderedFilter
{
    private readonly IAntiforgery antiforgery;

    public RequireHttpsForAuthFilter(IAntiforgery antiforgery)
    {
        this.antiforgery = antiforgery;
    }

    public int Order => int.MinValue;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!context.HttpContext.Request.IsHttps)
        {
            context.Result = AuthProblems.Result(400, "https_required", "HTTPS is required.");
            return;
        }

        if (HttpMethods.IsPost(context.HttpContext.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext);
            }
            catch (AntiforgeryValidationException)
            {
                context.Result = AuthProblems.Result(400, "csrf_invalid", "The CSRF token is missing or invalid.");
            }
        }
    }
}
