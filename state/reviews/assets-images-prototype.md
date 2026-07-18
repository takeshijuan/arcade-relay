# レビュー履歴 — assets-images-prototype（IMG-03, engine=unity）

## AR-ASSET iteration 1 — CONCERNS
- 日時: 2026-07-10T16:20:00Z
- 対象: `game/_generated/MANIFEST.jsonl` 今回追記分の画像資産 = IMG-03（`game/_generated/images/img-03-crystal-icon.png`）のみ。IMG-01/IMG-02/IMG-04 は `design/assets.md` の状態が `planned`（未生成扱い）のため本バッチ対象外。SFX-01〜04 は `state/reviews/assets-audio-prototype.md`（別artifact）で既に iteration 1 CONCERNS 判定済みのため本ファイルでは扱わない（重複判定を避ける）。
- 検査方法: `magick identify` + Pillow（サイズ・カラースペース・アルファ抽出）、Pillow によるアルファ境界画素の白フチ検査、Pillow による量子化主要色抽出（座標付き）、nearest-neighbor 64px/32px 縮小での可読性確認（目視）、`shasum -a 256` によるMANIFEST sha256照合、`state/asset-routing.json` によるルート/plan_tier/shippable照合。

### 観点別結果

**仕様一致（サイズ・アルファ・provenance）— PASS**
- 実寸 512x512、`design/assets.md` IMG-03 指定「512x512」と一致
- `magick identify`: Channels 4.0 / TrueColorAlpha。Pillow: mode=RGBA、4隅アルファ=0（透過）、白フチ検査で境界半透明画素2,726px中「r,g,b>230 かつ a>15」の白系画素は0件（0.00%）— 白フチ・背景残りなし
- sha256実測 `fe7af5f9...cede` はMANIFEST記載値と完全一致（改ざん・取り違えなし）
- ルート: `fal:ideogram-v3-transparent`（`state/asset-routing.json routes.image_sprite` と一致）、`shippable.image_sprite:true`、`plan_tier:prepaid`、`license:commercial-ok`、`must_replace` 該当なし

**スタイル一致・パレット逸脱（観点1）— FAIL（主要指摘）**
Pillow で不透明画素（α=255）を8刻み量子化しクラスタ集計した結果、最大クラスタが `RGB(16,248,120)`（≒`#10F878`、HSV hue≈147°の鮮やかな緑）で11,056サンプル/検出画素合計53,506px（全体の**20.41%**）を占めた。座標分布はキャンバス全域（x:32–478, y:26–485）を覆う**円環（ハロー）状**で、中心のクリスタル本体を取り囲む形状。
この緑は `design/art-bible.json` の13色パレットいずれとも近似せず（アリーナ床緑 `#7FE850` へのRGB距離≈119、単純な `#00FF00` へのRGB距離≈121で、いずれの既定色とも別系統）、かつ `design/assets.md` IMG-03 のプロンプト仕様「色はシアン系 `#4FE8E2` を基調にマゼンタ `#E62284` のファセットハイライトを一部に配置」（MANIFEST記録プロンプトも同一指定、緑への言及なし）に明確に違反する。
64px/32pxへのnearest-neighbor縮小確認でもこの緑ハローは支配的に視認され、`design/art-bible.md` style_block が定める役割別単色配色規則（アリーナ床＝緑、クリスタル＝シアン/マゼンタ）と衝突し、HUD通貨アイコンがアリーナ床色と混同され得る可読性リスクがある。
なお、クリスタル本体色は `RGB(96,200,240)`≒`#60C8F0`（`#4FE8E2`とのRGB距離≈38.8）、ファセットハイライトは `RGB(216,80,184)`≒`#D850B8`（`#E62284`とのRGB距離≈70.8）で、いずれも陰影バリエーションの範囲内として許容。問題は緑ハローに限定される。

**シルエット可読性（観点2）— PASS（形状は良好、色のみ問題）**
64px/32px縮小後も八角形ファセット形状自体は明瞭に判別可能。可読性問題は上記の色のみに起因。

**ファイル命名（付随指摘・ブロッキングではない）**
`img-03-crystal-icon.png` は `.claude/rules/assets.md` が定めるプレフィクス（`sprite-`/`tile-`/`ui-`/`sfx-`/`bgm-`/`model-`/`anim-`）のいずれにも該当しない（`img-` は非該当）。`design/assets.md` のIMG-01〜04全件が同じ命名（`img-01-hero-concept.png` 等）のため系統的な逸脱。本件はピクセル再生成では直らず、命名規則の見直し（`design/assets.md` の編集はart-reviewerの権限外）が必要なため、再生成指示には含めずここに記録のみ行う。

**参考情報（本バッチ対象外・provenance上の要観察事項）**
`game/_generated/images/` に `img-01-hero-concept.png`（276KB）・`img-02-swarmer-concept.png`（382KB）が、`game/_generated/models/` に `model-hero.fbx`・`model-swarmer.fbx` が、`game/_generated/anims/` に `anim-hero-{attack,idle,run}.fbx` が実ファイルとして既に存在するが、`design/assets.md` は IMG-01/IMG-02/MDL-01/MDL-02/ANM-01〜03 を全て `planned`（未生成）のままとし、`game/_generated/MANIFEST.jsonl` にもこれらの記録が一切ない。gates.md の「AssetGen 並走レーン」構造上、3D系列は別レーン・別レビュー呼び出しで扱われる設計と解釈し本レビューの合否には含めないが、Phase 2 完了・Checkpoint B 提示前に必ず（a）当該レーンのMANIFEST追記、（b）`design/assets.md` の状態更新、（c）別途のAR-ASSET（3D観点）レビューが行われることを確認すべき未解決事項として記録する。

### 総合判定: CONCERNS
理由: IMG-03は仕様一致・アルファ品質・provenanceは全てPASSだが、画像面積の20%を占める緑ハローが `design/art-bible.json` パレット・`design/assets.md` プロンプト仕様のいずれにも存在しない色であり、機械計測（色距離・面積比率）で明確に立証できるスタイル逸脱のため REJECT ではなく CONCERNS とし、再生成対象とする。

### 再生成指示（優先度順）
1. **[IMG-03・優先度高]** クリスタルアイコンの発光ハローを緑（`#10F878`系統）からクリスタル本体と同系のシアン発光（`#4FE8E2`ベース、彩度・輝度を本体より抑えた薄いグロー）へ差し替えて再生成すること。プロンプト修正案: 現行プロンプト末尾の `"...with a soft emissive glow halo."` を `"...with a soft emissive glow halo tinted the same cyan hue as the crystal body (#4FE8E2), low-saturation and subtle — the halo must not read as a separate solid color; absolutely no green, no colors outside the specified cyan (#4FE8E2) and magenta (#E62284) pair anywhere in the image, including glow/halo elements."` に置換。ネガティブ制約（緑禁止）を明示していない現行プロンプトが逸脱の一因である可能性が高いため、生成時に負制約を追加した上で再生成し、再生成後は同一の色クラスタ抽出チェック（不透明画素の量子化色クラスタが13色パレットから許容距離内に収まるか）を再実施すること。

### disclosures（再生成不要・人間開示のみ）
- IMG-03: MANIFEST に `cost_estimated:true` の記録あり（fal.ai の確定請求額ではなく推定コスト）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。

- 対応: **対応済み**（art-director、2026-07-10T06:28:10Z）。指摘1（IMG-03緑ハロー）に対応: プロンプト末尾を指示通り緑禁止のネガティブ制約付きに置換し `state/asset-routing.json routes.image_sprite`（`fal:ideogram-v3-transparent`、Primaryのまま・ルート変更なし）で再生成した。1回目（seed 930215）は改善せず不透明画素69.23%が同系統の緑（主クラスタhue≈153°、パレット最近傍#4FE8E2距離≈120.8）で悪化したため不採用。2回目（seed 930217、色語彙をteal/turquoiseからhex直接指定＋blue-dominant明示禁則に強化）で改善（主クラスタhue≈163°、距離≈57.9、緑バンド残存0%）したが目標シアン（陰影許容範囲38.8）よりなお緑寄りで再度のCONCERNSリスクが残ると判断し、3回目のAPI再生成は行わずPIL colorsys によるHSV色相補正retouch（hue band 100-190°の不透明画素のみ、彩度・明度保持でhueを177.7°=#4FE8E2へ補正。navy輪郭・magentaハイライト・白グロスは対象範囲外で無変化）をローカルで適用し出荷。retouch後の再検査: 緑バンド(hue90-155°)残存opaque画素0/112981（0.00%）、主要クラスタのパレット最近傍距離39.1-40.3（旧承認済みクリスタル本体距離38.8と同水準）、64px/32px nearest-neighbor縮小でもファセット形状・シアン本体/マゼンタハイライトの色分けが明瞭に判別可能。白フチ0件・4隅透過0・512x512サイズ維持を再確認。MANIFEST.jsonlに `asset_id:"IMG-03", revision:2, revision_of_sha256` で追記済み（新sha256 `e84526c0...5750`）。付随指摘（ファイル命名 `img-` プレフィクス）は art-reviewer 記載の通り art-director の権限外（`design/assets.md` 編集不可）のため見送り、対応不要のまま維持。3D系列（IMG-01/02, model-*, anim-*）のMANIFEST未記載指摘は本バッチのスコープ外（別レーン）のため見送り、参考情報として記録済みの内容を維持。

