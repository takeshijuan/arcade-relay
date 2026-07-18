---
name: forge
description: ArcadeRelay マスター入口。preflight → ブレスト → concept / prototype / build の3自律フェーズを順に実行し、1プロンプトで遊べるゲーム完成品（phaser 2D / unity 3D / unreal 3D — state/engine.txt）まで到達する。state/stage.txt を読んで途中から冪等に再開できる。
argument-hint: "[ゲームの初期アイデア（任意。省略時はブレストでゼロから発想）]"
allowed-tools: Read, Glob, Grep, Write, Edit, Bash, Task, Workflow, AskUserQuestion, SendUserFile, PushNotification, Skill
---

# /forge — マスター入口（1プロンプトで完成品まで）

命名・ID・パスは `.claude/docs/contract.md` が単一情報源。ここに書かれていない名前を発明しない。
状態はファイルが真実（`state/`）。各Phase完了時に `state/active.md`（現在地/次アクション/未解決事項）を更新する。

## Phase 0: 前提確認・再開位置決定（冪等）

1. `state/stage.txt` を読む（無ければ「未着手」）。`state/active.md` があれば読み、前回の未解決事項を把握する。
2. 下表に従い再開位置を決める。stage値は完了済みフェーズを示す（contract.md §1）:

| state/stage.txt | 意味 | 再開位置 |
|---|---|---|
| （無し/空） | 未着手 | Phase 1 から |
| `brief` | ブレスト完了 | Phase 1（`state/asset-routing.json` が無い場合のみ）→ Phase 3 |
| `concept` | Checkpoint A 承認済み | Phase 4 |
| `prototype` | Checkpoint B 通過 | Phase 5 |
| `build` | Checkpoint C 到達（受領未了の可能性） | Phase 5（`/forge-build` 再実行。forge-build の Phase 0 が build/done を検知し、受領確認から再開する） |
| `done` | 受け渡し完了 | Phase 6 の最終報告を再提示して終了 |

3. 矛盾検出: stage値に対応する成果物（`.claude/docs/pipeline.yaml` の `artifacts.required`）が欠落していたら、その成果物を生むフェーズまで巻き戻して再実行する（例: stage=`concept` なのに `design/gdd.md` が無い → Phase 3 から）。stage.txt は書き換えず、フェーズ完了時に正しい値で上書きされるに任せる。

## Phase 1: preflight（キー検証・ルーティング決定・状態初期化）

`state/asset-routing.json` が既に存在すれば**このPhaseをスキップ**する（生成中のルート再判定禁止 — contract.md §10）。**ただし既存ファイルに `shippable` キーが無い（旧スキーマ）場合はスキップせず再生成する**（旧形式のまま生成レーンが参照すると全ルートが事実上出荷可扱いになるため）。

1. `.env` を読む（無ければ `.env.example` をコピーする案内を出す）。対象キー: `FAL_KEY` `ELEVENLABS_API_KEY` `RETRO_DIFFUSION_API_KEY` `MESHY_API_KEY`（**3D 案件では準必須** — Meshy 直API が 3D Primary。未設定でも停止しないが、警告を出し fal 経由 Meshy に第一候補を繰り下げた旨を notes に記録する — contract §10）（任意: `IDEOGRAM_API_KEY` `OPENAI_API_KEY` `TRIPO_API_KEY`）。予算: `ASSET_BUDGET_USD` があれば `state/budget.txt` の初期値に使う（手順6）。
2. 存在するキーごとに残高/認証を ping する:

