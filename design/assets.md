# Assets Manifest — Crystal Vanguard

engine: `unity`（`state/engine.txt`）。raw 生成物 + provenance の正本は `game/_generated/`（contract §6/§11）、エンジン取込先は `game/Assets/Generated/`。MANIFEST 正本パスは `game/_generated/MANIFEST.jsonl`。atlas 化は engine=phaser のみのため本作では行わない（`design/art-bible.json` `resolution.tile_px: null` と一致）。

全プロンプト草案は生成時に `design/art-bible.json` の `style_block` が機械的に前置される前提で書いている（本ファイルには前置文を書き写さない）。ルート列は `state/asset-routing.json` が真実で、ここでは `primary | pixel | local` の一般区分のみを記す（具体プロバイダは asset-routing.json 参照）。

## 画像

<!-- 3D プロジェクトのため画像資産は (a) image-to-3D 入力用コンセプト画（キャラクター単体・style_block 準拠）と (b) HUD/UI 用 2D スプライトの2用途。
     ファイル名は game/_generated/images/ 配下（engine=unity。phaser の game/assets/ ではない — contract §11）。 -->

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|
| IMG-01 | concept_art | `game/_generated/images/img-01-hero-concept.png` | 1024x1024 | P-01, P-02 | ヒーロー単体のキャラクターコンセプト画。アリーナ中心に立つ縦長の二足直立シルエット、正面やや斜め（3/4）向き、武器を持たないニュートラルな構え（自動攻撃のため手動武器振りポーズは不要）。両腕を体側からやや離した A ポーズ寄りの姿勢（image-to-3D の auto-rig 入力に適した形）。装甲主色 `#3488D1`・副色（スカート/ブーツ）`#164583`・白トリム `#F2F5FA` を各パーツに配置。`character_reference`（`design/refs/crop-01-hero.png`）の意匠を踏襲する。 | primary | generated |
| IMG-02 | concept_art | `game/_generated/images/img-02-swarmer-concept.png` | 1024x1024 | P-03 | スウォーマー（通常個体）単体のキャラクターコンセプト画。低く横に広い四足シルエット（体高より体長が明確に大きい）、前傾姿勢で今にも接近しそうな構え、3/4向き。体色 `#8B12A5`、陰影・角の暗部 `#350F49`。四足の関節位置が image-to-3D の quadruped リグ入力として判別しやすい、脚が重ならないニュートラルな立ちポーズ。 | primary | generated |
| IMG-03 | sprite | `game/_generated/images/img-03-crystal-icon.png` | 512x512 | P-04 | HUD/Menu 用クリスタル通貨アイコン。幾何学的ファセット形状のクリスタル単体を正面から見たアイコン化表現、発光ハロー付き。色はシアン系 `#4FE8E2` を基調にマゼンタ `#E62284` のファセットハイライトを一部に配置。背景なし・アルファ透過必須（Menu「統計」タブのクリスタル残高表示、Menu「アップグレード」タブのコスト表示に使用）。 | primary | generated |
| IMG-04 | sprite | `game/_generated/images/img-04-hit-vfx.png` | 512x512 | P-02 | 自動攻撃ヒット時に発生させる短命 VFX 用スプライト（Unity Particle System のパーティクルテクスチャ）。放射状の閃光/衝撃波1枚絵、中心が明るく縁が透過フェードする形状。色は白系ハイライト＋敵主色 `#8B12A5` を打ち消す補色系（暖色寄りの発光、`#FF3B30` 系は被弾警告色と混同するため使用しない。中心白〜シアン `#4FE8E2` 寄りの発光を採用）。背景なし・アルファ透過必須。 | primary | generated |
| IMG-05 | sprite | `game/_generated/images/img-05-ui-frame-kit.png` | 1024x1024 | P-04 | HUD/Menu/Title/Result 共通の UI 装飾キット（1枚シート）。(a) 9-slice 対応の角丸パネル枠（中央に均一な引き伸ばし可能領域を持つ）、(b) タブ/ボタンの選択・非選択フレーム、(c) 見出しリボン＋コーナー装飾。cel-shaded・太め(2-3px)ダークネイビー輪郭で art-bible.json `style_block` に統一。主色 青 `#3488D1`/`#164583`、白トリム `#F2F5FA`、選択強調にシアン `#4FE8E2`・マゼンタ `#E62284` アクセント、パネル地は半透明ダーク `#12081F`。背景透過必須（アルファ機械検証・白背景不可）。9-slice の中央領域は無地で引き伸ばし可能なこと。Title/Menu/Result/HUD の装飾に使用（S-30）。 | primary | generated |
| IMG-06 | concept_art | `game/_generated/images/img-06-arena-backdrop.png` | 2048x2048 | P-01 | アリーナ外周の背景環境アート（Unity Skybox / 背景バックドロップ用途）。key image candidate-1（Bright Toon Neon）に寄せ、地平のダーク void `#12081F` から上方へネオン系の淡いグラデーション（紫 `#8B12A5`→シアン `#4FE8E2`→マゼンタ `#E62284`）で抜ける。低コントラスト・低ディテールで前景（マットグリーン床・青ヒーロー・紫敵・発光クリスタル）のシルエット可読性を損なわないこと（前景と競合する明部・細かな模様を置かない）。取込先 `game/Assets/Generated/`。背景として使用（S-26）。 | primary | generated |

