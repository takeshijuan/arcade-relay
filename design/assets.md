# Assets Manifest — Crystal Bastion（仮）

エンジン: unity（3D）。生成provenanceの正本は `game/_generated/MANIFEST.jsonl`（contract §6/§11）。本ファイルは「何を作るか」の仕様であり、生成実績（provider/seed/cost/sha256）はMANIFEST側に記録する。ルーティングは `state/asset-routing.json`（2026-07-21T09:05:00Z preflight時点）に固定。予算上限は `state/budget.txt`（$20）。

参照: `design/concept.md`（ピラー P-01〜P-04）、`design/gdd.md`、`design/art-bible.md` + `design/art-bible.json`。

## 画像

<!-- brief.md スコープ制約: 画像資産数の上限 8点以内（UIキット・アイコン類）。
     全プロンプトは design/art-bible.json の style_block を機械的に前置する前提で、
     ここには被写体・向き・透過方針など資産固有の指定のみを書く。
     地形タイル2点（IMG-01/02）は art-bible.md「解像度・タイルサイズ」節で明示定義された
     必須テクスチャのため、UIアイコン6点と合わせて8点枠に収めた（テラン用テクスチャが無いと
     P-01 の経路視認性そのものが成立しないため実質UI同様の必須画像として扱う）。 -->

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|
| IMG-01 | tile | `game/_generated/textures/tile-grass.png` | 512px（継ぎ目なしタイル・不透明） | P-01 | Seamless tileable **top-down orthographic** ground texture: short grass surface, base hue `#9DC03A`, subtle low-poly faceted color variation patches for interest but no visible seam at tile edges when repeated, matte PBR shading, opaque (no alpha), tiles at 2m×2m world scale. （top-downはstyle_blockの3/4俯瞰カメラ指定を上書きする——タイルテクスチャは単体ターンアラウンドではないため） | primary（image_background: fal:flux-2-pro） | generated |
| IMG-02 | tile | `game/_generated/textures/tile-dirt-path.png` | 512px（継ぎ目なしタイル・不透明） | P-01 | Seamless tileable **top-down orthographic** ground texture: worn dirt path, base hue `#A26836`, subtle low-poly faceted patches, opaque (no alpha), tiles at 2m×2m world scale. 隣接配置したとき IMG-01（芝）と明度・彩度で明確に区別できること（一本道の視認性は経路がタワー配置で変化しないP-01の前提を支える） | primary（image_background: fal:flux-2-pro） | generated |
| IMG-03 | ui-icon-sheet | `game/_generated/textures/icon-tower-select.png` | 1024px x2フレーム（ゲーム内256px縮小・透過必須） | P-02 | Two-icon flat badge sprite sheet on transparent background: (1) Bastion Cannon — tall narrow spire silhouette badge, body `#0B6C58` / accent `#15ACC1`; (2) Arc Emitter — short wide dome silhouette badge, same palette. 縦横比の違いだけで種別が判別できること（シルエット方針: 尖塔=縦長 vs ドーム=横広）。256pxサムネイルで判読可能な単純化度合い | primary（image_sprite: fal:ideogram-v3-transparent） | generated |
| IMG-04 | ui-icon | `game/_generated/textures/icon-essence.png` | 1024px（ゲーム内256px縮小・透過必須） | P-04 | Single flat badge icon on transparent background: small glowing crystal shard/orb, color `#ABFFFF`（コアエネルギー色の予約用途と統一）, simple circular frame, used as essence（通貨）残高表示アイコン | primary（image_sprite: fal:ideogram-v3-transparent） | generated |
| IMG-05 | ui-icon-sheet | `game/_generated/textures/icon-achievements.png` | 1024px x5フレーム（ゲーム内256px縮小・透過必須） | P-04 | Five-icon flat badge sprite sheet on transparent background, each visually distinct and matched to: ACH-01 初勝利（shield+checkmark）／ACH-02 完全防衛（unbroken shield outline, no cracks）／ACH-03 累計撃破（crosshair/target over `#973FA5`）／ACH-04 範囲特化（radiating arc waves over `#15ACC1`）／ACH-05 倹約防衛（minimal-count build-spot glyph）。UIパネル色 `#12262A` 背景を想定した前景色は `#EAF6F5` 基調 | primary（image_sprite: fal:ideogram-v3-transparent） | generated |
| IMG-06 | ui-icon-sheet | `game/_generated/textures/icon-upgrades.png` | 1024px x3フレーム（ゲーム内256px縮小・透過必須） | P-04 | Three-icon flat badge sprite sheet on transparent background, matched to: UPG-01 初期資金（coin stack）／UPG-02 割引率（percentage discount tag）／UPG-03 essence獲得率（crystal shard with up-arrow, `#ABFFFF`）。ACH アイコン（IMG-05）と同一の意匠言語で統一 | primary（image_sprite: fal:ideogram-v3-transparent） | generated |
| IMG-07 | ui-icon-sheet | `game/_generated/textures/icon-enemy-indicator.png` | 1024px x2フレーム（ゲーム内256px縮小・透過必須） | P-03 | Two-icon flat silhouette sprite sheet on transparent background, used in wave予告UI: Marauder（small bipedal silhouette, body `#973FA5` / accent `#C71A23`）と Warbeast（larger quadruped silhouette, same palette, 見た目のボリュームがMarauder比約1.7倍）。二足 vs 四足のシルエット差だけで256px縮小後も即判別できること（シルエット方針と一致） | primary（image_sprite: fal:ideogram-v3-transparent） | generated |
| IMG-08 | ui-icon | `game/_generated/textures/icon-core-hp.png` | 1024px（ゲーム内256px縮小・透過必須） | P-01 | Single flat badge icon on transparent background: faceted crystal-heart hybrid silhouette, exclusive use of core-energy color `#ABFFFF`, used beside the Core HP バー。防衛対象としての最優先視認性（コアクリスタルはP-01「配置の結果が返ってくる先」の象徴） | primary（image_sprite: fal:ideogram-v3-transparent） | generated |