```bash
set -a; source .env 2>/dev/null; set +a

# fal — GET認証確認（存在しないrequest idへの照会で認証層のみ検証。401/403=キー無効、404等=認証OK）
curl -s -o /dev/null -w "%{http_code}" -H "Authorization: Key $FAL_KEY" \
  "https://queue.fal.run/fal-ai/flux-2-pro/requests/00000000-0000-0000-0000-000000000000/status"

# Retro Diffusion — クレジット残高（{"credits": N}。N=0 なら警告）
curl -s -H "X-RD-Token: $RETRO_DIFFUSION_API_KEY" \
  "https://api.retrodiffusion.ai/v1/inferences/credits"

# ElevenLabs — プラン検証（.tier を確認。"free" は商用ライセンス無し）
curl -s -H "xi-api-key: $ELEVENLABS_API_KEY" \
  "https://api.elevenlabs.io/v1/user/subscription" | jq '{tier, character_count, character_limit}'

# Ideogram（任意キー。設定されている場合のみ）— 認証層のみ検証（401/403=キー無効、それ以外=認証OK）
[ -n "$IDEOGRAM_API_KEY" ] && curl -s -o /dev/null -w "%{http_code}" \
  -H "Api-Key: $IDEOGRAM_API_KEY" "https://api.ideogram.ai/v1/ideogram-v3/generate"

# OpenAI（任意キー。設定されている場合のみ）— 認証確認（200=有効、401=キー無効）
[ -n "$OPENAI_API_KEY" ] && curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $OPENAI_API_KEY" "https://api.openai.com/v1/models"

# Meshy 直API（3D 案件で準必須）— 残高取得で認証検証（docs.meshy.ai/en/api/balance）
# **200 のみ有効**（plan_tier="pro+" — Free にキー発行なしの間接証明。balance レスポンスに tier フィールドは無い）。
# 401/403=キー無効。**それ以外（5xx/429/timeout=000）=検証不能** — いずれの非200も routes.model_*/anim を
# fal:meshy-* に繰り下げ、plan_tier="unknown" と理由を notes に記録する（未検証キーに pro+ を付けない）
[ -n "$MESHY_API_KEY" ] && curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $MESHY_API_KEY" "https://api.meshy.ai/openapi/v1/balance"

# Tripo 直API（任意キー）— 残高エンドポイントで認証検証（エンドポイント形式は未検証 — 200 以外は
# 「検証不能」として notes に記録し、fallbacks からは除外しない。401/403 のみ無効としてルート除外）
[ -n "$TRIPO_API_KEY" ] && curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $TRIPO_API_KEY" "https://api.tripo3d.ai/v2/openapi/user/balance"
```

3. **プラン階層判定**: ElevenLabs の `tier` が `free` の場合は**商用不可の警告**を必ず出し、当該ルートを `shippable: false` にする（assets-config.md ハード禁止事項: Free プランでの出荷用生成禁止。Starter $6/mo 以上が必須）。Meshy は balance 200 で `plan_tier: "pro+"`（Free にキー発行なし = 間接証明。assets-config.md 3D 節）。実測できないプロバイダは `plan_tier: "unknown"` とし notes に「検証不能」を記録する（Checkpoint で開示）。
4. ルーティング決定を `state/asset-routing.json` に書き出す。ルートは `.claude/docs/assets-config.md` のルーティング表（2D表＋3D表）に従い、キーの有無・検証結果で Primary → 第二候補 → Fallback → ローカル縮退 の順に決める。**3D ルート（model_* / anim）は engine が未確定でも常に書き出す**（brief 確定前に preflight が走るため。engine=phaser なら単に使われない）。**3D の Primary は Meshy**: `MESHY_API_KEY` 有効なら `meshy:direct`、無効/未設定なら `fal:meshy-*` を第一候補に繰り上げ、その旨（と 3D 案件での準必須警告）を notes に記録する（contract §10）。任意キー（IDEOGRAM / OPENAI / TRIPO）は認証 ping が無効（401/403）だった場合、`fallbacks` から該当ルートを除外し `notes` に理由を記録する（Tripo の ping が 401/403 以外の不明応答なら除外せず「検証不能」と記録）:

```json
{
  "generated_at": "<ISO8601>",
  "checks": {
    "fal":             {"key": true,  "auth": "ok",  "plan_tier": "prepaid"},
    "elevenlabs":      {"key": true,  "tier": "starter", "plan_tier": "starter", "commercial_ok": true},
    "retro_diffusion": {"key": false, "credits": null, "plan_tier": "unknown"},
    "ideogram":        {"key": false, "plan_tier": "unknown"},
    "openai":          {"key": false, "plan_tier": "unknown"},
    "meshy":           {"key": true,  "auth": "ok",  "plan_tier": "pro+", "note": "balance 200 = キー有効 ≒ Pro 以上（Free にキー発行なし）"},
    "tripo":           {"key": false, "plan_tier": "unknown"}
  },
  "routes": {
    "image_sprite":     "fal:ideogram-v3-transparent",
    "image_background": "fal:flux-2-pro",
    "pixel_art":        "local:nearest-neighbor",
    "bg_removal":       "fal:birefnet-v2",
    "sfx":              "elevenlabs:sfx-v2",
    "bgm":              "elevenlabs:music-v2",
    "model_character":  "meshy:direct-image-to-3d+rigging",
    "model_prop":       "meshy:direct-image-to-3d",
    "model_environment":"meshy:direct-image-to-3d",
    "anim":             "meshy:direct-animation"
  },
  "shippable": {
    "image_sprite": true, "image_background": true, "pixel_art": false, "bg_removal": true,
    "sfx": true, "bgm": true,
    "model_character": true, "model_prop": true, "model_environment": true, "anim": true
  },
  "fallbacks": {
    "image_sprite":    ["ideogram:direct", "openai:gpt-image-1.5"],
    "sfx":             ["local:jsfxr"],
    "bgm":             ["local:stable-audio-open-small"],
    "model_character": ["fal:meshy-v6-image-to-3d+rigging", "tripo:direct", "local:blender-procedural-rigify"],
    "model_prop":      ["fal:meshy-v6-image-to-3d", "fal:hunyuan3d-v2", "fal:trellis", "fal:hyper3d-rodin", "local:blender-procedural"],
    "model_environment": ["fal:meshy-v6-image-to-3d", "fal:hunyuan3d-v2", "fal:trellis", "fal:hyper3d-rodin", "local:blender-procedural"],
    "anim":            ["fal:meshy-rigging-multi-animation", "local:code-motion"]
  },
  "degraded": false,
  "notes": ["RETRO_DIFFUSION_API_KEY 無し: ピクセルアート案件はローカル縮退（nearest-neighbor縮小+パレット量子化）",
            "meshy 直APIの rigging/animation エンドポイントの必要プラン階層は未検証（403 時は fal 経由へ資産種別単位で切替え、未解決事項に記録 — assets-config.md 3D 節）"]
}
```

- `shippable` はルート別の出荷可否（ElevenLabs Free / ローカル縮退（jsfxr 除く）/ 非商用ライセンス経路は `false`）。**`routes` の全キーを `shippable` にも必ず含める**（引いて undefined になるキーを作らない。上例の `pixel_art: false` はローカル縮退時の値 — Retro Diffusion 有効なら `true`）。**生成レーンが `shippable: false` のルートで生成した資産は必ず未解決事項に積まれ Checkpoint で人間に提示される**（contract §10）。
- `MESHY_API_KEY` 無効/未設定の場合: `routes.model_*` / `routes.anim` は `fal:meshy-*` に繰り下げ、fallbacks の先頭から fal:meshy を除いて詰める。FAL_KEY も無い場合、3D ルートは `local:blender-procedural-rigify`（Blender 実在確認: `which blender` または `/Applications/Blender.app`。無ければ `local:engine-primitives`）に縮退し、`shippable: false`・生成物は全て `must_replace: true` になる旨を notes に記録する。

5. **キー欠落/無効/Free 時**（`FAL_KEY` 系統が全滅、または `ELEVENLABS_API_KEY` 無し/Free の場合）、AskUserQuestion で選ばせる:
   - 「キー設定して中断」— `.env.example` から該当キーの取得URL・手順（fal: https://fal.ai/dashboard/keys / ElevenLabs: Starter以上のAPI Key）を提示し、設定後に `/forge` を再実行するよう案内して終了。
   - 「ローカル縮退モードで続行」— 該当 routes を縮退列（画像: mflux/ComfyUI FLUX schnell + rembg、SFX: jsfxr、BGM: Stable Audio Open Small）に設定し、`"degraded": true` と品質影響を `notes` に記録して続行。
   - `RETRO_DIFFUSION_API_KEY` のみの欠落は中断しない（notes に記録し、ピクセルアート案件になった場合のみ縮退）。