**art-director 追記（2026-07-14・Phase 3 Visual Brushup バッチ / Checkpoint C 修正依頼）**: 人間の Checkpoint C 修正依頼（`state/checkpoint-b-feedback.md` の「Checkpoint C 修正依頼」節: UIが簡素すぎる／背景が欲しい／見た目を key image に接近させたい）を受け、UI 装飾キット **IMG-05**（P-04・全 UI シェルのパネル/フレーム/選択装飾）と背景環境アート **IMG-06**（P-01・アリーナ外周スカイボックス/バックドロップ）を新規起票した（状態 `planned` = AssetGen 対象）。いずれも `design/art-bible.json` の `style_block`/`palette` 準拠で生成し、生成後は AR-ASSET でパレット一致・アルファ縁品質（IMG-05）・前景可読性の非阻害（IMG-06）を判定する。brief.md 画像上限（12点以内）に対し IMG-01〜06 の6点で上限内・超過なし。キャラクターテクスチャ増強（修正依頼3）は新規 3D モデル/テクスチャ再生成ではなく in-engine のマテリアル/URP Outline で対応する方針（`state/stories.yaml` S-28）のため MDL 新規生成は起票しない（MDL-01 approved / MDL-02 must-replace を据え置き）。

**art-director 追記（2026-07-14・IMG-05/06 生成実施・状態 planned→generated）**: IMG-05 は route `fal:ideogram-v3-transparent`（state/asset-routing.json routes.image_sprite）で単一画像に (a)(b)(c) を同時合成する試行を4回実施したが、いずれもモデルがパネル/ボタン/リボンの複数要素・複数指定色を1枚に正しく反映できず（試行1-2はヒーロー/スウォーマー風キャラクターを誤生成、試行3-4は色指定の取り違え・要素欠落）不採用（廃棄4回分の課金を`cost_basis`に計上）。そこで要素単位（(a)パネル枠1回・(b)選択/非選択タブ2状態1回・(c)リボン+コーナー装飾1回、計3回）で個別生成し、各要素をアルファ境界でクロップの上、ローカル（PIL、追加API呼び出しなし）で1024x1024の単一シートへ均等パディングでグリッド合成した（`purpose: local-composite-of-3-api-generated-elements`）。IMG-06 は route `fal:flux-2-pro`（routes.image_background）で1回生成、2048x2048、bottom→top で void `#12081F` → purple → cyan → magenta の色相順を実測確認（色相: 292–301°→268–293°→187°、参照値 purple 289.4°/cyan 177.6°/magenta 330.0°に対し概ね一致、低コントラスト・低彩度で前景非阻害の要件を満たす）。両資産ともアルファ/仕様の機械検証を実施し `generated` へ更新、`game/_generated/MANIFEST.jsonl` に追記した（詳細は当該MANIFESTエントリの `generation_attempts` / `alpha_verification_note` 参照）。AR-ASSET 未実施のため最終合否は次工程に委ねる。