<!-- brief.md スコープ制約: 音資産数上限 SFX 6点／BGM 1曲（厳守）。
     engine=unity のため出力形式は OGG のみ（tech-stack-unity.md「資産の取り扱い」— Safari用M4Aはphaser専用要件で不要）。
     raw生成物の置き場は game/_generated/audio/（画像=textures/、3Dモデル=models/ と同じ _generated 配下の慣例に合わせる）。
     SFXは全て単発（loop:falseが既定・ループ指定なし）。BGM-01のみloop:true・seamless。
     6点予算の制約上、タワー2種（Bastion Cannon/Arc Emitter）の発射音は1本に統合し（SFX-02）、
     敗北時のコア崩壊SFX（gdd.md 勝敗判定節）もコア被弾音（SFX-04）を流用する
     （実装側で再生速度/ボリュームを変えて重み付けを演出可。brief.mdが列挙する6項目
     「設置/射撃/敵撃破/コア被弾/ウェーブ開始/勝利ジングル」とSFX-01〜06が1対1対応）。
     duration_secondsは全SFXで明示（ElevenLabs SFX v2はduration_seconds未指定だとコスト5倍になるため必須）。
     SFXはseed固定不可のため4変種生成→ゲーム内文脈でベスト選別し、選別理由をMANIFESTに追記する（audio-designerの生成時作業）。 -->

## SFX

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|
| SFX-01 | sfx | `game/_generated/audio/sfx-tower-place.ogg` | 0.4秒（実測0.22秒。API duration_seconds下限0.5s+無音トリムにより短縮） | P-01 | 軽く硬質な設置音。ローポリの防衛タワー（Bastion Cannon / Arc Emitter 共通）をビルドスポットに置いた瞬間の短い「コツン」。金属+クリスタルが混ざったニュアンスの軽い衝突音、低音の重さは出さず短く歯切れよく減衰。トリガー: 左クリックでのタワー設置確定（`BuildSpotSystem` 設置イベント）。取り返しのつかない一手の手応えとして軽すぎず安っぽくならない質感にする。 | primary（sfx: elevenlabs:sfx-v2） | generated |
| SFX-02 | sfx | `game/_generated/audio/sfx-tower-fire.ogg` | 0.35秒（実測0.48秒。API duration_seconds下限0.5s制約） | P-02, P-03 | タワー発射音（Bastion Cannon砲撃 / Arc Emitter範囲放出の共通汎用音。6点予算制約により1種へ統合 — 実装側でピッチ・音量を僅かに変えて役割差を演出する余地を残す）。短いエネルギー放出インパクト、軽いパルス感、リアル寄りだが重すぎない質感。トリガー: `TowerCombatSystem` の発射エフェクトトリガー。 | primary（sfx: elevenlabs:sfx-v2） | generated |
| SFX-03 | sfx | `game/_generated/audio/sfx-enemy-defeat.ogg` | 0.5秒（実測0.35秒。無音トリムにより短縮） | P-03 | 敵撃破音。Marauder/Warbeast共通、ローポリクリーチャーが弾けて消滅する短い「パチッ」とした崩壊音。生々しすぎない、ライトファンタジー寄りの軽い破裂質感。トリガー: `EnemyHealthSystem` の撃破エフェクト/SFXトリガー（HP<=0判定）。敵が溶けていく実感（P-03）を音でも即座に返す。 | primary（sfx: elevenlabs:sfx-v2） | generated |
| SFX-04 | sfx | `game/_generated/audio/sfx-core-hit.ogg` | 0.6秒（実測0.46秒。無音トリムにより短縮） | P-01, P-03 | コア被弾音。敵がゴールに到達しコアHPが減少した瞬間の短い「ヒュン→ゴツン」としたクリスタル打撃音。予算制約により敗北成立時のコア崩壊SFX（gdd.md「勝敗判定」節）も本ファイルを流用する（実装側で再生速度を落とし音量を上げ崩壊の重みを演出）。トリガー: コアHP減算イベント／敗北判定成立イベント。配置ミスの結果が即座に返る（P-01）ことを痛みとして伝える。 | primary（sfx: elevenlabs:sfx-v2） | generated |
| SFX-05 | sfx | `game/_generated/audio/sfx-wave-start.ogg` | 1.2秒（実測0.69秒。無音トリムにより短縮） | P-01 | ウェーブ開始告知音。短いホーン/チャイム系のアラート、緊張感を煽るが不快にならない軽やかな上昇フレーズ。トリガー: `WaveSpawnSystem` のウェーブ予告表示イベント（次ウェーブ開始）。準備フェーズ（`WAVE_PREP_SEC`）で配置を検討する猶予の始点を告げる（P-01）。 | primary（sfx: elevenlabs:sfx-v2） | generated |
| SFX-06 | sfx | `game/_generated/audio/sfx-victory-jingle.ogg` | 3.0秒（実測1.92秒。無音トリムにより短縮） | P-03, P-04 | 勝利ジングル。全8ウェーブ防衛達成時（gdd.md「勝敗判定」節）の短いファンファーレ。BGM-01と同一キー（D major）で調和する明るい上昇分散和音、オーケストラ風の軽い質感（ブラス+グロッケン/トライアングルの煌めき）。トリガー: 勝利判定成立イベント。負けても伸びる防衛網（P-04）の対極としての「積み上がった配置が報われた」瞬間を明るく肯定する。 | primary（sfx: elevenlabs:sfx-v2） | generated |

## BGM

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 | ループ要件 | 長さ | BPM/キー |
|---|---|---|---|---|---|---|---|---|---|---|
| BGM-01 | bgm | `game/_generated/audio/bgm-main-theme.ogg` | 38.4秒（1ループ） | P-03 | ミディアムテンポのオーケストラ風ライトファンタジー。防衛シーン全編で流れ続けるBGMとして戦闘の緊張感を邪魔しない、明るく前向きなトーン（ダーク・重厚な音作りは避ける）。楽器編成: ストリングス（pizzicato中心の軽快なリズム基盤）+ 木管（フルート/クラリネットの主旋律）+ 軽いブラスアクセント + 打楽器（ティンパニは控えめ、トライアングル/グロッケンの煌めきを要所に）。composition_plan: セクションA（bar1–8、木管主旋律+ストリングスpizzicato伴奏、静かめの導入）→セクションB（bar9–16、ブラス/パーカッションが加わり盛り上がり、bar16終端の和声・音量をbar1頭へシームレスに接続するボイシングで着地）。全16小節を1ループとし、`force_instrumental:true`（歌詞無し）。ループ再生前提のため、意図的なフェードイン/フェードアウトは付けない。 | primary（bgm: elevenlabs:music-v2） | generated | seamless | 38.4秒（16小節×4/4、1拍0.6秒） | BPM 100 / D major |

