---
name: forge-build
description: Phase 3（本実装・仕上げ）を full-build.js ワークフローで自律実行し、Checkpoint C（完成品受け渡し）を行って stage を build → done にする。
argument-hint: "[review-mode 上書き（full|lean|solo・省略時 state/review-mode.txt）]"
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, Workflow, Task, AskUserQuestion, SendUserFile, PushNotification
---

# /forge-build — Phase 3: 本実装・仕上げ（自律）

Checkpoint B のフィードバックを消化して全ストーリーを実装し、アセット本生成・フル QA を経て完成品を受け渡す。

## Phase 0: 前提チェック

| 前提 | 確認 | 無い場合の対応 |
|---|---|---|
| `state/checkpoint-b-feedback.md` | Read で存在確認 | 「Checkpoint B が未実施です。先に `/forge-prototype` を実行してください」と案内して**停止**（full-build.js の必須入力） |
| `docs/architecture.md` `docs/conventions.md` `state/stories.yaml` `qa/report.md` ＋ エンジンのプロジェクトマーカー（contract §11） | Glob で全件存在確認 | 欠けがあれば `/forge-prototype` へ案内して停止 |
| `state/engine.txt` | Read（無ければ `phaser`） | unity/unreal の場合は `state/engine-info.json` の binary 実在も確認し、無ければ `/forge` のエンジン preflight へ案内して**停止** |
| `state/asset-routing.json` | Read で存在確認 | 「preflight 未実施です。先に `/forge` を実行してください」と案内して**停止**（アセット本生成がルーティング表に最も強く依存する） |
| `state/stage.txt` | Read | `concept` 以前なら `/forge-prototype` へ案内して停止。`build`/`done` なら再実行の上書き警告を出し AskUserQuestion で続行可否を確認 |
| `state/review-mode.txt` | Read | 無ければ既定 `lean` |
| `state/budget.txt` | Read | 無ければ既定 `20`（USD）として扱う |

`$ARGUMENTS` に `full|lean|solo` があれば今回のみ reviewMode として使う。

## Phase 1: ワークフロー起動

Workflow ツールで起動する:

- scriptPath: `.claude/workflows/full-build.js`
- args: `{"reviewMode": "<mode>", "engine": "<state/engine.txt の値。無ければ phaser>", "checkpointBFeedbackPath": "state/checkpoint-b-feedback.md"}`

起動後ユーザーに伝える: 「Phase 3 をバックグラウンドで開始しました。アセット本生成とフル QA を含むため最長のフェーズです。完了すると通知が届きます。進捗は `/workflows` で確認できます。」**ポーリング禁止**、完了通知を待つ。AR-ASSET / CR-CODE / QA-PLAY / CD-CHECKPOINT の各ループはスクリプト側の責務。実行中の verdict 都度提示は行わない。reviewMode=`full` の場合、ワークフローが戻り値に蓄積した verdictHistory（全ループの verdict 履歴）を Phase 3 の Checkpoint C 提示に全件含める（contract §9）。予算超過見込みでワークフローが人間判断を要求してきた場合は AskUserQuestion で中継する（続行/生成打ち切り）。

## セッション断からの再開（retro-e3 指摘3）

ワークフロー実行中にセッションが断たれた場合の正式手順:

1. まず state/（`state/stories.yaml` の status・`state/active.md`・`state/stage.txt`）と `git log` で**最後に完了したフェーズ境界**（Replan 完了 / レーン合流+batchVerify 完了 / Integrate（3D 取込）完了 / Polish batchVerify 完了 / QA round N 完了）を特定する。
2. **第一選択は尾部再構成**: 残工程だけを同一プロンプト・同一スキーマで新規 Workflow として起動する（インライン tail script）。
3. `resumeFromRunId` の直行再開は、未完了 agent の再実行結果が変わるとキャッシュ分岐が連鎖し重複コミット・重複作業を生むリスクがある（E3 実測: 約 1h の浪費）— **完了直後の再開（分岐面が小さい）に限って使う**。
4. いずれの場合も再開前に `git log --oneline -20` で重複コミットの有無を確認する。

## Phase 2: 完了確認

完了通知の戻り値を読む。**失敗終了**: エラーと `/workflows` のログ参照を報告し、stage は変更せず停止。