6. 状態初期化（既存ファイルは上書きしない＝冪等）:

```bash
mkdir -p state
[ -s state/budget.txt ]      || echo "${ASSET_BUDGET_USD:-20}" > state/budget.txt
[ -s state/review-mode.txt ] || echo "lean" > state/review-mode.txt
```

## Phase 2: ブレスト（唯一の対話フェーズ）

1. Skill ツールで `forge-brainstorm` を起動する。`$ARGUMENTS`（ユーザーの初期アイデア）があれば args としてそのまま渡す。
2. 完了検証: `design/brief.md` が存在し、`state/stage.txt` が `brief`、`state/engine.txt` が contract §11 の3値のいずれかになっていること。なっていなければ成果物存在を確認の上 `brief` を書き込み、engine.txt が無ければ brief の実行環境セクションから復元する（自己修復）。**brief からも復元できない場合は phaser に黙って倒さず**、AskUserQuestion でエンジンを確認してから書き込む（エンジンは以降変更禁止の最重要分岐 — contract §11）。

## Phase 2.5: エンジン preflight（engine=unity/unreal のみ。冪等）

`state/engine.txt` が `phaser`（または無い）ならスキップ。`state/engine-info.json` が既に存在し binary が実在するならスキップ。

1. **unity**: Unity Hub CLI でインストール済みエディタを解決する:

```bash
"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless editors --installed
```

   `6000.` 系の最新（バージョン降順の先頭）を選び、`state/engine-info.json` に contract §11 のスキーマで書き出す（engine / version / binary / validated_at）。**6000. 系が1本も無い場合は、他バージョン（2022 系等）があっても「無い」扱い**（非対応エディタを黙って選ばない）。Hub 自体が無い（コマンドが実行できない）場合も含め、無い場合は AskUserQuestion:
   - 「Unity Hub でエディタをインストールして再実行」— Hub CLI のインストールコマンド例（`-- --headless install --version <6000.x LTS>`。Hub 不在なら `brew install --cask unity-hub` から）を提示して停止
   - 「phaser に切り替える」— `state/engine.txt` を `phaser` に書き換え、brief の実行環境セクションも更新して続行
2. **unreal**: `ls "/Users/Shared/Epic Games/UE_"*/Engine/Build/BatchFiles/RunUAT.sh` で実在確認し、最新バージョンを `state/engine-info.json` に書き出す（`binary` = RunUAT.sh のフルパス、`ue_root` = `/Users/Shared/Epic Games/UE_5.x` のエンジンルート）。**あわせて `df -g /` でディスク空きを検査し、20GB 未満なら cook/パッケージ失敗リスクを警告**（tech-stack-unreal.md「エンジン導入」）。エンジンが無い場合は AskUserQuestion:
   - 「エンジンを導入して再実行」— 導入手順（tech-stack-unreal.md「エンジン導入」: dev.epicgames.com/portal へのログイン→ .pkg DL → `sudo installer -pkg ... -target /`。**ブラウザログインが1回必要**・ディスク空きは最低 100GB 推奨）を提示して停止
   - 「unity / phaser に切り替える」— engine.txt と brief を更新して続行
3. 書き出し後、ビルド系コマンドは以後 `state/engine-info.json` の binary を使う（実行中の再解決禁止 — contract §11）。

## Phase 3〜5 共通: 自律フェーズの進行規則

- 各スキルは自身の Checkpoint で停止する（review-mode `full`/`lean` 時 — contract.md §9）。**承認後に制御が戻るので、`state/stage.txt` が前進したことを確認してから次のPhaseへ進む**。前進していない場合（REJECT・中断）は状況を `state/active.md` に記録し、PushNotification で通知して停止する（次回 `/forge` で再開）。
- `state/review-mode.txt` が `solo` の場合: **Checkpointで停止しない**。フェーズ完了通知は各サブスキル（forge-concept / forge-prototype / forge-build が Checkpoint 提示時に送る PushNotification）に一本化し、**/forge 自身はフェーズ完了通知を送らない**（サブスキルが通知を送れずに失敗・エラー停止した場合のみ /forge が通知する）。連続実行する。
- サブスキル/workflow がエラーで戻った場合も同様に active.md 記録 + PushNotification + 停止。

