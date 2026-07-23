# Contributing to ArcadeRelay

Thanks for helping improve ArcadeRelay. This project is a Claude Code harness,
so changes often affect prompt contracts, generated outputs, and autonomous
workflow behavior. Keep changes narrow and verify the path you touched.

## Good First Areas

- Documentation that makes setup, commands, or generated artifacts clearer.
- Prompt or gate wording that reduces ambiguity without changing contracts.
- Harness workflow fixes with a reproducible before/after scenario.
- Generated-game QA improvements that produce concrete playtest evidence
  (headless-browser runs for phaser; batchmode PlayMode or Automation test
  output for unity/unreal).

## Before You Start

1. Check existing issues and pull requests.
2. For behavior changes, open an issue first unless the fix is small and obvious.
3. Read `.claude/docs/contract.md`; it is the source of truth for names, IDs,
   paths, commands, and stage values.
4. Keep `/forge` and `forge-*` command names stable unless a maintainer approves
   a compatibility-breaking migration.

## Local Setup

```bash
cp .env.example .env
```

Provider keys are optional for most documentation and harness changes. Never
commit `.env`, API keys, generated credentials, or private provider output.

The generated game is self-contained under `game/` after a harness run. When a
`game/package.json` exists (engine=phaser), validate game changes from that
directory:

```bash
cd game
npm install
npm run typecheck
npm run build
```

For unity (marker: `game/ProjectSettings/ProjectVersion.txt`) or unreal
(marker: `game/ForgeGame.uproject`) games, use the verification commands from
the "検証コマンド" section of the matching `.claude/docs/tech-stack-unity.md`
or `.claude/docs/tech-stack-unreal.md`.

If those commands are unavailable because no game has been generated yet, state
that clearly in the pull request.

For harness workflow changes (`.claude/workflows/*.js`), run the DSL stub test
suite in addition to the syntax check:

```bash
node --check .claude/workflows/concept-design.js
node --check .claude/workflows/prototype.js
node --check .claude/workflows/full-build.js
node --test '.claude/tests/workflows/**/*.test.mjs'
```

(`node --check` validates only its first file argument, so check each script
with its own command.)

## Change Guidelines

- Preserve file-backed state semantics: `state/` is the source of truth during a
  harness run.
- Update `.claude/docs/contract.md` before changing any canonical name, path,
  stage value, agent name, skill name, or workflow filename.
- Keep generated asset provenance in the engine's manifest:
  `game/assets/MANIFEST.jsonl` (phaser) or `game/_generated/MANIFEST.jsonl`
  (unity/unreal).
- Do not commit candidate raw assets from `design/refs/_candidates/`.
- Keep docs in English for OSS-facing files; Japanese harness internals are
  acceptable where they already exist.
- Add or update tests, scripted checks, or QA evidence when behavior changes.

## Pull Request Checklist

Before opening a pull request:

- Run `git diff --check`.
- Confirm no secrets are present in the diff.
- Update README or docs if user-facing behavior changed.
- Explain which `/forge` phase or generated artifact is affected.
- Include verification commands and results.
- Include screenshots, logs, or `qa/evidence/` files for playtest behavior
  (browser captures for phaser; batchmode screenshots and test reports for
  unity/unreal).

## Security Reports

Do not open public issues for vulnerabilities, leaked keys, or exploit details.
Follow [SECURITY.md](SECURITY.md).

## Code of Conduct

Participation in this project is governed by [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