**art-director 追記（2026-07-14・AR-ASSET iteration 2 CONCERNS対応・IMG-05 element(a)再生成）**: `state/reviews/assets-images.md` AR-ASSET iteration 2 の指摘（element(a)＝9-sliceパネル枠の中央領域が指定色`#12081F`からRGB距離約145逸脱し、遠近感のあるベゼル風の非均一な陰影を持ち「中央領域は無地で引き伸ばし可能」という本行の仕様に違反）に対応した。route `fal:ideogram-v3-transparent` で element(a) のみ4回再生成（seed 130520〜130523。プロンプトに3Dパースペクティブ/ベゼル/フォトフレーム様の禁止語と、中央領域を単色ベタ塗りとする厳格な指定を追加）、最終採用案（seed130523）は完全均一（std=0）だが純黒`#000000`だったため、境界（青リング/白ハイライト/紫内リング縁取り）を一切変更しないローカル色置換（純黒画素のみ`#12081F`へ厳密変換）で仕上げた。既存シートのelement(a)セル領域のみを差し替え、他4要素（タブ選択/非選択・リボン・コーナー装飾）はピクセル単位で無変更（numpy diff差分0で確認済み）。再検証は中央領域4象限すべてでdist=0・std=[0,0,0]（要求基準dist≤30・std半減を大幅に超過達成）。追加コスト$0.24（fal 4回試行）、`game/_generated/MANIFEST.jsonl` IMG-05 revision:2 として追記済み（sha256更新: `23d462d5...`）。状態は引き続き`generated`（次工程でAR-ASSET再判定）。

<!-- audio-designer 起票。brief.md「音の方向」（BGMはドライビングなエレクトロ・BPM固定・シームレスループ60〜90秒・インスト／SFXはパンチの効いた電子音系）と
     brief.md スコープ制約「音資産数の上限: SFX 6点 / BGM 1曲（ループ）」に完全一致させて起票（超過なし）。
     ルートは state/asset-routing.json の routes.sfx = elevenlabs:sfx-v2 / routes.bgm = elevenlabs:music-v2（shippable:true、plan_tier:starter=商用可）を採用。
     全SFXは duration_seconds を明示（ElevenLabs SFX v2 は自動判定比5倍高いため必須 — assets-config.md）。
     BGMは全曲でジャンル/BPM/キーを固定する方針（本作はBGM1曲のみのため固定対象はそれ自体の内的一貫性）。
     概算コスト（着手前見積もり。実績はMANIFEST.jsonlのcost_usdで確定）: SFX 6点×4変種選別ジェネレーションを想定し約$0.03/生成×24生成≈$0.72、
     BGM 60秒×$0.15/分（ループ検証再生成1回分のバッファ込みで2回生成想定）≈$0.30。音声小計 約$1.02。
     画像+3D小計 約$2.00（本ファイル「集計と予算」節の既存値）と合わせても state/budget.txt 上限$100に対し十分な余裕があり、超過見込みなし。 -->

