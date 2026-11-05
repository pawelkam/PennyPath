#!/usr/bin/env python3
"""Check the published Vue app and ASP.NET routes, with no extra packages."""
import json
import re
import sys
import urllib.error
import urllib.request

base = sys.argv[1].rstrip("/") if len(sys.argv) > 1 else "http://127.0.0.1:8080"


def get(path, expected=200):
    try:
        response = urllib.request.urlopen(base + path, timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        assert response.status == expected, (path, response.status, expected)
        return response.headers, response.read()


headers, body = get("/")
assert "text/html" in headers["Content-Type"]
assert b'<div id="app">' in body
assets = re.findall(rb'(?:src|href)="(/assets/[^" ]+)"', body)
assert assets, "No Vite assets found in the page"
for asset in assets:
    headers, content = get(asset.decode())
    assert content and "text/html" not in headers["Content-Type"], asset
assert get("/expenses/example")[1] == body, "SPA navigation did not serve index.html"
assert json.loads(get("/health")[1])["status"] == "healthy"
assert len(json.loads(get("/weatherforecast")[1])) == 5
for path in ("/api", "/api/unknown", "/api/unknown.json", "/API/unknown", "/assets/missing.js"):
    headers, content = get(path, 404)
    assert b'<div id="app">' not in content, path
print("Vue, static assets, SPA navigation, health, sample API and 404 checks passed")
