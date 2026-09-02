# 設計記録 — オーケストレータ／モデル階層ルーティング（2026-09-02）

> 設計判断とその根拠の記録。運用上の正本は `model-routing.md`（階層表・段階エスカレーション・委譲規約・計測）と contract §12 — 本文書は表を再掲しない。

## 目的

メインセッションを**判断と人間接点だけを担うオーケストレータ**に固定し、実作業をモデル階層（judge=opus / producer=sonnet / mechanical=haiku）のサブエージェントへ振り分ける。コストは「最高価格帯が実作業・機械作業・文脈読込に使われない」ことで下げ、品質は「安い階層で失敗した箇所だけを上位階層へ段階エスカレーション」することで守る。

## v0.4.1.0 で観測した問題

| # | 問題 | 影響 |
|---|---|---|
| 1 | `agentType` 無しの呼び出し（verify-evidence / setup-crosscheck）と外部 reviewer（`pr-review-toolkit:*`。frontmatter に model 無し）が**セッションモデルを継承** | 機械検証・コードレビュー（story ×2 reviewer ×最大2周）が最高価格帯で走る |
| 2 | 失敗時の再試行（CR-CODE fix 2周目・QA fix・batch-verify 不合格）が同じ階層で繰り返される | 同じモデルで同じ失敗を繰り返し、人間へのエスカレーションが増える |
| 3 | CLAUDE.md が 6 文書（≈66KB）を `@` 自動 import | オーケストレータの毎ターンと**全サブエージェント**（150+ 本/run）の文脈に常駐 |
| 4 | トークン消費の計測が無い | コスト改善が検証不能 |

## 判断と根拠

- **階層と不変条件** → model-routing.md §1。mechanical（haiku）は読み取り専用の実在確認・突合と直列区間の状態確定に限定した。当初 bookkeep/close/replan-extract も haiku にしたが、レビュー（Claude 対抗・removed-behavior 監査）で「並走レーン中の git/stories.yaml 更新は 1 回の全面書き直しで他レーンの更新を消す」「replan-extract は実装順の判断を含む」と指摘され producer/judge に戻した。
- **verify-evidence の自己申告擬装** → `rawLine`（`ls -l`/`stat` の生出力行）を必須化し workflow がパス名を突合。
- **段階エスカレーション** → §2。batch-verify の再試行は当初「初回の unresolved を再試行の値で置換」していたが、Codex・Claude 対抗レビューが「schema-valid な再試行応答で確定済み BLOCKER が消える」ことを再現 → `resolvedPrior` に原文列挙された項目だけを解消扱いにする fail closed マージへ変更。再試行は生の `agent()`（agentR の null 再試行を重ねてリポジトリ変更を 4 回走らせない）。CR-CODE の最終 fix は「直前の fix が実行済み」を条件に加えた（レビューペア失敗で最終 iteration が初回 fix になる経路で注記が偽になる）。
- **QA fix は常に judge** — QA-PLAY MAX 2 のため fix は 1 回しかなく、失敗＝人間エスカレーション。full-build の acceptance 未通過は担当 assignee のレーンにだけ分配（両レーンに全件渡すと担当外の opus fix が重複起動）。
- **検証コマンドは委譲しない** → §3。当初 haiku Task に typecheck/build を委譲し `EXIT=0` 行で判定する案だったが、Codex（P0）・Claude 対抗（confidence 8）とも「サブエージェントが返す文字列は要約と同じ信頼境界にあり、`echo EXIT=$?` は失敗後にも印字できる」と指摘。exit code はオーケストレータの Bash で `PIPESTATUS` から観測し、unity の XML failed 0 / unreal の `BUILD SUCCESSFUL` 条件を維持する。単発の `jq` 集計・エンジンパス解決も同様に委譲しない（Task を起こす方が高く、永続化されるパスを幻覚し得る）。
- **Checkpoint 下書き** → 読み取り専用 `Explore` に委譲し、オーケストレータは戻り値の未解決一覧との突合で検収（「全文を読まないが省略が無いか確認する」は構造的に不可能 — Claude 対抗 #2）。
- **文脈節減** → §4。外部 reviewer と engineer の到達経路として gates.md / review-loops.md を**パス**で指す（agents の参照ドキュメント節にも gates.md を追加）。
- **計測** → §5。終端サンプル `end` と full-build の `phaseT('Build')` を追加（Codex PR コメント P2・複数レビューが一致）。出力トークンのみ・resume 時 0 の限界を明記。
- **用語** — 「段階エスカレーション（judge 階層）」と review-loops.md の「エスカレーション（人間提示）」を呼び分ける。

## 見送り・未検証（次ランで再評価）

- 外部 reviewer（`pr-review-toolkit:*`）を sonnet に落とした判断: 次ランの tokenUsage と CR-CODE 検出率で再評価（QA-PLAY + バッチ検証を backstop とする設計）。
- prototype.js の QA fix は重大バグ 1 件ごとに opus セッション（M-8a/b の resume 安全設計）。full-build と同じ assignee 単位バッチ化でコンテキスト読込と検証コマンド実行を 1/3 にできるが、既存テスト（M-8a/b）の前提変更を伴うため別 PR。
- contract.md（唯一の自動 import ≈19KB）の §6/§10/§11 は engineer/qa/art 系のみが要る — 分割すれば全 agent の常駐文脈をさらに半減できる。別 PR。
- 実測比較の基準は E3（agent 156 本・全て旧割当）。
