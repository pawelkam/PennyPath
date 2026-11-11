using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PennyPath.Api.Auth;

public static class AuthConfiguration
{
    public const string CookieName = "__Host-PennyPath.Auth";
    public const string CsrfHeader = "X-CSRF-TOKEN";
    public const string AttemptsPolicy = "auth-attempts";
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);

    public static IServiceCollection AddLanHosting(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var protection = services.AddDataProtection()
            .SetApplicationName(environment.IsProduction() ? "PennyPath" : $"PennyPath.{environment.EnvironmentName}");
        if (environment.IsProduction())
        {
            var keysPath = configuration["DataProtection:KeysPath"];
            if (string.IsNullOrWhiteSpace(keysPath) || !Directory.Exists(keysPath))
            {
                throw new InvalidOperationException("Configure an existing private DataProtection:KeysPath directory.");
            }

            protection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            var proxyAddress = configuration["Proxy:KnownProxy"];
            if (!environment.IsProduction() && string.IsNullOrWhiteSpace(proxyAddress))
            {
                return;
            }

            if (!IPAddress.TryParse(proxyAddress, out var proxy))
            {
                throw new InvalidOperationException("Proxy:KnownProxy must be the exact reverse proxy IP address.");
            }

            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            options.RequireHeaderSymmetry = true;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.KnownProxies.Add(proxy);
        });

        return services;
    }

    public static IServiceCollection AddPrivateAccountAccess(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.Configure<IdentityOptions>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = string.Empty;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.SignIn.RequireConfirmedEmail = false;
            options.SignIn.RequireConfirmedAccount = false;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        });

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<TimeProvider>((options, clock) =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = "/";
                options.Cookie.Domain = null;
                options.ExpireTimeSpan = SessionLifetime;
                options.SlidingExpiration = false;
                options.TimeProvider = clock;
                options.Events.OnValidatePrincipal = async context =>
                {
                    var deadline = context.Properties.ExpiresUtc;
                    if (deadline is null || clock.GetUtcNow() >= deadline.Value)
                    {
                        context.RejectPrincipal();
                        return;
                    }

                    await SecurityStampValidator.ValidatePrincipalAsync(context);
                    // Identity may renew the principal; the original deadline stays fixed.
                    context.Properties.ExpiresUtc = deadline;
                };
                options.Events.OnRedirectToLogin = context => AuthProblems.WriteAsync(
                    context.HttpContext, 401, "unauthorized", "Authentication is required.");
                options.Events.OnRedirectToAccessDenied = context => AuthProblems.WriteAsync(
                    context.HttpContext, 403, "forbidden", "Access is denied.");
            });
        services.AddOptions<SecurityStampValidatorOptions>()
            .Configure<TimeProvider>((options, clock) => options.TimeProvider = clock);

        services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build());
        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfHeader;
            options.Cookie.Name = "__Host-PennyPath.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.Path = "/";
            options.Cookie.Domain = null;
        });
        services.AddScoped<RequireHttpsForAuthFilter>();
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
                .Select(entry => entry.Key.Split('.').Last().ToLowerInvariant())
                .Select(key => key is "email" or "password" ? key : "body")
                .Distinct().ToDictionary(key => key, _ => new[] { "invalid" });
            return AuthProblems.Result(400, "validation_failed", "Check the submitted fields.", errors);
        });
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(AttemptsPolicy, context =>
            {
                // HTTP is always rejected as https_required by the authorization filter.
                var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return !context.Request.IsHttps
                    ? RateLimitPartition.GetNoLimiter("http:" + ip)
                    : RateLimitPartition.GetFixedWindowLimiter("https:" + ip, _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retry = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                    ? delay : TimeSpan.FromMinutes(1);
                context.HttpContext.Response.Headers.RetryAfter = Math.Max(1, Math.Ceiling(retry.TotalSeconds))
                    .ToString(CultureInfo.InvariantCulture);
                await AuthProblems.WriteAsync(context.HttpContext, 429, "rate_limited", "Try again later.");
            };
        });
        return services;
    }
}