補足: SFX/BGM 共通で生成後は ffmpeg `loudnorm`（-16 LUFS）→ 無音トリム → エンジン既定形式（unity: OGG のみ）変換をローカルパイプラインで実施する（assets-config.md「生成後パイプライン」）。BGM-01 は小節境界クロスフェード編集後、同一ファイルを2連結してシーム位置のクリックノイズ/RMS段差をスキャンするループ検証に合格するまで再生成する。全音声資産は音量（-16 LUFS）が揃っていることを出荷条件とする。

## 3Dモデル

<!-- brief.md スコープ制約: 3Dモデル数上限5体以内（タワー2種・敵2種・コアクリスタル1）。
     全5体は gdd.md「モーション方式（全MDL共通）」の確定判断により rig_type: none
     （スケルタルアニメ無し・静的メッシュ、動きは全てコード駆動）。
     したがってMANIFESTのkindは全て character_rigged を使わず prop で統一する
     （MANIFESTのkind enumは character_rigged | prop | environment | animation_only の4値のみで
     「リグ無しキャラクター」の専用値が無いため。Marauder/Warbeast は概念上は敵クリーチャーだが、
     リグを持たない静的メッシュという生成実態に合わせ kind=prop として記録する）。
     ルートも同じ理由で全5体 model_prop（meshy:direct-image-to-3d、リグAPI呼び出し無し）に統一し、
     state/asset-routing.json の model_character（+rigging付き）ルートは使用しない
     （不要なrigging API呼び出しコストを避ける — gdd.md の確定判断と整合）。
     各行のプロンプト草案は「2Dコンセプト画（image-to-3D入力）」の被写体指定。
     style_block は機械前置。コンセプト画自体は design/refs/mdl-concepts/ に中間生成物として保存し、
     8点のUI画像予算（##画像節）とは別管理・別ID無し（MDL生成パイプラインの内部ステップとして
     MANIFESTのMDL行にまとめて記録する）。 -->

| id | kind | ファイル名 | ポリ予算 | リグ | アニメ（ANM-xx） | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|---|---|
| MDL-01 | prop | `game/_generated/models/model-bastion-cannon.glb` | 6,000 tri | none | なし | P-01, P-02 | Single-subject concept turnaround: tall narrow spire-shaped defense tower, lattice/grid leg base, overall height 3.5m（調整レンジ3.2–3.8m、全高の約1/3以下の幅）, matte PBR body `#0B6C58`, darker base/leg shading `#00371A`, glowing sensor-window accent `#15ACC1`, plain neutral studio backdrop | primary（model_prop: meshy:direct-image-to-3d） | generated |
| MDL-02 | prop | `game/_generated/models/model-arc-emitter.glb` | 6,000 tri | none | なし | P-01, P-02 | Single-subject concept turnaround: squat dome-shaped defense tower, wide low silhouette, overall height 2.2m（調整レンジ2.0–2.5m、幅は全高に対して広め）, same palette as MDL-01（body `#0B6C58` / base `#00371A` / accent `#15ACC1`）, plain neutral studio backdrop。MDL-01と横並びにしたとき縦横比だけで種別判別できること | primary（model_prop: meshy:direct-image-to-3d） | generated |
| MDL-03 | prop | `game/_generated/models/model-marauder.glb` | 4,000 tri | none | なし | P-02, P-03 | Single-subject concept turnaround: **small bipedal creature**, body height 0.85m（調整レンジ0.7–1.0m）, thin legs, head large relative to body, matte PBR body `#973FA5`, horn/eye accent `#C71A23`, underbelly shading `#3A0104`, plain neutral studio backdrop。**注記（art-bible.md「3Dスタイル方針」AR-BIBLE iteration1指摘反映の転記）**: `character_reference`（`design/refs/crop-04-enemy-pack.png`）に写る個体はいずれも四足接地姿勢のため、そこから継承してよいのは色・素材・表面処理のみであり、骨格・ポーズ（二足/四足の別・寸法比）は本行の数値仕様（二足シルエット・体高0.7–1.0m）を必ず優先する。四足ポーズをそのまま模倣しないこと | primary（model_prop: meshy:direct-image-to-3d） | generated |
| MDL-04 | prop | `game/_generated/models/model-warbeast.glb` | 6,000 tri | none | なし | P-02, P-03 | Single-subject concept turnaround: **large quadruped creature**, body height 1.5m（調整レンジ1.3–1.7m、Marauder比約1.7倍）, horizontally elongated low-slung heavy body, same enemy palette as MDL-03（body `#973FA5` / accent `#C71A23` / underbelly `#3A0104`）, plain neutral studio backdrop。`character_reference`（`crop-04-enemy-pack.png`）の四足接地ポーズはこのモデルにはそのまま流用可 | primary（model_prop: meshy:direct-image-to-3d）→ 第二候補（fal:fal-ai/meshy/v6/image-to-3d）→ Fallback（fal:fal-ai/hunyuan3d/v2 → TRELLIS 系 → fal:fal-ai/hyper3d/rodin）→ Tripo 直API（TRIPO_API_KEY・preflight 認証200済み・残量は使用時確認）→ ローカル縮退（Blender プリミティブ合成・`must_replace:true`） | generated（2026-07-22・Primary〔Meshy直API image-to-3d〕が初回試行で成功、fallbackチェーンは未使用。詳細は下記「生成時の実施メモ（MDL-04）」） |
| MDL-05 | prop | `game/_generated/models/model-core-crystal.glb` | 3,000 tri | none | なし | P-01, P-03 | Single-subject concept turnaround: tall glowing crystal on a stone pedestal, overall height 2.5m（調整レンジ2.2–2.8m）, faceted low-poly crystal body using core-energy color `#ABFFFF`（画面内で他要素に流用しない専有色）, pedestal stone color `#A9CBD5`, plain neutral studio backdrop。追加で emissive マップ（1024px）を持つ（art-bible「3Dスタイル方針」テクスチャ・PBR節） | primary（model_prop: meshy:direct-image-to-3d） | generated |

補足: 全5体は静的メッシュ（リグ無し）のため出力形式は **GLB**（assets-config.mdの出力形式方針「静的=GLB」）。取込先は `game/Assets/Resources/Generated/`（contract §11 改定 — `Resources.Load` 方式。AssetKeys の値は Resources 相対パス。tech-stack-unity.md「資産の取り扱い」）。スケール規約は art-bible.md「3Dスタイル方針」節の `bbox_authoring_m` レンジをそのまま使用し、authoring-time計測をMANIFESTへ記録する。

