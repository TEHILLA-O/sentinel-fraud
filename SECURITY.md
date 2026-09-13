# Security Policy

## Authorized systems only

Sentinel processes payment-like events for demonstration and authorized environments you control. Do not point generators or APIs at third-party production payment rails. Demo credentials in Compose are for local labs only; change them before any shared deployment.

## Supported versions

Fixes land on the default branch (`main`).

## Reporting a vulnerability

Please open a private GitHub security advisory, or contact the maintainer via the GitHub profile, with:

- Affected service (ingestion, processor, cases, admin, web)
- AuthZ bypass, decision tampering, injection, or secret exposure details
- Reproduction with local Compose/Aspire when possible

Do not post live card data or production secrets in public issues.

## Responsible use

- Treat JWT signing keys, database credentials, and dashboard cookies as sensitive.
- Privileged ruleset and admin changes must remain audited.
- Keep Testcontainers and Compose networks local unless you have a hardened deploy design.
- See `docs/SECURITY.md` for application security notes.

## Scope of this policy

This document covers the Sentinel repository and local/demo deployments. It does not authorize fraud testing against real customer payment systems without authorization.
