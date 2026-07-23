# ArcadeRelay アセット生成設定（2026-07 リサーチ・公式Doc検証済み）

> art-director / audio-designer と workflow スクリプトはこの表に従って生成する。
> preflight（/forge 冒頭）がキー・残高・プラン階層を検証し、結果を `state/asset-routing.json`（`plan_tier`/`shippable`/`notes[]` 付き — スキーマ正本は forge スキル Phase 1）に書き出す。
> **生成中のルート再判定は禁止**（ルーティング表が真実）。
> **生成レーンは API を呼び出す Bash 呼び出しに限り、冒頭で `set -a; source .env 2>/dev/null; set +a` を実行してから curl する**（サブエージェントのシェルにキーは継承されない）。検証・後処理（ffmpeg / npx / python 等）では source しない — サードパーティ子プロセスへの全キー継承を避ける（contract §10）。キー値の echo・ログ・MANIFEST への書き出しは禁止。

## ルーティング表

| 資産種別 | Primary | Fallback | キー無しローカル縮退 |
|---|---|---|---|
| 画像: スプライト/キャラ/UI | fal.ai `fal-ai/ideogram/v3/generate-transparent`（生成時ネイティブ透過・seed・style_codes・character_reference） | Ideogram V3 公式REST直（`api.ideogram.ai/v1/ideogram-v3/generate`。seed/style_codes互換） → 三次: OpenAI `gpt-image-1.5` を**pin**（`background:"transparent"`。**gpt-image-2 は透過エラーのため使用禁止**） | mflux or ComfyUI + FLUX **schnell/klein**（Apache-2.0＝商用可）+ rembg |
| 画像: 背景/タイルセット | fal.ai `fal-ai/flux-2-pro`（参照画像8枚 + hexパレット厳密指定） | 同上 | 同上 |
| ピクセルアート案件（全画像を置換） | Retro Diffusion `api.retrodiffusion.ai`（RD_FAST/RD_PLUS、RD_TILE=`tile_x/tile_y`、RD_ANIMATION=`return_spritesheet:true`、`remove_bg:true`、**`check_cost:true`で呼出し前予算ゲート**） | PixelLab hosted MCP（注意: text mode 64x64/4フレーム等の実制限） | nearest-neighbor縮小 + パレット量子化 |
| 背景除去 | fal.ai `fal-ai/birefnet/v2` | ローカル `rembg -m isnet-anime` | 同左 |
| SFX | ElevenLabs SFX v2: `POST /v1/sound-generation`（model `eleven_text_to_sound_v2`、**`duration_seconds`明示**=自動比5x安・0.5〜30s、ループ素材は `loop:true`） | —（リトライのみ） | **jsfxr**（パブリックドメイン・決定的・出荷可） |
| BGM | Eleven Music: `POST /v1/music`（model `music_v2`、`composition_plan`でセクション長指定、`force_instrumental:true`、seed。$0.15/分） | ローカル Stable Audio Open Small（Community License: 収益$1M未満商用可） | 同左（無理なら jsfxr アンビエント + must-replace 印） |

## 3D ルーティング表（engine=unity/unreal のみ。MDL/ANM 資産）

> **fallback 全段試行の義務**: Primary の API 失敗時、fallback を 1 段も試さずにローカル縮退/プレースホルダ/must-replace 化することを禁止する。ルーティング表の fallback を上から順に全段試行し、各試行の「ルート名 + HTTP ステータス（または失敗理由）」を必ず記録・報告する（全段失敗の場合のみローカル縮退可 — retro-e3 指摘7）。

2D 表の画像行は 3D エンジンでも UI・テクスチャ・コンセプト画用に併用する。3D モデル/アニメは **全行 Meshy Primary**（contract §10）— `MESHY_API_KEY` 有効時は直API を第一候補、無効/未設定時は `FAL_KEY` 経由の fal ホスト版 Meshy を第一候補に繰り上げる（Meshy の二重化）:

| 資産種別 | Primary（Meshy 直API・キー有効時） | 第二候補（Meshy 二重化: fal 経由） | Fallback（Meshy 全滅時のみ） | キー無しローカル縮退 |
|---|---|---|---|---|
| キャラクター（リグ+アニメ付き・ヒューマノイド） | Meshy 直 `POST /openapi/v1/image-to-3d`（PBR・GLB/FBX・非同期 task）→ rigging/animation API（docs.meshy.ai/en/api/rigging-and-animation。**Pro で解放されるかは未検証** — 403/権限エラー時はこの資産種別のみ第二候補へ切替え、切替を notes/未解決事項に記録） | fal.ai `fal-ai/meshy/v6/image-to-3d`（$0.80/生成。単一アニメで足りる場合は `enable_rigging`+`animation_action_id` を同一呼び出しに含めて完結可）→ 複数クリップは `fal-ai/meshy/rigging/multi-animation`（$0.20/リクエスト + $0.12/クリップ・最大10。+Z前方・300k面上限） | Tripo 直API（`TRIPO_API_KEY`。UniRig基盤・非ヒューマノイド対応） | Blender headless プロシージャル（プリミティブ合成 blockout + **Rigify**（Blender同梱・GPL、生成リグの出力は制約なし）で標準ヒューマノイドボーン）。Blender も無ければエンジン内プリミティブ合成＋コードモーション。いずれも `must_replace: true` |
| キャラクター（非ヒューマノイド/クリーチャー） | Meshy 直 image-to-3d（リグは Tripo 直の方が対応形状広い） | fal.ai Meshy 系 | Tripo 直API（Quadruped/Avian/Serpentine 等） | 同上（4足なら箱+円柱の blockout） |
| プロップ（小物） | Meshy 直 image-to-3d | fal.ai `fal-ai/meshy/v6/image-to-3d` | fal.ai `fal-ai/hunyuan3d/v2`（$0.16〜） → TRELLIS 系 → `fal-ai/hyper3d/rodin` | Blender プリミティブ合成 or エンジン内プリミティブ。`must_replace: true` |
| 環境・地形 | Meshy 直 image-to-3d（コンセプト画→image-to-3D） | fal.ai Meshy 系 | fal.ai Hunyuan3D/TRELLIS 系 → `fal-ai/hyper3d/rodin` | Blender プロシージャル地形（Displace+ノイズ）or エンジン内 Terrain。`must_replace: true` |
| スケルタルアニメ追加分（ANM） | Meshy 直 Animation API（既存リグ済みモデルへ） | fal.ai `fal-ai/meshy/rigging/multi-animation`（action_id 指定） | — | コードによるプロシージャルモーション（ボブ/回転/バウンス）。`must_replace: true` |

- **Meshy 直API の裏取り済み事実（2026-07・公式Doc）**: base URL `https://api.meshy.ai`、認証 `Authorization: Bearer $MESHY_API_KEY`、残高 `GET /openapi/v1/balance` → `{"balance": N}`（200=キー有効。レスポンスに plan/tier フィールドは無い）。POST 系は task id を返す非同期 — task の GET をポーリングして完了を待つ。**Meshy Free プランには API キー発行自体が無い**（API は Pro=$20/mo 以上のみ）ため、キー有効 ≒ Pro 以上（商用可）の間接証明になる。出典: docs.meshy.ai/en/api/{quick-start,image-to-3d,rigging-and-animation,balance,authentication,pricing} / help.meshy.ai
- **未検証事項（生成時 feature-flag 扱い・Checkpoint で開示）**: (1) 直API の rigging/animation が Pro で解放されるか（Studio 以上の可能性 → 403 時は fal 経由へ資産種別単位で自動切替＋記録）(2) 直API クレジットの USD 換算（保守見積 $0.02/credit で MANIFEST の `cost_usd` に記録し `"cost_estimated": true` を必ず付ける）(3) fal ホスト版 Meshy 出力の商用ライセンス継承（fal モデルページに Commercial use 可のバッジのみ確認 → ライセンスフラグ節で開示）
- **実勢コスト（fal 経由・モデルページ実測 2026-07）**: image-to-3d $0.80/生成（flat）、rigging/multi-animation $0.20 + $0.12/クリップ（例: 3クリップ = $0.56）。**ヒーロー1体（モデル+リグ+idle/walk/run）≈ $1.36**。Hunyuan3D v2 $0.16〜。予算見積もり（design/assets.md「集計と予算」）はこの実勢値で行う
- 出力形式: **静的 = GLB / リグ・アニメ付き = FBX**（Unity Humanoid・UE Interchange との互換性が最も安定）。取込先はエンジン別（contract §11）
- ローカル縮退（Blender）実装ノート: ボーンは Unity HumanBodyBones 互換命名（Hips/Spine/Chest/Head/LeftUpperArm…）にすると Avatar 自動マッピングが安定。Blender 4.4+ はレイヤード Action API（`action.layers[].strips[].channelbags[].fcurves`）— 旧 `Action.fcurves` は廃止済み
- **Mixamo の自動化禁止**（Adobe ToS がバックエンドアクセス・スクレイピングを明示禁止。手動DLのみ可のためハーネスでは使わない）
- オープンモデルのローカル実行（TRELLIS/Hunyuan3D の Apple Silicon 動作）は非公式フォーク依存のため**ルーティングに含めない**（確実動作しない）

