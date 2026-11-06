# Security policy

## Reporting a vulnerability

Please report vulnerabilities privately through
[GitHub's private vulnerability reporting](https://github.com/joecrockett5/tally/security/advisories/new),
not in a public issue.

Include the affected version (shown by `curl http://localhost:8080/healthz`), the
steps to reproduce it and the impact you expect.

## Supported versions

Only the [latest release](https://github.com/joecrockett5/tally/releases/latest)
receives security fixes.

## Things that are by design

Tally has **no passwords or accounts**, and it trusts `X-Forwarded-*` headers from
any address. It is meant for people you trust, on a private network or behind your
own reverse proxy (see [Deploying](README.md#deploying)). Reports that amount to
"anyone who can reach the app can use it" are expected behaviour, not vulnerabilities.