**生成時の実施メモ（MDL-01/02/03/05・2026-07-21・art-director）**:
- Meshy image-to-3d はプロンプト内の実寸ヒント（「overall height 3.5m」等）を出力スケールに反映しない（全出力が概ね同程度の正規化バウンディングボックスに収まる既知の挙動）。そのため生成直後に Blender headless で各モデルの主要軸（Z、glTF Y-up→Blenderインポート後Z-up）を art-bible.md「スケール規約」の初期値（3.5m / 2.2m / 0.85m / 2.5m）へ均一スケールで補正し、床面（min Z）を原点に接地させたうえで再エクスポート・再計測している。`bbox_authoring_m` は補正後の実測値（MANIFEST記録済み）。
- MDL-01（プロンプト上のポリ予算6,000）はMeshy出力が6,023 triとわずかに超過したため、gltfpack未導入のためBlenderのDecimateモディファイア（比率調整）で5,899 triへ縮小して差し替え済み（`gltfpack -si` の代替手段。assets-config.md「生成後パイプライン」）。
- MDL-02の初回コンセプト画（seed 424243）は「defense tower」プロンプトが誤って緑色の生物的な塊として生成されたため、「無機質な機械構造物・生物ではない」を明示したプロンプトへ改訂し再生成（seed 424246）。MDL-03の初回コンセプト画（seed 424244）は体色が指定`#973FA5`より青紫寄りに逸脱したため、色相を強調したプロンプトへ改訂し再生成（seed 424247）。旧生成物は不採用として上書き（design/refs/mdl-conceptsは最終採用版のみ保持）。
- 全4体で `non_manifold_verts`（MANIFEST記録・頂点比0.4〜1.3%）が0ではない。gltf-transform validateはエラー0（穴・破断メッシュではなくメッシュシーム近傍の軽微なトポロジ）だが、art-reviewerのAR-ASSET観点2（非多様体検査）向けに開示事項として明記する。
- MDL-04（Warbeast）はコアループ縦串（S-04敵移動/S-05タワー攻撃）に含まれず、WAVE 3以降で初出現（S-12・phase:build）のため本バッチでは見送り、Phase 3に回す。

**生成時の実施メモ（MDL-04・2026-07-22・art-director・Phase 3）**:
- checkpoint-b-feedback指摘2（「MDL-04 Warbeast再生成方針」節）に基づき生成。Primary（Meshy直API image-to-3d、コンセプト画seed 424260・破棄試行なし）が初回試行で成功したため、`degradedRoutes` は空配列（fallbackチェーン=fal経由meshy-v6→Hunyuan3D/TRELLIS/Rodin→Tripo直API→ローカル縮退は未使用。MANIFEST MDL-04行に開示済み）。
- Meshy出力のbaseColorTextureを実測した結果、body色クラスタ（`#973FA5`目標）は既に閾値内（距離35.7）だったが、underbelly暗色域（`#3A0104`目標）が生成物にほぼ反映されておらず（距離108.5、該当色相帯にV<0.35画素が0件）、horn/eyeアクセント（`#C71A23`目標）も閾値超過（距離48.4）だった。プロンプト再生成は行わず、MDL-01/MDL-02のAR-ASSET iteration1指摘対応で確立した決定論的HSVクラスタ再センタリング手法（`game/_generated/scripts/recolor_basecolor_mdl04.py` + `swap_basecolor_mdl04.py`）を適用し、3クラスタとも距離40未満（body 2.2 / underbelly 1.0 / horn 10.2）まで補正した。この色補正はMANIFESTの`color_correction`フィールドに開示済み（純粋なAI一発生成ではなく生成→ローカル色補正の合成）。
- ポリ予算6,000 triに対し実測5,913 tri（予算内、Decimate不要）。`bbox_authoring_m`は[1.3003, 2.5747, 1.5]（全高1.5mはart-bible.md「スケール規約」の初期値と一致、体長2.57mは体高比約1.7倍で「horizontally elongated low-slung heavy body」仕様と整合）。`non_manifold_verts`は35（頂点比0.62%、5,665頂点中）で他4体と同水準の軽微なトポロジ（gltf-transform validateエラー0）。
- コスト: Meshy直API `consumed_credits`=30（他4体と同額）+ コンセプト画1点（$0.06）= $0.66（`cost_estimated:true`、他MDL行と同一の保守見積基準）。

## スケルタルアニメーション

`design/gdd.md`「モーション方式（全MDL共通）」および `design/art-bible.md`「アニメ方針」の確定判断により、**本作はANM資産を1件も発注しない**（MDL-01〜05は全て `rig_type: none`）。動き（タワーの発射時回転・リコイル、敵の移動・向き追従・sin波ボブ、コアクリスタルの被弾フラッシュ/スケールパルス）は全てコード駆動のtransform操作で表現し、Meshyのrigging/animation APIは使用しない（`state/asset-routing.json` の `anim` ルートは本作では未使用）。将来リグ付き資産を追加する場合のみ、assets-config.mdの3Dルーティング表（Meshy直APIのrigging/animation → 403時fal経由へ切替）に従う。

| id | 対象 MDL | クリップ名 | 内容（例: walk / run / idle） | P-xx | ルート | 状態 |
|---|---|---|---|---|---|---|
| （該当なし — 上記のとおりANM資産は発注しない） | — | — | — | — | — | — |

## 集計と予算

- 画像: 8点（IMG-01〜08） / SFX: 6点（SFX-01〜06、brief上限6点） / BGM: 1曲（BGM-01、brief上限1曲） / 3Dモデル: 5点（MDL-01〜05） / アニメ: 0点（ANM資産なし）
- 概算コスト合計（画像+3Dモデルのみ・当初保守見積・Phase 1時点）: 約 $4.46（見積の内訳は下記のまま保持。**実績値は `game/_generated/MANIFEST.jsonl` の `cost_usd` 合算が正**）
  - 画像: IMG-01/02（flux-2-pro, 保守見積 $0.05/点 ×2 = $0.10）+ IMG-03〜08（ideogram-v3-transparent, 保守見積 $0.06/点 ×6 = $0.36）= $0.46
  - 3Dモデル: MDL-01〜05（Meshy直API image-to-3d、リグ無し・単一呼び出し。直API単価は assets-config.md「未検証事項」により未確定のため fal経由実勢値 $0.80/生成 を保守見積として流用 ×5 = $4.00。**`cost_estimated: true` をMANIFEST記録時に必ず付与**）
  - SFX/BGMのコストは audio-designer が生成後に加算する（state/budget.txt の $20 上限に対し現時点で $15.54 の残枠があり、brief記載の音資産規模（SFX6+BGM1）は通常この範囲に収まる想定）
