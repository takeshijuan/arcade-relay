---
name: forge-prototype
description: Phase 2（プロトタイプ）を prototype.js ワークフローで自律実行し、Checkpoint B（遊べる縦串へのフィードバック）を回収して stage を prototype にする。
argument-hint: "[review-mode 上書き（full|lean|solo・省略時 state/review-mode.txt）]"
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, Workflow, Task, AskUserQuestion, SendUserFile, PushNotification
---

# /forge-prototype — Phase 2: プロトタイプ（自律）

承認済みの企画・設計から「遊べる縦串」（起動→コアループ1周→リスタート）を自律実装し、Checkpoint B で人間の1回フィードバックを回収する。フィードバックは Phase 3（/forge-build）の入力になる。

## Phase 0: 前提チェック

| 前提 | 確認 | 無い場合の対応 |
|---|---|---|
| `design/concept.md` `design/gdd.md` `design/art-bible.md` `design/art-bible.json` `design/assets.md` | Glob/Read で全件存在確認 | 1つでも欠けたら「Phase 1 の成果物が不足しています。先に `/forge-concept` を実行してください」と案内して**停止** |
| `state/asset-routing.json` | Read で存在確認 | 「preflight 未実施です。先に `/forge` を実行してください」と案内して**停止**（プレースホルダー資産生成がルーティング表に依存する） |
| `state/engine.txt` | Read（無ければ `phaser`） | unity/unreal の場合は `state/engine-info.json` の binary 実在も確認し、無ければ「エンジン preflight 未実施です。先に `/forge` を実行してください」と案内して**停止** |
| `state/stage.txt` | Read | `brief` 以前なら `/forge-concept` へ案内して停止。`prototype` 以降なら再実行で game/ を上書きする旨を警告し AskUserQuestion で続行可否を確認 |
| `state/review-mode.txt` | Read | 無ければ既定 `lean` |
| `state/checkpoint-a-feedback.md` | Read（**任意**） | 無くてもよい（Checkpoint A が無修正承認だった場合は存在しない） |

`$ARGUMENTS` に `full|lean|solo` があれば今回のみ reviewMode として使う。

## Phase 1: ワークフロー起動

Workflow ツールで起動する:

- scriptPath: `.claude/workflows/prototype.js`
- args: `{"reviewMode": "<mode>", "engine": "<state/engine.txt の値。無ければ phaser>", "checkpointAFeedbackPath": "state/checkpoint-a-feedback.md"}`
  （`state/checkpoint-a-feedback.md` が存在しない場合は `checkpointAFeedbackPath` を**省略**する — contract §4 で optional）

起動後ユーザーに伝える: 「Phase 2 をバックグラウンドで開始しました。完了すると通知が届きます。進捗は `/workflows` で確認できます。」**ポーリング禁止**、完了通知を待つ。ストーリー実装ループ（CR-CODE）と QA-PLAY はスクリプト側の責務。実行中の verdict 都度提示は行わない。reviewMode=`full` の場合、ワークフローが戻り値に蓄積した verdictHistory（全ループの verdict 履歴）を Phase 3 の Checkpoint B 提示に全件含める（contract §9）。

## セッション断からの再開（retro-e3 指摘3）

ワークフロー実行中にセッションが断たれた場合の正式手順:

1. まず state/（`state/stories.yaml` の status・`state/active.md`・`state/stage.txt`）と `git log` で**最後に完了したフェーズ境界**（Setup 完了 / レーン合流+batchVerify 完了 / Integrate 完了 / QA round N 完了）を特定する。
2. **第一選択は尾部再構成**: 残工程だけを同一プロンプト・同一スキーマで新規 Workflow として起動する（インライン tail script）。
3. `resumeFromRunId` の直行再開は、未完了 agent の再実行結果が変わるとキャッシュ分岐が連鎖し重複コミット・重複作業を生むリスクがある（E3 実測: 約 1h の浪費）— **完了直後の再開（分岐面が小さい）に限って使う**。
4. いずれの場合も再開前に `git log --oneline -20` で重複コミットの有無を確認する。

