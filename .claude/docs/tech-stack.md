# ArcadeRelay 技術スタック規約（game/ 配下・engine=phaser の正本）

> エンジン選択は contract.md §11（`state/engine.txt`）。このファイルは **engine=`phaser`（2D・既定）** の正本。
> `unity` は tech-stack-unity.md、`unreal` は tech-stack-unreal.md を読むこと。以下の規約は phaser 選択時に全て適用される。

## スタック（固定）

- **Phaser 3**（最新安定版）+ **TypeScript**（strict）+ **Vite**
- `game/` は自己完結プロジェクト: `cd game && npm install && npm run dev` で起動できること
- 追加ランタイム依存は原則禁止（Phaser のみ）。ビルド/検証系 devDependencies は可

## 必須 package.json スクリプト

```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc --noEmit && vite build",
    "typecheck": "tsc --noEmit",
    "preview": "vite preview"
  }
}
```

## ディレクトリ構造

```
game/
  index.html
  package.json / tsconfig.json / vite.config.ts
  src/
    main.ts              # Phaser.Game 初期化のみ
    config.ts            # ★全ゲームパラメータ（下記）
    scenes/              # BootScene / TitleScene / MenuScene / GameScene / ResultScene（contract §11 必須シーン集合）
    systems/             # ゲームロジック（Scene非依存の純粋クラス/関数）
      meta/              # メタ進行ロジック（metaTypes.ts / metaSchema.ts / metaProgression.ts — Phaser 非依存）
    persistence/         # 永続化 I/O 層（localStorage の唯一の置き場。systems/ から直接呼ばない）
    ui/                  # HUD・メニュー等の表示コンポーネント
    types.ts             # 共有型
  assets/                # 生成資産（画像/音声/atlas）+ MANIFEST.jsonl
```

## コード規約（rules/ が編集時に強制する内容の正本）

1. **マジックナンバー禁止** — 全ゲームパラメータ（速度・重力・スコア・色・時間）は `src/config.ts` に名前付き定数で集約。チューニングは config だけで完結させる
2. **delta-time 必須** — 移動・タイマーは `update(time, delta)` の delta を使う。フレームレート依存の実装禁止
3. **Scene は薄く** — Scene はライフサイクルと配線のみ。ロジックは `systems/` の純粋クラスへ（単体で理解・差し替え可能に）
4. **入力抽象化** — キー/タッチ入力は1モジュールに集約（後のリマップ・モバイル対応のため）
5. **資産参照はキー定数** — `assets/` のパスとテクスチャキーは `src/config.ts` の `ASSET_KEYS` 経由。ハードコードパス禁止
6. **音声はユーザー操作後に再生開始** — ブラウザの autoplay 制限対応（初回入力で AudioContext resume）
7. **リサイズ対応** — `Phaser.Scale.FIT` + `autoCenter` を既定とする
8. **シーン構成固定** — BootScene / TitleScene / MenuScene / GameScene / ResultScene（contract §11 必須シーン集合。正準フロー: Boot→Title→Menu→Game→Result→{Game|Menu}）。MenuScene の必須要素: プレイ開始・アウトゲーム表示（アンロック/実績/統計）・設定（音量・操作表示）・終了導線
9. **永続化 I/O は `src/persistence/` に集約** — `localStorage` を `systems/`・`scenes/`・`ui/` から直接呼ばない。メタ進行ロジック（`systems/meta/`）は値を受けて値を返すのみ（「セーブ / 永続化」節参照）

## セーブ / 永続化（contract §6 のセーブ規約の phaser 実装正本）

- **保存先**: `localStorage` キー `arcaderelay-save`。形式は JSON、先頭フィールド `save_version`（number・必須）
- **層の分離**（contract §11）: メタ進行ロジック = `src/systems/meta/`（`metaTypes.ts`=バージョン別プレーン型 / `metaSchema.ts`=マイグレーション関数チェーン+検証 / `metaProgression.ts`=RunResult を受けて新 SaveData を返す純粋 reducer）。I/O = `src/persistence/`（`SaveAdapter` 実装。`localStorage` 参照はここだけ — テストではインメモリ Storage を注入可能にする）
- **マイグレーション**: v(n)→v(n+1) の関数を順に適用。現行より新しい版は変換せず破損相当として扱う（暗黙ダウングレード禁止）。関数は追加のみ・書き換え禁止
- **破損時プロトコル（黙示初期化禁止 — rules/gameplay-code.md が強制）**: パース失敗・`save_version` 欠落・未来版・スキーマ検証失敗（必須フィールド欠落・型不正）のいずれも、(1) 生データを `arcaderelay-save.bak.<epoch>` キーへ退避 → (2) `console.error('[SaveCorruption] reason=... backup=...')` を1回 → (3) 既定値で再生成し `recovered: true` を UI 層（Title/Menu）に伝播
- **保存タイミング**: Result 到達時に `applyRunResult` → 即 persist を1回（リスタート連打で二重保存しない）
- **テスト規約**: 実 `localStorage` を使わずインメモリ Storage モックを注入（保存→新規インスタンスで再ロード→復元一致、破損→`.bak`+エラー1回+既定値復旧、の両テスト必須）

## 検証コマンド（実装ストーリーごと + バッチ）