## SFX（brief.md 音資産数上限: 6点。本節で上限を過不足なく使い切る）

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|
| SFX-01 | sfx | `game/_generated/audio/sfx-01-attack-hit.ogg` | 0.5s | P-02 | 自動攻撃の瞬間ヒット（ANM-01再生と同期・IMG-04ヒットVFXと同期）に鳴らす、パンチの効いた短い電子インパクト音。低域の鋭いアタック+高域の短いクリックで「確実に当たった」手応えを1発で伝える。余韻を残さず即減衰。単発・ループなし。 | primary | generated |
| SFX-02 | sfx | `game/_generated/audio/sfx-02-dash.ogg` | 0.6s | P-01 | ダッシュ回避発動（DASH_DURATION 0.2s と重なる立ち上がり）に鳴らす鋭いエアリーウィッシュ（風切り）音。アタックが速く尾が短い電子系ピッチダウンスウィープで「一瞬で抜けた」感を出す。単発・ループなし。 | primary | generated |
| SFX-03 | sfx | `game/_generated/audio/sfx-03-player-hit.ogg` | 0.5s | P-01, P-03 | プレイヤー被弾（ENEMY_CONTACT_DAMAGE適用・hero マテリアルのヒットフラッシュと同期）に鳴らす低く鈍い電子インパクト＋短いディストーションノイズ。危機感を即座に伝える警告色のヒットSFX。単発・ループなし。 | primary | generated |
| SFX-04 | sfx | `game/_generated/audio/sfx-04-crystal-pickup.ogg` | 0.6s | P-04 | クリスタル自動回収（CRYSTAL_PICKUP_RADIUS到達）に鳴らす明るい上昇チャイム（ベル系電子音の2〜3音アルペジオ）。取得の心地よさ・報酬感を伝える。単発・ループなし。 | primary | generated |
| SFX-05 | sfx | `game/_generated/audio/sfx-05-wave-start.ogg` | 1.2s | P-03 | ウェーブ切替の瞬間（currentWave増加検知。HUDウェーブ数値の0.3秒パルス演出と同期 — gdd「難易度曲線」節）に鳴らす緊張感のある電子シグナル。低域パルス+短いライザーで「圧力が増した」ことを予告する。単発・ループなし。 | primary | generated |
| SFX-06 | sfx | `game/_generated/audio/sfx-06-upgrade-purchase.ogg` | 0.5s | P-04 | Menu「アップグレード」タブでの購入確定（UPG-01〜03いずれか）に鳴らす満足感のある明快な電子確定音（短い2音の上昇コンボ）。単発・ループなし。 | primary | generated |

**audio-designer 追記（本バッチの対象特定根拠・prototypeフェーズ時点）**: `state/stories.yaml` の `phase:prototype`（S-01〜S-11）を突合した結果、SFX配線コード自体（S-15 ウェーブSFX、S-17 攻撃SFX同期、S-19 全SFX配線＋BGMループ）は全て `phase:build` であり、prototype フェーズのどのstoryもSFX再生を acceptance に含めない。一方でSFXが対応する**ゲームメカニクス**は S-06（自動攻撃=SFX-01）・S-07（ダッシュ=SFX-02）・S-08（被弾/死亡=SFX-03）・S-09（クリスタル回収=SFX-04）がいずれも `phase:prototype`（P-01/P-02 コアループそのもの）である一方、SFX-05（ウェーブ開始）が紐づく S-15 と SFX-06（購入確定）が紐づく S-12 はメカニクス自体が `phase:build`。よって **SFX-01〜04 をコアループ縦串必須としてprototypeフェーズで先行生成し、SFX-05/06 は対応メカニクスの実装自体がPhase 3のため生成をPhase 3へ繰り延べていた**。

**audio-designer 追記（2026-07-13・Phase 3 build バッチ）**: SFX-05/06・BGM-01 を本バッチで生成し `game/_generated/MANIFEST.jsonl` に追記、状態を `generated` へ更新した。SFX-05/06 は4変種生成→ffmpeg astats/loudnorm実測によるベスト選別（詳細は MANIFEST の `selection_reason`）。全音声資産（SFX-01〜06・BGM-01）が `design/assets.md` 上限（SFX 6点・BGM 1曲）を過不足なく満たした。AR-ASSET 未実施（本バッチは produce のみ）のため状態は `generated` のまま。

## BGM（brief.md 音資産数上限: 1曲・ループ）