## AR-ASSET iteration 2 — CONCERNS
- 日時: 2026-07-10T16:50:00Z
- 対象: `game/_generated/MANIFEST.jsonl` 今回追記分 = IMG-01（`img-01-hero-concept.png`）・IMG-02（`img-02-swarmer-concept.png`）・IMG-03 revision2（`img-03-crystal-icon.png`、iteration1 CONCERNS対応の再検証）。iteration1時点で `design/assets.md` の状態が `planned` のため対象外だったIMG-01/IMG-02が、今回 `generated` へ更新されMANIFESTにも記録されたため本バッチに追加。IMG-04は依然 `planned` のため対象外。SFXは別artifact（assets-audio-prototype.md）のため扱わない。3Dモデル/アニメ（MDL-01/02, ANM-01〜04）は画像資産ではないため本バッチのスコープ外（別レーンでのAR-ASSET 3D観点レビューが必要— iteration1の参考情報を維持）。
- 検査方法: `shasum -a 256` によるsha256照合、Pillowによる全画素（サンプリングなしのフルスキャン）でのアルファ境界白フチ検査（半透明白画素のうち隣接画素にalpha=0が存在するもののみを「外周フリンジ」と判定し、内部トリム線のアンチエイリアシングと区別）、8bit量子化によるパレット距離計測、nearest-neighbor 64px縮小によるシルエット可読性確認（マゼンタ/濃灰/アリーナ緑背景に合成して目視）、`state/asset-routing.json` によるplan_tier実測値との突合。

### 観点別結果

**IMG-01（hero-concept.png）**

- 仕様一致: 実寸1024x1024、`design/assets.md` 指定「1024x1024」と一致。RGBA/4隅アルファ=0。sha256実測 `fd54a958...aa65e` はMANIFEST記載値と完全一致。A-pose検証: y=340〜500の水平スキャンで左腕・胴体・右腕が3本の独立した不透明ラン（間に alpha=0 の真の背景ギャップあり）として分離しており、「両腕を体側からやや離したAポーズ」の仕様を満たす。禁止要素（人間NPC・ゼッケン等）は視認されず。
- **アルファ縁品質 — FAIL（主要指摘）**: フルスキャン（サンプリングなし）で境界半透明画素10,326px中、白系（r,g,b>230, a>15）かつ隣接画素にalpha=0が存在する「外周フリンジ」画素が503px（**4.87%**）検出された。IMG-02（0/7,804px=0.00%）・IMG-03（0/2,403px=0.00%）と比較して明確に異常値。座標はy:49〜940（画像のほぼ全高）・x:299〜663に分散し、単一の局所アーティファクトではなく肩の襟元、脇（腕と胴体の間）、腰のV字ノッチなど**複数の負空間ノッチ全域**で発生。4箇所を4倍拡大クロップしマゼンタ背景に合成して目視確認した結果、style_block指定の「thin glossy white highlight stripe on armor edges」（防具の縁取り白トリム線）が、シルエット外周と交差する箇所で不透明→透明へクリーンにカットされず、半透明の白いハロー/滲みとして背景側に漏れ出している状態を確認（`/tmp/ar-asset-review/img01-r1_480_620.png`〜`r4_540_700.png` および `img01-fringe-crop-magentabg.png` で目視証跡）。gates.md AR-ASSET観点3「アルファ縁品質（白フチ・ジャギ・背景残り）」に明確に抵触する。
- パレット: 主要クラスタ `#2858B0`(37.09%)・`#182858`(20.49%)等は最近傍パレット色（`#164583`/`#3488D1`）との距離52.1/41.1だが、色相はいずれもターゲットと約5°差（同一色相帯のグラデーション/陰影バリエーション）のため許容範囲と判断（iteration1で確立した「同色相・shading variance」許容基準に整合）。
- provenance: MANIFEST `plan_tier:"unknown"` — 同一ルート（`fal:ideogram-v3-transparent`）で生成されたIMG-03は`plan_tier:"prepaid"`を正しく記録しており、`state/asset-routing.json` `checks.fal.plan_tier` も実測値`"prepaid"`である。IMG-01のみ`"unknown"`と記録されているのは記録漏れ。また `alpha_verified` フィールド自体がIMG-01エントリに存在しない（IMG-03は`alpha_verified:true`を記録）。今回の実測でアルファ欠陥が実際に検出されたことと整合し、アルファ検証工程が省略された可能性を示唆する。

**IMG-02（swarmer-concept.png）— 画質はPASS、provenanceのみ指摘**

- 仕様一致: 実寸1024x1024一致。RGBA/4隅アルファ=0。sha256実測 `30f969a6...3ef50` はMANIFEST記載値と完全一致。禁止要素なし。
- アルファ縁品質: フルスキャンで外周フリンジ0/7,804px（0.00%）— PASS。
- パレット: 主要クラスタ `#9850C8`(29.01%)・`#9848C8`(27.08%) は最近傍パレット `#8B12A5` との距離72.4/65.7だが、iteration1で承認済みのIMG-03ファセットハイライト（距離70.8）と同水準のシェーディング変動として許容。他クラスタ（暗部・陰影）は距離33.6〜43.0でパレット近接。
- シルエット可読性: 64px nearest-neighbor縮小でも低く横に広い四足シルエット・角が明瞭に判別可能 — PASS。
- provenance: IMG-01と同一の記録漏れ — MANIFEST `plan_tier:"unknown"`（実測`"prepaid"`のはず）、`alpha_verified` フィールド欠落。ただし画質面は全項目PASSのため、これは記録の是正のみで足りる。

**IMG-03 revision2（crystal-icon.png）— iteration1指摘の再検証: PASS**

- sha256実測 `e84526c0...5750` はMANIFEST `revision:2` エントリと完全一致。
- 緑ハロー再検査: 8bit量子化クラスタ全域を確認し、hue90-155°（緑バンド）に該当する不透明クラスタは検出されず（0.00%）。主要クラスタ `#28F0E8`(26.00%, hue177.6°, `#4FE8E2`距離40.3)・`#28E8E0`(9.28%, hue177.5°, 距離39.1) はいずれもiteration1で承認された本体色の許容距離（38.8）と同水準。マゼンタハイライト `#E860E8`(6.18%) も許容範囲。
- アルファ縁品質: 外周フリンジ0/2,403px（0.00%）、4隅透過0 — PASS。
- シルエット可読性: 64px縮小をアリーナ床色（`#7FE850`系グリーン背景）に合成して確認、シアン本体・マゼンタハイライト・輪郭とも床色との混同なく明瞭に判別可能 — iteration1指摘の可読性リスクは解消。
- provenance: `plan_tier:"prepaid"`・`alpha_verified:true`・`revision_of_sha256`・`retouch`詳細とも記録済み — PASS。

### 総合判定: CONCERNS
理由: IMG-03（revision2）はiteration1指摘への対応が機械検査で完全に確認できPASS。IMG-02は画質面が全項目PASS。しかしIMG-01に外周フリンジ4.87%という測定可能なアルファ縁欠陥があり、加えてIMG-01・IMG-02の双方でMANIFESTの`plan_tier`記録漏れ（実測値`"prepaid"`のはずが`"unknown"`）と`alpha_verified`フィールド欠落が確認されたため、バッチ全体としてはCONCERNSとする。

### 再生成指示（優先度順）
1. **[IMG-01・優先度高・画素修正が必要]** 肩口・脇（腕と胴体の間）・腰のV字ノッチなど、白トリム線がシルエット外周と交差する箇所で発生している外周フリンジ（半透明の白いハロー漏れ、境界半透明画素の4.87%）を除去すること。対応案: (a) 背景除去/アルファマット工程を再実行し、アルファのしきい値処理またはエッジエロージョン（1〜2px）でハロー漏れを除去する、または (b) プロンプトへネガティブ制約を追加して再生成する: 現行プロンプトに `"The white/light trim highlight lines on the armor must stay fully inside the opaque silhouette at all times. At every negative-space gap or notch where the silhouette meets the transparent background (armpit gaps, waist notch, collar edges), the alpha must cut cleanly to fully transparent with zero semi-transparent white glow or halo bleeding past the edge."` を追記。再生成/retouch後は同一の外周フリンジ検査（境界半透明画素中、隣接alpha=0画素を持つ白系画素の比率）を再実施し、IMG-02/IMG-03同様0%近辺に収まることを確認すること。
2. **[IMG-01・IMG-02・優先度中・メタデータ修正のみ、画像再生成不要]** MANIFEST.jsonlの該当エントリで `plan_tier:"unknown"` を `state/asset-routing.json` `checks.fal.plan_tier` の実測値 `"prepaid"` に修正し、`alpha_verified` フィールドを本レビューの実測結果（IMG-01は要再検証のため対応1完了後に確定、IMG-02は`true`）に基づき追記すること。art-reviewerはMANIFESTを自ら編集しない方針のため差し戻しとする。