- 実装ストーリーごと（並走レーン中）: `npm run typecheck` を実行し、**自分の編集ファイル起因のエラーのみ 0 にする**（他レーンの書きかけ WIP・他レーンが提供予定の API 参照に起因するエラーは無視してよい — バッチ検証が最終確認。`tsc --noEmit` はファイル書込なしで並走安全だが、結果には他レーンの WIP が混入し得る）。**レーン中は `npm run build` を実行しない**（dist/ が他レーンと衝突する — retro-e2 案A+B の検証バッチ化）
- レーン合流後のバッチ検証区間（直列）: `npm run typecheck && npm run build` が exit 0。失敗はエラーのファイルパスと `git log --oneline -- <path>` で原因 story を特定し、最小修正と原因 story を `state/reviews/batch-verify.md` に記録（正本実装は workflow の batchVerify）
- headless ブラウザで console エラー 0（QA-PLAY ゲートで実施。必須シーン遷移 Title→Menu→Game→Result→Menu と永続化検証を含む — gates.md QA-PLAY 観点2/5）

## 将来のエンジン非依存化に向けた線引き

- `systems/` は Phaser API・`localStorage` を import/参照しない（型・数値ロジックのみ）— ここがエンジン非依存層（`systems/meta/` も同様）
- Phaser 依存は `scenes/` `ui/` `main.ts` に、ブラウザ永続化 API は `persistence/` に閉じ込める

## 既知の落とし穴（engine=phaser）

> QA / バッチ検証で判明した環境起因の一般則を即時追記する（gates.md QA-PLAY）。run 成果物にだけ埋めない。

- **`npm run typecheck && npm run build` は vitest を実行しない** — `build` は `tsc --noEmit && vite build` なので、ユニットテストの実行時失敗（型は通るがランタイムで落ちる系）はバッチ検証をすり抜ける。テストを持つプロジェクトでは、バッチ検証区間で `npx vitest run`（exit 0）も併せて実行すること。型チェックだけを根拠に「レーンの成果は健全」と判断しない
- **最小フェイク Scene のドリフト** — `src/ui/**` のテストは実 Phaser.Game を使わず「そのクラスが呼ぶ Scene 表面だけ」を満たすフェイク（`scene.add.text` 等）で代替している。後続 story が同じクラスに新しい Scene 表面（`scene.add.rectangle` / `scene.tweens.add` / `setDepth` 等のチェーンメソッド）を追加すると、**production の型は `as never` キャストで通るためコンパイルは成功し、テストだけが `TypeError: ... is not a function` で落ちる**。UI クラスに新しい Scene API 呼び出しを足したら、同名の `*.test.ts` のフェイク Scene も同じコミットで追随更新する（E4 実例: `src/ui/shopWindow.test.ts` の `fakeRectangle` を定型として横展開した）
- **確率分岐を含む systems のテストは rng を必ず注入する** — vitest は既定で `Math.random` を固定しない（seed 機構が無い）ため、`advanceDay(run)` のように rng 引数が省略可能な関数をテストが既定値のまま呼ぶと、確率分岐（天候抽選など）を引いた回だけ落ちる **flaky test** になる。しかも「たまに通る」ので CI/バッチ検証を長く素通りする。rng を引数で受ける設計にしたら、テスト側は必ず固定 rng（`() => 1` 等）を注入し、期待する分岐を assert で固定する（E4 QA-PLAY round 1 で `dayCycle.test.ts` が約30%の確率で失敗し検出）
- **BootScene の資産ロード一覧を手で維持しない** — 「実ファイルがあり表示側も実装済みのキーだけを手で列挙する」方式のロード配列は、資産生成バッチの完了後に**誰も更新せず必ずドリフトする**。表示側が `scene.textures.exists()` でフォールバックしている場合、未ロードは console エラーにならず**プレースホルダのまま黙って出荷される**（E4 で生成済み画像26点が実機に一切反映されず QA-PLAY で acceptance FAIL）。ロード対象は `config.ts` の `ASSET_KEYS` から機械導出し（value のパスプレフィクスで image/audio を判別）、「`ASSET_KEYS` の全キーがロード対象に含まれる」「ロード対象の全キーに実ファイルが存在する（404=`loaderror` 防止）」の2条件をユニットテストで固定する
- **art レーンとの並走で「未生成資産の仮登録」を解除するときは 3 点セットで直す** — gameplay レーンが art 資産の到着を待たず実装するために、未生成スプライトシートを「保留キー一覧」へ仮登録して安全にビルドを通す設計を採った場合、art 資産が実際に到着した時点で (1) 保留一覧からキーを外す (2) `config.ts` のフレーム実寸定数（暫定値→実測値。`sips -g pixelWidth -g pixelHeight` 等で計測しフレーム数で割る）を追随更新する (3) ロード対象テストのファイル名パターン（`import.meta.glob` 等）に新しいファイルを追加する、の 3 点を**同時に**やらないと `npx vitest run` が失敗する。1 箇所だけ直すと別の失敗に化けるため、修正時は 3 点セットを一度に確認する（E4 実例: `bootAssetManifest.ts` の `PENDING_ASSET_KEYS` — 資産到着時の batch-verify で発生）