## ハード禁止事項（ライセンス/品質ガード）

- **ElevenLabs Free プランでの出荷用生成禁止**（非商用ライセンス）。preflight で subscription API を叩き **Starter($6/mo)以上を検証**
- **ElevenLabs 公式MCP経由のSFX生成禁止**（5秒上限バグ級制約）。必ずREST直
- **gpt-image-2 禁止**（透過背景廃止）。OpenAIルートは gpt-image-1.5 固定
- **rembg の `bria-rmbg` モデル禁止**（CC非商用）。許可: isnet-anime / birefnet-* / u2net
- **MusicGen / AudioGen（audiocraft）出力の出荷禁止**（CC-BY-NC重み）。プレースホルダ専用、MANIFEST に `"license":"placeholder-nc","must_replace":true` を必ず記録
- **白背景PNGの出荷禁止** — スプライトは全数アルファチャンネル機械検証
- **（3D）Meshy/Tripo Free プラン出力の出荷禁止**（CC BY 4.0 = クレジット必須・Tripo Free は商用不可。Pro 以上のみ）
- **（3D）Hunyuan3D 出力は EU・英国・韓国向け出荷禁止**（Tencent Community License の Territory 除外）。100万MAU超見込みは Tencent への書面申請必須。該当資産は MANIFEST の `license` に `tencent-community` と記録し Checkpoint で開示
- **（3D）Mixamo のバックエンド自動化・API的アクセス禁止**（Adobe ToS 違反）
- **（3D）gltf-validator でエラーが出る GLB の出荷禁止**（全 GLB を機械検証）

## スタイル一貫性プロトコル

1. Checkpoint A で key image 1枚を人間承認 → `design/art-bible.json` を導出:
   ```json
   {
     "style_block": "全画像プロンプトに前置する固定スタイル記述",
     "palette": ["#RRGGBB", "..."],
     "style_codes": ["ideogramのstyle code"],
     "reference_images": ["design/refs/crop-01.png", "..."],
     "character_reference": "design/refs/hero.png",
     "resolution": {"sprite": 512, "tile": 64}
   }
   ```
2. 全画像生成は `style_block` を機械的に前置 + seed 記録。hero は `character_reference` を全ポーズで共用
3. 資産50超なら fal で FLUX LoRA を1回訓練（$2・商用権付き）し、以後 LoRA id を pin
4. 音楽はジャンル/BPM/キー固定の style block + seed。SFX は seed 無し → 共通語彙で4変種生成→ベスト選別

## 生成後パイプライン（全段ローカル）

画像: 即時DL（fal URL≈10分・Ideogram≈24hで失効）→ アルファ検証 → (必要なら)背景除去 → トリム → タイルはオフセット重ね合わせ継ぎ目検査 → `free-tex-packer-cli` で Phaser atlas JSON（phaser のみ。unity/unreal はエンジン側のテクスチャ/スプライト機構に任せる）
音声: `ffmpeg loudnorm`（-16 LUFS）+ 無音トリム → BGMは**ループ検証**（小節境界クロスフェード→2連結してシームのクリック/RMS段差スキャン。失敗は再生成）→ 出力形式はエンジン別（phaser: OGG Vorbis 128-160kbps + M4A/AAC（Safari）/ unity: OGG / unreal: WAV）
3Dモデル（MDL/ANM）: 即時DL → **スキーマ検証**（GLB: `npx @gltf-transform/cli validate <file>.glb` でエラー0確認。Khronos validator 互換。機械可読の保存は `--format md` + "No errors" のテキストマッチが現実的 — JSON 出力は無い。**FBX: Blender headless で import → GLB export → 同じ validate を通す** — 変換不能・エラーは不合格。FBX を素通りさせない）→ Blender headless でポリゴン数・ボーン数・マテリアル数・非多様体検査 → **authoring-time 寸法計測**（第一情報源。実施手順: (a) プロバイダ API レスポンスに寸法/bbox があれば MANIFEST の `bbox_authoring_m` に転記、無ければ (b) Blender headless で `obj.dimensions` を計測して `bbox_authoring_m: [x,y,z]`（m 単位）を MANIFEST に記録。**Integrate 前に必須** — FBX は leaf bone の tail が roundtrip で再現されないため、reimport 計測は構造検査＝トポロジ・ボーン名・クリップ有無専用に限定）（1 unit 基準でヒト型 1.6–2.0m 相当。glTF=m / UE=cm の換算に注意）→ ポリゴン予算チェック（hero ≤ 50k tri / prop ≤ 10k tri / 環境 ≤ 100k tri。超過は `gltfpack -si` で自動 decimate）→ エンジン取込（unity: Assets/Resources/Generated/ へコピー / unreal: Interchange Python でインポート）→ 取込後にエンジン内バウンディングボックスを `bbox_authoring_m` と突合して再検証