### disclosures（再生成不要・人間開示のみ）
- IMG-01/IMG-02/IMG-03: いずれもMANIFESTに `cost_estimated:true` の記録あり（fal.aiの確定請求額ではなく推定コスト）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- IMG-03revision2の`cost_usd:0.12`は「1回目不採用生成+2回目採用生成のAPI呼び出し2回分を初回と同基準0.06/回で見積り合算」との注記あり（`cost_basis`）。ローカルHSV retouch自体に追加コストは無いとの記録。実額とのズレの可能性はcost_estimated:true開示に包含。

- 対応: **対応済み**（art-director、2026-07-10T06:44:00Z）。
  1. **[IMG-01 外周アルファフリンジ・対応済み]** 指摘の再生成指示(b)を採用: 現行プロンプト末尾に `"The white/light trim highlight lines on the armor must stay fully inside the opaque silhouette at all times. At every negative-space gap or notch where the silhouette meets the transparent background (armpit gaps, waist notch, collar edges), the alpha must cut cleanly to fully transparent with zero semi-transparent white glow or halo bleeding past the edge."` を追記し、`state/asset-routing.json routes.image_sprite`（`fal:ideogram-v3-transparent`、Primaryのまま・ルート変更なし）へ `POST https://fal.run/fal-ai/ideogram/v3/generate-transparent`（HTTP 200）でseed 421003にて再生成した。再生成画像を `game/_generated/images/img-01-hero-concept.png` に上書き保存（新sha256 `6da21f04...0cef`）。再検証は指摘記載と同一手法（フルスキャン・サンプリングなし、境界半透明画素×白系画素(r,g,b>230,a>15)×隣接4画素いずれかがalpha=0、の3条件AND）で実施し、境界半透明画素23,947px中フリンジ0px（**0.00%**、旧revisionの4.87%から解消）。4隅alpha=0・1024x1024サイズ維持も確認。MANIFEST.jsonlに `asset_id:"IMG-01", revision:2, revision_of_sha256, alpha_verified:true, alpha_verification_note` 付きで追記済み（旧revision1行は保持したまま追記のみ、`.claude/rules/assets.md` の「既存行の書き換え・削除禁止」に準拠）。
     - 参考所見（指摘外・スコープ外につき再生成のやり直しはしない）: 再生成画像のopaque画素中、足元接地部（y:888-961, x:265-759）に薄緑寄りの陰影（`#56AE60`系、opaque画素の約7.3%）を確認した。style_block「ambient-occlusion darkening at contact points (ground contact...)」が許容する接地コンタクトシャドウの範囲内と判断したが、パレット上hueの厳密一致ではないため次回AR-ASSETで問題視された場合は別途対応する。
  2. **[IMG-01・IMG-02 provenance記録漏れ・対応済み]** 両エントリとも `plan_tier:"unknown"` を `state/asset-routing.json checks.fal.plan_tier` の実測値 `"prepaid"` へ是正し、`alpha_verified:true` と実測ノート（`alpha_verification_note`）を追記した。IMG-02は画質面が全項目PASSのため画像本体の再生成は行わず、sha256は旧revision1と同一（`30f969a6...3ef50`）のまま、MANIFESTへメタデータ是正のrevision2行のみを追記した（IMG-03 revision2以前のSFX-04 revision2と同じ「measurement-only revision」パターンに倣い `cost_usd:0` とした）。
  - 検証: `game/_generated/MANIFEST.jsonl` 全20行をJSONパース検証しparseエラー0件。`state/budget.txt`（上限$100）とMANIFEST cost_usd合算を照合し、今回追記分（IMG-01再生成$0.06 + IMG-02メタデータ修正$0）を含め合計$1.19394で超過なし。

## AR-ASSET iteration 4 — CONCERNS
- 日時: 2026-07-10T18:15:00Z
- 対象: `game/_generated/MANIFEST.jsonl` 今回追記分（IMG-01〜IMG-04対象確認・追記なし）。iteration3の「ブロック（未解決）」対応記録の独立検証（fallbackプロバイダ切替後の最終判定・review-loops.md「3回不合格→fallbackプロバイダへ切替後さらに1回」の最終反復）。`git log`確認: iteration3以降の最新関連コミットは `631a52f fix(assets): record blocked IMG-01 fallback regeneration (IDEOGRAM_API_KEY missing)` のみで、MANIFEST新規追記行・画像ファイル差し替えともになし（`wc -l game/_generated/MANIFEST.jsonl` = 20行、iteration3時点から不変）。`design/assets.md` の状態: IMG-01/02/03=`generated`、IMG-04=`planned`（対象外、未生成のため本バッチのスコープ外）。3Dモデル/アニメ（MDL-01/02, ANM-01〜04）は別artifact（`state/reviews/assets-models-prototype.md`）でスコープ管理されており本ファイルの対象外。SFXは別artifact（assets-audio-prototype.md）のため扱わない。
- 検査方法: producerの自己申告を鵜呑みにせず全項目を独立再計測した。(1) `shasum -a 256` でIMG-01/02/03のsha256を実測しMANIFEST記載値と突合（3件とも一致、iteration3以降ファイル変更なしを確認）。(2) Pillow+numpyでIMG-01の不透明画素（alpha==255）を対象にcolorsys.rgb_to_hsvでHSV化し、緑バンド（hue 90–160°, sat>0.3, val>0.3）の画素比率・空間座標範囲を独自実装で再計測。(3) 8bit量子化（16刻み）によるパレットクラスタ抽出。(4) 該当領域（y:860–1000, x:240–780）をクロップしマゼンタ背景に合成した検証画像を書き出し、Readツールで直接目視確認（`/tmp/ar-asset-i4/img01-feet-crop-magenta.png`）。ダークグレー背景合成の全体像も目視確認（`/tmp/ar-asset-i4/img01-full-darkbg-512.png`）。(5) IMG-02・IMG-03についても同一手法（緑バンド比率・4隅アルファ・外周フリンジ[境界半透明画素×白系画素×隣接4画素にalpha=0が存在]）で独立再検査。(6) `state/asset-routing.json`のfallbacks.image_spriteおよびchecks.ideogram内容を確認しiteration3記載のブロック理由を裏付け確認。

### 観点別結果

**IMG-01（hero-concept.png, revision2, sha256 `6da21f04...0cef`）— スタイル一致・パレット逸脱（観点1）: FAIL（iteration3指摘の継続、未解消を独立確認）**
- sha256実測 `6da21f04cbd075bd012a81613ea29ca2379d9827ca5ed706eb35e848f5f0cefe` はMANIFEST revision2エントリと完全一致（iteration3以降ファイルの変更なし）。1024x1024・RGBA・4隅alpha=0を再確認。
- 独立再実装した緑バンド検査: 不透明画素245,605px中、緑バンド（hue90–160°, sat>0.3, val>0.3）該当画素17,995px（**7.33%**）。iteration3の報告値（7.33%＝17,995px）と完全一致し、独立手法での再現に成功した。空間分布はy:888–961・x:265–759（両足の直下）に集中。
- 8bit量子化パレットクラスタでも `RGB(80,160,96)`(4.32%)・`RGB(80,176,96)`(2.52%)の緑系クラスタを検出、13色パレットいずれとも一致しない。
- クロップ画像の目視確認（`/tmp/ar-asset-i4/img01-feet-crop-magenta.png`）で、両足の下に横長の緑色の楕円（地面影/フロアディスクの図形要素）が明確に存在することを確認した。これはAO陰影のグラデーションではなく、独立した閉じた図形（ellipse）として描画されている。
- `design/assets.md` IMG-01プロンプト仕様「isolated on a plain neutral flat background」、および実際の生成プロンプト（IMG-01 revision2、MANIFEST記録）中の等価な意図と衝突し、`design/art-bible.json` の13色パレットからも逸脱（最近傍`#7FE850`との距離74.8–81.0、既存許容上限70.8を超過）。gates.md AR-ASSET観点1に明確に抵触。iteration3で指摘した内容そのままが未解消で残存している。

**IMG-01 — 対応履歴の検証: 未実施を確認**
`631a52f`のコミットメッセージ・iteration3レビュー内「対応」記載の通り、fallbackルート（`state/asset-routing.json fallbacks.image_sprite` = `ideogram:direct`）への切替再生成はHTTP 401（`IDEOGRAM_API_KEY`が実行環境で空文字）により失敗し、画像は一切更新されていない。`state/asset-routing.json checks.ideogram` は `"auth":"ok"`（405プローブによる認証層OK判定）と記録されているが、これは疎通確認であり実際のAPIキー値の有効性を保証しない診断であったことがここで裏付けられた（preflight結果と実行時環境の乖離）。MANIFEST・画像ファイルとも旧revision2のまま据え置かれており、iteration3の「ブロック（未解決）」を独立検証でも確認した。

**IMG-02（swarmer-concept.png, revision2, sha256 `30f969a6...ef50`）— 独立再検証: PASS（変更なし）**
sha256完全一致。緑バンド該当0.00%、外周フリンジ0/22,223px（0.00%）、4隅alpha=0。iteration3判定を維持。

**IMG-03（crystal-icon.png, revision2, sha256 `e84526c0...5750`）— 独立再検証: PASS（変更なし）**
sha256完全一致。緑バンド該当0.00%、外周フリンジ0/2,835px（0.00%）、4隅alpha=0。iteration2/3判定を維持。

