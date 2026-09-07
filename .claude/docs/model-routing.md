# ArcadeRelay モデル階層ルーティング（オーケストレータ／サブエージェント）

> **メインセッションはオーケストレータ — 判断と人間接点だけを担い、実作業はモデル階層のサブエージェントへ振り分ける。**
> 階層表はこのファイルが正本。workflow スクリプトの `TIER` 定数と agents/*.md の `model:` frontmatter はここに追随する（`.claude/tests/workflows/model-routing.test.mjs` が同期を機械検証）。
> 目的: コストは「最高価格帯が実作業・機械作業・文脈読込に使われない」ことで下げ、品質は「安い階層で失敗した箇所だけを上位階層へ段階エスカレーション」することで守る。

## 1. 階層表

| tier | model | 用途 |
|---|---|---|
| `judge` | `opus` | 設計判断・ゲート判定・アーキテクチャ・実装順判断・**段階エスカレーション先** |
| `producer` | `sonnet` | 起草・実装・資産生成・検収・プレイテスト・コードレビュー・並走レーン中の状態更新 |
| `mechanical` | `haiku` | 読み取り専用の実在確認・突合と、直列区間の状態確定（判断も並走 git 操作も含まない作業） |
| `orchestrator` | セッションモデル（利用者の Claude Code 設定に依存） | メインセッションのみ。再開位置決定・AskUserQuestion・`state/stage.txt` 書込・通知・Checkpoint 提示・検証コマンドの exit code 観測 |

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

tools 備考: design 系 3 体（creative-director / design-reviewer / game-designer）も Bash を持つ（v0.5.1.0 / retro-e4）。用途は `date -u` の時刻記入（contract §7）と読み取り専用 git に限る — 各 agent.md「Bash の使用範囲」節。E4 まで Bash 非保持のため時刻を推測記入せざるを得なかった（`args` 経由の起動時刻注入は数時間ランで実時系列とズレ、script 内の `new Date()` は Workflow 実行系が throw する）。

harness 外の agent（`pr-review-toolkit:code-reviewer` / `pr-review-toolkit:silent-failure-hunter`）は frontmatter に `model` を持たずセッションモデルを継承するため、workflow が `model: TIER.producer` を**必ず明示**する。外部 agent は agents/*.md の「参照ドキュメント」節も持たないため、プロンプトに `gates.md` / `review-loops.md` の**パス**を書いて読ませる（CLAUDE.md からは自動 import されない — §4）。

`mechanical` 階層に専用 agent は無い。workflow が呼び出し単位で `model: TIER.mechanical, effort: 'low'` を明示する。対象ラベル: `verify-evidence-*`（証跡の実在確認。`agentType` 無し）/ `setup-crosscheck-stories`（stories.yaml 突合。`agentType` 無し）/ `finalize-state`（直列区間の active.md 更新。`agentType: tech-director` は役割宣言として据え置き）。
**mechanical にしないもの**: `bookkeep-*` / `close-*`（並走レーン中の stories.yaml ピンポイント Edit とパス限定 commit — 全面書き直しや `git add -A` を1回でもやると他レーンの更新を消すため producer 階層のまま）/ `replan-extract`（実装順の判断を含む — tech-director の judge 階層のまま）。

mechanical 階層の検証 agent は自己申告を擬装し得る（コマンドを走らせずに schema を埋める）ため、`verify-evidence-*` は `ls -l <path>` の生出力行 `rawLine` を必須にし、workflow がフルパスの一致を突合する。これは**抑止であって証明ではない**（rawLine も同じ agent の申告。workflow はファイルを読めない）— 信頼境界での実在確認は §3 のとおりスキル Phase 2 でオーケストレータ自身の Bash が戻り値の `evidencePaths` を `test -s` する。

**不変条件**: workflow の全 `agent()` 呼び出しは**セッションモデルを継承しない** — `agentType` が上表の harness 10 体のいずれか、または `model` を明示する。テストが機械検証する。

## 2. 段階的エスカレーション（安い階層で失敗した箇所だけ上位へ）

「段階エスカレーション（judge 階層）」は review-loops.md の「エスカレーション」（MAX_ITER 到達後の人間提示）とは別概念。プロンプト・ログ・文書では「段階エスカレーション」と呼び分ける。

| 箇所 | 初回 | 段階エスカレーション |
|---|---|---|
| CR-CODE fix | producer（assignee の frontmatter model） | **最終 iteration（MAX_ITER=2）かつ直前の fix が実行済み**なら judge — プロンプト冒頭に「根本原因から直せ」注記（レビューペア失敗で最終 iteration が初回 fix になった場合は producer のまま） |
| QA-PLAY fix（round 1 後の修正） | — | **常に judge**（人間エスカレーション前の唯一の修正機会。full-build は acceptance 未通過のうち所有者が分かる story（Replan/Polish 一覧）を担当 assignee のレーンにだけ分配し、所有者不明（prototype 由来・完了済み story）は従来どおり両レーンへ渡す） |
| batch-verify（レーン合流後の直列検証） | producer（gameplay-engineer） | `ok:false` / `unresolved` 残存 / agentR リトライ後も null なら **judge で 1 回だけ再試行**（label `batch-verify-<phase>-escalate`。生の `agent()` — null 再試行を重ねない）。**fail closed マージ**: 初回の `unresolved` は再試行が `resolvedPrior` に原文で列挙した項目だけ解消扱い、残りは引き継ぐ。`fixedNotes` は両試行を合算。再試行が null なら初回結果で続行し記録 |
| DR-CONCEPT / DR-GDD / AR-BIBLE の revise | producer | **最終 iteration（MAX_ITER）の revise は judge** |

既知の限界: 最終 revise / 最終 fix は review-loops.md の共通形どおり再レビューされない。Checkpoint に載る残指摘は fix 前の指摘であり、機能面の backstop は QA-PLAY とバッチ検証。

エスカレーション対象外（理由）: AR-ASSET の再生成・fallback（API ルーティング遵守が主で推論力ではない）/ 資産監査（画像の機械検査を含み qa-lead の手順が正本）/ CD-CHECKPOINT・Setup・Replan（最初から judge）。

実装: 条件付きは `withTier(opts, cond, TIER.judge)`（条件不成立時は `model` キー自体を渡さず frontmatter model に任せる）、無条件（QA fix・batch-verify 再試行）は `model: TIER.judge` の直接指定。judge 昇格箇所を列挙するときは両方を grep する。

## 3. オーケストレータ委譲規約（/forge 系スキル）

オーケストレータ（スキルを実行するメインセッション）が**自分で行う**もの: 再開位置の決定・矛盾検出の裁定・AskUserQuestion・`state/stage.txt` 書込・PushNotification・Checkpoint 提示文の最終確認・**QA 証跡の実在確認**（戻り値 `evidencePaths` を自分の Bash で `test -s` — workflow 内の検証は agent の申告に依存する）・**検証コマンドの実行**（exit code は自分の Bash で観測する — サブエージェント経由の文字列は偽装可能で exit code の代わりにならない）・単発の集計コマンド（`jq` 1 行等 — Task を起こすより Bash の方が安い）・エンジン実体パスの解決（`state/engine-info.json` に永続化され以後再解決されない値を、幻覚し得るモデルに書かせない）。

**委譲する**もの — Task ツールで起動し、構造化サマリだけを読む:

| 作業 | subagent_type / model | 起点 | 返させるもの・制約 |
|---|---|---|---|
| preflight（キー ping・プラン判定・`state/asset-routing.json` 生成） | general-purpose / sonnet | `/forge` Phase 1 手順 1〜4 | JSON の要約（checks/routes/shippable/notes）と AskUserQuestion 要否。**キー値を返却・notes・JSON に一切書かない**（contract §10）。AskUserQuestion を使わない |
| Checkpoint 提示文の下書き | **Explore（読み取り専用）** / sonnet | `/forge-concept` `/forge-prototype` `/forge-build` Phase 3 | 提示項目一式の Markdown 下書き。生成物（qa/report.md・state/reviews・MANIFEST）はプロンプトインジェクション面のため書込可能な agent に読ませない |

下書きの検収規約: オーケストレータは workflow 戻り値の `unresolvedFindings` / `knownIssues` / `licenseFlags`（full では `verdictHistory` も）の**全項目が下書きに含まれるか**を機械的に突合し、欠落があれば差し戻す。生ファイル全文を自分で読む必要はないが、突合の根拠は自分が保持する戻り値であって下書き担当の申告ではない。

委譲時はスキル本文の該当手順（コマンド・スキーマ・判定規則）をそのまま指示に含める。AskUserQuestion・stage 書込・通知はサブエージェントに行わせない。

## 4. 文脈節減

`CLAUDE.md` の `@` 自動 import は `contract.md` のみ。review-loops / tech-stack / assets-config / pipeline / gates はパス参照とし、各 agent 定義の「参照ドキュメント」節に従って必要な agent が自分で読む（rules/ は編集時に自動適用される）。自動 import 1 文書あたり全 agent・全ターンの文脈に常駐するため、汎用文書を増やさない。外部 agent（§1）と workflow プロンプトは文書を**ベース名ではなくパス**で指す。

## 5. 計測

各 workflow は代表 phase 境界（`phaseT()`）と終端（`tokenEnd()`、`phase: 'end'`）で `budget.spent()` を記録し、戻り値に `tokenUsage: [{ phase, outputTokensBefore }]` を含める。隣接記録の差分がその区間の出力トークン。full-build は Build ∥ AssetGen 並走区間の前に `phaseT('Build')` を宣言する（無いと最大の消費区間が Replan に帰属する）。

限界（読む側の前提）: `budget.spent()` は**その turn の出力トークン累計**（メインループと全 workflow の共有カウンタ）で、入力トークン・モデル単価は含まない — §4 の import 縮小効果や opus→sonnet の単価差は現れない。resume（キャッシュ replay）では再生分が 0 になるため、再開ランの値は run 間比較に使わない。/forge 系スキルは workflow を直列に 1 本ずつ起動する前提。
