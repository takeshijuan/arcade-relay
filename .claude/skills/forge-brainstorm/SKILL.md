---
name: forge-brainstorm
description: ArcadeRelay 唯一の対話フェーズ。AskUserQuestion で1問ずつゲーム像を固め、design/brief.md を確定して stage を brief にする。
argument-hint: "[初期アイデア（任意・自由文）]"
allowed-tools: Read, Glob, Grep, Write, AskUserQuestion
---

# /forge-brainstorm — ブレスト（唯一の対話フェーズ）

ユーザーとの1問1答でゲーム像を固め、`design/brief.md` を書き出す。ここで決めた内容が以降の全自律フェーズ（Phase 1〜3）の唯一の入力になる。**質問は必ず AskUserQuestion で1問ずつ**。選択肢を用意し、自由入力は Other に委ねる。

## Phase 0: 前提チェック

| 前提 | 確認 | 無い/該当した場合の対応 |
|---|---|---|
| `.claude/docs/templates/brief.md` | Read で存在確認 | 無ければハーネス破損。「テンプレートが見つかりません。リポジトリの `.claude/docs/templates/brief.md` を復元してください」と伝えて停止 |
| `design/brief.md` | Read で存在確認 | **既存なら** AskUserQuestion で「上書きして最初からブレストし直す / 既存 brief を保持して中止（→ /forge-concept へ）」を確認。保持なら停止 |
| `state/stage.txt` | Read（無くてもよい） | `concept` 以降の値なら「後工程の成果物と矛盾する可能性」を警告した上で続行可否を確認 |

`$ARGUMENTS` に初期アイデアがあれば読み取り、以降の質問の選択肢生成に反映する（例: 「ネコが主役のシューティング」→ ジャンル質問の第1候補をシューティングにする）。

## Phase 1: ヒアリング（AskUserQuestion・この順で1問ずつ）

各質問は選択肢4個以内＋Other。前の回答を踏まえて次の選択肢を具体化すること。

0. **実行環境（エンジン）** — header: `エンジン`。「どの環境で動くゲームを作る？」（contract.md §11。**必ず最初に聞く** — 以降のスコープ・アート・資産の選択肢がこれに依存する）
   選択肢: 「ブラウザ2D（Phaser・既定。最速で完成）」「Unity 3D（macOS ネイティブ。AI 生成 3D モデル＋スケルトン資産を使用）」「Unreal 3D（UE 5.x。エンジン導入済みが前提 — 未導入なら Epic ログインが1回必要）」
   質問文に明記: 「3D エンジンを選ぶと資産生成に 3D ルート（Meshy 直API primary / fal 経由が二重化 — contract §10）が加わり、生成コスト・所要時間が2Dより増えます。」
   回答を engine 値（`phaser` / `unity` / `unreal`）として確定する。
1. **遊び手とプレイ時間** — header: `遊び手`。「誰が・1セッション何分遊ぶゲーム？」
   選択肢例: 「自分と友人・1回3分」「不特定の Web 訪問者・1回1分」「子ども向け・1回5分」「ゲーマー向け・1回10分」
2. **ジャンルと参照作品** — header: `ジャンル`。「近いジャンル・参照作品は？」
   選択肢例: 「アクション（Vampire Survivors 系）」「シューティング（Galaga／弾幕系）」「パズル（2048／Tetris 系）」「ランナー（Chrome Dino 系）」。回答後、参照作品名が曖昧なら追加で1問だけ具体作品を聞く。
3. **Core fantasy** — header: `体験の核`。「プレイヤーは何になりきり、何が気持ちいい？」
   選択肢は直前2問の回答から4案を生成する（例: 「大群を薙ぎ払う無双感」「ギリギリ回避のスリル」）。
4. **操作** — header: `操作`。選択肢: 「カーソルキー / WASD のみ」「マウスのみ」「キーボード＋マウス」「ワンボタン（タッチ兼用）」
5. **勝敗** — header: `勝敗`。選択肢: 「エンドレス＋ハイスコア（勝利なし）」「時間サバイバル（N 秒生存で勝ち）」「クリア型（目標達成で勝ち）」「対 CPU 撃破型」
5b. **アウトゲーム / やり込み** — header: `やり込み`。「ラン間で何を積み上げる？（ハイスコア+統計は必須。追加で2つ選ぶ — contract §11 メタ進行必須）」
   選択肢は直前の勝敗・ジャンル回答から2要素の組を4案生成する（例: 「通貨+スキンアンロック（見た目収集）」「実績+ラン間アップグレード（ローグライト）」「通貨+ステージアンロック」「実績+統計深掘り」）。multiSelect は使わず組で選ばせる（gdd の採用表と1:1対応）。