- `state/budget.txt`（$20）を超過する見込みが生成前に判明した場合は、資産を削るか Checkpoint で人間へエスカレーションする（超過確定後の生成停止はart-director/audio-designer共通の義務 — contract §10）。
- **実績（Phase 3・MDL-04生成後・art-director・2026-07-22）**: `game/_generated/MANIFEST.jsonl` の `cost_usd` 合算（`cost_usd`フィールドを持つ全20行の実測合計）は **$5.5979**。`state/budget.txt`（$20）に対し残枠約$14.40。IMG-01/02/05/06/07・BGM-01は本バッチ未生成のため残枠内で生成予定。
- **実績更新（Phase 3・BGM-01生成後・audio-designer・2026-07-22）**: BGM-01（`cost_usd:0.096`、採用分のみ。不採用2試行分$0.192は`cost_usd_all_attempts`に開示済みで予算合算には含めない）を追加し、`cost_usd`合算（全20行）は **$5.5979**（前行のMDL-04時点合計に既にBGM分が含まれていたため増分なし — 前回集計時の記録誤差の可能性があり、正の値は都度`game/_generated/MANIFEST.jsonl`を直接合算すること）。`state/budget.txt`（$20）に対し残枠約$14.40。残るIMG-01/02/05/06/07（art-director領分）のみ本バッチ未生成。

### Phase 2（prototype）画像生成スコープ（art-director・2026-07-21）

- prototype phase の縦串実装（state/stories.yaml phase:prototype の acceptance）を精査した結果、資産の実シーン取込・俯瞰カメラ配置・音再生配線は S-19（`資産統合`）が担当し **phase: build** に属する。つまり prototype 縦串の acceptance テストは MDL/IMG の実在に依存しない（primitive/テキストで代替可能）。
- そのうえで、コアループの中心的な意思決定 UI に直接紐づく3点のみ prototype 段階で先行生成した:
  - IMG-03（タワー選択アイコン）: S-05/S-08 のビルドスポット左クリック→タワー種別選択メニューで使用。P-02 のシルエット判別要件に直結
  - IMG-04（essence アイコン）: S-03 Menu のアウトゲーム表示（所持 essence）で使用
  - IMG-08（コアHPアイコン）: S-08 HUD のコアHP表示で使用
- 残り5点（IMG-01/02 地形タイル、IMG-05 実績アイコン、IMG-06 アップグレードアイコン、IMG-07 敵インジケータ）は S-15/S-19 等 build phase のストーリーでのみ参照されるため、Phase 3（`/forge-build`）の art-director 呼び出しに送る（`status: planned` のまま据え置き）。
- 生成実績は `game/_generated/MANIFEST.jsonl`（IMG-03/04/08、3件、合計 $0.18・`cost_estimated:true`）。アルファ検証（角ピクセル `srgba(0,0,0,0)`・不透明白ピクセル比率0%）を全数実施しトリム済み。

### Phase 2（prototype）3Dモデル生成スコープ（art-director・2026-07-21）

- 3Dモデルは画像と異なり「実シーン取込（S-19・build phase）が無くても acceptance テストが通る」とは言えない代替が無いため、`state/stories.yaml` の phase:prototype acceptance を基準に対象を選定した: S-04（敵Marauderの移動・コア防衛/敗北）と S-05（タワー2種の設置・攻撃・経済）がコアループ縦串の中心的な意思決定対象であり、MDL-01（Bastion Cannon）・MDL-02（Arc Emitter）・MDL-03（Marauder）・MDL-05（コアクリスタル）の4点を先行生成した。
- MDL-04（Warbeast）はWAVE 3以降で初出現（S-12・phase:build専用の難易度曲線）でありprototype縦串の acceptance に登場しないため、Phase 3（`/forge-build`）へ送る（`status: planned` のまま据え置き）。
- 生成実績は `game/_generated/MANIFEST.jsonl`（MDL-01/02/03/05、4件）。パイプライン: art-bible.json `style_block` を前置した2Dコンセプト画（`fal:ideogram-v3-transparent`）→ Meshy直API image-to-3d → Blender headless で art-bible「スケール規約」の authoring 高さへ均一スケール補正（Meshyはプロンプト中の実寸ヒントを無視し出力を正規化バウンディングボックスへ収める既知の挙動のため）→ MDL-01のみ Decimate で ポリ予算内(5,899/6,000 tri)へ調整 → 全4体 `npx @gltf-transform/cli validate` エラー0・非多様体頂点は軽微(頂点比0.4〜1.3%)で開示事項として記録 → Blender headlessプレビューレンダー保存(`game/_generated/previews/`)。詳細は上記「3Dモデル」節末尾の実施メモを参照。
- コスト実績: Meshy直API `consumed_credits`=30/体（応答実測値）× 4体 = 120 credits。直API credit→USD 換算はassets-config.md「未検証事項(2)」により未確定のため保守見積 $0.02/credit を適用（$0.60/体 ×4 = $2.40、全件 `cost_estimated:true`）。画像+SFX+3Dモデルの累計は約 $2.62（`state/budget.txt` の $20 上限に対し残枠 約$17.38）。

### Phase 3（build）資産生成スコープ（tech-director Replan・2026-07-22・checkpoint-b-feedback 反映）

Checkpoint B で「盤面が暗い（地形/背景未生成・カメラ本配置未了）」との監査指摘（retro-e3 指摘2）を受けた Phase 3 の生成対象。**環境ビジュアル（IMG-01/02）を最優先**とし、実装は S-21（環境ビジュアル本仕上げ）が取込・カメラ/ライト本配置を担う。

