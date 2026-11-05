using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace PennyPath.Api.Auth;

public sealed class AuthCredentials
{
    private string? email;

    [Required, EmailAddress, StringLength(256)]
    public string? Email
    {
        get => email;
        init => email = value?.Trim();
    }

    [Required(AllowEmptyStrings = true), StringLength(128)]
    public string? Password { get; init; }
}

public sealed record AccountResponse(string UserId, string Email);
public sealed record CsrfResponse(string RequestToken);

public static class AuthProblems
{
    public static ProblemDetails Details(int status, string code, string title,
        Dictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Type = "about:blank" };
        problem.Extensions["code"] = code;
        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        return problem;
    }

    public static ObjectResult Result(int status, string code, string title,
        Dictionary<string, string[]>? errors = null) => new(Details(status, code, title, errors))
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };

    public static Task WriteAsync(HttpContext context, int status, string code, string title)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(Details(status, code, title), options: null,
            contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
