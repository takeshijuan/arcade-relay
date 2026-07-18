# Security Policy

## Supported Versions

ArcadeRelay has not cut a stable release yet. Security fixes are applied to the
default branch until versioned releases begin.

## Reporting a Vulnerability

Do not report vulnerabilities, leaked credentials, exploit details, or private
provider output in public issues.

Preferred reporting path:

1. Use GitHub private vulnerability reporting or a private GitHub security
   advisory for this repository if available.
2. If private reporting is not enabled, open a minimal public issue using the
   "Security contact request" template and do not include sensitive details.
3. A maintainer will arrange a private channel for the report.

Please include:

- Affected files, commands, or generated artifacts.
- Reproduction steps.
- Impact and whether secrets, generated assets, or user data are involved.
- Suggested fix, if known.

## Response Expectations

Maintainers aim to acknowledge valid vulnerability reports within 14 days. Fix
timelines depend on impact, exploitability, and whether third-party provider
terms or generated assets are involved.

## Security Scope

In scope:

- Harness workflows, hooks, and agent prompts.
- Secret handling around `.env` and provider keys.
- Generated asset provenance and license metadata.
- Generated game scaffolding when created by ArcadeRelay.

Out of scope:

- Vulnerabilities in unrelated local Claude Code installations.
- Third-party provider outages or account configuration issues.
- Generated game content that was manually edited outside the harness without a
  reproducible ArcadeRelay path.