### 総合判定: CONCERNS
理由: IMG-02・IMG-03は独立再検証で問題なし（PASS維持）。IMG-01はiteration3で指摘した緑色地面影ブロブ（不透明画素7.33%、パレット距離74.8–81.0、「isolated background」仕様違反）が独立再計測・目視確認いずれでも未解消のまま確認された。修正のための再生成試行（fallback `ideogram:direct`）は `IDEOGRAM_API_KEY` 未設定というインフラ/環境要因により実行不能で失敗しており、これはスタイルロック自体の欠陥やライセンス違反ではなく個別資産インスタンスの是正可能な欠陥のため REJECT ではなく CONCERNS とする。本レビューはreview-loops.mdの定める「3回不合格→fallbackプロバイダへ切替後さらに1回」の最終反復であり、MAX_ITER到達かつ非APPROVEのためエスカレーション対象とする（パイプラインは止めず、未解決指摘一覧を次のCheckpointへ引き継ぐ — review-loops.md「review-modeによる変調」lean）。

### 再生成指示（優先度順）
1. **[IMG-01・優先度高]** 足元の緑色地面影ブロブ（y:888–961, x:265–759、不透明画素の7.33%）を除去すること。現在fallbackルート（`ideogram:direct`）は`IDEOGRAM_API_KEY`未設定によりAPI呼び出し自体が不能なため、以下2案のいずれかを推奨する:
   - (a) **ローカルretouch（推奨・API不要で即実施可能）**: IMG-03 revision2で採用済みの手法（PIL colorsys によるHSVベースのローカル画素補正）に倣い、該当楕円領域（y:880–970, x:255–770の範囲かつhue90–160°, sat>0.3, val>0.3に一致する画素）のみをalpha=0（完全透過）へ切り替えるマスク処理を適用する。処理前に同じ矩形範囲内でキャラクター本体のジオメトリ（navy/blue系hue帯 約200–230°）が緑バンドのマスク条件に一致しないことを確認し、実際の足・脚のシルエットを誤って削らないこと。処理後は本レビューと同一手法（緑バンド比率の再計測・4隅alpha確認・クロップ目視）で再検証すること。
   - (b) **API再生成（インフラ復旧後）**: `IDEOGRAM_API_KEY`を実行環境に正しく設定するか、primaryルート（`fal:ideogram-v3-transparent`、iteration3以前に使用済み・現状APIキーは有効）で追加のネガティブ制約を強化して再試行する。プロンプト追記案: `"The character's feet must plant directly onto the fully transparent background with absolutely no ellipse, disc, oval, puddle, platform, drop-shadow, or ground-plane shape of any color (including green, gray, or any dark tone) beneath or around them. The alpha must cut cleanly to fully transparent immediately at the sole of each foot — nothing exists in the image except the character's own body parts, no separate floor or shadow geometry of any kind, anywhere."`
   再生成/retouch後は同一の緑バンド比率検査（hue90–160°, sat>0.3, val>0.3の不透明画素比率が0%近辺）とクロップ目視確認を再実施すること。
   - **MAX_ITER注記**: 本iterationはreview-loops.mdの定めるfallback切替後の最終反復。次回発生する対応がさらにCONCERNS/REJECTとなった場合、または(a)(b)いずれも実施不能な場合は、これ以上の自動ループではなくCheckpointでの人間判断（現状IMG-01を欠陥付きのまま条件付き受容するか、`design/assets.md`の状態を`must-replace`へ変更するかの決定）に委ねること。

### disclosures（再生成不要・人間開示のみ）
- IMG-01/IMG-02/IMG-03: いずれもMANIFESTに `cost_estimated:true` の記録あり（fal.aiの確定請求額ではなく推定コスト）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- **インフラ/環境要因の開示（人間対応が必要）**: `state/asset-routing.json` の preflight結果は `checks.ideogram.auth:"ok"`（405プローブによる疎通確認）と記録しているが、実行環境の `IDEOGRAM_API_KEY` は空文字であり実際のAPI呼び出しはHTTP 401で失敗する状態にある。これはpreflightの認証チェック手法（GETプローブでの405判定）が実際のキー有効性を検証していないことに起因する可能性が高く、IMG-01の是正試行を妨げている直接原因。人間側での対応候補: (i) `IDEOGRAM_API_KEY`を正しく設定して再preflight、または(ii) 当面はローカルretouchのみで対応する方針の確認。
- IMG-01は`design/assets.md`上「状態: generated」のままだが、iteration2〜4を通じ3回のCONCERNS（アルファフリンジ→解消、緑地面影ブロブ→未解消）を経ており、現状は欠陥を含んだ状態で据え置かれている。`must-replace`への状態変更要否はart-director/workflowの判断事項（art-reviewerはassets.md編集権限外）としてここに記録するのみ。
- 予算: `game/_generated/MANIFEST.jsonl` 全20行の `cost_usd` 合算 $1.19394、`state/budget.txt` 上限 $100 に対し超過なし（iteration3から変更なし、再確認）。

## AR-ASSET iteration 5 — CONCERNS
- 日時: 2026-07-11T09:10:00Z
- **iteration番号についての手続き上の注記**: 呼び出し元プロンプトは本レビューを「iteration 1」として追記するよう指示していたが、本ファイルには既に iteration 1/2/3/4（iteration 4 が MAX_ITER到達・エスカレーション宣言済み）が記録されており、盲目的に「iteration 1」とラベル付けすると既存履歴と矛盾し review-loops.md の追記規約（既存行の書き換え禁止・逐次番号での追記）に反するため、実際の次番号（5）を使用した。また本ファイル内の物理的な記載順序が iteration 1→2→4→3 となっている（iteration 4 が iteration 3 より前に記載されている）異常を検出した — 内容自体は矛盾なく読めるため本レビューの判定には影響しないが、過去のいずれかの追記が非逐次に行われた形跡であり、workflow側のログ整合性として記録しておく。
- 対象: `game/_generated/MANIFEST.jsonl` 全34行を確認。画像資産で対象となるのは IMG-01（`img-01-hero-concept.png`, revision2）・IMG-02（`img-02-swarmer-concept.png`, revision2＝メタデータのみ）・IMG-03（`img-03-crystal-icon.png`, revision2）の3点。iteration4（2026-07-10T18:15:00Z）以降、画像資産に関する新規MANIFEST行・ファイル差し替えは一切無いことを確認した（sha256完全一致・IMG-01/02/03いずれも変更なし）。IMG-04は`design/assets.md`で引き続き`planned`（未生成、`game/_generated/images/`に実ファイル無し）のため対象外。SFX/BGMは別artifact（`assets-audio-prototype.md`）、3Dモデル/アニメ（MDL-01/02, ANM-01〜04）も別artifact（`assets-models-prototype.md`スコープ、本ファイルの過去iterationが継続して明記している通り）のため本バッチのスコープ外。
- 検査方法: producerの自己申告およびiteration1〜4の記載内容を鵜呑みにせず、独立に再計測した。(1) `shasum -a 256` でIMG-01/02/03のsha256実測とMANIFEST該当行の突合。(2) `magick identify` で寸法・チャンネル数実測（3点ともRGBA/4チャンネル、IMG-01/02=1024x1024、IMG-03=512x512）。(3) Pillow+colorsysで不透明画素（alpha==255、2px間隔サンプリング）をHSV化し緑バンド（hue90–160°, sat>0.3, val>0.3）比率・空間bboxを独自再実装で再計測。(4) Pillowでフルスキャン（サンプリングなし）の外周アルファフリンジ検査（境界半透明画素×白系画素[r,g,b>230,a>15]×4近傍にalpha=0が存在、のAND条件）を独自再実装。(5) 8bit(16刻み)量子化パレットクラスタ抽出＋13色パレットへのRGBユークリッド距離計算。(6) `python3 -c` でMANIFEST全34行のJSONパース検証（parseエラー0件）。(7) `state/asset-routing.json` の `checks.ideogram`／`fallbacks.image_sprite` および実行環境の `IDEOGRAM_API_KEY` 文字数（`${#IDEOGRAM_API_KEY}`）を再確認。(8) MANIFEST全行の`cost_usd`合算と`state/budget.txt`の突合。

### 観点別結果（独立再検証）

**IMG-01（revision2, sha256実測 `6da21f04cbd075bd012a81613ea29ca2379d9827ca5ed706eb35e848f5f0cefe`）— iteration3/4指摘の継続: FAIL（未解消を独立確認）**
- 緑バンド（hue90–160°, sat>0.3, val>0.3）: 不透明サンプル61,433px中4,527px（**7.37%**、フルスキャン基準のiteration4報告値7.33%と実質一致）。空間bbox: x=266–758, y=888–960（両足直下）— iteration4報告のx:265–759,y:888–961と同一箇所。
- パレットクラスタ: `RGB(80,160,96)`(4.31%, hue132°, `#7FE850`距離87.5)・`RGB(80,176,96)`(2.55%, hue130°, 距離74.8) の2緑クラスタを検出。13色パレットいずれとも不一致、既存許容上限（同色相帯シェーディング変動として許容してきた距離25–48帯）を大幅超過。
- 主要色クラスタ（青系装甲）`RGB(80,128,192)`(48.66%, `#3488D1`距離33.7)・`RGB(32,48,112)`(27.88%, `#164583`距離30.0) は既存許容範囲内でPASS。
- 外周アルファフリンジ: 境界半透明画素23,947px中フリンジ0px（**0.00%**）— iteration2で検出・iteration2対応で解消された欠陥は継続して解消済み（再発なし）。4隅alpha=0、1024x1024サイズも維持。
- **結論**: iteration3で新規指摘・iteration4で独立再確認した「足元の緑色地面影ブロブ」（`design/assets.md`「isolated on a plain neutral flat background」仕様違反、`design/art-bible.json`パレット逸脱）は本iterationでも寸分違わず存在しており、未解消。gates.md AR-ASSET観点1（スタイル一致・パレット逸脱）に抵触したままである。