| id | 種別 | ファイル名 | サイズ | P-xx | プロンプト草案 | ルート | 状態 | ループ要件 | 長さ | BPM/キー |
|---|---|---|---|---|---|---|---|---|---|---|
| BGM-01 | bgm | `game/_generated/audio/bgm-01-main-loop.ogg` | 60s | P-01, P-02, P-03 | ドライビングなエレクトロ（4つ打ちキック主体、シンセベース+アルペジオリード、歌なし）。全シーン共通のループBGM（Title/Menu/Game/Result）としてゲーム全体の緊張と推進力を支える。composition_plan相当のセクション構成: **A（bar1-8, 0-15s）**フィルター掛けキック+パーカッションのみでビルドアップ、**B（bar9-24, 15-45s）**フルグルーヴ（ベース+リード+ハイハット全開、群れに追われる圧を維持しつつ前へ進む推進力を出す）、**C（bar25-32, 45-60s）**Aと同質感まで音数を減衰させループ起点（bar1相当のテクスチャ）へ帰還し、末尾から先頭へのシームレス接続を作る。 | primary | generated | seamless | 60s | 128 BPM / A minor |

**audio-designer 追記（2026-07-13・生成結果の実装乖離）**: composition_plan通りの「C区間で自然にAの質感へ帰還してループ」は実際の生成では再現されず（1回目試行=60s要求で末尾がRMS約-90dBまでフェードアウトする通常曲アウトロ挙動になり不採用）、2回目試行（74s要求・フェードアウトを明示的に抑制する指示へ変更）でフルエネルギーが持続する区間（t=0〜64s）を確保した上で、**ffmpegによる150msクロスフェード編集（末尾150ms×冒頭150ms、三角窓）でループ点を人工的に作成**した（生成モデル任せの自然なシームレスループではなく編集で保証）。詳細は `game/_generated/MANIFEST.jsonl` BGM-01 の `generation_attempts` / `loop_edit_method` / `loop_verification` 参照。また `force_instrumental:true` はAPI仕様上 `composition_plan` 併用時に使用不可（422エラー）だったため、`positive_global_styles`/`negative_global_styles` での instrumental強制・vocals除外に代替した（MANIFESTの `force_instrumental_deviation` に開示）。

**根拠（P-xx突合）**: SFX-01（攻撃ヒット）はP-02「照準ゼロの自動攻撃」——手動操作なしで敵を仕留め続ける手応えを音で補強する。SFX-02（ダッシュ）はP-01「紙一重回避」——躱せた瞬間の高揚を音で強化する。SFX-03（被弾）はP-01（際どさの裏返しとしての失敗フィードバック）とP-03「群れ密度の圧力」（包囲されつつある危機感の伝達）。SFX-04（クリスタル取得）とSFX-06（購入確定）はP-04「積み上がる再挑戦」——報酬取得と強化投資の心地よさを音で担保する。SFX-05（ウェーブ開始）はP-03——難度上昇の節目を明示し「逃げ場がなくなっていく」緊張の起点を告げる。BGM-01は特定の1ピラーではなくP-01/P-02/P-03の全体（回避の緊張・自動攻撃の連続性・群れの圧力）を通底で支えるドライビングな推進力として機能するため3ピラーを併記した。

**brief.md「音の方向」との突合**: 「BGMはドライビングなエレクトロ（BPM固定・シームレスループ60〜90秒・インスト）」→ BGM-01は128BPM固定・60秒（ffmpegクロスフェード編集によるシームレスループ、上記「生成結果の実装乖離」参照）・インスト（composition_planのstyle指定による代替措置、force_instrumental非使用は上記参照）で一致。「SFXはパンチの効いた明快な電子音系（攻撃ヒット・ダッシュ・被弾・クリスタル取得・ウェーブ開始・購入）」→ 列挙された6トリガーをSFX-01〜06に過不足なく1対1で対応させた（全6点2026-07-13時点で生成完了）。「ElevenLabs starterのサブスク枠内で生成し、枠不足時は縮退を報告する」→ state/asset-routing.json 検証済み（elevenlabs: tier=starter, commercial_ok=true, shippable.sfx/bgm=true）。character_count枠（40000/月）内で全生成が完了し縮退は発生しなかった（2026-07-13時点 character_count実測2866/40000）。

