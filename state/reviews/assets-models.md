# レビュー履歴: 3Dモデル資産バッチ（Phase 3・build — MDL-04 Warbeast）

対象: `game/_generated/models/model-warbeast.glb`（MDL-04）+ 付随物（`game/_generated/previews/model-warbeast-gltf-validate.md` / `model-warbeast-metrics.json` / `model-warbeast-preview.png` / `design/refs/mdl-concepts/concept-mdl-04-warbeast.png` / `game/_generated/scripts/recolor_basecolor_mdl04.py` / `swap_basecolor_mdl04.py`）
照合元: `design/art-bible.json`（style_block/palette）、`design/art-bible.md`「3D スタイル方針」節（ポリ予算6,000tri・全高1.5m/レンジ1.3–1.7m）、`design/assets.md`「3Dモデル」節 MDL-04行、`game/_generated/MANIFEST.jsonl`（MDL-04行）
（MDL-01/02/03/05 は別バッチ `state/reviews/assets-models-prototype.md` で既にAR-ASSET iteration1〜3 APPROVE済み・本レビューの対象外）

## AR-ASSET iteration 1 — APPROVE

- 日時: 2026-07-22T07:27:13Z
- 機械検査（reviewerが独立実行。producerのスクリプトは参照せず新規実行。一時ファイルは `/tmp/ar_warbeast_check/` `/tmp/ar_warbeast_check2/` に保存し対象ディレクトリは汚していない）:
  1. **sha256整合性**: `shasum -a 256 game/_generated/models/model-warbeast.glb` の実測値 `78a4678223a4191049cb649eb63ca0c6ff07f28060992a37f7f89005c3d04e7d` が MANIFEST MDL-04行の `sha256` と完全一致（取り違え・改ざん無し）。
  2. **仕様準拠（`npx @gltf-transform/cli validate`、独立実行）**: **ERROR 0件**。WARNING 1件のみ（`MESH_PRIMITIVE_GENERATED_TANGENT_SPACE`, severity1, ランタイムタンジェント生成の情報通知）——このWARNINGはMDL-01/02/03（既存承認済みバッチ）でも同一に検出される既知パターンであることを`model-bastion-cannon-gltf-validate.md`等と比較して確認済みで、MDL-04固有の新規劣化ではない。ブロッカーではない。保存済み `model-warbeast-gltf-validate.md` の内容とも一致。
  3. **予算・構造**: `npx @gltf-transform/cli inspect` で三角形数=**5,913**（MANIFESTの`polycount`と完全一致、`design/art-bible.md`「3Dスタイル方針」のMDL-04ポリ予算6,000tri以内）。マテリアル数=1（`Material_0`、baseColor/emissive/normal/metallicRoughness各テクスチャ2048x2048——この4テクスチャセットはMDL-01/02/03でも同一構成であることを確認済みでMeshy出力の既定パターン。emissiveTexture同梱は「コアクリスタルのみ追加emissiveマップを持つ」というart-bible.md記載と字面上は齟齬があるが、全既存承認済みprop/creatureモデルに共通する挙動のため新規指摘としない）。Blender headless（`bmesh`、独自実装スクリプト）で `remove_doubles(dist=1e-5)` 後に連結成分検査した結果 **islands=1**（浮遊ジオメトリ無し）。非多様体頂点は merge後2,961頂点中12件（0.41%）——MANIFEST自己申告（35/5665頂点=0.62%）とは計測基準（マージ前後）が異なるため数値は一致しないが、桁は同水準であり、gltf-validatorのエラー0・単一連結メッシュと矛盾しない。MDL-01〜05バッチのAR-ASSET iteration1〜3で既に「非ブロッカーの計測手法差異」として確立済みの扱いをそのまま適用する（開示事項として記録）。
  4. **スケール・向き**: `npx @gltf-transform/cli inspect` のscene bboxMin/bboxMax（`[-0.65017,0,-1.28737]`〜`[0.65017,1.5,1.28737]`）と、Blender headlessでの独立`obj.dimensions`計測（X=1.3004 / Y(Blender Z-up)=1.5 / Z=2.5748）の**両者が一致**し、かつMANIFESTの`bbox_authoring_m: [1.3003, 2.5747, 1.5]`と小数点4桁まで一致。全高1.5mは`design/art-bible.md`「スケール規約」のMDL-04初期値（1.5m、レンジ1.3–1.7m）と完全一致。体長2.5747mは全高比1.72倍で「Marauder比約1.7倍」の仕様（Marauder全高0.85m、1.5/0.85=1.76）とも整合。up_axis記載`+Y`はglTF既定として妥当。
  5. **リグ**: `bone_count:0` `rigged:false` `rig_type:"none"` `animations:[]` の宣言通り、Blender headlessインポートで **ARMATURE_COUNT=0 / BONE_COUNT=0 / ANIMATION_COUNT=0** を確認。`gdd.md`「モーション方式（全MDL共通）」の確定判断（動きは全てコード駆動）と整合。bind_pose_checkは`n/a`で適切。
  6. **スタイル一致（色・シルエット）**: `design/refs/mdl-concepts/concept-mdl-04-warbeast.png`（2Dコンセプト画）と`game/_generated/previews/model-warbeast-preview.png`（Blenderレンダープレビュー）を目視確認。四足接地・低く長い胴体という仕様（「large quadruped ... horizontally elongated low-slung heavy body」）どおりのシルエットで、ローポリファセット・マットPBR・柔らかいAO接地影という`style_block`にも合致。既承認済みMDL-03（Marauder）と同一のパレット系統（body系マゼンタ・ホルン/アイのレッドアクセント）で画風の一貫性を確認。**baseColorTexture独立抽出（Blender headlessで`img.save()`）→ Pillow測色**を実施したところ、body領域の実測値（149,63,164、目標`#973FA5`=(151,63,165)との距離2.2）は**MANIFESTの`measured_after_correction.body_avg_rgb`と完全一致**——同一マスク・同一手法をshipped GLBに対して再現できることを確認し、self-reported色補正の計測パイプライン自体の信頼性を裏付けた。underbelly/hornクラスタは、目標色`#3A0104`(hue≈357°)と`#C71A23`(hue≈357°)が**色相上ほぼ同一**（明度のみで区別）という設計上の特性により、テクスチャ側からhueのみで事後的に再分類する手法では両者が混ざり単純比較できない（reviewer側の測定アプローチの限界であり、producerの静的マスクによる自己検証手法自体を否定する材料ではない）。レンダー目視でも下腹部の暗色シェーディングとホルン/アイの明るい赤アクセントは視覚的に区別できており、シルエット・配色ともに仕様との齟齬は検出されなかった。
  7. **provenance/plan_tier**: `plan_tier:"pro+"`が`state/asset-routing.json`の`checks.meshy.plan_tier:"pro+"`と一致。ルート`model_prop`は`shippable:true`（フォールバック未使用でfal経由のライセンス継承問題は非該当——`degradedRoutes:[]`をチェックポイント指摘2の新規約に沿って正しく開示）。`license:"commercial-ok"`の根拠記載あり。`cost_estimated:true`（Meshy直APIクレジット→USD換算が未検証見積）。`color_correction.applied:true`（決定論的HSVリマップによる人間/エージェント関与のテクスチャ後処理、純粋なAI一発生成ではない旨をMANIFESTに明記）。`must_replace`は付与されておらず適切（Primary成功のため縮退なし）。

