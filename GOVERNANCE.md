# Governance

ArcadeRelay uses maintainer-led governance while the project is early-stage.

## Decision Model

The primary maintainer has final decision authority. For material decisions,
prefer a public issue or pull request discussion with:

- The problem being solved.
- Alternatives considered.
- Compatibility impact, especially for `/forge` and `forge-*`.
- Migration plan and rollback path.

Accepted decisions that affect the harness contract should be reflected in
`.claude/docs/contract.md`.

## Compatibility Policy

The public project name is ArcadeRelay. The command namespace remains `/forge`
for compatibility. Breaking changes to command names, stage values, state file
schemas, or generated artifact paths require explicit maintainer approval.

## Release Policy

Before stable `1.0.0`, minor versions may include breaking changes. Once the
public command and artifact contracts stabilize, releases should follow Semantic
Versioning:

- Major: incompatible public contract changes.
- Minor: backward-compatible functionality.
- Patch: backward-compatible fixes.

Every release should include human-readable notes in `CHANGELOG.md` and a Git
tag.
