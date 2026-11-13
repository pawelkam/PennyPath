using System.Net;
using Microsoft.Net.Http.Headers;

namespace PennyPath.Api.Tests;

// CookieContainer otherwise evaluates Expires against the real wall clock and
// discards valid test sessions issued at a deliberately historical instant.
internal sealed class ClockCookieHandler(TimeProvider clock) : DelegatingHandler
{
    private readonly Dictionary<(string Name, string Domain, string Path), StoredCookie> cookies = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        lock (cookies)
        {
            var jar = new CookieContainer();
            foreach (var (key, stored) in cookies.ToArray())
            {
                if (stored.Expires is not null && stored.Expires <= clock.GetUtcNow())
                {
                    cookies.Remove(key);
                    continue;
                }

                // Expiration was checked using the same clock as the server.
                // Let CookieContainer enforce domain, path and Secure matching.
                var cookie = new Cookie(key.Name, stored.Value, key.Path)
                {
                    Secure = stored.Secure, HttpOnly = stored.HttpOnly
                };
                if (!stored.HostOnly) cookie.Domain = key.Domain;
                jar.Add(stored.Origin, cookie);
            }

            var header = jar.GetCookieHeader(uri);
            if (header.Length > 0) request.Headers.TryAddWithoutValidation("Cookie", header);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.Headers.TryGetValues("Set-Cookie", out var values))
        {
            lock (cookies)
            {
                foreach (var cookie in SetCookieHeaderValue.ParseList(values.ToList()))
                {
                    var domain = cookie.Domain.HasValue ? cookie.Domain.ToString() : uri.Host;
                    var path = cookie.Path.HasValue ? cookie.Path.ToString() :
                        uri.AbsolutePath[..(uri.AbsolutePath.LastIndexOf('/') + 1)];
                    var expires = cookie.MaxAge is { } maxAge ? clock.GetUtcNow() + maxAge : cookie.Expires;
                    var key = (cookie.Name.ToString(), domain, path);
                    if (expires is not null && expires <= clock.GetUtcNow())
                    {
                        cookies.Remove(key);
                    }
                    else
                    {
                        cookies[key] = new StoredCookie(cookie.Value.ToString(), cookie.Secure, cookie.HttpOnly,
                            expires, !cookie.Domain.HasValue, uri);
                    }
                }
            }
        }

        return response;
    }

    private sealed record StoredCookie(string Value, bool Secure, bool HttpOnly, DateTimeOffset? Expires,
        bool HostOnly, Uri Origin);
}