成功時、必須成果物を実在確認する: `qa/report.md`（更新済み）と MANIFEST.jsonl（エンジン別正本パス — contract §6: phaser=`game/assets/MANIFEST.jsonl` / unity・unreal=`game/_generated/MANIFEST.jsonl`。以下 `$MANIFEST`）。
さらに Bash で engine の tech-stack 文書「検証コマンド」の build 相当が exit 0 であることを確認する（phaser: `cd game && npm run build` / unity: `ForgeBuild.BuildMac` batchmode / unreal: BuildCookRun フル）。欠落・失敗はワークフロー失敗として停止。

## Phase 3: Checkpoint C 提示（完成品受け渡し）

以下を整形して提示する:

1. **遊び方**（エンジン別 — tech-stack 文書の dev/preview 行）:
   - phaser: `cd game && npm install && npm run dev`（開発） / `cd game && npm run build && npm run preview`（本番ビルド）
   - unity: `open game/Build/ForgeGame.app`（ビルド済み） / Unity エディタで game/ を開いて Play
   - unreal: `open game/Build/Mac/ForgeGame.app`（パッケージ済み）
   操作方法（design/gdd.md 準拠）を要約して添える
2. **QA 結果**: `qa/report.md` を **SendUserFile** で送付し、QA-PLAY 最終判定と `qa/evidence/` の代表スクリーンショット 2〜3 枚を表示する
3. **コスト合計**: Bash で `$MANIFEST` の全行の `cost_usd` を合算する（例: `jq -s 'map(.cost_usd) | add' "$MANIFEST"`。jq 不可なら Read して集計）。`合計 $X.XX / 予算 $<state/budget.txt>` の形で提示
4. **ライセンスフラグ一覧**: MANIFEST の `license` / `must_replace` を集計し、以下を提示する:
   - `must_replace: true` の資産（placeholder-nc 等・出荷前要差し替え）の件数とファイル一覧
   - ElevenLabs 使用時: 「Studio Games」条項（商用×マルチプラットフォーム出荷は Enterprise 相談要）
   - Ideogram 使用時: アプリ内 AI 生成表記条項
   - 3D 資産使用時: Hunyuan3D の Territory 除外（EU/英国/韓国）・Meshy/Tripo のプラン条件（assets-config.md）
   - unreal の場合: UE EULA（エンジンコード/コンテンツの生成AI入力禁止・ロイヤリティ 5%/$1M 超過分）
   - 共通: 米国では純 AI 出力の著作権が不確定（MANIFEST の人間関与記録が防御材料）
5. **未解決事項**: 各レビューループの持ち越し指摘・妥協点・CD-CHECKPOINT が列挙した既知の課題を隠さず全件
6. **レビュー履歴（reviewMode=`full` のみ）**: 戻り値の verdictHistory（gate / artifact / iteration / verdict / findings 要約）を全件提示する

提示と同時に **PushNotification** を送る（例: 「ArcadeRelay: Checkpoint C — ゲームが完成しました」）。
提示が完了したら `state/stage.txt` に `build` の1語のみを Write する。

## Phase 4: 受領確認

- **full / lean**: AskUserQuestion で確認。選択肢: 「受領する（完了）」「修正を依頼する（内容を Other に記入）」。
  - **受領** → Phase 5 へ。
  - **修正依頼** → 次を行って停止:
    1. **スキル自身が** `state/checkpoint-b-feedback.md` へ以下を**追記**する（Edit で末尾に追加。既存内容の上書き禁止）:
       ```markdown
       ## Checkpoint C 修正依頼（<ISO8601 — `date -u +%Y-%m-%dT%H:%M:%SZ` の実行出力（推測記入禁止 — contract §7）>）
       <修正依頼の内容全文>
       ```
    2. 内容の要約を `state/active.md` の未解決事項にも記録する
    3. 案内: 「stage は `build` のまま。`/forge-build` を再実行すると、いま追記したフィードバックが反映されます」
- **solo**: 停止しない。提示・通知のみで受領扱いとし Phase 5 へ（未解決事項は提示内容に全て含まれていること）。

## Phase 5: 完了処理

1. `state/stage.txt` に `done` の1語のみを Write
2. `state/active.md` を更新: 現在地=「done・受け渡し完了」、次アクション=「なし（チューニングはエンジン別 config 正本で完結 — contract §11: phaser=game/src/config.ts / unity=GameConfig.cs / unreal=GameConfig.h）」、未解決事項=ライセンスフラグと must_replace 一覧
3. 締めの案内: 遊び方コマンドを再掲し、「パラメータ調整はエンジン別 config 正本（phaser: `game/src/config.ts` / unity: `game/Assets/Scripts/GameConfig.cs` / unreal: `game/Source/ForgeGame/GameConfig.h`）だけで完結します」と伝える
