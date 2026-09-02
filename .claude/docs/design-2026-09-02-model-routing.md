# ArcadeRelay — オーケストレータ／モデル階層ルーティング設計（2026-09-02）

> 設計判断の記録。運用上の正本は `model-routing.md`（階層表・エスカレーション・委譲規約）と contract §12。

## 目的

メインセッション（最高価格帯モデル）を**判断と人間接点だけを担うオーケストレータ**に固定し、
実作業をモデル階層（judge=opus / producer=sonnet / mechanical=haiku）のサブエージェントへ振り分ける。
コストは「最高価格モデルが実作業・機械作業・文脈読込に使われない」ことで下げ、
品質は「安い階層で失敗した箇所だけを上位階層へ段階エスカレーションする」ことで守る。

## 現状（v0.4.1.0）の問題

| # | 問題 | 影響 |
|---|---|---|
| 1 | workflow の `agent()` 呼び出しのうち `agentType` 無し（verify-evidence / setup-crosscheck）と外部 reviewer（`pr-review-toolkit:*`。frontmatter に model 無し）は**セッションモデルを継承**する | 機械検証・コードレビュー（story ×2 reviewer ×最大2周）が最高価格帯で走る |
| 2 | bookkeep / close / finalize-state / replan-extract 等の機械作業が producer/judge 階層（sonnet/opus）で走る | 単純作業に中位以上のモデル |
| 3 | 失敗時の再試行（CR-CODE fix 2周目・QA fix・batch-verify 不合格）が同じ階層で繰り返される | 同じモデルで同じ失敗を繰り返しやすく、人間へのエスカレーションが増える |
| 4 | CLAUDE.md が 6 文書（contract / review-loops / tech-stack / assets-config / pipeline / gates ≈ 50KB）を `@` 自動 import | オーケストレータの毎ターンと**全サブエージェント**の文脈に約 12k トークンが常駐（agent 150+ 本/run） |
| 5 | スキル（/forge 系）が preflight の curl・コスト集計・検証コマンド・Checkpoint 要約の組み立てを**メインセッションで直接実行** | 判断不要の作業が最高価格帯 + 生ファイル（qa/report.md 等）がオーケストレータの文脈に流入 |
| 6 | トークン消費の計測が無い | コスト改善が検証不能 |

## 設計

### 1. 階層表（正本: `model-routing.md` §1）

| tier | model | 担当 | 用途 |
|---|---|---|---|
| judge | opus | creative-director / design-reviewer / tech-director | 設計判断・ゲート判定・アーキテクチャ・エスカレーション先 |
| producer | sonnet | game-designer / art-director / audio-designer / gameplay-engineer / ui-engineer / qa-lead / art-reviewer / `pr-review-toolkit:code-reviewer` / `pr-review-toolkit:silent-failure-hunter` | 起草・実装・生成・検収・プレイテスト |
| mechanical | haiku | 専用 agent 無し（workflow が `model: 'haiku', effort: 'low'` を明示） | 実在確認・突合・status/active.md 更新・抽出 |
| orchestrator | セッションモデル | メインセッションのみ | 再開位置決定・AskUserQuestion・stage 書込・通知・Checkpoint 提示 |

不変条件: **workflow の全 `agent()` 呼び出しはセッションモデルを継承しない**
（`agentType` が harness 10 体のいずれか、または `model` を明示）。テストが機械検証する。

### 2. 段階的エスカレーション（安い階層で失敗した箇所だけ上位へ）

| 箇所 | 1回目 | エスカレーション |
|---|---|---|
| CR-CODE fix | producer（assignee） | 最終 iteration（2）の fix は judge（opus） |
| QA-PLAY fix（round 1 後の修正） | — | judge（opus）。人間エスカレーション前の唯一の修正機会のため |
| batch-verify | producer（gameplay-engineer） | `ok:false` または agentR リトライ後も null なら judge（opus）で 1 回再試行（label `-escalate`）。両試行の fixedNotes を合算して記録。両方 null は BLOCKER 1 件 |
| DR-CONCEPT / DR-GDD / AR-BIBLE の revise | producer | 最終 iteration（MAX_ITER）の revise は judge（opus） |

AR-ASSET 再生成・fallback は API 作業のため producer のまま（推論力よりルーティング遵守）。

### 3. オーケストレータ委譲規約（スキル）

自身が行うもの: 再開位置の決定・矛盾検出の裁定・AskUserQuestion・`state/stage.txt` 書込・PushNotification・Checkpoint 提示文の最終確認。
委譲するもの（Task ツール、`subagent_type: general-purpose`、構造化サマリを返させ、オーケストレータは**サマリのみ**読む）:

| 作業 | model | 起点 |
|---|---|---|
| preflight（キー ping・routing.json 生成） | sonnet | forge Phase 1 |
| エンジン preflight（Hub CLI / RunUAT 解決） | haiku | forge Phase 2.5 |
| 完了確認の検証コマンド（typecheck/build 相当。`<cmd>; echo EXIT=$?` の生出力末尾を返させ、オーケストレータが `EXIT=0` 行の実在で判定） | haiku | forge-prototype/build Phase 2 |
| コスト集計・ライセンスフラグ抽出 | haiku | forge Phase 6 / forge-build Phase 3 |
| Checkpoint 提示文の下書き（qa/report・stories.yaml・state/reviews から） | sonnet | forge-concept/prototype/build Phase 3 |

### 4. 文脈節減

CLAUDE.md の `@` 自動 import を `contract.md` のみに絞る。review-loops / tech-stack / assets-config / pipeline / gates は
パス参照に変える（各 agent 定義は既に「参照ドキュメント」節で必要文書を読む指示を持つ。rules/ は編集時に自動適用される）。

### 5. 計測

各 workflow は phase 境界で `budget.spent()` を記録し、戻り値に `tokenUsage: [{phase, outputTokensBefore}]` を含める。
スキルは Checkpoint 提示に「トークン消費（phase 別）」を 1 行添える。`budget.spent()` は turn 共有カウンタのため、/forge 系スキルが workflow を直列に 1 本ずつ起動する前提で差分を phase 消費とみなす。

### 6. 変更ファイル

- 新規: `.claude/docs/model-routing.md`、`.claude/tests/workflows/model-routing.test.mjs`、本文書
- 更新: `CLAUDE.md`（絶対規約 7 追加・import 縮小）、`.claude/docs/contract.md`（§12）、3 workflow、4 skill（forge / forge-concept / forge-prototype / forge-build。forge-status は不変）、`README.md`、`CHANGELOG.md`、`VERSION`（0.5.0.0）

### 7. テスト

- 階層表 ↔ agents/*.md frontmatter の `model` 同期 / workflow `TIER` 定数の同期
- 3 workflow の全 agent() 呼び出しが不変条件を満たす（セッションモデル非継承）
- mechanical ラベル（verify-evidence / crosscheck / bookkeep / close / finalize-state / replan-extract）は haiku + effort low
- エスカレーション: CR-CODE fix iter2 = opus・iter1 ≠ opus / QA fix = opus / batch-verify 不合格・初回 null → `-escalate` が opus・合格なら無し・両方 null は BLOCKER 1 件 / reviewLoop 最終 revise = opus
- CLAUDE.md の `@` import が contract.md のみ
- 戻り値に tokenUsage（phase() 呼び出し列と一致）

## 見送り・未検証

- 外部 reviewer（`pr-review-toolkit:*`）を sonnet に落とした判断は次ランの tokenUsage と CR-CODE 検出率で再評価する（QA-PLAY + batch-verify を backstop とする設計）。
- 実測比較の基準は E3（agent 156 本・全て旧割当）。
