# ArcadeRelay Contract — 命名・ID・パスの単一情報源

> **このファイルが全コンポーネント（agents / skills / workflows / hooks / rules / templates）の整合性の背骨。**
> ここに無い名前・ID・パスを発明しない。変更時は必ずここを先に更新し、参照側を追随させる。

## 1. パイプラインとステージ

ステージ値（`state/stage.txt` に保存。この5値のみ）:

```
brief → concept → prototype → build → done
```

| stage | 意味 | 到達条件 |
|---|---|---|
| `brief` | ブレスト完了、brief確定 | `design/brief.md` 存在 |
| `concept` | Phase1完了、Checkpoint A承認済み | `design/concept.md` + `gdd.md` + `art-bible.md` 承認 |
| `prototype` | Phase2完了、Checkpoint B通過 | 遊べる縦串 + `state/checkpoint-b-feedback.md` |
| `build` | Phase3完了、Checkpoint C到達 | フルQA合格 |
| `done` | 受け渡し完了 | — |

## 2. エージェント名（`.claude/agents/<name>.md`・この10体のみ）

Producer: `creative-director` `tech-director` `game-designer` `art-director` `audio-designer` `gameplay-engineer` `ui-engineer`
Reviewer: `design-reviewer` `art-reviewer` `qa-lead`

コードレビューは新規agentを作らず、既存の `pr-review-toolkit:code-reviewer` / `pr-review-toolkit:silent-failure-hunter` を使う。

## 3. スキル名（`.claude/skills/<name>/SKILL.md`・この6つのみ）

公開名は ArcadeRelay だが、既存運用との互換性のためコマンド名前空間は `forge` を維持する。

`forge` `forge-brainstorm` `forge-concept` `forge-prototype` `forge-build` `forge-status`

## 4. Workflowスクリプト（`.claude/workflows/<name>.js`・この3つのみ）

| script | 起動元スキル | args（JSON） | 終端 |
|---|---|---|---|
| `concept-design.js` | `/forge-concept` | `{briefPath, reviewMode, engine?}` | Checkpoint A素材を返す |
| `prototype.js` | `/forge-prototype` | `{reviewMode, engine?, checkpointAFeedbackPath?}` | Checkpoint B素材を返す |
| `full-build.js` | `/forge-build` | `{reviewMode, engine?, checkpointBFeedbackPath}` | Checkpoint C素材を返す |

Workflowスクリプト内から harness agent を使う時は `agent(prompt, {agentType: '<agent名>'})`（§2の名前をそのまま使う）。
`engine` は §11 の3値のいずれか。**Workflowスクリプトはファイルを読めないため、起動元スキルが `state/engine.txt` を読んで渡す**。省略時は `phaser`（後方互換）。

## 5. ゲートID・判定形式

判定を返すagentは**応答の1行目**に必ず:

```
<GATE-ID>: APPROVE | CONCERNS | REJECT
```

| Gate ID | 判定者 | 対象 |
|---|---|---|
| `DR-CONCEPT` | design-reviewer | design/concept.md |
| `DR-GDD` | design-reviewer | design/gdd.md |
| `AR-BIBLE` | art-reviewer | design/art-bible.md + key image |
| `AR-ASSET` | art-reviewer | 生成資産（個別/バッチ） |
| `CR-CODE` | (既存コードレビュー) | game/ のコード変更（エンジン別対象パスは §11） |
| `QA-PLAY` | qa-lead | 動くgame/のプレイテスト |
| `CD-CHECKPOINT` | creative-director | Checkpoint A/B/C 提示前の最終判定 |

review→revise の最大反復数と合格基準は `.claude/docs/review-loops.md` に定義。

## 6. 成果物パス（生成物はすべてリポジトリ相対でこの場所）