**IMG-01 — 再生成の実行可否（インフラ状況）の再確認**
`state/asset-routing.json checks.ideogram` は引き続き `{"key":true,"auth":"ok","plan_tier":"unknown"}`（405プローブによる疎通確認のみで実キー有効性は未保証）、`fallbacks.image_sprite=["ideogram:direct"]`。実行環境の `IDEOGRAM_API_KEY` は本レビュー時点でも**文字数0**（空文字）を再確認した。iteration3〜4でブロックされたフォールバック経路（Ideogram直API）は依然として実行不能な状態が継続している。一方、iteration4が提案したローカルretouch案（(a): IMG-03 revision2で実績のあるPIL colorsysベースのHSVマスク処理で該当楕円領域のみalpha=0へ切替え）はAPIキーに依存せず即時実行可能であり、本iteration時点でも未実施のまま。

**IMG-02（revision2＝メタデータのみ、画像本体sha256実測 `30f969a665a14b3cc9c2871d768eab254658da23ea028c68e7ea94ca6343ef50`）— 独立再検証: PASS（変更なし）**
sha256完全一致（iteration1以降、画像本体は不変）。緑バンド0/90,879px（0.00%）、外周フリンジ0/22,223px（0.00%、iteration3で記録されたフルスキャン分母22,223pxと完全一致）、4隅alpha=0、1024x1024維持。パレットクラスタは紫〜青系（hue225–277.5°）のみで許容範囲内。

**IMG-03（revision2, sha256実測 `e84526c06f9daaa1efb310f652c6a6a1cdbdc6afbb2f4f045fc7d2404cd57510`）— 独立再検証: PASS（変更なし）**
sha256完全一致。緑バンド0/28,252px（0.00%）、外周フリンジ0/2,835px（0.00%、iteration4記録の分母2,835pxと完全一致）、4隅alpha=0、512x512維持。主要クラスタはシアン本体（hue175–180°、`#4FE8E2`距離47.7）・マゼンタハイライト（hue300°、`#E62284`距離111.1。ファセットハイライトの陰影変動として既存iterationが許容済みの範囲）のみで緑系残存なし。

**MANIFEST整合性・予算**
34行全件JSONパース成功（parseエラー0）。`cost_usd`合算 $1.19394（iteration3/4報告値と完全一致、変更なし）、`state/budget.txt`上限$100に対し超過なし。

### 総合判定: CONCERNS
理由: IMG-02・IMG-03は独立再検証で完全にPASSを維持。IMG-01は iteration3 で新規検出・iteration4 で独立再確認・本iterationでも三度目の独立再確認により**寸分違わず同一の緑色地面影ブロブ欠陥（不透明画素7.37%、パレット距離74.8–87.5）が未解消**であることを確認した。これは機械計測で明確に立証できる個別資産のスタイル逸脱であり、スタイルロック自体の欠陥やライセンス違反ではないため REJECT ではなく CONCERNS とする。

**review-loops.md 手続き上の重要な指摘（優先度: 最高・プロセス）**: 本artifactは iteration4（2026-07-10T18:15:00Z）の時点で既に「review-loopsの定める『3回不合格→fallbackプロバイダへ切替後さらに1回』の最終反復」かつ「MAX_ITER到達かつ非APPROVEのためエスカレーション対象」と明記されている。IMG-01・IMG-02・IMG-03いずれも iteration4 以降ファイルの変更が一切ないことを本iterationで確認した以上、これは新規のrevise対応の結果ではなく**同一の未解決状態に対する重複レビュー呼び出し**である。呼び出し元workflowが本レビューを「iteration 1」（＝MAX_ITER=3の新しいカウントの起点）として扱っている場合、IMG-01に対して自動的にさらなる再生成試行を繰り返し要求する恐れがあるが、review-loops.md の定めではIMG-01の自動ループは既に**iteration4で使い切っている**。これ以上の自動revise呼び出しはMAX_ITER規約違反となるため、workflow側はこの状態を新規ループの再起点にせず、iteration4が既に指示した通り**Checkpointでの人間判断（(a) IDEOGRAM_API_KEYの設定または(b) ローカルretouchの実施どちらを取るか、あるいは(c) `design/assets.md`のIMG-01状態を`must-replace`へ変更して現状のまま出荷対象から除外するか）に委ねること**を強く推奨する。

### 再生成指示（優先度順・ただし上記MAX_ITER注記を踏まえ、workflowが追加の自動ループを起動する場合の参考情報として記載）
1. **[IMG-01・優先度高]** 足元の緑色地面影ブロブ（x:266–758, y:888–960、不透明画素の7.37%）を除去すること。
   - (a) **ローカルretouch（推奨・即時実行可能）**: IMG-03 revision2の手法に倣い、該当矩形領域内かつhue90–160°・sat>0.3・val>0.3に一致する画素のみをalpha=0へ切替えるマスク処理を適用。処理前にキャラクター本体（navy/blue系hue帯 約200–230°）が同条件に誤って一致しないことを確認すること。
   - (b) **API再生成**: `IDEOGRAM_API_KEY`（現在0文字＝未設定）を実行環境に正しく設定するか、primaryルート（`fal:ideogram-v3-transparent`）でiteration3提案済みのネガティブ制約（"no ellipse, disc, oval, puddle, platform, drop-shadow, or ground-plane shape of any color beneath or around the feet"）を用いて再試行する。
   - いずれの場合も再生成/retouch後は同一手法（緑バンド比率・外周フリンジ・4隅alpha・sha256突合）で再検証すること。

### disclosures（再生成不要・人間開示のみ）
- IMG-01/IMG-02/IMG-03: いずれもMANIFESTに `cost_estimated:true` の記録あり（fal.aiの確定請求額ではなく推定コスト）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- **IMG-01はMAX_ITER到達済みかつ未解消（人間判断が必要）**: iteration4で確定したエスカレーション状態が本iterationでも変わらず継続している。緑色地面影ブロブは技術的には再生成/retouchで是正可能だが、フォールバック経路（`ideogram:direct`）は`IDEOGRAM_API_KEY`未設定（実測0文字）により実行不能なまま。人間側の意思決定（APIキー設定／ローカルretouch実施の指示／`must-replace`への状態変更受容のいずれか）が無い限りこれ以上の自動対応は行えない。3D入力としての実害は無いことをiteration3で確認済み（現行MDL-01は本欠陥混入前の旧画像から生成済み）だが、IMG-01自体がHUD等で単体使用される場合は視認リスクが残る。
- **review-loops.md 手続き上の懸念（上記総合判定内で詳述）**: 呼び出し元プロンプトが本呼び出しを「iteration 1」相当として扱っている場合、IMG-01のMAX_ITER超過状態を誤って新規カウントでリセットし追加の自動再生成ループを起動するリスクがある。workflow側での確認を推奨する。
- ファイル命名（`img-` プレフィクスが`.claude/rules/assets.md`の規定プレフィクス外）: iteration1で記録済み・art-director権限外のため対応見送りのまま継続（新規指摘ではない）。

## AR-ASSET iteration 3 — CONCERNS
- 日時: 2026-07-10T17:20:00Z
- 対象: iteration2 CONCERNS対応の独立再検証（IMG-01 revision2 `img-01-hero-concept.png` sha256 `6da21f04...0cef`／IMG-02 revision2 `img-02-swarmer-concept.png` sha256 `30f969a6...3ef50`）＋バッチ全体の再確認（IMG-03 revision2 `img-03-crystal-icon.png` sha256 `e84526c0...5750`）。`design/assets.md` の状態は IMG-01/02/03=`generated`、IMG-04=`planned`（対象外）。git log確認: 直近のasset関連コミットは `825d3e5 fix(assets): resolve AR-ASSET iteration2 findings for IMG-01/IMG-02` のみで、iteration2以降のMANIFEST新規追記行なし。3Dモデル/アニメ（MDL-01/02, ANM-01〜04）は別レーン（AR-ASSET 3D観点）のためスコープ外、SFXは別artifact（assets-audio-prototype.md）のため扱わない。
- 検査方法: producerの自己申告（MANIFESTの`alpha_verification_note`等）を鵜呑みにせず、全項目を独立再計測した。(1) `shasum -a 256` で3ファイルのsha256を実測しMANIFEST記載値と突合。(2) `magick identify` でサイズ・チャンネル数を実測。(3) Pillow+numpyでフルスキャン（サンプリングなし）のアルファ境界白フチ検査（境界半透明画素[0<a<255]×白系画素[r,g,b>230かつa>15]×4近傍にalpha=0が存在、の3条件ANDで独自に再実装・再計測）。(4) 8bit量子化パレットクラスタ抽出＋HSVによるhue/sat/val算出＋13色パレットへのRGBユークリッド距離計算（IMG-01・IMG-02双方）。(5) 検出した外れクラスタの空間分布をベクトル化hue計算でマスク化し座標範囲を特定。(6) 該当領域をクロップし透過部をマゼンタ/ダークグレー背景に合成した検証画像を書き出し、Readツールで直接目視確認（`/tmp/ar-asset-review-i3/img01-feet-crop.png` 等）。(7) `state/asset-routing.json` でplan_tier実測値・shippableフラグを突合。(8) `state/budget.txt` とMANIFEST全行のcost_usd合算を再照合。