## Provenance（必須）

全生成を MANIFEST.jsonl（正本パスはエンジン別 — contract §6: phaser=`game/assets/MANIFEST.jsonl` / unity・unreal=`game/_generated/MANIFEST.jsonl`）に1行1資産で追記:

```json
{"file":"assets/sprites/hero.png","provider":"fal:ideogram-v3-transparent","model":"ideogram-v3","prompt":"...","seed":12345,"style_codes":["..."],"cost_usd":0.06,"plan_tier":"prepaid","sha256":"...","license":"commercial-ok","generated_at":"ISO8601"}
```

3D 資産（MDL/ANM）は追加フィールド必須:

```json
{"file":"_generated/models/model-hero.fbx","kind":"character_rigged","provider":"meshy:image-to-3d+rigging","model":"meshy-6","prompt":"...","seed":12345,"format":["glb","fbx"],"polycount":24800,"bone_count":52,"rigged":true,"rig_type":"humanoid","animations":["idle","walk","run"],"texture_resolution":2048,"pbr":true,"units":"meters","up_axis":"+Y","bbox_authoring_m":[0.9,1.8,0.5],"cost_usd":1.36,"cost_estimated":false,"plan_tier":"pro","sha256":"...","license":"commercial-ok","validator":{"gltf_validator":"pass","non_manifold_verts":0,"bind_pose_check":"pass"},"generated_at":"ISO8601"}
```

- `kind`: `character_rigged | prop | environment | animation_only`（2D 資産は省略可）
- `rig_type`: `humanoid | quadruped | other | none`
- `validator`: 機械検証結果をそのまま埋め込む（Checkpoint 提示でそのまま見せる）
- `bbox_authoring_m`: authoring-time 計測寸法 [x,y,z]（m 単位。3D 資産必須 — 生成後パイプライン参照。AR-ASSET のスケール観点はこの値を第一情報源とする）
- `plan_tier`: preflight の実測値（`state/asset-routing.json` の `checks.<provider>.plan_tier`）をそのまま転記。`cost_estimated: true` はクレジット→USD 換算が未検証見積であることを示す

- 予算: `state/budget.txt`（既定は `.env` の `ASSET_BUDGET_USD`、無ければ $20）を MANIFEST 合算で強制。超過見込みで生成停止→Checkpointで人間へ
- Steam AI 開示文は MANIFEST から自動生成
- 人間/エージェントによる修正・キュレーション（リタッチ、選別理由）も追記（著作権保護可能性の強化）

## スタイル一貫性プロトコル（3D 追記）

3D 資産の画風統一は「統一スタイルの 2D コンセプト画（key image 系列）→ image-to-3D」の二段構えを既定とする。全モデル生成は art-bible.json の style_block を反映したコンセプト画を入力にし、同一プロバイダ・同一設定を固定。キャラクターは character_reference のコンセプト画を全ポーズ・全アニメで共用する。

## Checkpointで人間に提示するライセンスフラグ

- ElevenLabs「Studio Games」条項: 商用×マルチプラットフォーム出荷は Enterprise 相談が必要
- Ideogram: アプリ内AI生成表記条項
- 米国では純AI出力の著作権が不確定 → MANIFEST の人間関与記録が防御材料
- （3D）Hunyuan3D 使用時: EU/英国/韓国の Territory 除外と MAU 制限（上記ハード禁止事項）
- （3D）Meshy/Tripo: Pro 以上プランであることの確認結果（Free 出力は CC BY 4.0 / 商用不可。Meshy 直API はキー有効=Pro以上の間接証明 — `state/asset-routing.json` の `plan_tier` 実測を提示）
- （3D）fal ホスト版 Meshy 出力のライセンス継承は未検証（fal モデルページの Commercial use バッジのみ確認）— fal 経由生成分がある場合は必ず開示
- （3D）`cost_estimated: true` の資産がある場合: クレジット→USD 換算が保守見積であること
- （unreal）UE EULA: エンジンコード/コンテンツを生成AIへの入力に使うことは禁止（自作コードは対象外）。ロイヤリティ 5%（$1M 超過分）