6. **スコープ制約** — header: `スコープ`。「数時間の自律実装で完成させる。どこまで盛る？」
   選択肢（engine=phaser）: 「最小: 1画面・敵1種・ギミックなし」「小: 敵2〜3種＋ギミック1つ」「中: ステージ2〜3＋ボス1体（上限）」。
   選択肢（engine=unity/unreal）: 「最小: 1アリーナ・敵1種・キャラ1体」「小: 1アリーナ・敵2種＋ギミック1つ（キャラ2体）」「中: アリーナ2＋ボス1体（上限。3Dモデル5体以内）」。
   「中」超の要望が出たらカット候補を提案して合意を取る。
7. **アート方向** — header: `アート`。
   **engine=phaser の場合** — 選択肢: 「ピクセルアート」「フラット2D／ベクター風」「手描きイラスト風」「ミニマル図形」
   **質問文に必ず明記**: 「ピクセルアートを選ぶと全画像生成が Retro Diffusion ルートに切り替わります（それ以外は fal.ai / Ideogram ルート）。」
   さらに `state/asset-routing.json` が存在し `checks.retro_diffusion.key` が `false` の場合は、「Retro Diffusion キー未設定のため、ピクセルアートを選ぶとローカル縮退（nearest-neighbor 縮小＋パレット量子化）になります」も質問文に明記し、それでもピクセルアートが選ばれたら縮退承諾済みとして brief に記録する。
   **engine=unity/unreal の場合** — 選択肢: 「ローポリ／フラットシェード」「スタイライズド（トゥーン調）」「セミリアル PBR」「ミニマル幾何学」
   質問文に明記: 「3D モデル生成は Meshy 直API（MESHY_API_KEY）が第一候補・fal.ai 経由が第二候補（contract §10）。両キー未設定ならプロシージャルプレースホルダ（must-replace）になります。」

## Phase 2: 確定確認

回答を「エンジン／遊び手／ジャンル・参照／core fantasy／操作／勝敗／アウトゲーム／スコープ／アート方向」の9行要約にまとめ、AskUserQuestion で確認する。選択肢: 「この内容で確定」「修正したい（どの項目か Other で指定）」。修正指定があればその項目だけ Phase 1 の該当質問を再実施し、再度この確認に戻る。

## Phase 3: brief.md 書き出し

1. `.claude/docs/templates/brief.md` を Read し、そのセクション構成に**厳密に沿って** `design/brief.md` を Write する。回答をそのまま貼らず、自律エージェントが判断に使える宣言文に整える（例: 「敵は最大2種。3種目の追加提案は却下する」）。
2. 実行環境セクションに **エンジン（`phaser`/`unity`/`unreal`）と次元を必ず明記**する（contract.md §11。`state/engine.txt` と一致させる）。
3. アート方向セクションには、engine=phaser なら **「ピクセルアート: はい／いいえ」を必ず1行で明記**する。これはアセット**生成時**のルーティング（Retro Diffusion ルートを使うか否か — assets-config.md）の分岐条件であり、ルート自体は brief 確定後、`/forge` の preflight 再確認または `/forge-concept` 起動時に `state/asset-routing.json` 上で決まる（preflight の実行順は brief より先）。engine=unity/unreal なら 3D スタイル方針（ローポリ等）とポリゴン予算の目安を書く。
4. スコープ制約セクションには Phase 1-6 で合意した上限と「盛らない」宣言を書く（3D エンジンはモデル数上限も）。
5. アウトゲーム / やり込みセクションには Phase 1-5b で確定した選択2要素と志向の1文を書く（templates/brief.md。gdd「メタ進行（アウトゲーム）」節の採用表の上位制約になる）。

## Phase 4: 状態更新と次案内

1. `state/engine.txt` に engine の1語のみ（`phaser`/`unity`/`unreal`）を Write する。
2. `state/stage.txt` に `brief` の1語のみを Write する。
3. `state/active.md` を更新: 現在地=「brief 確定（engine: <値>）」、次アクション=「/forge-concept 実行（unity/unreal はその前に /forge のエンジン preflight）」、未解決事項=ブレスト中に先送りした論点があれば列挙。
4. ユーザーに伝える: 「brief を確定しました。次は `/forge-concept` で Phase 1（企画・設計）を自律実行します。以降は Checkpoint A まで対話はありません。」