### 観点別結果

**IMG-01 revision2 — iteration2指摘（外周アルファフリンジ）の対応確認: PASS**
- sha256実測 `6da21f04cbd075bd012a81613ea29ca2379d9827ca5ed706eb35e848f5f0cefe` はMANIFEST revision2エントリと完全一致。1024x1024・RGBA・4隅alpha=0を確認。
- 独立再実装した外周フリンジ検査: 境界半透明画素23,947px（MANIFESTの自己申告値と一致）中、外周フリンジ0px（**0.00%**）。iteration2で検出された4.87%の欠陥は解消を独立確認した。
- provenance: `plan_tier:"prepaid"`（`state/asset-routing.json checks.fal.plan_tier`実測値と一致）・`alpha_verified:true`が記録されており、iteration2指摘2は解消。

**IMG-02 revision2 — iteration2指摘（provenance記録漏れ）の対応確認: PASS（+ 測定方法の相違を記録）**
- sha256実測 `30f969a665a14b3cc9c2871d768eab254658da23ea028c68e7ea94ca6343ef50` はMANIFEST記載値と完全一致（画像本体は旧revision1から不変のまま、意図通り）。
- `plan_tier:"prepaid"`・`alpha_verified:true` が記録されており是正を確認。外周フリンジは独立検査でも0px（0.00%）でPASSを再確認。
- **プロセス上の付随所見（ブロッキングではない）**: 独立再計測した境界半透明画素の総数は22,223pxで、既存レビュー記録の自己申告値7,804pxと乖離がある（フリンジ判定の分子はいずれも0のため合否結論には影響しない）。原因は未特定（測定手法の違いの可能性）。今後同種の計測を行う際は、境界画素の分母計測ロジックも再現可能な形でMANIFEST/レビューに残すことを推奨する。

**IMG-03 revision2 — 変更なしの再確認: PASS**
- sha256実測 `e84526c06f9daaa1efb310f652c6a6a1cdbdc6afbb2f4f045fc7d2404cd57510` はMANIFEST記載値と完全一致。目視合成画像でもシアン本体・マゼンタハイライト・薄い自己色相グロー以外の要素なし、緑系統の残存なし。iteration2判定を維持。

**IMG-01 — 新規指摘: 足元の緑色オフパレット地面影ブロブ（観点1: スタイル一致・パレット逸脱）— FAIL**

8bit量子化パレットクラスタ分析で、不透明画素（245,605px）中 `#50A860`（3.94%）・`#50B060`（2.39%）の2クラスタ（合算6.33%、hue検出ベースの厳密マスクでは7.33%＝17,995px）が検出された。hue≈130–131°（緑）、彩度0.52–0.55、明度0.66–0.69で、13色パレットいずれとも一致せず最近傍（`#7FE850` アリーナ床緑）との距離が**74.8–81.0**（同一画像内で許容している陰影バリエーション帯 距離25.7–48.0 の約2倍、AR-BIBLE revision履歴で確立された許容上限70.8さえ超過）。
座標マスクで空間分布を特定した結果、y:888–961・x:265–759（画像下端、両足のすぐ下）に完全に集中しており、クロップ画像を目視確認（`/tmp/ar-asset-review-i3/img01-feet-crop.png`）したところ、**両足の下に横長の緑色の楕円形シェイプ（地面/接地影のブロブ）が明確に描画されている**ことを確認した。これは色調の陰影バリエーション（AOダークニング）ではなく、独立した地面/影の図形要素である。
これは design/assets.md IMG-01 プロンプト草案および実際の生成プロンプト（MANIFEST記録）が要求する「isolated on a plain neutral flat background」（プレーンな背景に単体で分離）「no ground/floor/scene elements」に反する。iteration2対応時の art-director 自身の参考所見（「次回AR-ASSETで問題視された場合は別途対応する」）で「見送り」と記載されていた事項であり、本レビューが該当する「次回」にあたるため、正式な不合格指摘として提起する。
**技術的リスク（美観以外）**: MANIFEST上 IMG-01 の `purpose` は `"image-to-3d input for MDL-01"` と記録されている。現行 MDL-01（`generated_at:"2026-07-10T06:22:03Z"`）は本revision2（`generated_at:"2026-07-10T06:42:34Z"`）より前の画像から生成されており本欠陥の影響は受けていないことをタイムスタンプで確認したが、IMG-01の現行出荷ファイルとMDL-01生成に実際使われた画像が異なるものになっている（provenanceの実体乖離）。将来 IMG-01 を再度3D入力として使う場合、この緑ブロブが不要なジオメトリ（足元に融着した緑の円盤）として3D再構成される risk がある。

**IMG-01 — シルエット可読性・禁止要素チェック: PASS（緑ブロブ以外）**
64px相当への概念確認および全体構図の目視で、人間NPC・ゼッケン・スポーツユニフォーム等の禁止要素は検出されず。Aポーズ（両腕を体側からやや離した姿勢、y=340–500水平スキャンで左腕・胴体・右腕が独立した不透明ランとして分離）もiteration2確認内容を維持。

**IMG-02 — 追加確認: PASS**
underbelly/接地部クロップ目視（`/tmp/ar-asset-review-i3/img02-underbelly-crop.png`）でIMG-01のような地面影ブロブは確認されず、四足とも透過背景へクリーンに接続。パレットクラスタは全て紫〜青系統（hue232.5–277.5°）で13色パレットとの距離33.6–72.4、既存の陰影許容範囲内。

### 総合判定: CONCERNS
理由: iteration2で指摘した外周アルファフリンジ（IMG-01）・provenance記録漏れ（IMG-01/IMG-02）はいずれも独立再検証でPASSを確認した。IMG-02・IMG-03は新規指摘なし（IMG-02はフルスキャン分母の記録差異のみ、ブロッキングではない）。一方でIMG-01に新規の機械検証可能な欠陥（不透明画素7.33%を占める緑色オフパレット地面影ブロブ、パレット距離74.8–81.0、「isolated background」仕様違反）を検出したため、REJECTではなくCONCERNSとし、IMG-01を再生成対象とする。

### 再生成指示（優先度順）
1. **[IMG-01・優先度高]** 足元の緑色地面影ブロブを除去して再生成すること。プロンプト修正案: 現行プロンプト（IMG-01 revision2で使用したalpha縁修正込みの全文）の末尾にさらに以下を追記する: `"The character must be rendered fully isolated with absolutely no ground plane, floor disc, drop shadow, contact-shadow ellipse, or any shape of any color (including green) beneath or around the feet — the alpha must cut cleanly to fully transparent at the sole of each foot, with nothing but empty transparent space surrounding the character on all sides."` 再生成後は本レビューと同一手法（8bit量子化パレットクラスタのHSV hue解析で緑バンド[hue 90–160°, sat>0.3, val>0.3]の不透明画素比率が0%近辺であること、および足元クロップの目視確認で地面影ブロブが存在しないこと）を再実施して確認すること。
   - **MAX_ITER注記（review-loops.md）**: IMG-01はiteration2で外周アルファフリンジ指摘（1回目のCONCERNS、対応済み・解消確認済み）、本iteration3で緑地面影ブロブ指摘（2回目のCONCERNS、別種の欠陥）。同一ルート（`fal:ideogram-v3-transparent`、Primary）での残り再生成余地はMAX_ITER 3のうち1回。次回（iteration4相当）も本欠陥または新規欠陥でCONCERNS/REJECTとなった場合は、`state/asset-routing.json fallbacks.image_sprite`（`ideogram:direct`）へのフォールバック切替を明示的に指示すること。

### disclosures（再生成不要・人間開示のみ）
- IMG-01/IMG-02/IMG-03: いずれもMANIFESTに `cost_estimated:true` の記録あり（fal.aiの確定請求額ではなく推定コスト）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- IMG-02: フルスキャンで測定した境界半透明画素の総数（本レビュー実測22,223px）が既存レビュー記録の自己申告値（7,804px）と乖離している（外周フリンジの分子はいずれも0のため合否判定への影響はない）。measurement-methodologyの記録改善が望ましいという運用上の所見であり、資産の再生成理由にはならない。
- 予算: `game/_generated/MANIFEST.jsonl` 全20行の `cost_usd` 合算 $1.19394、`state/budget.txt` 上限 $100 に対し超過なし。

