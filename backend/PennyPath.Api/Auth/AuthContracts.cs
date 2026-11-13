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
    public static string Field(string key) => key.Split('.').Last().ToLowerInvariant() switch
    {
        "email" => "email",
        "password" => "password",
        "name" => "name",
        "amount" => "amount",
        "purchasedate" => "purchaseDate",
        "categoryid" => "categoryId",
        "subcategoryid" => "subcategoryId",
        "timezoneid" => "timeZoneId",
        "scope" => "scope",
        "page" => "page",
        "pagesize" => "pageSize",
        _ => "body"
    };

    public static ObjectResult Validation(Dictionary<string, string[]> errors) =>
        Result(400, "validation_failed", "Check the submitted fields.", errors);

    public static ObjectResult SettingsRequired() =>
        Result(409, "settings_required", "Choose the account time zone first.");

    public static ObjectResult Unauthorized() =>
        Result(401, "unauthorized", "Authentication is required.");

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