| id | 種別 | 状態 | 生成優先度 | 使用ストーリー |
|---|---|---|---|---|
| IMG-01 | tile（芝） | generated（2026-07-22。2試行目〔seed 20260733〕採用・HSV色補正+seamless-tile後処理を実施。下記「生成時の実施メモ（IMG-01/02/05/06/07・2026-07-22）」参照） | **最優先**（盤面の暗さ解消・P-01 経路視認性） | S-21（取込・地面配置） |
| IMG-02 | tile（土道） | generated（2026-07-22。2試行目〔seed 20260734〕採用・HSV色補正+seamless-tile後処理を実施） | **最優先**（一本道の視認性） | S-21（取込・経路配置） |
| IMG-05 | ui-icon-sheet（実績×5） | generated（2026-07-22。HSVクラスタ色補正済み） | 中 | S-15（Menu 実績パネル） |
| IMG-06 | ui-icon-sheet（UPG×3） | generated（2026-07-22 revision3。バッチ一貫性チェック指摘対応でUPG-03クリスタルをIMG-04/IMG-08と同系の淡いペールシアンへ再補正。下記「revise実施メモ（IMG-06/IMG-07）」参照） | 中 | S-15（Menu UPG 購入 UI） |
| IMG-07 | ui-icon-sheet（敵×2） | generated（2026-07-22 revision2。バッチ一貫性チェック指摘対応でbody/accent比をMDL-03/04方向へ是正〔残差は非blocker開示〕） | 中 | S-08 HUD 拡張/ウェーブ予告・S-12/S-19 |
| BGM-01 | bgm | generated（2026-07-22。3試行目〔song-id AjlJ6ygaTHlbBz111th7〕採用・下記「生成時の実施メモ（BGM-01）」参照） | 中 | S-19（Game 中ループ再生配線） |
| SFX-01〜06 | sfx | generated | — | S-19（各イベント再生配線・SFX-05 ウェーブ開始含む） |
| MDL-04 | prop（Warbeast） | generated（2026-07-22。Primary成功・degradedRoutes空） | 中（WAVE 3 以降で出現） | S-12（ウェーブ構成）・S-19（View 割当） |

#### MDL-04 Warbeast 再生成方針（checkpoint-b-feedback 指摘2・新規約）

- **placeholder 直行禁止**: Primary（Meshy 直API）が失敗しても即プレースホルダに落とさず、上表「ルート」列の fallback チェーンを**全段順に試行**する（Meshy 直 → fal 経由 Meshy → Hunyuan3D/TRELLIS/Rodin → Tripo 直API → 最後にローカル縮退）。
- **試行ルートと HTTP コードを列挙**: 各段の試行結果（プロバイダ・エンドポイント・HTTP ステータス・失敗理由）を MANIFEST の MDL-04 行に `degradedRoutes: [{provider, endpoint, http_status, reason}, ...]` として全列挙し、最終採用ルートを `provider` に記録する。縮退（ローカル）に至った場合のみ `must_replace:true` を付与し Checkpoint C で人間へ個別警告する。
- Tripo は preflight 認証 200 済み（`state/asset-routing.json`）だが残量は使用時に確認する。残量不足で 402/403 が返る場合はその HTTP コードも `degradedRoutes` に記録して次段へ進む。
- 生成後は他 MDL と同じパイプライン（gltf-transform validate エラー0 → Blender headless で art-bible「スケール規約」の 1.5m authoring 高さへ均一スケール補正・接地 → `bbox_authoring_m` 実測記録 → プレビューレンダー保存）を通す。

**生成時の実施メモ（BGM-01・2026-07-22・audio-designer）**:
- Eleven Music (`POST /v1/music`, model `music_v2`) は `composition_plan` 使用時に `force_instrumental` パラメータを渡すと422（`force_instrumental can only be used with prompt`）で拒否される。歌詞無し（instrumental）は各セクションの `lines: []`（空配列必須フィールド）で表現し、`force_instrumental` は付与していない（design/assets.md本文の記載と実装の差分として開示）。同様に `music_length_ms` は `composition_plan` と同時指定不可（422）で、尺は `sections[].duration_ms` の合計（19200+19200=38400ms）で制御した。
- **3試行を要した**（全て38.4秒・同一composition_plan構造、プロンプト文言のみ変更）。1試行目（song-id `ysYwTFMm7c67Uymd2h9E`）は「no fade in/out」を明示指定したにもかかわらず末尾に約1.1〜2秒の指数的フェードアウト（RMSが34.2秒時点-13dBから38.2秒時点-82dBまで減衰、`silencedetect -45dB`が37.27–38.4秒区間で無音判定）が付き、ループ再生時に無音→フル音量の跳躍が生じるため不採用。2試行目（song-id `glEW4SuYC5TTjAgnzY4W`）はネガティブ指定をさらに強化した結果、逆に末尾6.36秒が-89dBの無音域になり（`silencedetect`32.04–38.4秒）不採用。3試行目（song-id `AjlJ6ygaTHlbBz111th7`）で「ゲームの無限ループキューであり終わりのある楽曲ではない」「最後のビートまで一定の音量・密度を維持し、そのままループ点へ直結する」という再フレーミングに変更し採用: `silencedetect -45dB`が全曲でゼロ検出（無音区間なし）、末尾は約100〜200msの自然なノートリリース減衰のみで、そのRMS水準（末尾2秒で-27〜-34dB）が曲頭0.5秒のRMS水準（-33〜-37dB）と近接していたため、ループクロスフェード編集（下記）が高品質に成立した。
- Eleven Music APIは seed パラメータを受け付けない（`composition_plan`・`prompt` いずれのモードでも未対応、422/無視のいずれでもなく単に仕様に存在しない）。provenanceは応答ヘッダの `song-id` で代替記録した（MANIFEST `seed_note`/`generation_attempts` 参照）。
- **ループ編集（小節境界クロスフェード）**: 100 BPM・1拍0.6秒・1小節2.4秒のため小節境界は 0, 2.4, …, 36.0, 38.4秒。末尾300ms（37.8→正確には38.1–38.4秒）と先頭300msを`acrossfade`（triangularカーブ）でブレンドし、本体（0–38.1秒）と結合して全長38.4秒を変えずにシームを均した。検証は「同一ファイルを2連結してシーム位置をスキャン」を2段階実施: (1) 波形サンプル差分（クリック検出）— シーム地点の最大サンプル差分266（post-encode 260）に対し、ランダムサンプリングした曲中40窓の最大差分は平均863・中央値782（音楽的なアタックによる正常な差分）— シームの差分はこれらより明確に小さくクリック無し。(2) RMS段差検出 — シームの50msウィンドウ間RMS段差3.24dB（pre/post-encode同値）に対し、曲中の隣接窓200サンプルのRMS段差は平均4.85dB・中央値3.66dB・p90=9.56dB — シームの段差は中央値以下で異常な段差ではない。OGGエンコード後（lossy圧縮後）も同一手法で再検証し両テスト合格を再確認した。
- ラウドネス: `loudnorm` 2-pass linear（I=-16 LUFS, TP=-1.5dBTP, LRA=11）適用後、最終OGG実測値 I=-15.98 LUFS / TP=-2.82dBTP（-16±1の許容域内、クリッピング無し）。
- 出力形式: OGG Vorbis 145kbps（engine=unity既定・OGGのみ、tech-stack-unity.md「資産の取り扱い」）。
- コスト: 採用分1回 $0.096（38.4秒=0.64分×$0.15/分。ElevenLabs `/v1/music`応答にはSFXの`/v1/sound-generation`と異なりcredit/costヘッダが無いため`cost_estimated:true`、基準はassets-config.mdのBGM料率）。不採用2試行を含む探索コスト合計は$0.288（MDL-01/02の破棄コンセプト画と同じ開示方針でMANIFESTの`cost_usd_all_attempts`に記録、`cost_usd`本体には採用分のみ計上）。

