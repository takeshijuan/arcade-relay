# ArcadeRelay

ArcadeRelay is an experimental open source Claude Code harness, made by Fable 5,
for producing small, playable games through a structured multi-agent pipeline.

The public project name is ArcadeRelay. The command namespace remains `/forge`
for compatibility with the existing harness contract.

The harness supports three engines, selected during brainstorming and recorded
in `state/engine.txt` (contract §11):

- `phaser` (default): browser 2D games with Phaser 3, TypeScript, and Vite.
- `unity`: 3D games with Unity 6 LTS (URP, C#, Input System), built and tested
  headlessly in batchmode.
- `unreal`: 3D games with Unreal Engine 5.x (C++, Enhanced Input, Automation
  Tests). Engine install requires a one-time Epic account login.

For the 3D engines the asset pipeline also generates AI 3D models with
skeletons and animations. Meshy's direct API is the primary route when
`MESHY_API_KEY` is set, with fal.ai-hosted Meshy as the second route and
Hunyuan3D/TRELLIS/Rodin/Tripo fallbacks, all with strict license and provenance
gating.

## What It Does

ArcadeRelay turns one game brief into a playable game by relaying work
across specialized agents:

- `creative-director`, `game-designer`, and `tech-director` plan the game.
- `art-director` and `audio-designer` generate and validate assets
  (2D images, SFX/BGM, and rigged 3D models on unity/unreal).
- `gameplay-engineer` and `ui-engineer` implement the game in the selected
  engine's stack.
- `design-reviewer`, `art-reviewer`, and `qa-lead` run review gates and real
  playtests (headless browser for phaser, batchmode PlayMode tests for Unity,
  Automation Tests for Unreal).

The pipeline is intentionally checkpointed:

```text
[0] Configure optional asset-generation API keys
[1] /forge -> brainstorm the game brief
      -> autonomous concept, GDD, art bible, and asset plan
[2] Checkpoint A: approve concept and design
      -> autonomous vertical-slice implementation and playtest
[3] Checkpoint B: provide one round of prototype feedback
      -> autonomous full build, asset pass, polish, and QA
[4] Checkpoint C: receive the finished game
```

## Current Status

ArcadeRelay is early-stage infrastructure. The repository contains only the
Claude Code harness: agent prompts, skills, workflow definitions, hooks, rules,
and docs. Generated output (`design/`, `docs/architecture.md`,
`docs/conventions.md`, `game/`, `qa/`, `state/`) is created by the harness at
run time and is not tracked in this template repository. Sample games produced
by evaluation runs are archived separately.

## Requirements

- Claude Code with local skill and workflow support.
- Node.js only when running or validating a generated phaser `game/` project.
- Unity 6 LTS (installed via Unity Hub) only for unity runs; Unreal Engine 5.x
  only for unreal runs. The brainstorm preflight resolves and pins the editor
  path in `state/engine-info.json`.
- Optional provider keys for higher-quality generated assets.

## Quick Start

```bash
cp .env.example .env
```

Fill only the provider keys you want to use. Missing keys trigger local degraded
routes where available.

Run the harness from Claude Code:

```text
/forge
```

Useful commands:

```text
/forge          # preflight, brainstorm, and autonomous phases
/forge-status   # read-only status and next action
```

Resume individual phases:

```text
/forge-brainstorm
/forge-concept
/forge-prototype
/forge-build
```

Review intensity is controlled by `state/review-mode.txt`:

- `full`: stop at each checkpoint and show full review history.
- `lean`: default; stop at checkpoints, escalate only failed review loops.
- `solo`: continue without checkpoint stops, for unattended experiments.

## Repository Map

| Path | Purpose |
| --- | --- |
| `.claude/agents/` | Specialized producer and reviewer agent prompts |
| `.claude/skills/forge*/` | Claude Code command entry points |
| `.claude/workflows/` | Autonomous phase orchestration scripts |
| `.claude/tests/workflows/` | Workflow DSL stub tests (run with `node --test`) |
| `.claude/docs/contract.md` | Naming, IDs, paths, engines, and command contract |
| `.claude/docs/tech-stack*.md` | Per-engine stack rules (phaser / unity / unreal) |
| `.claude/docs/pipeline.yaml` | Pipeline stages consumed by status and hooks |
| `design/` | Generated brief, concept, GDD, art bible, and asset plan |
| `docs/` | Generated game architecture plus repo-level project docs |
| `game/` | Generated self-contained game project (engine-specific layout) |
| `qa/` | Playtest reports and evidence |
| `state/` | File-backed harness state and checkpoint feedback |

## Asset and License Notes

ArcadeRelay source code and documentation are licensed under MIT. Generated games
and generated assets may have additional provider-specific terms. Every accepted
asset should be recorded in the engine's manifest — `game/assets/MANIFEST.jsonl`
for phaser, `game/_generated/MANIFEST.jsonl` for unity/unreal — with provenance,
provider, model, prompt, cost, hash, and license fields.

Do not commit `.env` or API keys. The repository only includes `.env.example`.

## Verification

For repository-only changes:

```bash
git diff --check
```

For harness workflow changes (`.claude/workflows/*.js`), also run the workflow
DSL stub tests:

```bash
node --test '.claude/tests/workflows/**/*.test.mjs'
```

For generated game changes, run the checks for the engine recorded in
`state/engine.txt`. On phaser (marker: `game/package.json`):

```bash
cd game
npm install
npm run typecheck
npm run build
```

On unity (marker: `game/ProjectSettings/ProjectVersion.txt`) or unreal (marker:
`game/ForgeGame.uproject`), run the commands from the "検証コマンド" section of
`.claude/docs/tech-stack-unity.md` or `.claude/docs/tech-stack-unreal.md`
(batchmode build plus EditMode/PlayMode tests, or BuildCookRun plus Automation
tests).

If no game has been generated yet, state that in the pull request.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for local workflow, review expectations,
and pull request requirements. Please also read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
Questions and support channels are described in [SUPPORT.md](SUPPORT.md).

Security issues should be reported through [SECURITY.md](SECURITY.md), not public
issues.

## Maintainers

See [MAINTAINERS.md](MAINTAINERS.md) and [GOVERNANCE.md](GOVERNANCE.md).

Release notes live in [CHANGELOG.md](CHANGELOG.md). Known deferred work is
tracked in [TODOS.md](TODOS.md), and the repository layout rationale lives in
[docs/oss-repository-structure.md](docs/oss-repository-structure.md).

## License

MIT. See [LICENSE](LICENSE).
