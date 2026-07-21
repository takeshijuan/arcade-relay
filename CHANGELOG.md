# Changelog

All notable changes to ArcadeRelay will be documented here.

The project intends to follow Semantic Versioning after the public command and
artifact contracts stabilize.

## [Unreleased]

### Added

- OSS community-health files: license, contributing guide, security policy,
  support guide, code of conduct, maintainers, governance, issue templates, and
  pull request template.
- OSS repository-structure notes in `docs/oss-repository-structure.md`.

### Changed

- Public project name changed from GameForge to ArcadeRelay while preserving the
  `/forge` command namespace.

## [0.3.0.0] - 2026-07-21

### Added

- Parallel build execution: code stories now run in two concurrent assignee
  lanes (gameplay / ui) alongside asset generation in both the prototype and
  full-build pipelines, cutting Build/Polish wall-clock by an expected 50-60%
  (E2 baseline: 6h + 9h + 9h of serial story implementation).
- Batched engine verification: lanes never launch Unity/Unreal (single-instance
  lock) or `npm run build`; a serial batch-verification step at each lane join
  point runs the full engine checks, isolates failures to the offending story
  via per-file commit history (bisecting story commits when attribution is
  unclear), and records diagnoses in
  `state/reviews/batch-verify.md`. Failures escalate as `[BLOCKER]` items and
  inject warnings into all downstream phase prompts.
- Parallel-lane discipline (LANE_RULE) enforced in every lane-side agent
  prompt: ownership boundaries, append-only shared config with an explicit
  balance-tuning exception, pinpoint edits of shared state files, and
  cross-lane API references resolved by the batch verifier.
- Workflow DSL stub test harness (`.claude/tests/workflows/`, `node --test`):
  15 tests covering every batch-verification escalation branch, lane
  partitioning, warning propagation, and prompt-wiring regressions.

### Changed

- Commit discipline hardened for shared-index parallelism: per-file adds,
  pathspec-only commits, immediate solo commits for shared files, and
  commit-hash retrieval validated with `git show --stat` instead of trusting
  `rev-parse HEAD`.
- Engineer agent definitions gained lane-mode exceptions so per-story engine
  verification and `state/active.md` updates defer to the workflow's lane
  rules; code reviewers are read-only during lanes and treat cross-lane
  forward references per the new CR-CODE premise in `gates.md`.
- QA fix loops are resume-safe: fix prompts now carry round-scoped labels
  (`fix-qa-r<N>-...`), so resuming a workflow can no longer replay a previous
  round's cached fix result and silently skip a re-fix.

## [0.2.0.0] - 2026-07-17

### Added

- 3D engine support: ArcadeRelay can now build Unity 6 (URP) and Unreal Engine 5
  games end-to-end, alongside the existing Phaser 2D pipeline. Engine choice is
  locked at brainstorm time (`state/engine.txt`) and drives tech-stack rules,
  code-review targets, QA execution, and asset routing throughout the pipeline.
- 3D asset generation: rigged/animated character models (MDL/ANM asset IDs) via
  MeshyAI as the primary provider, with fal.ai-hosted and local Blender/Rigify
  fallbacks, machine validation (glTF validate, polycount/bone budgets,
  authoring-time dimensions), and full provenance in `MANIFEST.jsonl`.
- Mandatory out-game structure for every generated game: Title and Menu screens,
  meta-progression (achievements ACH-xx, unlocks UNL-xx, upgrades UPG-xx), and a
  save/persistence layer with an atomic-write + corruption-recovery protocol.
- A complete Unity 3D sample product from the E2 evaluation run: an arena
  survivor game with wave combat, dash, crystal economy, permanent upgrades,
  generated 3D hero model with animations, generated UI art, SFX/BGM, skybox
  backdrop, post-processing, and 300+ automated EditMode/PlayMode tests with
  QA evidence.
- E2 retrospective (`.claude/docs/retro-e2.md`) with build-phase
  parallelization and Unity craft-skill proposals for the next iteration.
- Repo versioning: `VERSION` file (4-digit scheme) starting at 0.2.0.0.

### Changed

- QA-PLAY gate now demands visual evidence: screenshots are captured via a
  RenderTexture fallback in batchmode and inspected before approval (HUD
  canvases must use ScreenSpaceCamera so they appear in captures).
- Generation lanes source `.env` only in the Bash calls that hit provider APIs;
  verification/post-processing subprocesses (ffmpeg, npx, python) no longer
  inherit API keys.
- Workflow scripts normalize their `args` input (JSON string or object) and
  escalate review-loop failures instead of masking them (silent-failure and
  adversarial review findings W-1/W-2 closed).

### Fixed

- Object pooling for the highest-churn spawn surfaces (enemies, crystals, VFX)
  with double-return guards and per-life reset of warn-once flags, removing
  per-wave Instantiate/Destroy hitches and a Material leak.
- Save robustness: half-written orphan `.tmp` saves are validated before
  promotion (a crash during the very first save no longer surfaces as a scary
  corruption error), and schema-invalid saves follow the full `.bak` +
  `[SaveCorruption]` + defaults protocol.
- Dash cooldown HUD bar now fills smoothly from the raw remaining time; only
  the text label keeps the allocation-free 0.1 s dirty check.
- Tests no longer write to the real `persistentDataPath` save location
  (temporary-directory seam), and duplicate-singleton teardown is deterministic.