**実施結果（art-director・2026-07-22）**: Primary（Meshy 直API image-to-3d）がコンセプト画seed 424260・破棄試行なしの初回試行で成功したため、fallback チェーン（fal 経由 Meshy → Hunyuan3D/TRELLIS/Rodin → Tripo 直API → ローカル縮退）は未使用。MANIFEST の MDL-04 行に `degradedRoutes: []` を明記し、Primary 成功時は空配列で開示することをこの規則の運用として確定した（本規則は Primary 失敗時のみ fallback 全段試行を要求するものであり、Primary 成功時に空チェーンを人為的に作る必要はない）。`must_replace` は付与していない（Meshy 直API・plan_tier: pro+ の生成物）。生成後パイプラインは他4体と同一のBlenderスクリプトを再利用して実施し、加えてMeshy出力のbaseColorTexture色ズレ（underbelly暗色域欠落・horn/eyeアクセント逸脱）を決定論的HSV補正で是正した（詳細は「3Dモデル」節末尾の実施メモ）。

**生成時の実施メモ（IMG-01/02/05/06/07・2026-07-22・art-director）**:
- 対象: Phase 3 スコープのうち画像資産5点全数（`game/_generated/MANIFEST.jsonl` に未記載）。ルートは全て `state/asset-routing.json` の Primary を使用（IMG-01/02: `image_background: fal:flux-2-pro` / IMG-05/06/07: `image_sprite: fal:ideogram-v3-transparent`）。全プロンプトに art-bible.json `style_block` を機械的に前置。予算照合: 生成前のMANIFEST合算（当時25行、audio-designer分BGM-01追記前）から本バッチ見込み$0.38を加えても `state/budget.txt`（$20）を大幅に下回るため生成継続。
- **IMG-01/IMG-02（タイル）は各2試行を要した**。1試行目（seed 20260728/20260729）はstyle_block由来の斜方向キーライト+散在する大きな低ポリ岩を含む「シーン」的な絵として生成され、(a) 明暗方向性がタイル境界で不連続になる、(b) IMG-02は指定の「土のみで全面を埋める」ではなく芝に囲まれた一本道のシーンになり境界がジグザグで不均一、という2つの理由でタイル化に不適と判断し破棄。2試行目でプロンプトを改訂（top-downの正投影を強調、キーライトの指向性を明示的に無効化し均一無方向照明を指定、大きな孤立オブジェクトを排除して小粒パターンを画面全体に均等分布させる指定、IMG-02は芝・パス境界を含めず全面土のみにする指定を追加）し採用（seed 20260733/20260734）。
- **タイル継ぎ目補正**: 採用画像も生成モデル自体には「本当のシームレスタイル」保証がないため、ローカルでオフセット重ね合わせ検査（画像を半分ずつロールし境界ピクセルと内部ピクセルのRGB差分を比較）を実施したところ、ラップ境界差分が内部差分の1.7〜2.6倍（continuous版と比較して明確な段差）を検出。ロール+境界帯（幅3%・ブラー1%）のフェザーブレンドで補正した結果、比率はIMG-01が1.16〜1.19倍・IMG-02が1.03〜1.09倍まで低下（内部の自然な変動とほぼ同水準）。**開示**: 2x2/3x3タイル敷き詰め目視では、IMG-01（芝）にわずかな十字状のソフトブレンド痕跡が視認可能（草の高周波ディテールのため）。IMG-02（土）はほぼ視認不可（暗い溝状の線として自然な地面の凹凸と紛れる程度）。完全な無痕ではないため、AR-ASSETでの追加レビューを推奨する開示事項として記録した。
- **パレット逸脱と補正（IMG-01/02/05/06/07 全数）**: 生成直後の実測で全5点がart-bible.jsonパレットまたはdesign/assets.mdの明示hex指定から大きく逸脱していた（IMG-01: 距離132.1／IMG-02: 距離59.75／IMG-05: ACH-03距離97.5・ACH-04距離38.4／IMG-06: UPG-03必須指定`#ABFFFF`に対し距離170.7／IMG-07: body距離97.8・accent距離54.2）。MDL-01/02（前回バッチ）と同じ手法（決定論的HSVクラスタ色補正: k-means 2〜3クラスタで背景/バッジ/前景を分離し、各クラスタの平均色相・彩度・明度をターゲットhexへシフトしつつ画素単位のローカルな陰影変動は保持）をローカルPillow/numpyスクリプトで適用し、全指標を距離0.8〜17.4（既存許容基準の40未満）まで補正。**provenance開示**: 色は生成→決定論的色補正の合成であり純粋なAI一発生成ではない（MANIFESTの`color_correction`フィールドに補正前後の実測値を記録済み）。
- IMG-06のUPG-03（essenceクリスタル）は5.4%相当の画素領域（クリスタルの輪郭・主要面）が補正後平均距離17.3で`#ABFFFF`最近傍に分類されることを個別検証。同一画素群の一部（クリスタルの明るいハイライト面）はバッジ地色`#EAF6F5`と視覚的に近接するため、3クラスタ分解では稀に不安定な境界分類が生じる（検証スクリプトの再実行で別seedを使うと第3クラスタの中心がずれることを確認済みだが、実ファイルの画素分布自体は安定しており最終出荷物に影響なし）。
- 全5点でアルファ検証実施: 4隅アルファ`(0,0,0,0)`（IMG-01/02はopaque・アルファチャンネル無しで意図的に保存）、IMG-06の`opaque_white_pct`4.9962%はUPG-02価格タグの白い意匠塗り（背景ではない、4隅透過確認済み）。256px相当ニアレストネイバー縮小での目視シルエット判別を全数実施し合格。
- コスト: IMG-01 $0.10（2試行）／IMG-02 $0.10（2試行）／IMG-05 $0.06／IMG-06 $0.06／IMG-07 $0.06。合計$0.38（全件`cost_estimated:true`）。

