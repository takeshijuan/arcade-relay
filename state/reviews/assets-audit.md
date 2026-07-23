# レビュー履歴: 資産監査（qa-lead）

対象: `game/_generated/MANIFEST.jsonl`（37行・うち資産生成行29行/エンジン取込検証行8行）、`state/budget.txt`、`state/asset-routing.json`、`.claude/docs/assets-config.md`「ハード禁止事項」「Checkpointで人間に提示するライセンスフラグ」節。
engine = `unity`（`state/engine.txt`）。

## 資産監査 iteration 1 — 完了

- 日時: 2026-07-22T20:40:35Z

### 1) 予算突合

MANIFEST.jsonl の `cost_usd` フィールドを持つ全29行（資産生成行のみ。エンジン取込検証行8行は `cost_usd` 無しのため対象外）を合算。

| カテゴリ | 行数 | 小計(USD) | 内訳 |
|---|---|---|---|
| 画像（IMG-01〜08。revision分も含む全生成行） | 14 | 1.26 | IMG-03/04=各0.06、IMG-08(v1)=0.06+rev2=0.18+rev3=0.06、IMG-01(v1)=0.1+rev2=0.1、IMG-02=0.1、IMG-05=0.06、IMG-06(v1)=0.06+rev2=0.06+rev3=0.06、IMG-07(v1)=0.06+rev2=0.24 |
| SFX（SFX-01〜06） | 6 | 0.0419 | 0.0033×3 + 0.004 + 0.008 + 0.02 |
| BGM（BGM-01。3試行中1採用分のみ計上） | 1 | 0.096 | cost_usd_all_attempts=0.288（3試行分）はMANIFEST内に別記あるが `cost_usd` フィールド自体は採用分のみ |
| 3Dモデル（MDL-01〜05。v1+AR-ASSET iteration1 revise分含む全生成行） | 8 | 5.04 | MDL-01=0.6+0.66、MDL-02=0.6+0.66、MDL-03=0.6+0.66、MDL-04=0.66、MDL-05=0.6 |
| **合計** | **29** | **6.4379** | |

- `totalAssetCost` = **$6.4379**（`cost_usd` の単純合算。破棄試行のうちMANIFEST行の `cost_usd` に計上されなかった探索コスト——例: MDL-01/02の破棄コンセプト画3枚・2枚分、BGM-01の却下2試行分$0.192——は各行の `notes`/`cost_estimate_basis` に開示済みだが `cost_usd` 列自体には含まれていないため、本合算には含めていない。含めた場合の参考値は概算 +$0.6程度）
- `budgetUsd`（`state/budget.txt`）= **$20**
- `overBudget` = **false**（$6.4379 ≪ $20、余裕あり）

### 2) ライセンスフラグ

`.claude/docs/assets-config.md`「Checkpointで人間に提示するライセンスフラグ」節の該当項目:

1. **ElevenLabs「Studio Games」条項** — 商用×マルチプラットフォーム出荷はEnterprise相談が必要。SFX-01〜06・BGM-01が `elevenlabs:sfx-v2` / `elevenlabs:music-v2` を使用（plan_tier=starter、`state/asset-routing.json` で commercial_ok=true 実測済みだが、Studio Games条項自体はStarterプランの範囲外の追加確認事項としてMANIFEST内 `license_note` に既に開示されている）。
2. **Ideogram: アプリ内AI生成表記条項** — `fal:ideogram-v3-transparent` を IMG-03/04/05/06/07/08（アイコン類）および MDL-01/02/03/04 の3Dコンセプト画生成に使用。
3. **米国では純AI出力の著作権が不確定 → MANIFEST の人間関与記録が防御材料** — 該当。MDL-01/02/03/04・IMG-01/05/06/07/08 に決定論的HSVテクスチャ/画像リタッチ（`color_correction.applied: true`）が多数記録され、人間/エージェント関与の防御材料として機能する状態を確認。
4. **（3D）Meshy: Pro以上プランであることの確認結果** — MDL-01〜05 全8生成行が `plan_tier: "pro+"` で一致。`state/asset-routing.json checks.meshy`（balance 200・キー有効=Free不可の間接証明）と整合。Free出力（CC BY 4.0）ではないことを確認。
5. **（3D）`cost_estimated: true` の資産がある場合: クレジット→USD 換算が保守見積であること** — 該当。MANIFEST全29行が `cost_estimated: true`。3Dモデル分はMeshy直APIクレジット消費量×保守見積$0.02/credit（未検証換算、assets-config.md 3D節 未検証事項(2)）、音声分はElevenLabsのcharacter-cost/公式レート換算。

非該当（開示不要と判断）: fal ホスト版 Meshy 出力（MANIFESTのproviderは全行 `meshy:direct-image-to-3d` で `fal:meshy-*` は不使用のため fal経由ライセンス継承未検証フラグは非該当）／Hunyuan3D使用（不使用）／unreal EULA（engine=unityのため非該当）。