## Phase 2: 完了確認

完了通知の戻り値を読む。**失敗終了**: エラーと `/workflows` のログ参照を報告し、stage は変更せず停止。

成功時、pipeline.yaml の必須成果物を実在確認する（engine フィールド付きの成果物は該当 engine のもののみ）:
`docs/architecture.md` `docs/conventions.md` `state/stories.yaml` `qa/report.md` ＋ エンジンのプロジェクトマーカー（contract §11: phaser=`game/package.json` / unity=`game/ProjectSettings/ProjectVersion.txt` / unreal=`game/ForgeGame.uproject`）
さらに Bash で engine の tech-stack 文書「検証コマンド」の typecheck 相当が exit 0 であることを軽く再確認する（phaser: `cd game && npm run typecheck`。依存未インストールなら `npm install` を先に実行 / unity: EditMode テスト / unreal: BuildCookRun -build）。欠落・失敗はワークフロー失敗として停止。

## Phase 3: Checkpoint B 提示

以下を整形して提示する:

1. **遊び方**（エンジン別 — tech-stack 文書の dev/preview 行）:
   - phaser: `cd game && npm install && npm run dev`（起動 URL は Vite 既定 http://localhost:5173）
   - unity: `open game/Build/ForgeGame.app`（ビルド済み）または Unity エディタで game/ を開く
   - unreal: `open game/Build/Mac/ForgeGame.app`（パッケージ済み）
   操作方法（design/gdd.md の操作定義を要約）を添える
2. **プレイ証跡**: `qa/evidence/` のスクリーンショット（代表 3〜5 枚）を **SendUserFile（display: render）** で表示。`qa/report.md` のパスと QA-PLAY 判定結果も併記
3. **実装済みストーリー**: `state/stories.yaml` の phase: prototype 分の id / title / status 一覧
4. **既知の課題**: CR-CODE / QA-PLAY のレビューループで持ち越した未解決指摘（`state/reviews/*.md` 由来）を隠さず全件列挙
5. **レビュー履歴（reviewMode=`full` のみ）**: 戻り値の verdictHistory（gate / artifact / iteration / verdict / findings 要約）を全件提示する

提示と同時に **PushNotification** を送る（例: 「ArcadeRelay: Checkpoint B（プロトタイプ）が遊べる状態になりました」）。

## Phase 4: フィードバック回収

Checkpoint B は承認ゲートではなく**1回のフィードバック回収**。ここで直しては再提示、を繰り返さない — 回収した内容は Phase 3（本実装）が消化する。

- **full / lean**: AskUserQuestion で聞く。選択肢: 「このまま進めてよい（フィードバックなし）」「フィードバックあり（内容を Other に記入）」。実際に遊んでから答えてもらうよう促す。
- **solo**: 停止しない。通知のみで次へ進む。

回収結果は**必ず** `state/checkpoint-b-feedback.md` に Write する（full-build.js の必須入力のため、フィードバックが無くてもファイルを作る）:

```markdown
# Checkpoint B フィードバック
- 日時: <ISO8601 — `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力を貼る（推測記入禁止 — contract §7）>
- モード: <full|lean|solo>
## フィードバック
<本文。無い場合は「フィードバックなし。そのまま本実装へ進行」。solo の場合は「solo モードのため未回収」>
```

## Phase 5: 状態更新と次案内

1. `state/stage.txt` に `prototype` の1語のみを Write
2. `state/active.md` を更新: 現在地=「Checkpoint B 通過」、次アクション=「/forge-build」、未解決事項=既知の課題＋回収フィードバック要約
3. 案内: 「Checkpoint B を通過しました。次は `/forge-build` で本実装・仕上げ（フル QA・アセット本生成）を行います。」
