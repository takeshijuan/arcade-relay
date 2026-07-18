<!--
  テンプレート: qa/report.md（出力先は contract.md §6 のこのパス固定。テンプレ名は qa-report.md）
  producer: qa-lead（Gate: QA-PLAY の実施記録そのもの。MAX_ITER 2）
  役割: 実プレイ検証の報告書（実行手段はエンジン別 — gates.md QA-PLAY: phaser=headlessブラウザ /
  unity=batchmode+PlayModeテスト / unreal=BuildCookRun+Automation）。Checkpoint B/C で人間が5分で読める形にする
  執筆ルール:
  - 全証跡は qa/evidence/ に保存し、本文からは相対パスで参照する。証跡の無い pass は無効
  - acceptance は state/stories.yaml の S-xx を全件、1行1ストーリーで検証する
  - 総合判定の1行目は QA-PLAY ゲート形式（contract.md §5）に従う
  完成時にガイドコメントはすべて削除する。
-->

# QA Report — <ゲームタイトル> / <対象フェーズ: prototype | build>

## 総合判定

<!-- 1行目は必ずゲート判定形式。2行目以降に判定理由を3行以内で。
     APPROVE = 重大バグ0・acceptance全通過 / CONCERNS = 修正可能な指摘あり / REJECT = コアループ不成立 -->

```
QA-PLAY: <APPROVE | CONCERNS | REJECT>
```

<判定理由を3行以内>

## 実行環境

<!-- 再現に必要な情報のみ。日時は ISO8601。 -->

- 日時: <ISO8601>
- エンジン: <phaser / unity / unreal>（バージョンも。例: Unity 6000.3.16f1 / Chromium 138 headless）
- OS: <例: macOS 15>
- 実行系: <phaser: Node/npm バージョン / unity: エディタパス / unreal: UE_ROOT>
- 検証対象: <git commit hash または diff の範囲>

## ビルド結果

<!-- 選択エンジンの tech-stack 文書「検証コマンド」を全部実行し、exit code を記録する。
     行はエンジンのコマンド表に合わせて差し替える（以下は phaser の例。
     unity: EditMode テスト / ForgeBuild.BuildMac / PlayMode テスト
     unreal: BuildCookRun -build / Automation RunTests / BuildCookRun フル）。失敗時はログ抜粋を添える。 -->

| コマンド | 結果 | 備考 |
|---|---|---|
| `npm run typecheck` | <exit 0 / fail> | |
| `npm run build` | <exit 0 / fail> | |
| `npm run preview` 起動 | <ok / fail> | |

## Console / ログエラー

<!-- 起動〜コアループ1周〜リスタートまでのエラー出力を全記録。エラー0が合格条件。
     phaser: ブラウザ console / unity: エディタログ+LogAssert / unreal: 実行ログ+Automation レポート。
     warning は件数と代表例のみ。エラーがある場合は全文と発生操作を書く。 -->

- エラー件数: <N>（0 が合格条件）
- warning 件数: <N>
- 詳細: <エラー全文と発生時の操作。0件なら「なし」>

## Acceptance 検証表

<!-- state/stories.yaml の対象ストーリーを全件。ガイド:
     - 検証操作: 実際に行った操作列（キー入力・待機時間）を再現可能な粒度で
     - 証跡: qa/evidence/ 配下のスクリーンショット/録画パス。pass にも必須
     - fail の行は「重大バグ一覧」に対応エントリを作る -->

| S-xx | acceptance（stories.yaml から転記） | 検証操作 | 判定 | 証跡パス |
|---|---|---|---|---|
| S-01 | | | <pass / fail> | qa/evidence/ |

## 必須シーン遷移と永続化（gates.md QA-PLAY 観点2/5）

<!-- 全ゲーム必須の2検証。実施しない/できない場合は APPROVE 不可。 -->

| 検証 | 手段（テスト名/操作列） | 判定 | 証跡パス |
|---|---|---|---|
| Title → Menu → Game → Result → Menu の1周 | | <pass / fail> | qa/evidence/ |
| セーブ → 再起動相当 → 復元一致 | | <pass / fail> | qa/evidence/ |
| 破損セーブ → .bak 退避 + [SaveCorruption] エラー1回 + 既定値復旧 | | <pass / fail> | qa/evidence/ |

## スクリーンショット目視所見（必須）

<!-- 撮影した各スクリーンショットを Read で目視した所見を1行ずつ書く（gates.md QA-PLAY 視覚証跡の目視義務）。
     機械検知（magick の mean 値）→ 目視の順。黒画面・文字欠落・ピンクマテリアルは不合格。
     「目視した」の宣言だけでなく「何が写っていたか」を書く（例: 「Menu: プレイ開始ボタンと統計3行が判読可能」）。 -->

| 証跡パス | mean 値 | 目視所見（何が写っているか） | 判定 |
|---|---|---|---|
| qa/evidence/ | | | <ok / ng> |

## ピラー検証所見

<!-- design/concept.md の P-xx を全件。数値でなく実プレイ感の所見を書く
     （例: 「P-01 紙一重の回避: 当たり判定が見た目より大きく、理不尽に感じる → CONCERNS」）。
     裏切りがあれば該当ピラーの「裁定に使う判断例」に照らして指摘する。 -->

| P-xx | 所見（実プレイ感） | 判定 |
|---|---|---|
| P-01 | | <ok / concern> |

## 重大バグ一覧

<!-- 進行不能・クラッシュ・acceptance fail の原因のみ（軽微な見た目問題は所見に書く）。
     再現手順は「初期状態から番号付き手順→期待結果→実際の結果」の形式。証跡パス必須。
     0件なら表を削除し「なし」と書く。 -->

| # | 症状 | 再現手順 | 期待/実際 | 証跡パス | 関連 S-xx |
|---|---|---|---|---|---|
| 1 | | | | qa/evidence/ | |

## 既知の妥協点・未検証事項

<!-- 隠さず列挙する（CD-CHECKPOINT の「正直さ」観点で照合される）。
     例: 「タッチ操作は未検証（キーボードのみ）」「BGM ループのシームは1箇所軽微なクリック音」 -->
