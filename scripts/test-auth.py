#!/usr/bin/env python3
"""Verify synthetic accounts over trusted HTTPS; never print cookies or passwords."""
import argparse
import http.cookiejar
import json
import os
from pathlib import Path
import secrets
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request


class Browser:
    def __init__(self, base, context):
        self.base = base
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.ProxyHandler({}),
            urllib.request.HTTPSHandler(context=context),
            urllib.request.HTTPCookieProcessor(self.jar))

    def request(self, path, expected=200, data=None, token=None):
        headers = {}
        body = None
        if data is not None:
            headers["Content-Type"] = "application/json"
            body = json.dumps(data).encode()
        if token is not None:
            headers["X-CSRF-TOKEN"] = token
        request = urllib.request.Request(self.base + "/api/auth/" + path, body, headers)
        try:
            response = self.opener.open(request, timeout=15)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            if response.status != expected:
                raise AssertionError(f"{path}: expected {expected}, got {response.status}")
            payload = response.read()
            if response.status < 300:
                assert "no-store" in response.headers.get("Cache-Control", ""), "Missing no-store"
            return json.loads(payload) if payload else None

    def csrf(self):
        return self.request("csrf")["requestToken"]

    def post(self, path, data, expected=200):
        return self.request(path, expected, data, self.csrf())


def save_state(path, browser, account):
    cookies = []
    for cookie in browser.jar:
        cookies.append({key: getattr(cookie, key) for key in (
            "version", "name", "value", "port", "port_specified", "domain",
            "domain_specified", "domain_initial_dot", "path", "path_specified",
            "secure", "expires", "discard", "comment", "comment_url", "rfc2109")}
            | {"rest": cookie._rest})
    # O_EXCL prevents replacing an existing artifact or following a symlink.
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "w") as stream:
        json.dump({"base": browser.base, "account": account, "cookies": cookies}, stream)


def run(args):
    parsed = urllib.parse.urlsplit(args.url)
    if parsed.scheme != "https" or not parsed.hostname or parsed.username or parsed.password or parsed.query or parsed.fragment or parsed.path not in ("", "/"):
        raise ValueError("Use an HTTPS origin without credentials, query or path")
    base = args.url.rstrip("/")
    context = ssl.create_default_context(cafile=args.ca_cert)
    first = Browser(base, context)
    if args.resume:
        path = Path(args.resume)
        assert path.stat().st_mode & 0o777 == 0o600, "Session state must have mode 0600"
        saved = json.loads(path.read_text())
        assert saved["base"] == base, "Session state belongs to another origin"
        for values in saved["cookies"]:
            first.jar.set_cookie(http.cookiejar.Cookie(**values))
        assert first.request("me") == saved["account"], "Principal changed after container replacement"
        print("Saved HTTPS session retained the same account ID")
        return

    second = Browser(base, context)
    password = secrets.token_urlsafe(24)
    suffix = secrets.token_hex(8)
    a = {"email": f"a-{suffix}@example.test", "password": password}
    b = {"email": f"b-{suffix}@example.test", "password": password}
    first.request("me", 401)
    assert first.request("register", 400, a)["code"] == "csrf_invalid"
    first.csrf()
    assert first.request("register", 400, a, "invalid")["code"] == "csrf_invalid"
    registered_a = first.post("register", a, 201)
    first.request("me", 401)  # Registration never signs the user in.
    registered_b = second.post("register", b, 201)
    assert first.post("register", a, 409)["code"] == "duplicate_email"
    assert first.post("login", {**a, "password": "incorrect-password"}, 401)["code"] == "invalid_credentials"
    anonymous_token = first.csrf()
    account_a = first.request("login", data=a, token=anonymous_token)
    account_b = second.post("login", b)
    assert account_a == registered_a and account_b == registered_b
    assert account_a["userId"] and account_a["userId"] != account_b["userId"], "Accounts must have distinct IDs"
    assert first.request("me") == account_a
    assert second.request("me") == account_b
    auth = next(cookie for cookie in first.jar if cookie.name == "__Host-PennyPath.Auth")
    assert auth.secure and auth.path == "/" and not auth.domain_specified
    assert auth.expires and not auth.discard, "Session must survive browser restart"
    assert auth.has_nonstandard_attr("httponly") or auth.has_nonstandard_attr("HttpOnly")
    same_site = auth.get_nonstandard_attr("samesite", auth.get_nonstandard_attr("SameSite", ""))
    assert same_site.lower() == "lax", "Session cookie must use SameSite=Lax"
    assert first.request("logout", 400, {}, anonymous_token)["code"] == "csrf_invalid"
    assert first.request("logout", 400, {})["code"] == "csrf_invalid"
    second.post("logout", {}, 204)
    second.request("me", 401)
    assert first.request("me") == account_a, "Logging out B must preserve A"
    # Keep A only when the caller needs to test persistence; otherwise test logout.
    if args.state:
        save_state(args.state, first, account_a)
        assert Path(args.state).stat().st_mode & 0o777 == 0o600
    else:
        first.post("logout", {}, 204)
        first.request("me", 401)
    print("HTTPS register/login/me/logout, CSRF and two-account isolation passed")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("url")
    parser.add_argument("--ca-cert", required=True, help="Trusted proxy root certificate (no private key)")
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--state", help="Create a private, disposable synthetic session artifact")
    mode.add_argument("--resume", help="Verify the saved principal without logging in again")
    arguments = parser.parse_args()
    try:
        run(arguments)
    except (AssertionError, ValueError, OSError, urllib.error.URLError, StopIteration):
        # Exceptions can contain response or cookie details; report no sensitive values.
        print("HTTPS authentication verification failed; check TLS, status codes and session persistence.", file=sys.stderr)
        sys.exit(1)
