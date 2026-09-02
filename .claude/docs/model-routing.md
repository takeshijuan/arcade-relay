# ArcadeRelay モデル階層ルーティング（オーケストレータ／サブエージェント）

> **メインセッション（最高価格帯モデル）はオーケストレータ — 判断と人間接点だけを担い、実作業はモデル階層のサブエージェントへ振り分ける。**
> 階層表はこのファイルが正本。workflow スクリプトの `TIER` 定数と agents/*.md の `model:` frontmatter はここに追随する（`.claude/tests/workflows/model-routing.test.mjs` が同期を機械検証）。
> 目的: コストは「最高価格帯が実作業・機械作業・文脈読込に使われない」ことで下げ、品質は「安い階層で失敗した箇所だけを上位階層へ段階エスカレーション」することで守る。

## 1. 階層表

| tier | model | 用途 |
|---|---|---|
| `judge` | `opus` | 設計判断・ゲート判定・アーキテクチャ・**エスカレーション先** |
| `producer` | `sonnet` | 起草・実装・資産生成・検収・プレイテスト・コードレビュー |
| `mechanical` | `haiku` | 実在確認・突合・status/active.md 更新・抽出（判断を含まない作業） |
| `orchestrator` | セッションモデル（Fable 5.1） | メインセッションのみ。再開位置決定・AskUserQuestion・`state/stage.txt` 書込・通知・Checkpoint 提示 |

### エージェント別割当（agents/*.md の `model:` と一致させる）

| agent | tier | model |
|---|---|---|
| creative-director | judge | opus |
| design-reviewer | judge | opus |
| tech-director | judge | opus |
| game-designer | producer | sonnet |
| art-director | producer | sonnet |
| audio-designer | producer | sonnet |
| gameplay-engineer | producer | sonnet |
| ui-engineer | producer | sonnet |
| qa-lead | producer | sonnet |
| art-reviewer | producer | sonnet |

harness 外の agent（`pr-review-toolkit:code-reviewer` / `pr-review-toolkit:silent-failure-hunter`）は frontmatter に `model` を持たずセッションモデルを継承するため、workflow が `model: TIER.producer` を**必ず明示**する。

`mechanical` 階層に専用 agent は無い。workflow が呼び出し単位で `model: TIER.mechanical, effort: 'low'` を明示する（`agentType` は据え置き — 役割宣言は engineer/tech-director のまま、実行モデルだけを下げる）。対象ラベル: `verify-evidence-*` / `setup-crosscheck-stories` / `bookkeep-*` / `close-*` / `finalize-state` / `replan-extract`。

**不変条件**: workflow の全 `agent()` 呼び出しは**セッションモデルを継承しない** — `agentType` が上表の harness 10 体のいずれか、または `model` を明示する。テストが機械検証する。

## 2. 段階的エスカレーション（安い階層で失敗した箇所だけ上位へ）

| 箇所 | 初回 | エスカレーション |
|---|---|---|
| CR-CODE fix | producer（assignee の frontmatter model） | **最終 iteration（MAX_ITER=2）の fix は judge** — プロンプト冒頭に「根本原因から直せ」注記 |
| QA-PLAY fix（round 1 後の修正） | — | **常に judge**（人間エスカレーション前の唯一の修正機会） |
| batch-verify（レーン合流後の直列検証） | producer（gameplay-engineer） | `ok:false` または agentR リトライ後も null なら **judge で 1 回だけ再試行**（label `batch-verify-<phase>-escalate`）。両試行の `fixedNotes` を合算して人間可視チャネルに載せる。再試行 agent も null なら初回結果で続行し `[BLOCKER]` を記録 |
| DR-CONCEPT / DR-GDD / AR-BIBLE の revise | producer | **最終 iteration（MAX_ITER）の revise は judge** |

エスカレーション対象外（理由）: AR-ASSET の再生成・fallback（API ルーティング遵守が主で推論力ではない）/ 資産監査（画像の機械検査を含み qa-lead の手順が正本）/ CD-CHECKPOINT・Setup・Replan（最初から judge）。

実装は各 workflow の `withTier(opts, cond, TIER.judge)` — 条件不成立時は `model` キー自体を渡さず agentType の frontmatter model に任せる（`model: undefined` を渡さない）。

## 3. オーケストレータ委譲規約（/forge 系スキル）

オーケストレータ（スキルを実行するメインセッション）が**自分で行う**もの: 再開位置の決定・矛盾検出の裁定・AskUserQuestion・`state/stage.txt` 書込・PushNotification・Checkpoint 提示文の最終確認（隠された未達が無いか）。

**委譲する**もの — Task ツール（`subagent_type: general-purpose`）で起動し、構造化サマリだけを読む。生の curl 出力・`qa/report.md` 全文・MANIFEST 全行をオーケストレータの文脈に入れない:

| 作業 | model | 起点 | 返させるもの |
|---|---|---|---|
| preflight（キー ping・プラン判定・`state/asset-routing.json` 生成） | sonnet | `/forge` Phase 1 手順 1〜4 | 書き出した JSON の要約（checks/routes/shippable/notes）と AskUserQuestion 要否 |
| エンジン preflight（Unity Hub CLI / RunUAT.sh 解決・`state/engine-info.json` 書出） | haiku | `/forge` Phase 2.5 | 解決結果（version/binary）または「無し」＋AskUserQuestion 要否 |
| 完了確認の検証コマンド（typecheck / build 相当） | haiku | `/forge-prototype` `/forge-build` Phase 2 | `<cmd>; echo EXIT=$?` の実行出力末尾（`EXIT=` 行を含む生出力）。オーケストレータは `EXIT=0` 行の実在で判定し、サブエージェントの要約文だけで合格にしない |
| コスト集計・ライセンスフラグ抽出 | haiku | `/forge` Phase 6・`/forge-build` Phase 3 | 合計 USD / 予算 / フラグ一覧 / must_replace 一覧 |
| Checkpoint 提示文の下書き | sonnet | `/forge-concept` `/forge-prototype` `/forge-build` Phase 3 | 提示項目一式の Markdown 下書き |

委譲時はスキル本文の該当手順（コマンド・スキーマ・判定規則）をそのまま指示に含める。AskUserQuestion・stage 書込・通知はサブエージェントに行わせない。

## 4. 文脈節減

`CLAUDE.md` の `@` 自動 import は `contract.md` のみ。review-loops / tech-stack / assets-config / pipeline / gates はパス参照とし、各 agent 定義の「参照ドキュメント」節に従って必要な agent が自分で読む（rules/ は編集時に自動適用される）。自動 import 1 文書あたり全 agent・全ターンの文脈に常駐するため、汎用文書を増やさない。

## 5. 計測

各 workflow は phase 境界で `budget.spent()`（その turn の出力トークン累計 — メインループと全 workflow の共有カウンタ。/forge 系スキルは workflow を直列に 1 本ずつ起動するため、隣接記録の差分 ≒ その phase の消費）を記録し、戻り値に `tokenUsage: [{ phase, outputTokensBefore }]` を含める（`phaseT()` ヘルパー）。スキルは Checkpoint 提示に「トークン消費（phase 別差分）」を 1 行添える — 階層変更の効果を run 間で比較する実測材料。