- 対応: **ブロック（未解決）**（art-director、2026-07-10T17:45:00Z）。指摘1（IMG-01足元の緑色地面影ブロブ）に対応するため、iteration3注記の指示通り同一ルート（`fal:ideogram-v3-transparent`、Primary）はこれ以上使わず `state/asset-routing.json fallbacks.image_sprite`（`ideogram:direct` = Ideogram V3 公式REST直 `api.ideogram.ai/v1/ideogram-v3/generate`）へ切替えて1回だけ再生成を試行した。プロンプトは指摘記載のネガティブ制約（地面影/接地シャドウ禁止）を追記した全文（`.env` 経由の `IDEOGRAM_API_KEY` を `Api-Key` ヘッダで使用、seed 421004）。結果: **HTTP 401 Unauthorized**（レスポンスボディ `{"type":"about:blank","title":"Unauthorized","detail":"No authorization token provided","status":401}`）。原因調査: `.env` 内 `IDEOGRAM_API_KEY=` の値部分が空文字（`${#IDEOGRAM_API_KEY}` 実測=0。対照として同一手法で確認した `FAL_KEY` は69文字で正常）。`state/asset-routing.json checks.ideogram` は preflight時点で `"key":true,"auth":"ok"`（GETプローブ405で認証層OKと判定）と記録されているが、現在の実行環境の `.env` にはキー値が実際には設定されていない状態であり、preflight結果と実行時環境の間に乖離がある。指定されたFallbackルート（`ideogram:direct`）はこの乖離により実行不能と判明したため、握り潰さずHTTPステータスとともにここに記録し、ルーティング表に無い代替プロバイダ（三次のOpenAI gpt-image-1.5もMANIFEST/asset-routing.json記載の通り`auth:"invalid"`で既に除外済み、ローカル縮退はasset-routing.jsonのfallbacks配列に明記が無いため独断で使用しない）は使用せず、IMG-01の再生成を**未実施のまま**呼び出し元へエスカレーションする。IMG-01は現行revision2（sha256 `6da21f04...0cef`、足元緑ブロブ7.33%を含む）のまま据え置き、`design/assets.md` の状態も `generated`（要改善）のまま変更しない。呼び出し元での対応候補: (a) `IDEOGRAM_API_KEY` を実行環境に正しく設定してから本再生成のみ再試行、(b) `state/asset-routing.json` を再preflightしてルート実態を最新化、(c) 人間判断でこのままのIMG-01を条件付き受容（3D入力としては現行MDL-01が本欠陥混入前の旧画像から生成済みで実害なしと確認済み。ただしHUD等での単体使用時は緑ブロブが視認され得る）。

## IMG-01 revision3 対応（呼び出し元指示による再開・iteration4/5指摘への最終対応）
- 対応: **解消（対応済み）**（art-director、2026-07-11T00:00:00Z）
- 経緯: iteration4/5で確定した「MAX_ITER到達・エスカレーション状態」（review-loops.md「3回不合格→fallbackプロバイダへ切替後さらに1回」を消化済み）に対し、呼び出し元より明示的に再生成指示を受けた。フォールバック経路（`ideogram:direct`）は本対応時点でも `IDEOGRAM_API_KEY` 実測0文字により引き続き実行不能なことを再確認したため、呼び出し元指示通り**同一ルート（`fal:ideogram-v3-transparent`、Primary、ルート変更なし）を維持**して対応した。
- **API再生成 3試行（詳細はMANIFEST.jsonl IMG-01 revision3エントリの`revision_reason`参照）**:
  1. seed 421005（`expand_prompt:false`、否定文をpromptに埋め込み）→ 構図破綻（第二の紫クリーチャー＋リード紐＋白楕円地面影が出現、iteration4/5指摘より悪化）で**破棄**。
  2. seed 421006（`negative_prompt`専用フィールドを正しく使用、`expand_prompt`既定値=true/MagicPrompt有効のまま）→ 構図・意匠は正しいが足元にグレー系ドロップシャドウ楕円が残存（色は変わったが「isolated on a plain neutral flat background」違反という同一カテゴリの欠陥）。
  3. seed 421007（"flat die-cut sticker cutout"framingを追加）→ 地面影は解消したが厚い白フチのステッカー輪郭ハロー（フルスキャン外周フリンジ19.5%）が新規発生、iteration2で解消済みのアルファフリンジ欠陥への回帰のため**破棄**。
  - **技術的知見（今後の生成に活用）**: `fal-ai/ideogram/v3/generate-transparent` の実OpenAPIスキーマ（`https://fal.ai/api/openapi/queue/openapi.json?endpoint_id=...`で取得）を確認した結果、`style_type`/`style_codes`/`style_reference_images`は実在パラメータではなく黙って無視されることが判明した（過去のMANIFEST行の`style_codes`記載は art-bible.json 対応付けのための記録であり、実際のAPIリクエストには影響していなかった）。実パラメータは `prompt`/`negative_prompt`/`seed`/`rendering_speed`/`expand_prompt`/`aspect_ratio`/`num_images`/`sync_mode` のみ。除外指定は本文への埋め込みでなく`negative_prompt`フィールドを使うべきこと、また`expand_prompt`（既定true=MagicPrompt）を無効化すると逆に構図ドリフトが悪化する傾向を確認した。
- **ローカルretouch（試行2の出力を採用しIMG-03 revision2の手法を応用）**: PIL+numpyスクリプトで (a) 不透明画素からのPIL `ImageFilter.MaxFilter`膨張距離7px超の孤立半透明塊（足元ドロップシャドウ楕円、除去44,092px）をalpha=0化、(b) 白系境界フリンジ画素（多段階アンチエイリアス勾配の"オニオン層"）を収束するまで反復除去（14反復でfringe px 314→0に収束）。retouch前後で追加API呼び出しは無し。
- **独立再検証（iteration3/4/5と同一手法で自己検証、次回AR-ASSETで art-reviewer による独立確認が必要）**: 4隅alpha=0、緑バンド(hue90-160°,sat>0.3,val>0.3)不透明サンプル93,279px中0px（0.00%、iteration3/4/5指摘は解消）、外周フリンジ フルスキャン0/27,483px（0.00%、iteration2水準を回復）、1024x1024/RGBA維持。パレット主要クラスタは13色パレットとの距離9.0–85.8の範囲内（最大値85.8はトリムハイライトの淡いラベンダー掛かった陰影バリエーションと判断。iteration3/4/5が許容したIMG-03マゼンタハイライト距離111.1より小さい）。
- ファイル: `game/_generated/images/img-01-hero-concept.png`（旧sha256 `6da21f04...0cef` → 新sha256 `e2a46e48912de612455cc20ae4af8baac36a101d8edf6a37a7b4b56868e6a18f`）。MANIFEST.jsonlにrevision3として追記済み（`revision_of_sha256`で旧revision2と連鎖）。
- **未解消・見送り事項**:
  - トリムハイライト色が指定`#F2F5FA`から視覚的に淡いラベンダーへややシフトしている点（パレット距離85.8）は、3回のAPI試行を通じ完全な色一致を得られなかったため、シェーディングバリエーションとして受容し見送った。次回AR-ASSETで問題視された場合は追加retouch（HSVでの色相シフト補正）を検討する。
  - ファイル命名（`img-`プレフィクスが`.claude/rules/assets.md`規定プレフィクス外）は iteration1 からの既知事項で art-director 権限外のため継続して見送り。
  - `design/assets.md` のIMG-01状態は既存の`generated`のまま変更なし（本対応で欠陥は解消したため`must-replace`への変更は不要と判断）。
- 予算: 本対応で3試行分のAPI費用 $0.18（cost_estimated）を追加。`game/_generated/MANIFEST.jsonl` 全35行の `cost_usd` 合算 $1.37394、`state/budget.txt` 上限 $100 に対し超過なし。
- **次回アクション**: 本revision3は art-director の自己検証のみで、art-reviewerによる独立AR-ASSET再判定は未実施。次回AR-ASSET呼び出しで独立確認を受けること。

## AR-ASSET iteration 6 — APPROVE
- 日時: 2026-07-11T11:10:00Z
- **iteration番号についての手続き上の注記**: 呼び出し元プロンプトは本レビューを「iteration 2」として追記するよう指示していたが、本ファイルには既に iteration 1〜5（画像バッチ）と「IMG-01 revision3 対応」producer応答セクションが記録されており、既存最大値5に対し「iteration 2」で追記すると review-loops.md の逐次追記規約に反し既存履歴と矛盾する。iteration5が確立した先例（既存最大番号+1を使う）に倣い、実際の次番号（6）を使用した。
- 対象: `game/_generated/MANIFEST.jsonl` 全35行（末尾IMG-01 revision3行を含む）中の画像資産 = IMG-01（`img-01-hero-concept.png`, revision3, 「次回AR-ASSET呼び出しで独立確認を受けること」と producer 自身が明記した未検証分の独立検証）・IMG-02（`img-02-swarmer-concept.png`, revision2, 変更なしの再確認）・IMG-03（`img-03-crystal-icon.png`, revision2, 変更なしの再確認）の3点。IMG-04は`design/assets.md`で引き続き`planned`（`game/_generated/images/`に実ファイル無し、`grep`で確認）のため対象外。SFX/BGMは別artifact（`assets-audio-prototype.md`）、3Dモデル/アニメ（MDL-01/02, ANM-01〜04。lines 8-34の revision履歴・Unity取込後検証を含む）も別artifact（3D観点レビュー）のスコープのため本バッチでは扱わない（過去iterationの一貫した取り扱いを継続）。
- 検査方法: producerの自己検証（IMG-01 revision3の`alpha_verification_note`記載値）を鵜呑みにせず独立に再計測した。(1) `shasum -a 256` で3ファイルのsha256実測とMANIFEST該当行（IMG-01は`revision:3`行、IMG-02/IMG-03は`revision:2`行）の突合。(2) `magick`不要、Pillow+numpyで全画素をベクトル化計算（サンプリングなしのフルスキャン）し、RGBAモード・寸法・4隅アルファを実測。(3) numpy によるHSV変換（colorsysと同じ変換式を独自にベクトル化実装）で不透明画素（alpha==255）中の緑バンド（hue90–160°, sat>0.3, val>0.3）比率と空間bboxを算出。(4) フルスキャンでの外周アルファフリンジ判定（境界半透明画素[0<a<255]×白系画素[r,g,b>230かつa>15]×4近傍いずれかにalpha=0、の3条件AND）。(5) 8bit（16刻み）量子化パレットクラスタ抽出＋13色パレットへのRGBユークリッド距離計算。(6) IMG-01の足元領域（y:820–1024）をマゼンタ背景に合成したクロップ画像、および全身をダークグレー背景に合成した縮小画像、64px nearest-neighbor縮小をマゼンタ背景に合成した画像をそれぞれ書き出しReadツールで直接目視確認（`/tmp/ar-asset-i6/img01-feet-crop-magenta.png`、`img01-full-darkbg.png`、`img01-64px-magentabg.png`）。(7) `python3 -c` でMANIFEST全35行のJSONパース検証（parseエラー0件）と`cost_usd`合算。(8) `state/asset-routing.json`の`checks.fal.plan_tier`実測値・`shippable.image_sprite`を再確認。