## 3Dモデル

<!-- engine=unity。ポリ予算・リグ方針・テクスチャ解像度は design/art-bible.md「3D スタイル方針」節（hero ≤50,000 tri / 敵 ≤20,000 tri）および
     design/art-bible.json（scale/polygon_budget_tri）と一致させている。全モデルは対応する IMG-xx コンセプト画を image-to-3D の入力に使う
     二段構え（assets-config.md「スタイル一貫性プロトコル（3D 追記）」）。テクスチャ解像度は全モデル共通 2048px albedo + metallic-roughness、
     フラット寄り・低振幅ラフネスでトゥーン質感を維持する（art-bible.md 3D スタイル方針）。取得形式はリグ付きのため FBX（+検証用に
     Blender headless で GLB 変換し `npx @gltf-transform/cli validate` を実施。原本 FBX は保持）。up軸 +Y・前方軸 +Z（Meshy既定）。 -->

| id | kind | ファイル名 | ポリ予算 | リグ | アニメ（ANM-xx） | P-xx | プロンプト草案 | ルート | 状態 |
|---|---|---|---|---|---|---|---|---|---|
| MDL-01 | character_rigged | `game/_generated/models/model-hero.fbx` | ≤50,000 tri | humanoid | ANM-01, ANM-02, ANM-03 | P-01, P-02 | 入力: IMG-01（hero concept art）。image-to-3D 後、auto-rig（humanoid スケルトン、+Z前方）。hero 身長 1.8m（調整レンジ 1.6–2.0m。art-bible.json `scale.hero_height_m`）で authoring-time 計測し `bbox_authoring_m` へ記録。同一リグへ idle/run/attack の3クリップ（ANM-01〜03）を multi-animation プリセットで一括生成。 | primary | approved（AR-ASSET iteration 3/4 APPROVE確定。`game/_generated/MANIFEST.jsonl` revision:3参照） |
| MDL-02 | character_rigged | `game/_generated/models/model-swarmer.fbx` | ≤20,000 tri | quadruped | ANM-04 | P-03 | 入力: IMG-02（swarmer concept art）。image-to-3D 後、quadruped リグ（Meshy でリグ困難な場合は資産単位で Tripo 直API へフォールバック — art-bible.md 3D スタイル方針 / assets-config.md 3D ルーティング表「非ヒューマノイド」行）。肩高 約1.0m・体長 約1.4m（体高より体長が大きい比率を維持。art-bible.json `scale.swarmer_*`）で authoring-time 計測。approach_loop の1クリップ（ANM-04）を生成。 | primary | must-replace（ジオメトリはMeshy image-to-3Dで生成・検証済みだがrig未完了。Meshy直API rigging=HTTP422（quadruped非対応）、フォールバックTripo直API=HTTP403（クレジット不足）。詳細はMANIFEST.jsonl MDL-02 の degraded_route 参照。**Checkpoint B 追認（2026-07-13）: build フェーズで静的メッシュとして取込み、接近表現はコードモーションで代替する（state/stories.yaml S-21）。ヘヴィ変種（S-14）は本メッシュのマテリアルバリアントを共用。** リグ付き置換はTripoクレジット補充時の任意フォローアップ） |
| MDL-03 | character_rigged | （未生成予定）| ≤20,000 tri（新規ジオメトリなし） | quadruped（MDL-02 のリグを共有） | ANM-04（MDL-02 と共用、新規アニメ無し） | P-03 | ヘヴィスウォーマー（任意・見た目/軽微なステータス差分のみ。gdd「敵・障害物」節）。**本行は新規3D生成を計画しない**: art-bible.md「パレット」節の決定（「新規ジオメトリを増やさずマテリアル色replaceのみで差分化する」）に従い、実装は Unity 側で MDL-02 の Prefab を複製しマテリアルを敵差し色（ヘヴィ変種）`#6B1030` のバリアントへ差し替えるのみで完結させる。brief の3Dモデル上限4点（hero1・敵1〜2・プロップ1）のうち本行は消費せず、プロップ1枠と合わせて未使用のまま温存する（gdd「クリスタル・アリーナ環境の視覚表現方針」と同じ節約方針）。実装余力が無ければ本行自体を省略しスウォーマー単一種で完結させてよい（gdd「敵種数の下限」）。 | local | planned |