```
design/brief.md            # ブレスト出力（ゲーム像・制約・参照作品）
design/concept.md          # ピラー P-xx を含む企画書
design/gdd.md              # ゲームデザインドキュメント（P-xx参照）
design/art-bible.md        # アートバイブル（人間可読）
design/art-bible.json      # 機械可読スタイルロック（style block/palette/参照crop/style_codes）
design/assets.md           # 資産マニフェスト（生成仕様: 種別/プロンプト/サイズ/提供者ルート）
design/refs/               # key image候補・参照画像・crop置き場（art-bible.json が参照）
docs/architecture.md       # ゲームアーキテクチャ（シーン/レベル構成・システム境界）
docs/conventions.md        # このゲーム固有のコード規約
game/                      # 自己完結ゲームプロジェクト（中身はエンジン別 — §11）
game/assets/MANIFEST.jsonl # 生成provenance（engine=phaser のみ。1行1資産: provider/model/prompt/seed/cost_usd/sha256/license）
game/_generated/           # raw生成資産＋MANIFEST.jsonl（engine=unity/unreal のみ — §11。macOSの大文字小文字非区別FSで game/Assets と game/assets が衝突するため分離）
qa/report.md               # プレイテスト報告
qa/evidence/               # スクリーンショット・録画等の証跡
```

MANIFEST.jsonl の正本パス（エンジン別・§11 の表と一致）:
`phaser` → `game/assets/MANIFEST.jsonl` / `unity`・`unreal` → `game/_generated/MANIFEST.jsonl`

メタ進行セーブデータ（**実行時生成物** — リポジトリ成果物ではない。実装規約の正本は各 tech-stack 文書「セーブ / 永続化」節）:

| engine | 保存先（実行時） | 形式 |
|---|---|---|
| `phaser` | `localStorage` キー `arcaderelay-save` | JSON（先頭フィールド `save_version` 必須） |
| `unity` | `Application.persistentDataPath/save.json` | JSON（`save_version` 必須・`.tmp` 経由のアトミック書込） |
| `unreal` | `USaveGame` スロット `ForgeGameSave`（実体 `Saved/SaveGames/`） | UPROPERTY シリアライズ（`SaveVersion` フィールド必須） |

- セーブ破損時は**黙って初期化しない**: 破損 = パース失敗・`save_version` 欠落・未来版・**スキーマ検証失敗（必須フィールド欠落・型不正）**のいずれか。生データを `.bak` へ退避 → `[SaveCorruption]` プレフィクスの明示エラーログ1回 → 既定値で再生成し UI 層へ `recovered` フラグを伝播（各エンジンの rules/ が強制。QA-PLAY 観点5 が検証。フィールド単位で既定値に埋めて握り潰す実装も違反）。
- `save_version` より新しい版のデータはマイグレーションせず破損相当として扱う（暗黙ダウングレード禁止）。

## 7. 状態ファイル（`state/`）

```
state/stage.txt                    # §1 の5値のいずれか1語のみ
state/review-mode.txt              # full | lean | solo の1語のみ
state/active.md                    # セッションハンドオフ（現在地/次アクション/未解決事項）
state/stories.yaml                 # ストーリー一覧（下記スキーマ）
state/reviews/<artifact>.md        # レビュー履歴（artifact例: concept, gdd, art-bible, s-03, qa, batch-verify）
state/reviews/checkpoint-a.md      # CD-CHECKPOINT 履歴（Checkpoint A）
state/reviews/checkpoint-b.md      # CD-CHECKPOINT 履歴（Checkpoint B）
state/reviews/checkpoint-c.md      # CD-CHECKPOINT 履歴（Checkpoint C）
state/checkpoint-a-feedback.md     # Checkpoint A の人間フィードバック
state/checkpoint-b-feedback.md     # Checkpoint B の人間フィードバック
state/session-log.txt              # セッション終了ログ（hook追記・追記のみ）
state/budget.txt                   # 資産生成の予算上限USD（数値のみ。既定 20）
state/asset-routing.json           # preflight結果（プロバイダルーティング表）
state/engine.txt                   # §11 の3値のいずれか1語のみ。無ければ phaser として扱う（後方互換）
state/engine-info.json             # エンジンpreflight結果（解決済みエディタ/エンジンのパス・バージョン。§11）
```

`stories.yaml` スキーマ:

```yaml
stories:
  - id: S-01              # 安定ID。振り直し禁止
    title: "プレイヤー移動"
    pillar: P-01           # design/concept.md のピラーIDを必ず参照
    assignee: gameplay-engineer   # §2 のagent名
    phase: prototype       # prototype | build
    status: todo           # todo | in-progress | review | done
    acceptance: "..."      # 検証可能な受け入れ条件
```

## 8. 安定ID形式

