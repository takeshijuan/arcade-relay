---
name: forge-concept
description: Phase 1（企画・設計）を concept-design.js ワークフローで自律実行し、Checkpoint A（企画設計承認）を人間に提示して stage を concept にする。
argument-hint: "[review-mode 上書き（full|lean|solo・省略時 state/review-mode.txt）]"
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, Workflow, Task, AskUserQuestion, SendUserFile, PushNotification
---

# /forge-concept — Phase 1: 企画・設計（自律）

brief を入力に concept / gdd / art-bible / assets manifest を自律生成し、Checkpoint A で人間承認を得る。

## Phase 0: 前提チェック

| 前提 | 確認 | 無い場合の対応 |
|---|---|---|
| `design/brief.md` | Read で存在確認 | 「brief がありません。先に `/forge-brainstorm` を実行してください」と案内して**停止** |
| `state/asset-routing.json` | Read で存在確認 | 「preflight 未実施です。先に `/forge` を実行してください」と案内して**停止**（key image 生成がルーティング表に依存する） |
| `state/engine.txt` | Read（無ければ `phaser` として扱う） | unity/unreal の場合は `state/engine-info.json` も確認し、無ければ「エンジン preflight 未実施です。先に `/forge` を実行してください」と案内して**停止** |
| `state/review-mode.txt` | Read | 無ければ既定 `lean` を使う（ファイルは作らなくてよい） |
| `state/stage.txt` | Read | `concept` 以降なら「Phase 1 は完了済みです。再実行すると design/ 配下を上書きします」と警告し、AskUserQuestion で続行可否を確認 |

`$ARGUMENTS` に `full|lean|solo` があれば**今回のみ**それを reviewMode として使う（`state/review-mode.txt` は書き換えない）。

## Phase 1: ワークフロー起動

Workflow ツールで起動する:

- scriptPath: `.claude/workflows/concept-design.js`
- args: `{"briefPath": "design/brief.md", "reviewMode": "<Phase 0 で決めた mode>", "engine": "<state/engine.txt の値。無ければ phaser>"}`

起動後ユーザーに伝える: 「Phase 1 をバックグラウンドで開始しました。完了すると通知が届きます。実行中の進捗は `/workflows` で確認できます。」

**ポーリング禁止**。完了通知が来るまで待つ。ワークフロー内の produce→review→revise ループ（DR-CONCEPT / DR-GDD / AR-BIBLE、review-loops.md）はスクリプト側の責務であり、このスキルからは介入しない。実行中の verdict 都度提示は行わない。reviewMode=`full` の場合、ワークフローが戻り値に蓄積した verdictHistory（全ループの verdict 履歴）を Phase 3 の Checkpoint A 提示に全件含める（contract §9）。

## Phase 2: 完了確認

完了通知を受けたら戻り値を読む。**失敗終了の場合**: エラー内容と `/workflows` のログ参照方法を報告し、`state/stage.txt` は変更せずに停止（再実行は `/forge-concept`）。

成功時、pipeline.yaml の必須成果物を Glob/Read で実在確認する:
`design/concept.md` `design/gdd.md` `design/art-bible.md` `design/art-bible.json` `design/assets.md`
欠けがあればワークフロー失敗として扱い停止する。

## Phase 3: Checkpoint A 提示

戻り値の Checkpoint A 素材を以下の形に整形する:

1. **要約**（5分で判断できる分量）: 何を作る企画か（1段落）／ピラー P-xx 一覧／コアループ1文
2. **成果物パス**: 上記5ファイル
3. **Key image 候補**: 戻り値記載の候補画像を **SendUserFile（display: render）で表示**する。ここで承認された1枚が `design/art-bible.json` のスタイルロックの基準になる旨を添える
4. **未解決指摘**: レビューループが MAX_ITER 到達で持ち越した指摘（`state/reviews/*.md` 由来）。隠さず全件列挙
5. **レビュー履歴（reviewMode=`full` のみ）**: 戻り値の verdictHistory（gate / artifact / iteration / verdict / findings 要約）を全件提示する

提示と同時に **PushNotification** を送る（例: 「ArcadeRelay: Checkpoint A（企画設計承認）の準備ができました」）。

### reviewMode = solo の場合

**停止しない**。上記の提示と PushNotification のみ行い、key image は候補の第1位を採用したものとして Phase 5 へ直行する。

## Phase 4: 承認ループ（full / lean のみ）

AskUserQuestion で判断を仰ぐ。選択肢:
「承認（この内容で Phase 2 へ）」「Key image を別候補に差し替えて承認（どれかは Other で指定）」「修正指示（内容は Other に記入）」

- **承認** → Phase 5 へ。
- **Key image 差し替え** → 選ばれた候補を基準に `design/art-bible.json` を更新するよう **Task で art-director に指示**し、更新後 Phase 5 へ。
- **修正指示** → 次の手順（**再提示は1回まで**）:
  1. 指示全文を `state/checkpoint-a-feedback.md` に Write（日時・対象成果物を明記）
  2. 対象に応じて Task で producer に反映させる: concept.md / gdd.md → `game-designer`、art-bible / key image → `art-director`、assets.md → `art-director`（review-loops.md の対応表に従う）
  3. 修正後の成果物で Phase 3 の提示を再実施し、再度 AskUserQuestion
  4. 2回目も承認されない場合: 残指摘を `state/checkpoint-a-feedback.md` に追記し、AskUserQuestion で「未解決を持ち越して承認する / ここで中断する（stage 据え置き）」の二択を確認。中断なら停止

## Phase 5: 状態更新と次案内

1. `state/stage.txt` に `concept` の1語のみを Write
2. `state/active.md` を更新: 現在地=「Checkpoint A 承認済み（solo 時は自動通過）」、次アクション=「/forge-prototype」、未解決事項=持ち越し指摘
3. 案内: 「Checkpoint A を通過しました。次は `/forge-prototype` で遊べる縦串を作ります。」