**プロップ／環境モデル: 生成なし**（`design/art-bible.json` `notes.prop_budget_status` / `environment_budget_status` の通り、クリスタルはエンジン標準プリミティブ＋エミッシブマテリアル、アリーナ地面/境界は円柱・平面プリミティブ＋マテリアルで表現する gdd 決定のため、プロップ枠・環境枠は温存のまま消費しない）。

## スケルタルアニメーション

<!-- ルート primary = Meshy multi-animation プリセット（対象 MDL のリグ生成呼び出しに同梱、action_id 指定）。
     ローカル縮退時はコードによるプロシージャルモーション（must-replace 扱い）。 -->

| id | 対象 MDL | クリップ名 | 内容（例: walk / run / idle） | P-xx | ルート | 状態 |
|---|---|---|---|---|---|---|
| ANM-01 | MDL-01 | `attack` | 自動攻撃の瞬間ヒットに同期する単発（ループなし）攻撃モーション。再生長は `AUTO_ATTACK_INTERVAL` 初期値 0.6s を超えない（超える場合は発動間隔に合わせ再生速度をスケーリング — gdd「自動攻撃の当たり表現方式」）。生クリップ長 2.8s（85フレーム@30fps）。AUTO_ATTACK_INTERVAL 0.6s との再生速度スケール係数 ≈0.214（`game/_generated/MANIFEST.jsonl` ANM-01 revision:5 `duration_s`/`fps`/`playback_speed_scale_for_auto_attack_interval_0_6s` 参照）。 | P-02 | primary | approved（AR-ASSET iteration 3/4 APPROVE確定） |
| ANM-02 | MDL-01 | `idle` | 待機ループモーション。死亡演出時のフェード中待機ポーズとしても流用し、新規死亡クリップは追加しない（gdd「勝敗条件」の決定）。生クリップ長 4.0s（121フレーム@30fps）。 | P-01 | primary | approved（AR-ASSET iteration 3/4 APPROVE確定） |
| ANM-03 | MDL-01 | `run` | 移動中のループモーション（WASD/矢印入力中に再生）。生クリップ長 約0.633s（20フレーム@30fps）。 | P-01 | primary | approved（AR-ASSET iteration 3/4 APPROVE確定） |
| ANM-04 | MDL-02（MDL-03 採用時は同一クリップを共用） | `approach_loop` | プレイヤー方向への直線接近を表現するループモーション（歩行/疾走を単一クリップで表現しきる — gdd「敵接近AI」節）。 | P-03 | local | must-replace（MDL-02が未リグのためスケルタルアニメ生成不可。**Checkpoint B 追認（2026-07-13）により build フェーズで local:code-motion 代替に確定** — スウォーマーは静的メッシュ+コードモーション（前傾チルト+上下バウンス）で接近を表現する（state/stories.yaml S-21）。ルートを primary→local へ変更し AssetGen での Meshy 生成対象から外す。Tripo クレジット補充時は再リグ+ANM-04 再生成でスケルタルアニメへ置換可＝任意フォローアップ） |