- ピラー: `P-01`〜（design/concept.md で定義。3〜5個。全成果物がこれを参照）
- ストーリー: `S-01`〜（state/stories.yaml で定義）
- 資産: `IMG-01`〜（画像）/ `SFX-01`〜（効果音）/ `BGM-01`〜（音楽）/ `MDL-01`〜（3Dモデル。リグ・テクスチャ込み）/ `ANM-01`〜（スケルタルアニメーション）（design/assets.md で定義。MDL/ANM は engine=unity/unreal のみ使用）
- メタ進行: `ACH-01`〜（実績）/ `UNL-01`〜（アンロック対象。キャラ・ステージ・スキン共通）/ `UPG-01`〜（ラン間アップグレード）（design/gdd.md「メタ進行（アウトゲーム）」節で定義。engine 不問）
- ID は削除・振り直し禁止。廃止時は status/注記で示す。
- design/assets.md の資産状態語彙（この5値のみ）: `planned | generated | approved | rejected | must-replace`

## 9. review-mode の意味（全スキル・workflow共通）

| mode | Checkpoint A/B/C | 成果物レビューloop |
|---|---|---|
| `full` | 停止して人間承認 | 自動。workflowが全verdict履歴を蓄積し、完了後のCheckpoint提示で全件を人間に提示 |
| `lean`（既定） | 停止して人間承認 | 自動（MAX到達時のみ人間へ） |
| `solo` | 停止しない（通知のみで続行） | 自動 |

## 10. アセット生成ルーティング（詳細は assets-config.md）

環境変数: `FAL_KEY` `ELEVENLABS_API_KEY` `RETRO_DIFFUSION_API_KEY`（任意: `IDEOGRAM_API_KEY` `OPENAI_API_KEY` `MESHY_API_KEY` `TRIPO_API_KEY`。予算初期値: `ASSET_BUDGET_USD` → `state/budget.txt`）
3D資産（engine=unity/unreal）: **Primary は Meshy**。`MESHY_API_KEY` が有効なら **Meshy 直API を第一候補**（Meshy は Free プランに API キー発行が無いため、キー有効 ≒ Pro 以上 = 商用可）、`FAL_KEY` 経由の `fal-ai/meshy/*` を第二候補（Meshy の二重化 — 単一障害点を作らない）。両方失敗する資産種別のみ Hunyuan3D/TRELLIS/Rodin/Tripo に落とす。3D 案件（engine=unity/unreal）で `MESHY_API_KEY` 未設定は**準必須の欠落**として preflight が警告し `notes` に記録する — ルーティングの詳細は assets-config.md の 3D 節
生成レーンは **API を呼び出す Bash 呼び出しに限り**、冒頭で `set -a; source .env 2>/dev/null; set +a` を実行してから curl する（サブエージェントのシェルにはキーが継承されないため）。検証・後処理（ffmpeg / npx / python 等のサードパーティ CLI を走らせるステップ）では source しない — 子プロセスへの全キー継承＝サプライチェーン露出を避ける。キー値の出力・記録は禁止。
preflight結果は `state/asset-routing.json`（`checks.*` の実測 `plan_tier`・ルート別 `shippable`・`notes[]` を含む — スキーマ正本は forge スキル Phase 1）に書き出し、生成時はそれに従う（生成中の再判定禁止）。**`shippable: false` のルートで生成した資産は必ず未解決事項として蓄積し Checkpoint で人間に提示する**（MANIFEST 注記だけで済ませない）。

## 11. エンジン（`state/engine.txt`・この3値のみ）

```
phaser | unity | unreal
```

| engine | 次元 | tech-stack 正本 | プロジェクトマーカー | コード規約 rule | コード対象パス（CR-CODE） |
|---|---|---|---|---|---|
| `phaser` | 2D | `.claude/docs/tech-stack.md` | `game/package.json` | `rules/gameplay-code.md` + `rules/ui-code.md` | `game/src/**` |
| `unity` | 3D | `.claude/docs/tech-stack-unity.md` | `game/ProjectSettings/ProjectVersion.txt` | `rules/unity-code.md` | `game/Assets/Scripts/**`（.cs） |
| `unreal` | 3D | `.claude/docs/tech-stack-unreal.md` | `game/ForgeGame.uproject` | `rules/unreal-code.md` | `game/Source/**`（.cpp/.h） |

規則:

- **選択**: `/forge-brainstorm` の最初の質問（実行環境）で確定し、`state/engine.txt` に1語で保存＋ `design/brief.md` に明記する。以降のフェーズでの変更禁止。
- **既定**: `state/engine.txt` が無ければ `phaser`（既存2Dパイプラインの後方互換。既存プロジェクトは無変更で動く）。
- **unity/unreal は 3D 専用**として扱う（Unity 2D 等はスコープ外。2D は phaser を使う）。
- **エンジンpreflight**: brainstorm でエンジン確定後、スキルがエンジン実体を検証して `state/engine-info.json` に書き出す（unity: Unity Hub CLI で最新インストール済みエディタを解決 / unreal: `RunUAT.sh` の実在確認）。以後のビルド・QA はこのパスを使う（実行中の再解決禁止）。スキーマ:

```json
{
  "engine": "unity",
  "version": "6000.3.16f1",
  "binary": "/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity",
  "validated_at": "<ISO8601>"
}
```

（unreal の場合: `binary` = `RunUAT.sh` のフルパス、加えて `ue_root` = `/Users/Shared/Epic Games/UE_5.x` のエンジンルートを必須で持つ）

- **検証コマンドの正本**: 各 tech-stack 文書の「## 検証コマンド」節。スキル・workflow・agent はコマンドをハードコードせず、engine に対応する tech-stack 文書の同節を読む（workflow スクリプト内の定型プロンプトは例外的に engine 別プロファイル定数として持ってよいが、内容は tech-stack 文書と一致させる）。
- **生成資産の置き場**: raw 生成物＋MANIFEST は phaser=`game/assets/`、unity/unreal=`game/_generated/`。エンジン取込先は unity=`game/Assets/Generated/`、unreal=`game/Content/Generated/`（取込後も raw と MANIFEST は残す＝provenance の正本）。
- **unreal のプロジェクト名は `ForgeGame` 固定**（`game/ForgeGame.uproject`。マーカー検査とビルドコマンドを機械化するため）。
- **エンジン非依存コアの線引き**（tech-stack.md「将来のエンジン非依存化に向けた線引き」の一般化）: ゲームロジックはエンジンAPI非依存の純粋コード層（phaser: `game/src/systems/` / unity: `game/Assets/Scripts/Systems/`（MonoBehaviour 非依存の pure C#）/ unreal: `game/Source/ForgeGame/Systems/`（UObject 非依存の pure C++。ただし基本型は可））に置き、エンジン依存はシーン/コンポーネント層に閉じ込める。
- **必須シーン集合（全エンジン共通・全ゲーム必須）**: `Boot / Title / Menu / Game / Result` の5状態。phaser=`BootScene/TitleScene/MenuScene/GameScene/ResultScene`、unity=`Assets/Scenes/` の5シーン、unreal=`Content/Maps/` のレベル分割または状態遷移（どちらでも可。ただし5状態すべての実在と遷移を Automation テストで検証可能にすること — 「単一レベルだから Title/Menu 省略」は不可）。正準フロー: `Boot → Title → Menu → Game → Result → { Game（リスタート） | Menu }`。Menu の必須要素はプレイ開始・アウトゲーム表示（アンロック/実績/統計）・設定（音量・操作表示）・終了導線（ui-engineer の責務）。**Title と Menu のストーリーが `assignee: ui-engineer` で state/stories.yaml に存在しない分解は不合格**（workflow の Setup が機械検証し tech-director に差し戻す）。
- **メタ進行（アウトゲーム）必須**: design/gdd.md に「メタ進行（アウトゲーム）」節が必須（templates/gdd.md。ハイスコア/ベストタイム+統計=全ゲーム必須、通貨/アンロック/実績/ラン間アップグレードから2つ以上選択。DR-GDD 観点6 が判定）。ロジックはエンジン非依存コア層のサブフォルダ（phaser: `game/src/systems/meta/` / unity: `game/Assets/Scripts/Systems/Meta/` / unreal: `game/Source/ForgeGame/Systems/Meta/`）に、永続化 I/O は**永続化層**（phaser: `game/src/persistence/` / unity: `game/Assets/Scripts/Persistence/` / unreal: `game/Source/ForgeGame/Persistence/` — UObject/MonoBehaviour/ブラウザ API を許す唯一の I/O 層）に閉じる。セーブ規約は §6。
