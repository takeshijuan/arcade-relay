<!--
  テンプレート: design/art-bible.md（出力先は contract.md §6 のこのパス固定）
  producer: art-director / reviewer: art-reviewer（Gate: AR-BIBLE, MAX_ITER 3）
  役割: 人間可読のアート方針書。機械可読のスタイルロックは design/art-bible.json が正本で、
  このファイルは json の各値の「意図」を説明する側。値そのものを二重管理しない。
  執筆ルール: AR-BIBLE の4観点（機械可読性 / ゲーム内可読性 / 生成再現性 / 技術整合）に直接答える。
  曖昧形容詞のみの指定（「かわいい感じ」等）は不合格。完成時にガイドコメントはすべて削除する。
-->

# Art Bible — <ゲームタイトル>

## スタイル宣言

<!-- 3〜5文でスタイルを宣言する。必ず含めること:
     (1) ピクセルアートか否か（brief.md と一致）
     (2) 画風の固有名詞的な参照（例: 「16bit末期のアーケードSTG風」）
     (3) 線・塗り・光源の扱い（例: 「アウトライン1px黒・セル塗り・光源は左上固定」）
     この宣言を機械化したものが art-bible.json の style_block。 -->

## Key Image

<!-- Checkpoint A で人間承認を得た1枚。パスとその生成情報を記録する。
     以後の全資産はこの1枚との一致で採点される（AR-ASSET）。 -->

- パス: `design/refs/<ファイル名>.png`
- 生成: provider / model / seed / style_codes（art-bible.json と同値）
- この画で承認された点: <構図・質感・色などを箇条書き>

## パレット

<!-- 8〜16色を推奨。役割を必ず割り当てる（プレイヤー系/敵系/背景系/UI系/警告色）。
     hex 値は art-bible.json の palette と完全一致させること（食い違いは AR-BIBLE 不合格）。
     背景系とキャラ系で明度帯を分離するとシルエット可読性が上がる。 -->

| 役割 | hex | 用途メモ |
|---|---|---|
| プレイヤー主色 | `#RRGGBB` | |
| 敵主色 | `#RRGGBB` | |
| 背景基調 | `#RRGGBB` | |
| UI/テキスト | `#RRGGBB` | |
| 警告・ダメージ | `#RRGGBB` | |

## シルエット方針

<!-- ゲームは1画面・秒単位の判断。以下を明文化する:
     - プレイヤー/敵/障害物/収集物を「形」だけで区別するルール
       （例: プレイヤー=縦長・敵=角張り・収集物=円形）
     - ゲーム内表示サイズに縮小しても潰れない最小ディテール単位
     - 背景に対する前景のコントラスト確保策（縁取り・明度差など） -->

## 解像度・タイルサイズ

<!-- art-bible.json の resolution と一致させ、意図を添える。
     assets.md の全資産サイズ・tech-stack.md の表示系と矛盾しないこと（AR-BIBLE 観点4）。 -->

- スプライト生成解像度: <Npx>（ゲーム内表示: <Mpx>、縮小方式: <nearest / linear>）
- タイルサイズ: <Npx>
- 透過方針: 全スプライトはアルファチャンネル必須（白背景PNGは出荷禁止 — assets-config.md）

## アニメ方針

<!-- 何を動かし何を止めるかの割り切りを書く。
     - アニメさせる対象とフレーム数（例: hero 歩行4フレーム、敵は2フレーム明滅のみ）
     - スプライトシートの並び規約（横並び・フレーム順）
     - コードで代替する動き（tween での上下揺れ・点滅など）はアニメ資産を作らない、と明記 -->

## 3D スタイル方針（engine=unity/unreal のみ。phaser では節ごと削除）

<!-- 3D 版の技術整合（AR-BIBLE 観点4 の 3D 対応）。必ず含めること:
     - ポリゴン予算: hero / prop / 環境 の tri 上限（assets-config.md 既定から逸脱するなら理由）
     - テクスチャ解像度と PBR 方針（例: 2048px・albedo+metallic-roughness / フラット単色）
     - リグ方針: ヒューマノイドか否か、必要アニメクリップの語彙（idle/walk/run 等）
     - コンセプト画プロトコル: 全モデルは key image 系列のコンセプト画 → image-to-3D の
       二段生成（assets-config.md「スタイル一貫性プロトコル（3D 追記）」）。
     - スケール規約: glTF=m 基準・ヒト型 1.6–2.0m。unreal は取込時に cm 換算 -->

- ポリゴン予算: hero <N> tri / prop <N> tri / 環境 <N> tri
- テクスチャ: <解像度・PBR 有無>
- リグ: <humanoid / quadruped / none> / アニメクリップ: <一覧>
- スケール: 1 unit = <m/cm>、hero 身長 <N>m

## 機械可読スタイルロック（art-bible.json への参照）

<!-- 値の正本は design/art-bible.json。ここには各キーの意図だけを書く。
     json のキー構成は assets-config.md「スタイル一貫性プロトコル」で固定:
     style_block / palette / style_codes / reference_images / character_reference / resolution -->

| art-bible.json キー | 意図（なぜこの値か） |
|---|---|
| `style_block` | |
| `palette` | |
| `style_codes` | |
| `reference_images` | |
| `character_reference` | |
| `resolution` | |
