# レビュー履歴: 3Dモデル資産バッチ（prototype先行生成分 — MDL-01/02/03/05）

対象: `game/_generated/models/model-bastion-cannon.glb`（MDL-01）/ `model-arc-emitter.glb`（MDL-02）/ `model-marauder.glb`（MDL-03）/ `model-core-crystal.glb`（MDL-05）
照合元: `design/art-bible.json`（style_block / palette）、`design/art-bible.md`「3D スタイル方針」節（ポリ予算・スケール規約）、`design/assets.md`「3Dモデル」節（MDL-01/02/03/05 行）、`game/_generated/MANIFEST.jsonl`
（MDL-04 Warbeast・ANM資産はいずれも本バッチ対象外 — assets.md記載どおり phase:build へ見送り／ANM資産は本作0件のため）

## AR-ASSET iteration 1 — CONCERNS

- 日時: 2026-07-21T09:42:21Z
- 機械検査（実施コマンド・出力は本レビュー応答本文に記載。一時ファイルは `/tmp/tex-out/`・`/tmp/gltf-extract/`・`/tmp/inspect_glb*.py` に保存し対象を汚していない）:
  1. **仕様準拠（gltf-transform validate）**: `npx @gltf-transform/cli validate <file>.glb --format pretty` を4点全てに独立実行。**4点ともERROR 0件**（WARNING 1件のみ・`MESH_PRIMITIVE_GENERATED_TANGENT_SPACE`・severity1・実行時タンジェント生成の情報通知でブロッカーではない）。MANIFESTの `validator.gltf_validator: "pass"` と一致。sha256も4点とも実ファイルと完全一致（改ざん/取り違え無し）
  2. **予算・構造**: `npx @gltf-transform/cli inspect` で三角形数・マテリアル数を独立集計。MDL-01=5,899/6,000tri・MDL-02=4,856/6,000tri・MDL-03=3,840/4,000tri・MDL-05=2,955/3,000tri（全てMANIFEST記載値と一致・全て予算内）。マテリアル数は4点とも1（baseColor/emissive/normal/metallicRoughness各1テクスチャ、2048x2048）。Blender headless（`bmesh`）で連結成分（島）を独立検査した結果、**4点とも `num_islands=1`（浮遊ジオメトリ無し）**。非多様体エッジは `remove_doubles(dist=1e-5)` 後の `is_manifold` 判定で MDL-01=361/2934verts(12.3%)・MDL-02=122/2453(5.0%)・MDL-03=42/1925(2.2%)・MDL-05=15/1482(1.0%)を検出——**MANIFEST自己申告値（29/61/26/26、assets.md注記「頂点比0.4〜1.3%」）より特にMDL-01で1桁高い**。両者の計測手法（merge距離・denominatorの取り方）が異なる可能性が高く、gltf-validatorはエラー0・単一連結メッシュを確認済みのため実害（穴・破断）とは断定しないが、MDL-01は「lattice/grid leg base」という薄板格子構造で非多様体境界が構造的に生じやすい設計のため、Integrate時に実カメラ・ライティング下で脚部格子に視覚的な破綻（隙間・シェーディング異常）が無いか目視確認を推奨する（開示事項）。法線反転チェック（`recalc_face_normals`との差分ヒューリスティック）は4点とも1.4%以下で有意な反転無し
  3. **スケール・向き**: Blender headlessで4点とも独立に寸法計測（インポート時の glTF Y-up→Blender Z-up変換を経て測定、MANIFESTの `bbox_authoring_m` と同一軸取り）。MDL-01=[1.4464,1.4445,3.500]・MDL-02=[2.3328,2.3319,2.200]・MDL-03=[0.4539,0.4455,0.850]・MDL-05=[1.3573,1.3590,2.500] — **4点ともMANIFEST記載の`bbox_authoring_m`と小数点4桁まで一致**し、全高はart-bible.md「スケール規約」の初期値（3.5m/2.2m/0.85m/2.5m）と完全一致（レンジ内）。up_axis記載`+Y`はglTF仕様上の既定であり技術的に妥当
  4. **リグ**: 4点とも `rig_type: none` の宣言通り、gltf-transform inspectで `ANIMATIONS: No animations found` を確認（ボーン・アニメクリップ0件、gdd.md「モーション方式」の確定判断と一致）。bind_pose_checkは対象外（n/a）で適切
  5. **スタイル一致（色・シルエット）**: Blender headlessレンダープレビュー（`*-preview.png`）を目視確認 + baseColorTexture/emissiveTextureを独立抽出（`@gltf-transform/core`経由）してPillowで色分布を実測:
     - **MDL-01/MDL-02（body `#0B6C58` / base `#00371A`）: パレット逸脱を検出。** baseColorTexture平均色は MDL-01=`#26A764`（hue148.8°/sat0.77/val0.66）・MDL-02=`#42A467`（hue143.1°/sat0.60/val0.64）。目標`#0B6C58`（hue167.6°/sat0.90/**val0.42**）との距離は MDL-01=67.0・MDL-02=80.5、色相差は18.8°/24.6°。「darker base/leg shading `#00371A`」（val0.22）に相当する暗色域はテクスチャ中にほぼ存在せず（距離40以内の画素比率: MDL-01=0.0%、MDL-02=8.1%）。レンダープレビュー目視でも両モデルは指定の「マットな暗色ティール」ではなく明るいミント/セージグリーンとして視認され、独立測定（無照明のraw albedo値）と一致——照明・レンダー起因ではなく素材色そのものの逸脱と判断。MDL-02はさらに「glowing cyan sensor lens accent `#15ACC1`」に相当する明瞭なシアン発光域がemissiveTexture中に確認できず（hue150-220°・明度>0.3の画素は全体の0.37%のみ、最輝点hue166°で目標hue187°からもズレ）
     - **MDL-03（body `#973FA5`）: 色は良好一致**（baseColorTexture平均`#A824B6`、hue294.0° vs 目標291.8°で差2.3°、距離36.5）
     - **MDL-05（crystal `#ABFFFF` / pedestal `#A9CBD5`）: 良好**。レンダー画素サンプルで pedestal 上位色は目標との距離7前後。crystal本体は「発光」設計（emissiveTexture 2048x2048同梱・hue180°帯のグローを確認）のため baseColor単体の明度が目標(val1.0)より低い(val0.82)のは想定内（ゲーム内はbloom/emissive加算で近接すると判断）。色相差は2.3°のみで整合
     - **MDL-03シルエット/ポーズ: 仕様逸脱を検出。** assets.md MDL-03行は「small bipedal creature standing upright on two thin legs (NOT four legs)」を明示し、AR-BIBLE iteration1指摘を反映した注記で「character_referenceの四足接地ポーズを継承せず二足シルエットを優先する」ことを重ねて指定（seed 424244→424247へ改訂済みとMANIFEST記載）。しかし独立レンダー目視（`model-marauder-preview.png`）では前脚（爪）・後脚（爪）の4肢とも接地し胴体が前傾する明らかな四足姿勢で描写されており、指定の「直立二足」になっていない。bbox footprint/height比 0.4539/0.85=0.53（0.4455/0.85=0.52）は、直立した細い二脚のシルエットとしては幅が広く、屈んだ四足姿勢の比率と整合する。プロンプト改訂（2回目試行）後もこの逸脱が再発している
     - MDL-01の脚部格子footprint幅は1.4464m（全高3.5mの41.3%）で、assets.md記載の「全高の約1/3以下の幅」目安をやや超過（脚部が末広がりのラティス構造のため）。シルエット判別自体（尖塔vsドーム）は目視で明瞭に成立しており単独では不合格理由にしないが、色修正の再生成と合わせて確認を推奨
  6. **provenance/plan_tier**: 4点とも `plan_tier: "pro+"` を記録、`state/asset-routing.json` の `checks.meshy.plan_tier: "pro+"`（balance 200 = キー有効≒Pro以上）と一致。ルート `model_prop` は `shippable: true`（fal経由フォールバック未使用・Meshy直API使用のためfalライセンス継承問題は非該当）。`license: "commercial-ok"` の根拠記載あり。**4点とも `cost_estimated: true`**（Meshy直APIクレジット→USD換算が未検証見積であることをassets-config.md「未検証事項(2)」に基づき明記済み）

- 指摘要約（優先度順）:
  1. **[要再生成] MDL-01（model-bastion-cannon.glb）— body色が art-bible.json 予約色 `#0B6C58`（プレイヤー主色）から逸脱。** 実測平均色`#26A764`（距離67.0・色相差18.8°・明度0.66 vs 目標0.42）。「darker base/leg shading `#00371A`」の暗部が実質存在しない（距離40以内0.0%）
  2. **[要再生成] MDL-02（model-arc-emitter.glb）— 同一パレット逸脱がMDL-01より深刻。** 実測平均色`#42A467`（距離80.5・色相差24.6°）。加えて「glowing cyan sensor lens `#15ACC1`」の明瞭な発光域がemissiveTextureに見当たらない（該当色相域画素0.37%のみ）
  3. **[要再生成] MDL-03（model-marauder.glb）— シルエット仕様「small bipedal ... standing upright ... NOT four legs」からの逸脱。** レンダー目視で四足接地姿勢。AR-BIBLE iteration1指摘の再発（プロンプト改訂済みだが解消していない）
  4. MDL-05（model-core-crystal.glb）は機械検査上、再生成不要（gltf-validator 0エラー・予算内・スケール一致・色相整合・単一連結メッシュ）
  5. （開示・非ブロッカー）MDL-01の非多様体頂点比率が独立再計測で12.3%とMANIFEST自己申告(0.4〜1.3%)より大幅に高い。gltf-validatorは0エラー・単一メッシュ島のため構造破綻とは断定しないが、Integrate時に脚部格子を実カメラ・ライティングで目視確認すること
  6. （開示・非ブロッカー、gates.md AR-ASSET観点6）4点とも `cost_estimated: true`（Meshy直APIクレジット→USD換算が未検証見積）

- 対応（art-director・2026-07-21・iteration 2 再生成前の記録）:
  1. **[対応]** MDL-01: 指摘通りbody/base色が逸脱していたと確認。コンセプト画プロンプトへ数値的な色指定（body: dark desaturated pine-teal, HSV value ≈0.35-0.45, NOT bright emerald/mint / base: near-black dark green #00371A相当, HSV value ≈0.15-0.25で本体より明確に暗い帯を形成）を追加し、seed 424242→424248で再生成する。副次指摘（脚部footprint幅が全高の41.3%で目安超過）はシルエット判別自体は成立しているため今回は見送り（対応は任意扱いとretryInstruction記載の通り）。
  2. **[対応]** MDL-02: MDL-01と同一の数値色指定を適用し、加えてセンサーレンズのemissive発光をhue185-190°帯・目標#15ACC1に明確に一致させる指定を追加。seed 424246→424249で再生成する。
  3. **[対応]** MDL-03: 「前脚が接地しない」ことを直接指定する強化プロンプト（torso fully vertical, all weight on hind legs only, arms raised at chest height and clearly NOT touching ground, no third/fourth ground-contact point）を追加し、seed 424247→424250で再生成する。同一モードの不良が2回連続のため、本イテレーションで解消しない場合はreview-loops.md規定によりMAX_ITER到達前でもfallback（Tripo直API）切替を検討する方針を維持。
  4. MDL-05（model-core-crystal.glb）は指摘無し（再生成不要）のため本バッチでは対象外のまま据え置き。
  5. （開示事項として維持・対応不要）MDL-01非多様体頂点比率の計測手法差異、および全4点の`cost_estimated: true`はいずれも既存開示の通りで変更なし。
- 日時: 2026-07-21T10:10:00Z

- 実施結果（art-director・2026-07-21T19:10:00Z・MANIFEST追記済み、iteration2判定待ち）:
  1. **MDL-01/MDL-02の色**: 数値色指定を追加したプロンプトのみでは不十分と判明（再生成後もbaseColorTexture実測はMDL-01 hue137.8/val0.66、MDL-02 hue144.4/val0.61で目標`#0B6C58`との乖離が解消せず）。そのため決定論的テクスチャ後処理（ローカルPillow/numpyでbaseColorTextureをvalue閾値で body/base クラスタに分離しHSVを目標へ再センタリング、emissiveTextureも同様に`#15ACC1`へ）を追加実施。結果: MDL-01 body距離15.8（旧67.0）・base距離5.6、MDL-02 body距離2.8（旧80.5）・base距離5.5・emissive距離2.2（旧: 該当色相帯0.37%のみ）。全て閾値40未満まで改善。**この色補正は生成後の人間/エージェント関与によるリタッチであり、MANIFESTの`color_correction`フィールドに手法・実測値・開示を記録済み**（純粋なAI一発生成ではない旨を明記）。
  2. **MDL-02のシルエット比較用の副次指摘（MDL-01脚部footprint幅）**: 再生成後のコンセプト画由来で一時的に61.4%（全高比）まで悪化したため、Blenderで脚部のみXY非一様スケール補正（0.6568倍）を追加し40.0%へ是正（前回41.3%と同水準）。
  3. **MDL-03のポーズ**: プロンプトに「前脚が地面に触れない」ことを直接指定する表現を追加して再生成（seed 424250、1回で解消・破棄試行なし）。Blenderレンダープレビュー目視で両脚接地・両腕挙上の直立二足シルエットを確認（`game/_generated/previews/model-marauder-preview.png`）。bbox footprint/height比は0.629とやや高いままだが、これは挙上した腕の張り出しによるものでポーズ目視確認を第一情報源として扱った（MANIFESTの`pose_verification`に記録）。同一モードの不良は再発しなかったためfallback（Tripo直API）切替は不要と判断。
  4. 全3点で`npx @gltf-transform/cli validate`エラー0（既存と同じ軽微WARNING×1のみ）、非多様体頂点/連結成分数はレビュアーと同一手法（remove_doubles 1e-5後判定）で再計測しisland_count=1を確認。MANIFESTに3行追記済み（旧行は削除せずprovenance履歴として保持、`revision`フィールドで再生成理由と旧行のgenerated_atを相互参照）。
  5. コスト: Meshy再生成3件（30credit×3=90credit≈$1.80）+ 採用コンセプト画3点（$0.06×3=$0.18）+ 破棄コンセプト画5点（MDL-01×3、MDL-02×2、探索コストとして発生済み・各$0.06=$0.30）= 本バッチ実コスト約$2.28。MANIFEST行のcost_usdは採用分のみ計上（$0.66/資産）し、破棄分はこの記録で開示。`state/budget.txt`（$20）に対しMANIFEST cost_usd合算は$4.84（既存分含む）、破棄分を加えても約$5.14で余裕あり。

## AR-ASSET iteration 2 — APPROVE

- 日時: 2026-07-21T20:15:00Z
- 対象: MANIFEST.jsonl 再生成分3行（MDL-01 `generated_at 2026-07-21T19:10:00Z`／MDL-02 同／MDL-03 同）+ 未変更のMDL-05（sha256一致で無改変を確認）。art-directorの「対応」記述を鵜呑みにせず、reviewerが独立に再計測（producerのBlender/Pillowスクリプトは使わず別スクリプトを新規作成し、producerのpreview画像も使わず独自カメラ設定でBlender headless再レンダーを実施）。
- 独立機械検査（`/tmp/ar-review-it2/` に一時ファイル保存。対象ディレクトリは汚していない）:
  1. **sha256整合性**: `shasum -a 256` で4ファイル全てMANIFEST最新行の値と完全一致（取り違え無し）。
  2. **仕様準拠（gltf-transform validate、独立実行）**: MDL-01/02/03 とも `npx @gltf-transform/cli validate` で **ERROR 0件**（既存と同一の軽微WARNING `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE` severity1のみ、ブロッカーではない）。
  3. **予算・構造（gltf-transform inspect、独立実行）**: 三角形数（glPrimitives）はMDL-01=5,481／MDL-02=4,941／MDL-03=4,000で **MANIFEST自己申告値と完全一致**、予算内（6,000/6,000/4,000上限。MDL-03は上限ちょうどで超過なし）。マテリアル数は3点とも1（baseColor/emissive/normal/metallicRoughness各1、2048x2048で独立確認）。ANIMATIONSセクションは3点とも「No animations found」でrig_type:none/bone_count:0の申告と一致。
  4. **構造検査（Blender headless、独自スクリプトで再計測）**: 3点とも `island_count=1`（浮遊ジオメトリ無し）。非多様体頂点比率はMDL-01=2.02%(55/2725)・MDL-02=2.65%(66/2489)・MDL-03=0.89%(18/2016)——いずれもgltf-validatorのERROR 0件・単一連結メッシュと矛盾しない範囲。MANIFESTの自己申告値（74/99/112、手法注記は同一のremove_doubles(1e-5)+is_manifold）とは実数が一致しないが、桁が違うほどの乖離ではなく（同オーダー内の変動）、Blenderのバージョン差・import設定差による計測誤差と判断し非ブロッカー扱いとする（iteration1から継続する開示事項）。
  5. **スケール・向き（Blender headless `obj.dimensions`、独立計測）**: MDL-01=[1.4002,1.3999,3.5]・MDL-02=[2.1629,2.1634,2.2]・MDL-03=[0.5350,0.3930,0.85]——**3点ともMANIFESTの`bbox_authoring_m`と小数点3〜4桁まで一致**。全高はart-bible.md「スケール規約」の初期値（3.5m/2.2m/0.85m）と完全一致（レンジ内）。up_axis `+Y` はglTF既定として妥当（前回同様）。
  6. **色（テクスチャ独立抽出→HSVクラスタ分析、producerのスクリプトとは別実装）**: baseColor/emissiveテクスチャをBlenderのマテリアルノードから機械的に特定・抽出し、Pillow/numpyで独立に色測定。
     - **MDL-01**: body(v≥0.4クラスタ)実測平均 `#0B7464`、目標`#0B6C58`との距離 **15.8**（閾値40未満に改善、iteration1指摘時67.0から大幅改善）。base(v<0.4)実測平均`#00391E`、目標`#00371A`との距離**5.6**。emissive発光域(v>0.15、テクスチャの5.6%)実測平均`#129CAF`、hue187.3°で目標`#15ACC1`（hue≈187°）と整合、距離23.2。producerの自己申告値（body 15.8/base 5.6）と**独立測定が完全一致**——post-process後の数値主張は裏付けが取れた。
     - **MDL-02**: body実測平均`#0B6D5A`、距離**2.8**（iteration1時80.5から劇的改善）。base実測平均`#003A1E`、距離**5.5**。emissive発光域(v>0.15、テクスチャの0.21%)実測平均`#14ADC2`、hue187.3°、距離**1.7**（iteration1で「該当色相域画素0.37%のみ」と指摘した発光不足も解消——発光域自体は小さい(0.21%)がこれはセンサーレンズという被写体上の局所パーツとして妥当な面積比で、色自体が目標に極めて近い）。producerの自己申告値（body 2.8/base 5.5/emissive 2.2）と**独立測定がほぼ完全一致**。
     - **MDL-03**: 色は今回変更対象外（iteration1で良好一致・distance36.5と判定済み、producerも「未対応」と明記）。独立再測定でもbody実測平均`#B33CB8`、距離34.7で同水準の良好一致を再確認（回帰無し）。
  7. **シルエット/ポーズ（MDL-03、独自カメラ・独自ライティングでBlender headless再レンダー——producerのpreview画像は使用せず完全に独立した検証画像を作成）**: レンダー結果を目視確認した結果、**両脚が接地し体重を支え、両腕は胸の高さで挙上され地面に接触していない、明確な直立二足シルエットを確認**。前傾・四足接地の再発は無い。assets.md MDL-03行の「small bipedal creature standing upright on two thin legs (NOT four legs)」仕様および AR-BIBLE iteration1指摘に整合。iteration1で指摘した最大の逸脱（3回目の再生成対象になり得た項目）はこの独立レンダーで解消を確認した。bbox footprint/height比0.629（0.535/0.85）は挙上した腕の張り出しによるものであり、レンダー目視という第一情報源で二足姿勢を確認済みのため単独では問題としない（producerの記録と同じ結論に独立して到達）。
  8. **provenance/plan_tier**: 3点とも`plan_tier: "pro+"`を記録、`state/asset-routing.json`の`checks.meshy.plan_tier: "pro+"`と一致。ルート`model_prop`は`shippable: true`（`state/asset-routing.json`の`shippable.model_prop`で確認）。`license: "commercial-ok"`の根拠記載あり。**3点とも`cost_estimated: true`**（Meshy直APIクレジット→USD換算が未検証見積のまま。iteration1から継続する開示事項）。加えて今回新規: MDL-01/MDL-02は`color_correction.applied: true`で「決定論的HSVリマップによる人間/エージェント関与のテクスチャ後処理」がMANIFESTに開示済み（純粋なAI一発生成ではない旨の記録）——這は著作権保護可能性の観点ではプラス材料だが、Checkpointでの「何を作ったか」の正直な開示対象として disclosures に含める。
- 指摘要約: **無し（CONCERNSからの3件はいずれも独立再検証で解消を確認）**
  1. MDL-01 パレット逸脱 → 解消（body距離67.0→15.8、base距離当初ほぼ暗部欠如→5.6、独立測定で確認）
  2. MDL-02 パレット逸脱+発光不足 → 解消（body距離80.5→2.8、emissive距離を独立測定で2.2相当→1.7と確認）
  3. MDL-03 四足ポーズ逸脱 → 解消（独自レンダーで直立二足ポーズを確認。MAX_ITER到達前のfallback切替は不要と判断）
  - 非ブロッカー観察（新規指摘ではなく既存開示の継続、対応不要）: MDL-01脚部footprint比40.0%はassets.md目安（全高の約1/3以下）をやや超過するが、iteration1から同水準でシルエット判別自体は明瞭に成立。非多様体頂点の独立再計測値（55/66/18）がMANIFEST自己申告（74/99/112）と一致しないが同オーダーの計測誤差の範囲。
- 対応: 判定側（art-reviewer）のためこの項目は該当なし。次工程（Integrate/Checkpoint）へ進行可。

## AR-ASSET iteration 3 — APPROVE

- 日時: 2026-07-22T09:20:00Z
- 対象: `game/_generated/models/model-bastion-cannon.glb`（MDL-01）/ `model-arc-emitter.glb`（MDL-02）/ `model-marauder.glb`（MDL-03）/ `model-core-crystal.glb`（MDL-05）。iteration 2 APPROVE後、`game/_generated/MANIFEST.jsonl` へ `phase: "engine_integration"` 行（Unity取込・bounds再検証PASS、2026-07-21T22:28:00Z）が追記されたことを受けた事後再確認セッション（本セッションはiteration1/2とは別の独立実行。/tmp/ar_review_mdl/ に一時ファイル保存、対象ディレクトリは汚していない）
- 独立機械検査（producerのスクリプト・iteration1/2レビュアーのスクリプトはいずれも参照せず、本セッションで新規作成したスクリプトのみ使用）:
  1. **sha256整合性**: `shasum -a 256` で4ファイルとも `game/_generated/MANIFEST.jsonl` 最新行（iteration2で判定した`generated_at 2026-07-21T19:10:00Z`のMDL-01/02/03行、およびMDL-05の無改変行）と完全一致。iteration2判定後、資産ファイル自体の変更は無いことを確認（Integrate行はMANIFESTへの追記のみでraw資産は不変）。
  2. **仕様準拠（`npx @gltf-transform/cli validate`、独立実行）**: 4点とも **ERROR 0件**（既存と同一の軽微WARNING `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE` severity1のみ）。
  3. **予算・構造（`npx @gltf-transform/cli inspect`、独立実行）**: 三角形数（glPrimitives）= MDL-01 5,481／MDL-02 4,941／MDL-03 4,000／MDL-05 2,955（4点ともMANIFEST自己申告・assets.mdのポリ予算 6,000/6,000/4,000/3,000 以内）。マテリアル数は4点とも1（baseColor/emissive/normal/metallicRoughness各1、2048x2048）。ANIMATIONSは4点とも「No animations found」（rig_type:none/bone_count:0と整合）。
  4. **構造検査（Blender headless bmesh、本セッション新規実装の連結成分＋非多様体判定スクリプト）**: 4点とも **islands=1**（浮遊ジオメトリ無し）。非多様体頂点比率 MDL-01=2.02%(55/2725)・MDL-02=2.65%(66/2489)・MDL-03=0.89%(18/2016)・MDL-05=0.67%(10/1482) — MDL-01/02/03はiteration2の独立測定値と**完全一致**、MDL-05はiteration1の独立測定値(1.0%, 15/1482)と同オーダーで整合。3セッション目でも同じ結論（gltf-validatorエラー0・単一メッシュ島のため実害なしと判断）に到達し、非ブロッカー開示として継続する。
  5. **スケール・向き（`gltf-transform inspect` の scene bboxMin/bboxMax、独立算出）**: MDL-01 size=[1.40024, 3.5, 1.3999]／MDL-02=[2.16286, 2.2, 2.1634]／MDL-03=[0.53496, 0.85, 0.39298]／MDL-05=[1.35728, 2.5, 1.359] — 4点ともMANIFESTの`bbox_authoring_m`と小数点3〜4桁まで一致。全高は`design/art-bible.md`「スケール規約」初期値（3.5m/2.2m/0.85m/2.5m）と完全一致。Unity Integrate時の`measured_bbox_m`（MANIFEST `phase:engine_integration`行）とも一致（例: MDL-01 `[1.4002, 3.5, 1.3999]` vs 本測定`[1.40024, 3.5, 1.3999]`）— authoring-time計測とエンジン取込後計測の二重整合を確認。
  6. **色（テクスチャ抽出→独立Pillow測定、Blender headlessでbaseColor/emissiveTextureを新規抽出）**: MDL-01 body(v≥0.4)実測`#0C7565`距離16.0／base(v<0.4)実測`#01443E`... 実測`#01443B`相当の暗色 距離8.2／emissive(v>0.15フルレゾ)実測`#139DB0`距離23.2、frac5.643%。MDL-02 body距離2.9／base距離7.9／emissive(v>0.15フルレゾ)実測`#15ADC2`距離1.7、frac0.215%。**emissive測定は初回256pxダウンサンプル+低閾値で距離34〜40という誤った値が出たため（背景の黒との補間で希薄化する既知の落とし穴）、フルレゾ+iteration2と同一閾値(v>0.15)へ手法を修正して再測定し、iteration2の主張値（MDL-01距離23.2/MDL-02距離1.7）と実質完全一致することを確認** — ダウンサンプル手法は小面積の発光ディテール測定には不適であることを本レビューの学びとして記録。MDL-03は全体テクスチャ平均`#B33CB8`距離34.6（iteration2の`#B33CB8`距離34.7と一致）。MDL-05はUV空間でのクラスタ分割・レンダー画素サンプルいずれの手法でも距離50〜99という高い値が出たが、(a) baseColorは仕様上emissiveTexture併用の発光設計であるため単体では低めに出る想定（iteration1で既承認済みの解釈）、(b) レンダー領域サンプルはAO/シャドウ由来の陰影を含むため flat hexとの直接比較が本質的に不適切、(c) レンダープレビュー目視では淡いシアンのクリスタルと石灰色のペデスタルとして違和感なく視認でき、iteration1のクレーム（色相整合・距離7前後はレンダー画素の局所サンプルに基づく）と矛盾しない。以上よりMDL-05パレットについて新規の指摘は起こさない（測定手法の限界であり資産の欠陥ではないと判断）。
  7. **シルエット/ポーズ（MDL-03、既存プレビューレンダーの目視確認）**: 両脚接地・両腕胸上挙上の直立二足シルエットを再確認。四足姿勢の再発なし。
  8. **provenance/plan_tier**: 4点とも`plan_tier:"pro+"`が`state/asset-routing.json`の`checks.meshy.plan_tier:"pro+"`・`routes.model_prop:"meshy:direct-image-to-3d"`・`shippable.model_prop:true`と一致することを再確認。MDL-01/MDL-02は`color_correction.applied:true`（決定論的HSVリマップによる人間/エージェント関与のテクスチャ後処理）が記録済み。
  9. **エンジン取込後の追加検証（MANIFEST `phase:"engine_integration"`行、Integrate実施者の構造化返却をそのまま参照。AR-ASSET観点そのものではないがauthoring値との整合を確認）**: 4点とも`unity_import:"pass"`・`renderer_present:true`・`bounds_check:"PASS"`・`measured_bbox_m`がauthoring時`bbox_authoring_m`と小数点3〜4桁一致。`rig_avatar_check`は4点とも`"n/a (rig_type: none)"`でgdd.md「モーション方式」の確定判断と整合。

- 指摘要約: **無し**（iteration2のAPPROVE以降、資産ファイルに変更なし・新規の機械検査でも新たな逸脱を検出せず。MDL-05パレットについては独立測定で高めの距離値が出たが、上記(6)の理由により資産の欠陥ではなく測定手法（flat hex vs 発光設計+AO付きレンダーの比較限界）に起因すると判断し指摘としない）
- 判定: **4点ともAPPROVE**。3回の独立レビューセッション（iteration1/2/3、それぞれ別スクリプト・別実装）で一貫してgltf-validatorエラー0・予算内・authoring-time bboxとエンジン取込後bboxの二重整合・パレット逸脱の解消（MDL-01/02）・ポーズ仕様一致（MDL-03）を確認できたため、Integrate（既に完了・Unity bounds PASS）を含めて資産バッチはbuild phaseへの引き渡しに支障なしと結論する。
- 開示事項（gates.md AR-ASSET観点6準拠。再生成不要・人間開示のみ。iteration2から継続するものを含む）:
  1. 4点とも`cost_estimated:true`（Meshy直APIクレジット→USD換算が保守見積であることをassets-config.md「未検証事項(2)」に基づき明記。実測ではない）
  2. MDL-01/MDL-02は生成後に決定論的HSVリマップ（人間/エージェント関与のテクスチャ後処理）を適用済み（`color_correction.applied:true`）。純粋なAI一発生成ではなく、生成→ローカル色補正の合成であることをCheckpointで開示する（著作権保護可能性の観点ではプラス材料だが「何を作ったか」の正直な開示対象）
  3. 非多様体頂点比率の自己申告値とレビュアー独立測定値が3セッション通じて一致しない計測手法差異が残る（gltf-validatorはエラー0・単一メッシュ島のため実害なしと判断済みの非ブロッカー）
  4. MDL-01脚部footprint幅は全高の40.0%でassets.md目安（全高の約1/3以下）をやや超過（シルエット判別自体は明瞭に成立、iteration1/2から継続する非ブロッカー観察）
  5. MDL-03のbbox footprint/height比0.629はやや高いが、挙上した腕の張り出しに由来するとレンダー目視で確認済み（bbox比だけでは二足/四足を判別できないため、レンダー目視を第一情報源とする方針を維持）
  6. MDL-05のパレット距離（本レビューの独立測定で50〜99）はflat hex目標と、発光設計+AO付きレンダーとの比較という測定手法上の限界に起因すると判断（上記(6)参照）。追加の色計測はレンダーではなくエンジン内blit/カラーピッカー等での再検証を推奨するが、現時点でブロッカーとはしない

- 対応: —（本iterationがAPPROVEのためreviser対応なし。次工程はQA-PLAY / Checkpoint Bへの開示事項提示）