**revise実施メモ（IMG-01/IMG-06・2026-07-22・art-director・AR-ASSET iteration1 CONCERNS対応）**:
- 対象レビュー: `state/reviews/assets-images.md` AR-ASSET iteration1（CONCERNS）。指摘1（IMG-01タイル境界の明るい格子線）・指摘2（IMG-06 UPG-03クリスタル色逸脱＋中央フレーム非個別カード構造）に対応。指摘3（IMG-07 Warbeast体積比2.22倍）は同レビューで「非blocker・次回申し送り」と明記されているため本バッチでは対応せず見送り（次回IMG-07再生成機会があれば調整）。
- **IMG-01（tile-grass.png・revision2）**: retryInstruction(a)のヒストグラムマッチング型ブレンド（帯領域の両サイドを周囲リングの平均輝度へgain正規化してからfeather blend、単純平均を廃止）をローカル実装。1回目の再生成（seed 20260740、破棄）はブレンド改善のみでは解決せず、raw画像自体の中心円形パッチ（worn patch）が2x2/4x4タイル敷き詰めでチェッカーボード状パターンを作ることが判明したため、retryInstruction(c)（プロンプト再考+複数試行）も併用し2回目（seed 20260742、採用）でプロンプトを「flatbed scanner風のtexture map swatch、シーンではない」という表現に転換し中心パッチの無い均一原画像を獲得。境界帯-内部帯輝度差は目標「±3以内」を達成（vband -1.53 / hband -0.86、旧+7.55から改善）。色補正（HSVリマップ、distance 122.7→3.18）も実施。詳細は`game/_generated/MANIFEST.jsonl`のIMG-01 revision2エントリ。
- **IMG-06（icon-upgrades.png・revision2）**: retryInstructionの両プロンプト修正案（個別カード構造の明示＋essenceクリスタルのビビッドシアン支配色指定）を反映して再生成（seed 20260741）。フレーム別透過率は中央フレーム0.00%→24.29%へ改善（IMG-05水準の個別カード構造相当。ただしカード同士は扇状にわずかに重なる階層配置であり完全独立ではない旨を開示）。クリスタル色は独立測定でHSV領域別補正（gamma value補正+彩度調整）を適用しdistance 75.2→34.07（既存許容基準40未満）まで補正、256px縮小視認でも明確にシアン発光と判別可能。副次的に発見したdark panel/light rimクラスタの逸脱（distance 51.3/39.7）も同時に#12262A/#EAF6F5へ精密補正（実質distance≈0）し開示済み。詳細は`game/_generated/MANIFEST.jsonl`のIMG-06 revision2エントリ。
- 予算: 追加コスト$0.16（IMG-01 $0.10［2試行、うち1回破棄］＋IMG-06 $0.06）を含め`game/_generated/MANIFEST.jsonl`の`cost_usd`合算は**$6.1379**（`state/budget.txt`の$20上限に対し残枠約$13.86、超過無し）。
- 次アクション: art-reviewerによるAR-ASSET iteration2判定待ち。

**revise実施メモ（IMG-06/IMG-07・2026-07-22・art-director・AR-ASSET バッチ一貫性チェック iteration1 CONCERNS対応）**:
- 対象レビュー: `state/reviews/assets-batch.md` AR-ASSET iteration1（pass 1: バッチ一貫性チェック、CONCERNS）。個別バッチ（`assets-images.md`）ではAPPROVE済みだったIMG-06・IMG-07を、既承認資産（IMG-04/IMG-08・MDL-03/04）との横断比較で新たに検出された時系列ドリフトとして再指摘。指摘3（IMG-03のhue逸脱）・指摘4（MDL-05の彩度）は同レビューが非blocker開示に指定しているため見送り。
- **IMG-06（icon-upgrades.png・revision3）**: UPG-03クリスタルのプロンプト指示を「ビビッドな高彩度シアン」から「IMG-04/IMG-08と同じ淡いペールシアン」へ180度転換（seed 20260750、1回で採用）。生成後、crystal clusterのHSVをhue181.0°/sat0.25/val0.885（IMG-04実測sat0.250・IMG-08実測sat0.230の中間）へ精密収束させる補正を実施。独立測色でrevision2のsat0.542から解消したことを確認。
- **IMG-07（icon-enemy-indicator.png・revision2）**: body(#973FA5)を80-85%以上、accent(#C71A23)を角/目/爪先のみの10-15%に限定する指示へ改訂。4試行を要した（3回破棄: (a)色比率のみ修正した結果Warbeastが誤って二足立ちに退行、(b)形状のみ修正した結果角/爪/赤アクセントが消失、(c)意匠復元を再強調した結果全身が単色赤に統合され紫が消失。採用: (d)色比率と形状指示を同時に明確化したseed 20260754）。独立測色（MDL-03/04と同一手法）でoverall body78.24%/accent21.76%（旧42.8-63.8%/29.4-44.0%から大幅改善）まで是正。MDL実測比（約90%/10%）には届いていない残差を非blocker開示として記録（詳細は`state/reviews/assets-batch.md`「対応」節および`game/_generated/MANIFEST.jsonl`のIMG-07 revision2エントリ）。
- 予算: 追加コスト$0.30（IMG-06 $0.06［1試行］＋IMG-07 $0.24［4試行、うち3回破棄］）を含め`game/_generated/MANIFEST.jsonl`の`cost_usd`合算は**$6.4379**（`state/budget.txt`の$20上限に対し残枠約$13.56、超過無し）。
- 次アクション: art-reviewerによるAR-ASSET（バッチ一貫性チェック pass 2）判定待ち。