**注記（AR-ASSET iteration 3/4 記録改善対応・art-director追記 2026-07-13）**: ANM-01〜03 の FBX に同梱されるメッシュ（`char1`、55,499 tri）はスケルトン付き再ダウンロード元のdecimate前コピーであり hero ポリ予算（50,000 tri）を単体で超過するが、Unity 取込は `animationType=Humanoid` の Animation-only import として扱われるため描画には使用されない（描画は MDL-01 のdecimate済みメッシュ 47,174 tri）。retarget 用の骨格参照専用であることを明記する（各クリップの `duration_s`/`frame_count`/`fps` と合わせて `game/_generated/MANIFEST.jsonl` ANM-01〜03 revision:5 参照）。

## 集計と予算

- 画像: 6点（IMG-01〜06、全件生成完了。2026-07-14時点でIMG-05/06もPhase 3 Visual Brushupで生成完了=状態 generated） / SFX: 6点（SFX-01〜06、全生成完了。2026-07-13時点） / BGM: 1点（BGM-01、生成完了。2026-07-13時点） / 3Dモデル: 2点生成（MDL-01, MDL-02。MDL-03 は新規生成なしの material variant） / アニメ: 4点（brief.md 上限: 画像12点以内・SFX6点・BGM1点・3Dモデル4点以内・hero 3クリップ+敵1〜2クリップに対し、画像6点・SFX6点・BGM1点・3Dモデル2点生成・アニメ4クリップで全て上限内、過不足なし）
- 概算コスト合計（`game/_generated/MANIFEST.jsonl` の `cost_usd` 実測合算・2026-07-14時点）: 約 $2.95（旧小計 $2.71 に AR-ASSET iteration 2 CONCERNS対応の IMG-05 element(a) 再生成バッチ $0.24 を追加。内訳: IMG-05 は初回 fal:ideogram-v3-transparent 7回試行（廃棄4回+採用3要素個別生成分）× $0.06 ≈ $0.42 に加え、element(a)再生成4回試行（廃棄3回+採用1回）× $0.06 = $0.24（revision:2）、IMG-06 は fal:flux-2-pro 1回 ≈ $0.26（megapixelベース見積もり、`cost_estimated:true`））。全件 `cost_estimated:true`（プロバイダ請求の実測確定値ではなく、character_count差分按分またはassets-config.md記載レートに基づく見積もり）。brief.md 見積もり「$6〜10」・`state/budget.txt` 上限 $100 のいずれに対しても十分な余裕があり超過なし。

## 欠落チェック

<!-- art-director による整合パス（見出し構造・必須フィールド・id重複の確認）。個別の内容欠落は勝手に埋めず、ここに列挙するのみ。 -->

必須フィールド（id / サイズ相当 / プロンプト草案相当 / ルート / P-xx参照）の充足を全エントリ・全列（ヘッダ列数とのアラインメント含む）で機械確認した結果、**欠落なし**。

- 画像（IMG-01〜06）: id・サイズ・プロンプト草案・ルート・P-xx参照とも全6件で充足（IMG-05/06 は Phase 3 Visual Brushup 追加・2026-07-14時点で状態 generated）。
- SFX（SFX-01〜06）: 同上、全6件で充足。
- BGM（BGM-01）: 同上（+ループ要件・長さ・BPM/キー）、1件で充足。
- 3Dモデル（MDL-01〜03）: id・ポリ予算（サイズ相当）・プロンプト草案・ルート・P-xx参照とも全3件で充足。MDL-03 は「ファイル名」欄が `（未生成予定）`（新規3D生成なし・Unity側マテリアルバリアントで対応する旨をプロンプト草案欄に明記済み）だが、これは意図された設計判断であり欠落ではない。
- スケルタルアニメーション（ANM-01〜04）: id・クリップ名・P-xx参照・ルートとも全4件で充足。**注記**: 本テーブルはテンプレート（`.claude/docs/templates/assets.md`）上「サイズ」「プロンプト草案」列自体を持たない仕様（代わりに「対象MDL」「内容」列を持つ）ため、この2フィールドは非該当（N/A）であり欠落ではない。

id 重複・振り直しの確認: IMG-01〜06 / SFX-01〜06 / BGM-01 / MDL-01〜03 / ANM-01〜04 の全19件で連番の欠番・重複ともになし。