### 観点別結果（独立再検証）

**IMG-01（revision3, sha256実測 `e2a46e48912de612455cc20ae4af8baac36a101d8edf6a37a7b4b56868e6a18f`）— iteration3/4/5指摘（緑地面影ブロブ）・iteration2指摘（外周アルファフリンジ）とも解消を独立確認: PASS**
- sha256実測はMANIFEST revision3エントリと完全一致。1024x1024・RGBA・4隅alpha=0を確認。
- 緑バンド（hue90–160°, sat>0.3, val>0.3）: 不透明画素373,137px中0px（**0.00%**）。iteration3/4/5で繰り返し検出された足元緑地面影ブロブ（旧revision2で7.33–7.37%）は完全に解消。
- 外周アルファフリンジ: 境界半透明画素27,483px中0px（**0.00%**）。iteration2で検出・解消済みの欠陥も再発なし。
- 目視確認（`/tmp/ar-asset-i6/img01-feet-crop-magenta.png`）: 両足ともマゼンタ背景に対しクリーンなシルエット境界で接続しており、地面影・ドロップシャドウ・楕円ブロブの類は一切存在しない。全身像（`img01-full-darkbg.png`）でも単体キャラクターのみで、iteration5以前の試行1で発生した第二クリーチャー・リード紐等の混入は無い。64px nearest-neighbor縮小（`img01-64px-magentabg.png`）でも縦長二足人型シルエット・青系装甲の色ブロッキングが明瞭に判別可能——シルエット可読性PASS。禁止要素（人間NPC・ゼッケン等）も確認されず。
- パレットクラスタ: 主要な装甲色クラスタ `RGB(0,96,176)`(30.13%, `#164583`距離56.9)・`RGB(0,128,192)`(19.76%, `#3488D1`距離55.3)・`RGB(0,128,208)`(5.60%, `#3488D1`距離52.6) は同色相帯のシェーディングバリエーションとして許容範囲内。暗部クラスタ `RGB(32,48,96)`(11.09%)・`RGB(32,32,96)`(6.56%) も青〜紺の陰影として妥当。
- **トリムハイライト色のわずかな色相ずれ（観察事項・非ブロッキング）**: トリム/ハイライト系クラスタが2つに分離しており、`RGB(224,224,240)`(9.43%, hue240°, `#F2F5FA`距離29.4)は目標に近いが、`RGB(192,176,240)`(11.29%, hue255°, sat0.27, `#F2F5FA`距離85.8)はやや紫（ラベンダー）寄りに振れている。ただし(a)本クラスタは肩・兜のハイライト部という限定的な面積の副次要素であり、主要なシルエット読み取り（青系装甲）を阻害しない、(b)距離85.8は本レビュー履歴内で既に許容してきたシェーディング/ハイライト変動の上限（IMG-03マゼンタファセットハイライト距離111.1、iteration3/4/5で許容確定済み）を下回る、(c)色相255°はhero主色（#3488D1系、hue約205°）や敵主色（#8B12A5、hue289.4°）のいずれとも十分離れており役割別色分けの混同リスクは無い、(d)目視確認でも「白っぽいハイライト」として読み取れ画風ブレとして破綻していない——以上より再生成指示の対象とはせず、disclosuresとして開示のみ行う（art-director既知の申し送り事項、iteration3/4/5確立の許容基準との内部整合性を優先）。
- provenance: `plan_tier:"prepaid"`（`state/asset-routing.json checks.fal.plan_tier`実測値と一致）・`alpha_verified:true`・`revision_of_sha256`によるrevision2からの連鎖・`review_ref`記載とも充足。

**IMG-02（revision2, sha256実測 `30f969a665a14b3cc9c2871d768eab254658da23ea028c68e7ea94ca6343ef50`）— 独立再検証: PASS（変更なし）**
sha256完全一致（iteration1以降、画像本体は不変）。緑バンド0/363,404px（0.00%）、外周フリンジ0/22,223px（0.00%、iteration3/5記録の分母と完全一致）、4隅alpha=0、1024x1024維持。パレットクラスタは紫〜青系（hue225–277.5°）のみで13色パレットとの距離30.0–67.8、既存の陰影許容範囲内。

**IMG-03（revision2, sha256実測 `e84526c06f9daaa1efb310f652c6a6a1cdbdc6afbb2f4f045fc7d2404cd57510`）— 独立再検証: PASS（変更なし）**
sha256完全一致。緑バンド0/112,981px（0.00%）、外周フリンジ0/2,835px（0.00%、iteration4/5記録の分母と完全一致）、4隅alpha=0、512x512維持。主要クラスタはシアン本体（hue174.5–180°、`#4FE8E2`距離47.7–63.1）・マゼンタハイライト（hue300°、距離111.1、既許容）・白グロス（hue300°, sat0.13, 距離38.4）のみで緑系残存なし。

**MANIFEST整合性・予算**
35行全件JSONパース成功（parseエラー0）。`cost_usd`合算 $1.37394（iteration5報告時から変わらずIMG-01revision3試行分$0.18込みで確定）、`state/budget.txt`上限$100に対し超過なし。`state/asset-routing.json` `shippable.image_sprite:true`を確認、shippable:falseルート由来の資産なし。

### 総合判定: APPROVE
理由: IMG-01（revision3）・IMG-02（revision2）・IMG-03（revision2）とも独立再検証で機械検査可能な全項目（アルファ縁品質・緑バンド/パレット逸脱・仕様一致・provenance）がPASSした。IMG-01はiteration2（外周フリンジ）→iteration3/4/5（緑地面影ブロブ）と重ねた指摘がいずれも本revision3で解消されたことを独立に確認した。IMG-01のトリムハイライト色の軽微な色相ずれ（クラスタ距離85.8、面積11.29%）は本レビュー履歴が既に確立した許容基準（IMG-03距離111.1を許容済み）の範囲内であり、役割別色分けの混同や画風破綻を伴わないため再生成指示は出さずdisclosuresとして開示するに留める。IMG-04は`planned`のため引き続き対象外。本バッチに不合格資産（failedAssets）は無い。

### disclosures（再生成不要・人間開示のみ）
- IMG-01/IMG-02/IMG-03: いずれもMANIFESTに `cost_estimated:true` の記録あり（fal.aiの確定請求額ではなく推定コスト。IMG-01revision3は3試行分$0.18を含む）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- IMG-01（revision3）: トリムハイライト色の一部クラスタ（不透明画素の11.29%、`RGB(192,176,240)`）が指定`#F2F5FA`からラベンダー寄りに色相シフト（パレット距離85.8）している。3回のAPI再生成試行を経てもなお残る既知の簡略化で、art-director自身が申し送り済み。本レビューでは画風破綻・役割色混同のリスクなしと判断しPASS扱いとしたが、次回何らかの理由でIMG-01を再生成する機会があれば併せて是正を検討されたい。
- ファイル命名（`img-`プレフィクスが`.claude/rules/assets.md`規定プレフィクス[`sprite-`/`tile-`/`ui-`等]の外）: iteration1からの既知事項でart-director権限外のため継続して見送り（新規指摘ではない）。
- 3Dモデル/アニメ（MDL-01/02, ANM-01〜04）はMANIFEST上でrevision3/4としてUnity取込後検証（gates.md AR-ASSET※節・Integrateフェーズの責務）まで記録が進んでいるが、本ファイルのスコープ（画像資産）外のため判定に含めない。別artifact（3D観点のAR-ASSETレビュー）での確認状況を確認されたい。
- 予算: `game/_generated/MANIFEST.jsonl` 全35行の `cost_usd` 合算 $1.37394、`state/budget.txt` 上限 $100 に対し超過なし。

- 対応: （該当なし。本iterationはAPPROVEのため producer 対応不要）