### 3) 白背景PNG混入検査（全数・抜き取りなし）

MANIFEST記載の画像資産（IMG-01〜08、revisionにより現物ファイルは上書き済みのため disk上の最終版8ファイル全て）をPillowで全ピクセル走査（サンプリングなし）:

| ファイル | asset_id | サイズ | alpha有無 | opaque_pct | 不透明白率(RGB全ch≥250) | 判定 |
|---|---|---|---|---|---|---|
| icon-tower-select.png | IMG-03 | 874x741 | あり(0-255) | 37.43% | 0.0000% | PASS |
| icon-essence.png | IMG-04 | 842x842 | あり(0-255) | 30.74% | 0.0000% | PASS |
| icon-core-hp.png | IMG-08(rev3) | 878x879 | あり(0-255) | 43.99% | 0.0000% | PASS |
| icon-achievements.png | IMG-05 | 965x287 | あり(0-255) | 78.71% | 0.0000% | PASS |
| icon-upgrades.png | IMG-06(rev3) | 911x494 | あり(0-255) | 86.95% | 0.0784% | PASS（意匠上の白い割引タグ塗り。MANIFEST既disclosure「白背景ではない・4隅alpha=0」と整合、5%未満で白背景混入ではない） |
| icon-enemy-indicator.png | IMG-07(rev2) | 885x667 | あり(0-255) | 30.37% | 0.0000% | PASS |
| tile-grass.png | IMG-01(rev2) | 512x512 | なし(全画素alpha=255) | 100% | 0.0000%（平均色は緑系 #9AC33B相当、白ではない） | PASS（意匠上フルフレーム不透明のタイル地面テクスチャ。スプライトではないためalpha必須要件は非該当。白背景でもない） |
| tile-dirt-path.png | IMG-02 | 512x512 | なし(全画素alpha=255) | 100% | 0.0000%（平均色は茶系 #A26836相当、白ではない） | PASS（同上） |

- 違反0件。`mustReplaceAssets` 該当なし。
- `license` フィールドは全29行が `commercial-ok`、`must_replace` フィールド保持行は0件、`placeholder-nc` も0件（`grep` 全数確認済み）。

### 3b) 3D資産（MDL）のMANIFEST必須フィールド突合

3D資産（MDL-01〜05。ANM資産は本ゲームに存在せず該当なし — 全MDLとも `rigged: false` / `animations: []`）の生成行（8行。v1+revision含む）を対象に `plan_tier` / `bbox_authoring_m` / `validator` / `license` の記録有無、`shippable:false` ルート由来、`cost_estimated:true` を確認:

- 4必須フィールドの記録漏れ: **0件**（8生成行すべてに `plan_tier`（全て`pro+`）・`bbox_authoring_m`・`validator`（`gltf_validator: pass`）・`license`（`commercial-ok`）が記録済み）
- `shippable:false` ルート由来: **0件**（使用ルートは全て `meshy:direct-image-to-3d`＝`model_prop`。`state/asset-routing.json` の `shippable.model_prop: true` と整合）
- `cost_estimated:true`: **該当5資産（全MDL）**。個別開示は下表の通り。

`provenanceGaps`（構造化返却）には、必須フィールド欠落・shippable:false由来は無いため、`cost_estimated:true` の開示のみを列挙する:

| asset_id | file | cost_estimated | 備考 |
|---|---|---|---|
| MDL-01 | model-bastion-cannon.glb | true | Meshy直APIクレジット30 × 保守見積$0.02/credit（未検証換算）。plan_tier=pro+, bbox_authoring_m=[1.4002,1.3999,3.5], validator.gltf_validator=pass, license=commercial-ok は全て記録済み |
| MDL-02 | model-arc-emitter.glb | true | 同上（クレジット30） |
| MDL-03 | model-marauder.glb | true | 同上（クレジット30） |
| MDL-04 | model-warbeast.glb | true | 同上（クレジット30） |
| MDL-05 | model-core-crystal.glb | true | 同上（クレジット30） |

### 総括

- 予算超過なし（$6.44 / $20、約68%の余裕）。
- ライセンス面のブロッカーなし。全資産 `license: commercial-ok`、`must_replace` 残存資産0件。
- 白背景スプライト混入0件（全数機械検査）。
- 3DモデルのMANIFEST必須フィールド欠落0件・`shippable:false` ルート使用0件。唯一の開示事項は全3Dモデル共通の `cost_estimated:true`（Meshy直APIクレジット→USD換算が保守見積であること）で、Checkpointでの開示対象として記録。
- 対応: 本監査はqa-leadによる情報提供タスクであり、追加アクション不要（producer側への差し戻し事項なし）。Checkpoint提示時にライセンスフラグ5件・`cost_estimated:true`（全3Dモデル）を開示すること。