## Phase 3: 企画・設計（Checkpoint A）

Skill ツールで `forge-concept` を起動する。完了条件: `design/concept.md` `design/gdd.md` `design/art-bible.md` `design/art-bible.json` `design/assets.md` が揃い、stage が `concept` に前進。

## Phase 4: プロトタイプ（Checkpoint B）

Skill ツールで `forge-prototype` を起動する。完了条件: `docs/architecture.md` `docs/conventions.md` `state/stories.yaml` `qa/report.md` とエンジンのプロジェクトマーカー（contract §11: phaser=`game/package.json` / unity=`game/ProjectSettings/ProjectVersion.txt` / unreal=`game/ForgeGame.uproject`）が揃い、stage が `prototype` に前進（`state/checkpoint-b-feedback.md` が残る）。

## Phase 5: 本実装・仕上げ（Checkpoint C）

Skill ツールで `forge-build` を起動する。完了条件: フルQA合格（`qa/report.md` 更新・エンジン別正本パスの MANIFEST.jsonl 存在 — contract §6）で stage が `build` **または `done`** に前進（forge-build は受領確認・完了処理まで進むと `done` を書く）。stage が `build` のまま forge-build が停止案内を出した場合（修正依頼・中断）は Phase 6 へ進まず、共通規則に従い active.md 記録＋停止する（次回 `/forge` は Phase 5 から再開）。

## Phase 6: 完了・最終報告

前提: stage が `done`（forge-build が Checkpoint C 提示・受領確認・`done` 書き込みまで完了済み）。forge-build の Checkpoint C と重複する提示 — `qa/report.md` の SendUserFile・PushNotification・`state/stage.txt` への書き込み — は**再実行しない**。ここでは最終要約の再掲と補足のみを行う。

1. コスト集計と予算照合（MANIFEST パスはエンジン別 — contract §6。以下 `$MANIFEST` = phaser: `game/assets/MANIFEST.jsonl` / unity・unreal: `game/_generated/MANIFEST.jsonl`）:

```bash
jq -s 'map(.cost_usd // 0) | add' "$MANIFEST"   # 実績合計USD
cat state/budget.txt                             # 予算上限
```

2. ライセンスフラグ抽出:

```bash
jq -c 'select(.license != "commercial-ok" or .must_replace == true)' "$MANIFEST"
```

   これに assets-config.md の固定フラグ（ElevenLabs「Studio Games」条項 / Ideogram アプリ内AI生成表記条項 / 米国での純AI出力著作権の不確定性→MANIFESTの人間関与記録が防御材料。3D 使用時: Hunyuan3D の Territory 除外 / Meshy・Tripo のプラン条件 / unreal は UE EULA の生成AI入力禁止条項）を加えて列挙する。
3. 最終報告を組み立てて提示する:
   - **遊び方**（エンジン別 — 各 tech-stack 文書の「検証コマンド」dev/preview 行）: phaser: `cd game && npm install && npm run dev` / unity: `open game/Build/ForgeGame.app`（またはエディタで game/ を開く）/ unreal: `open game/Build/Mac/ForgeGame.app`。操作方法・勝利/敗北条件は `design/gdd.md` から要約。
   - **QA結果**: `qa/report.md` の要約（重大バグ0・acceptance通過状況）。
   - **コスト**: MANIFEST 合計 vs `state/budget.txt`。
   - **ライセンスフラグ**: 手順2の列挙 + `must_replace` 資産があれば差し替え指示。
   - **未解決事項**: `state/reviews/` で MAX_ITER 到達のまま非APPROVEの指摘一覧。
4. `state/active.md` を「受け渡し完了」で更新する（forge-build が更新済みなら差分のみ追記。`state/stage.txt` は forge-build が書き込み済みのため触れない）。