- 指摘要約: **無し（不合格資産0件）**
- 開示事項（gates.md AR-ASSET観点6準拠。再生成不要・人間開示のみ）:
  1. `cost_estimated:true`（Meshy直APIクレジット→USD換算がassets-config.md「未検証事項(2)」に基づく保守見積であり実測ではない）
  2. `color_correction.applied:true`（生成後にローカルの決定論的HSVクラスタ再センタリングでbaseColorTextureを補正済み。純粋なAI一発生成ではなく、生成→ローカル色補正の合成であることをCheckpointで開示する）
  3. 非多様体頂点比率の自己申告値（0.62%）とレビュアー独立測定値（0.41%、頂点マージ基準が異なる）が一致しない計測手法差異が残る（gltf-validatorはエラー0・単一連結メッシュ島のため実害なしと判断済みの非ブロッカー。MDL-01〜05バッチから継続する既知の開示パターン）
  4. emissiveTextureが同梱されている点はart-bible.md本文（「コアクリスタルのみ追加でemissiveマップを持つ」）と字面上は齟齬があるが、MDL-01/02/03（既承認済み）でも同一のMeshy既定出力パターンであり新規の逸脱ではない
- 対応: —（本iterationがAPPROVEのためreviser対応なし。次工程はIntegrate（エンジン取込・取込後bounds再検証）/ Checkpoint Cへの開示事項提示）
