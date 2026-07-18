<!--
  テンプレート: design/assets.md（出力先は contract.md §6 のこのパス固定）
  producer: art-director（画像・MDL/ANM）/ audio-designer（SFX・BGM） / reviewer: art-reviewer（Gate: AR-ASSET）
  役割: 資産マニフェスト＝生成仕様の正本。生成実績（provider/seed/cost/sha256）は
  MANIFEST.jsonl（エンジン別正本パス — contract §6）側に記録する。ここは「何を作るか」、MANIFEST は「何を作ったか」。
  執筆ルール:
  - 資産数は brief.md のスコープ制約内に収める
  - id は contract.md §8 の資産ID形式: 種別プレフィクス＋連番（IMG-01 / SFX-01 / BGM-01 /
    MDL-01 / ANM-01。MDL/ANM は engine=unity/unreal のみ）。
    安定ID（削除・振り直し禁止、廃止は状態で示す）
  - MANIFEST の正本パスはエンジン別（contract §6）: phaser=game/assets/ / unity・unreal=game/_generated/
  - プロンプト草案に art-bible.json の style_block を書き写さない（生成時に機械前置されるため。
    書くのは資産固有の内容: 被写体・ポーズ・向きのみ）
  - ルート列の値は assets-config.md のルーティング表に対応: primary | pixel | local
    （fallback への切替は state/asset-routing.json と AR-ASSET ループが管理。ここには書かない）
  - 状態列の値は contract.md §8 の資産状態語彙（この5値のみ）: planned | generated | approved | rejected | must-replace
    （must-replace = 非商用プレースホルダ。Checkpoint C までに必ず置換）
  完成時にガイドコメントはすべて削除する。
-->

# Assets Manifest — <ゲームタイトル>

## 画像

<!-- スプライト/UI/背景/タイルセット。各行のガイド:
     - ファイル名: game/assets/ 配下の相対パス。テクスチャキーは src/config.ts の ASSET_KEYS で管理
     - サイズ: 生成解像度で書く（art-bible.md の解像度方針と一致）。
       スプライトシートは「フレームサイズ x フレーム数」（例: 512x512 x4）
     - P-xx: この資産が支えるピラー。1つも書けない資産は作らない
     - プロンプト草案: 被写体・ポーズ・向き・地面接地など資産固有の指定のみ -->

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|
| IMG-01 | sprite | | | | | primary | planned |

## SFX

<!-- 効果音。各行のガイド:
     - サイズ列には長さ（秒）を書く。ElevenLabs SFX v2 は duration_seconds 明示（0.5〜30s）
     - プロンプト草案は「音の質感＋トリガーとなるゲーム内事象」（例: 「短い上昇チャイム、コイン取得」）
     - ループ素材（環境音など）は草案末尾に loop:true と明記
     - ルート local = jsfxr（決定的・出荷可） -->

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|
| SFX-01 | sfx | | | | | primary | planned |

## BGM

<!-- 楽曲。基本8列に加えループ要件・長さ・BPM/キーの指定欄を持つ。ガイド:
     - ループ要件: seamless（小節境界で完全ループ・生成後にループ検証必須）/ oneshot
     - 長さ: 秒数。ループ曲は1ループの長さ（BPM と小節数から割り切れる値にする）
     - BPM/キー: 全曲で固定方針（assets-config.md「音楽はジャンル/BPM/キー固定」）。
       曲間で変える場合も同一キーの平行調までに留める
     - プロンプト草案には composition_plan 相当のセクション構成（intro/loop 部の区切り）を書く
     - force_instrumental:true が既定（歌詞入りは不可） -->

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 | ループ要件 | 長さ | BPM/キー |
|---|---|---|---|---|---|---|---|---|---|---|
| BGM-01 | bgm | | | | | primary | planned | seamless | | |

## 3Dモデル（engine=unity/unreal のみ。phaser では節ごと削除）

<!-- キャラクター/プロップ/環境。各行のガイド:
     - kind: character_rigged | prop | environment（MANIFEST の kind と一致させる）
     - ファイル名: raw は game/_generated/models/ 配下（model- プレフィクス。リグ付き=FBX / 静的=GLB）
     - ポリ予算: tri 数上限（assets-config.md 既定: hero ≤ 50k / prop ≤ 10k / 環境 ≤ 100k）
     - リグ: humanoid | quadruped | other | none。アニメ列に必要クリップ（ANM の id）を列挙
     - プロンプト草案: image-to-3D 用コンセプト画の被写体指定（style_block は機械前置） -->

| id | kind | ファイル名 | ポリ予算 | リグ | アニメ（ANM-xx） | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|---|---|
| MDL-01 | character_rigged | | | humanoid | | | | primary | planned |

## スケルタルアニメーション（engine=unity/unreal のみ。phaser では節ごと削除）

<!-- 対象 MDL に付けるアニメクリップ。ルート primary = Meshy アニメプリセット（action_id）。
     ローカル縮退はコードモーション（must-replace）。 -->

| id | 対象 MDL | クリップ名 | 内容（例: walk / run / idle） | P-xx | ルート | 状態 |
|---|---|---|---|---|---|---|
| ANM-01 | MDL-01 | | | | primary | planned |

## 集計と予算

<!-- 生成着手前に必ず埋める。予算超過見込みなら資産を削るか Checkpoint で人間へ。 -->

- 画像: <N点> / SFX: <N点> / BGM: <N曲> / 3Dモデル: <N点> / アニメ: <N点>（brief.md の上限内であること。3D 系は unity/unreal のみ）
- 概算コスト合計: $<X.XX>（state/budget.txt の上限内であること。実績は MANIFEST.jsonl で強制）
