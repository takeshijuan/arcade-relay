# レビュー履歴: AR-ASSET バッチ一貫性チェック（style drift 検出・pass 1）

対象: `game/_generated/MANIFEST.jsonl` に記録された全画像資産（IMG-01〜08。最終revisionのみ）+ 全3Dモデルのレンダリングプレビュー（MDL-01〜05、`game/_generated/previews/*-preview.png`）を**生成順（generated_at の最終値）に並べた横断チェック**。
個別バッチ判定（`state/reviews/assets-images-prototype.md` / `assets-images.md` / `assets-models-prototype.md` / `assets-models.md`）は全てAPPROVE済み — 本レビューはそれらを覆すものではなく、**単一バッチ内では検出できない「同一色ファミリーの資産間ズレ（時系列ドリフト）」のみ**を対象にした追加チェック（pass 1）。
照合元: `design/art-bible.json`（palette / style_block）、`design/art-bible.md`、`design/assets.md`。

## AR-ASSET iteration 1（pass 1: バッチ一貫性） — CONCERNS

- 日時: 2026-07-22T08:16:16Z
- 手法: `game/_generated/MANIFEST.jsonl` の `generated_at`（revisionがある資産は最新行）でIMG-01〜08・MDL-01〜05を並べ、`/tmp/ar-batch-check/chrono-montage.png` に時系列コンタクトシートを作成して目視スキャン → 疑わしい色ファミリーをPython(PIL/numpy)で独立測色。生成順（最終値）:
  IMG-03(07-21 09:10) → IMG-04(07-21 09:10) → MDL-05(07-21 09:25) → IMG-08(07-21 09:58) → MDL-01(07-21 19:10) → MDL-02(07-21 19:10) → MDL-03(07-21 19:10) → MDL-04(07-22 07:12) → IMG-02(07-22 16:10) → IMG-05(07-22 16:10) → IMG-07(07-22 16:10) → IMG-01(07-22 18:40) → IMG-06(07-22 18:55)
- 一時ファイルは `/tmp/ar-batch-check/` に保存（対象ディレクトリは汚していない）。

### 機械検査で検出した2件のクロスバッチ・パレットドリフト

**1. コアエネルギー専有色 `#ABFFFF`（MDL-05クリスタル本体 / IMG-04 essence / IMG-08 core-hp / IMG-06 UPG-03）の彩度が生成順で単調にズレている**

独立測色（HSV、target hue180°/sat0.329/val1.000）:

| 資産 | 生成順 | 測定領域 | hue | sat | val | 所見 |
|---|---|---|---|---|---|---|
| MDL-05（core-crystal render） | 3番目 | 背景差分+輝度上位60% | 179.0° | **0.161** | 0.838 | 最も淡い（ほぼ灰白） |
| IMG-08（core-hp icon） | 4番目 | 不透明領域全体 | 184.5° | 0.230 | 0.902 | |
| IMG-04（essence icon） | 2番目 | 不透明領域全体 | 181.0° | 0.250 | 0.910 | |
| IMG-06 UPG-03（icon-upgrades crop） | 13番目（最新） | 右1/3クロップ・sat>0.25高彩度サブセット | 189.0° | **0.542** | — | 最も濃い（既承認水準の約2.2倍） |

`design/art-bible.md`「パレット」節は `#ABFFFF` を「画面内で他要素に流用しない専有色」と明示している。個別バッチ審査（`assets-images-prototype.md` / `assets-images.md`）はいずれもIMG-04・IMG-08・IMG-06をそれぞれ**単独でflat hexターゲットとの距離**のみ判定し合格させたが、**資産間で直接比較した独立測色は本チェックが初めて**。結果、同一の「専有色」が実際には資産ごとに彩度が単調に濃くなる時系列ドリフトを起こしており（MDL-05:0.161 → IMG-08:0.230 ≈ IMG-04:0.250 → IMG-06:0.542）、`/tmp/ar-batch-check/core-energy-compare.png` の目視でも MDL-05は灰白、IMG-04/08は淡いシアン、IMG-06は明確に濃いターコイズ、と3段階に見分けがつく。プレイヤーが最も高頻度に見るコアクリスタル本体（MDL-05・防衛対象そのもの）が最も色褪せて見える一方、後発のUI資産ほど彩度が上がっていく傾向は、専有色を「一目で同じもの」と認識させるスタイルロックの目的に反する。

**2. 敵専有色ファミリー（`#973FA5`本体/`#C71A23`アクセント）の色域比率が、HUD警告アイコン（IMG-07）と実際の3Dモデル（MDL-03/04）で構図的に逆転している**

独立測色（同一手法・hue分類: body-hue窓250–325° / accent-hue窓335–360°&0–15°、sat>0.25画素のみ集計）:

| 資産 | body-hue画素比 | accent-hue画素比 | body/accent比 |
|---|---|---|---|
| MDL-03（marauder render） | 90.4% | 9.1% | **9.90** |
| MDL-04（warbeast render） | 89.0% | 10.4% | **8.52** |
| IMG-07 左（Marauder badge） | 42.8% | 44.0% | **0.97** |
| IMG-07 右（Warbeast badge） | 63.8% | 29.4% | **2.17** |

`design/assets.md` IMG-07行は「body `#973FA5` / accent `#C71A23`」——bodyが支配色、accentは補助色という設計はMDL-03/04で実際に達成されている（body比90%前後）。しかしIMG-07（ウェーブ予告UIで使う敵種別インジケータ）は**この比率が資産ごとに個別チェックされていなかったため**、Marauder側でbody:accentがほぼ1:1（赤が紫と同等以上に主張）、Warbeast側でもbody比63.8%（3Dモデルの89%から26ポイント低い）まで崩れている。`/tmp/ar-batch-check/enemy-family-compare.png` の目視でも、MDL-03/04は「紫の身体+小さな赤いホーン/目」だが、IMG-07は「赤い身体のシルエット+紫の縁取り」に近い配色として視認され、**主色/アクセント色の役割が事実上入れ替わっている**。個別バッチ審査（`assets-images.md`）はIMG-07のhue逸脱を検出していたが「シルエット可読性（二足/四足）は成立」という理由でnon-blocker扱いにしており、3Dモデルとの構図比較（body/accent比）は行っていなかった。ウェーブ予告アイコン（プレイヤーが配置判断の材料にする先行情報）が実際の敵の見た目と色の主従関係で食い違うのは、P-01（配置の結果が返ってくる先の視認性）・P-03（敵の識別）に関わる実質的な可読性リスクであり、単純なhue distance不合格（すでにnon-blocker済み）とは別の新規指摘として扱う。

### 非blocker開示（本チェックで新規に確認した軽微なドリフト）

3. **タワー専有色 `#0B6C58`（IMG-03アイコン vs MDL-01/02レンダー）**: IMG-03（icon-tower-select、両半分）はhue 176.1°/179.3°・距離15.9/24.0。既存レビューが独立測色済みのMDL-01（hue≈168.7°相当・距離16.0）/MDL-02（hue≈166.9°相当・距離2.9）と比べ、IMG-03はhueが目標(167.6°)から約9〜12°シアン方向にズレている。距離自体はいずれも許容基準40未満で個別には合格水準だが、UIアイコンと3Dモデルの間でわずかな色相差が存在する。実害は小さいと判断し再生成対象にはしないが、次回IMG-03に触る機会があればMDL-01/02のhueへ合わせる余地ありとして記録。
4. IMG-07の敵体積比（Warbeast/Marauder=2.22倍、仕様は約1.7倍）は`assets-images.md` iteration1で既に開示済みの継続事項（本チェックでも同一の不透明画素数比較で再確認、変化なし）。

### provenance/plan_tier（本チェックでの再確認）

MANIFESTの全IMG/MDL行（`phase:"engine_integration"`行を除く）に対し必須フィールド（`file/provider/model/prompt/seed/style_codes/cost_usd/plan_tier/sha256/license/generated_at/asset_id`、MDL行は追加で`kind/polycount/bone_count/rigged/format/bbox_authoring_m/validator`）の欠落をPythonで機械スキャン——**欠落なし**。`must_replace:true`・`shippable:false`ルート由来資産は本バッチに存在しない（既存レビューの確認を再現・変化なし）。fal経由Meshy使用も無し（全MDLがMeshy直APIのみ）。新規のprovenance指摘は無し。

## 指摘要約（優先度順）

1. **[要再生成] IMG-06（icon-upgrades.png）UPG-03クリスタル** — 専有色`#ABFFFF`ファミリー内で彩度が既承認資産（IMG-04/IMG-08）の約2.2倍まで濃くなっている（sat 0.542 vs 0.230〜0.250）。既にAR-ASSET iteration2で個別合格済みだが、本バッチ一貫性チェックで新たに検出した資産間ドリフトのため再指摘する。
2. **[要再生成] IMG-07（icon-enemy-indicator.png）** — body(`#973FA5`)/accent(`#C71A23`)の画素比がMDL-03/04（約9:1）に対しIMG-07は約1:1〜2.2:1まで崩れ、主色/アクセント色の役割が構図的に逆転している。個別バッチではhue distance逸脱としてnon-blocker扱いだったが、3Dモデルとの構図比較は未実施だったため本チェックで新規に指摘する。
3. **[非blocker・開示] IMG-03のhueがMDL-01/02より9〜12°シアン方向にズレている** — 距離自体は許容基準内、次回改訂機会があれば調整。
4. **[非blocker・継続開示] MDL-05（core-crystal）の彩度が専有色ファミール内で最も淡い** — 3Dモデル単体の個別レビュー（3イテレーション）では「flat hex vs 発光設計+AOレンダーの比較限界」と判断済みのnon-blockerだが、本チェックでIMG-04/08/IMG-06と並べた結果、家族内で最も色褪せて見える点を横断的disclosureとして記録。MDL-05自体の再生成は指示しない（既存判定を維持）。

## 対応

**対応済み（IMG-06・IMG-07 再生成）**。art-director 2026-07-22T19:40:00Z。

1. **IMG-06（icon-upgrades.png・revision3）**: retryInstruction通り、UPG-03クリスタルのプロンプト指示をrevision2の「ビビッドな彩度の高いシアンを支配色に」から180度転換し「IMG-04 essence（hue181.0°/sat0.250/val0.910）・IMG-08 core-hp（hue184.5°/sat0.230/val0.902）と同じ淡いペールシアン、鮮やかなdeep-teal/turquoiseは避ける」へ変更（seed 20260750、1回で採用・破棄試行なし）。生成後、独立測色で3クラスタ（dark panel / light rim / crystal）へのHSVリマップを実施し、crystal clusterのHSVをhue181.0°/sat0.25/val0.885（IMG-04・IMG-08の実測レンジの中間）へ精密収束させた。独立再測定: crystal avg HSV (181.0°, 0.25, 0.885) — revision2で検出されたsat0.542から解消し、IMG-04(sat0.250)・IMG-08(sat0.230)と同じ家族レンジに復帰。dark panel/light rimも#12262A/#EAF6F5へ精密remap（距離0.0004/0.0001）。カード構造は個別形状のカード（frame1透過率9.46%、revision2の24.29%より狭いが0.00%の単一パネルには戻っていない）を維持。アルファ検証: 4隅(0,0,0,0)、opaque_white_pct 0.0999%（白背景出荷には該当しない）。並置比較（IMG-04/IMG-08/IMG-06）の目視で3点とも同一の淡いシアン発光として視認できることを確認。
2. **IMG-07（icon-enemy-indicator.png・revision2）**: retryInstruction通り、body(#973FA5)を各シルエットの80-85%以上、accent(#C71A23)を角・目・爪先のみの10-15%に限定する指示へ改訂。4試行を要した（詳細はMANIFEST `game/_generated/MANIFEST.jsonl` IMG-07 revision2エントリの`pipeline`フィールドに全記録）: (a) seed20260751は色比率のみ改善指示→body94.7%/accent5.2%まで改善したが、**Warbeastが誤って二足立ちの人型シルエットに退行**（design/assets.md IMG-07行の二足/四足シルエット差要件に違反する副作用を本チェック中に新規発見）のため破棄。(b) seed20260752は形状（Warbeast四足の熊型スタンス）のみ訂正指示→形状は正しくなったが角・爪・赤アクセントが完全消失（モンスター意匠が失われた）ため破棄。(c) seed20260753は角/爪の意匠復元を再強調→形状・意匠は正しいが全身が単色の赤に統合され紫が皆無になったため破棄。(d) seed20260754（採用）: 色比率指示と形状指示を同時に明確化し両立に成功。raw生成物は紫本体+暗紺色（shading/角/爪）の2クラスタに自然分離したため、暗紺色クラスタを`#C71A23`へHSV完全remap（大幅増輝し「陰影」ではなく明確な赤ハイライトとして視認できるよう補正）、紫クラスタも`#973FA5`へ精密remap。独立測色（MDL-03/04と同一のhue-window+sat>0.25手法）: overall body78.24%/accent21.76%（比3.60）、Marauder単体83.57%/16.43%（比5.09、目標に近い）、Warbeast単体74.43%/25.57%（比2.91）——旧版（body42.8-63.8%/accent29.4-44.0%、紫と赤の役割がほぼ逆転）から大幅改善し「紫が支配色・赤は小さなアクセント」という設計意図に明確に復帰した。ただしMDL-03/04の実測比（約90%/10%）には数値的に届いておらず、特にWarbeast側のaccent比が目標より高い——3回のプロンプト再試行（形状・色・モンスター意匠の同時両立の難度が高い）を経てもこれ以上の厳密な一致は得られなかったため、残差は非blocker開示として記録する（下記開示事項4を参照）。二足(Marauder)/四足(Warbeast)のシルエット差は最終採用版で維持・256px相当縮小視認でも即判別可能なことを確認。アルファ検証: 4隅(0,0,0,0)、opaque_white_pct 0.0%。
3. **IMG-03のhue逸脱（9〜12°）**: 本チェックでは対応せず、非blocker開示として維持（次回IMG-03改訂機会に調整）。
4. **MDL-05（core-crystal）の彩度が家族内で最も淡い**: 本チェックでは対応せず、既存判定（3イテレーション審査済みnon-blocker）を維持。

- sha256再計算・MANIFESTへ`revision:3`(IMG-06)/`revision:2`(IMG-07)として反映済み（`game/_generated/MANIFEST.jsonl`）。旧revisionのsha256・provenanceはそのままMANIFEST内に履歴として残置。
- 予算: 追加コスト$0.30（IMG-06 $0.06［1試行］＋IMG-07 $0.24［4試行、うち3回破棄］、いずれも`cost_estimated:true`）を含め`game/_generated/MANIFEST.jsonl`の`cost_usd`合算は**$6.4379**（`state/budget.txt`の$20上限に対し残枠約$13.56、超過無し）。ルーティングは`state/asset-routing.json`のPrimaryを維持（`image_sprite: fal:ideogram-v3-transparent`。生成中の再判定なし。degradedRoutesは無し——両資産ともPrimaryで最終的に成功、fallbackプロバイダへの切替は発生していない）。
- 次アクション: art-reviewerによるAR-ASSET（バッチ一貫性チェック pass 2）判定待ち。

### 追加開示事項（本対応で新規に確認した事項）

5. **（IMG-07・新規開示）** revision2はbody/accent比の数値目標（80%以上/10-15%、3Dモデル比90%/10%）に対し、overall78.24%/21.76%・Warbeast単体74.43%/25.57%と、目標にわずかに届いていない残差がある。プロンプトのみでの制御は形状（二足/四足）・色比率・モンスター意匠（角/爪/牙）の3要件を同時に満たすことが難しく、4試行中3試行でいずれか1要件が退行した。次回さらに調整機会があれば、色補正パイプライン側でaccent領域（暗紺色クラスタ）の面積自体を縮小する後処理（例: 爪先/角の先端のみを残し、脚の陰影ストライプ部分は紫系の暗色にフォールバックする等）を検討する。
6. **（IMG-07・新規開示）** revision2生成の中間試行で「色比率のみを修正する指示」が「形状（四足→二足）を意図せず破壊する」副作用を確認した。これはプロンプトエンジニアリングにおける非自明な相互作用であり、今後同様の同時多要件修正では各要件（形状・色・意匠）を独立して検証するプロセスが必要になることを示唆する。

## disclosures（gates.md AR-ASSET観点6準拠）

- 上記「非blocker開示」3・4行を参照。いずれも再生成では完全には解消しない可能性がある家族内ニュアンス差（既存資産のスタイル成立を壊さない範囲での微調整が望ましい）。
- 既存バッチレビュー（assets-images*.md / assets-models*.md）で開示済みの`cost_estimated:true`・`color_correction.applied:true`・非多様体頂点計測差異等はここでは再掲しない（変更なし・重複開示を避ける）。
- **（本対応で新規）** 上記「対応」節の追加開示事項5・6を参照。IMG-07（revision2）はbody/accent比の数値目標にわずかに届いていない残差（overall78.24%/21.76%、目標80%以上/10-15%）があり、Checkpointでの開示対象として維持する。IMG-06（revision3）・IMG-07（revision2）とも`cost_estimated:true`（fal:ideogram-v3-transparentの1回あたり保守見積コスト）。

## AR-ASSET iteration 2（pass 2: バッチ一貫性 — iteration1指摘の解消確認＋再横断チェック） — CONCERNS

- 日時: 2026-07-22T08:50:18Z
- 手法: iteration1で指摘した2件（IMG-06 UPG-03彩度ドリフト／IMG-07 body-accent比逆転）への対応後（`game/_generated/MANIFEST.jsonl` IMG-06 revision3 / IMG-07 revision2）を独立再測色。加えて `game/_generated/MANIFEST.jsonl` 全35行（生成データ29行＋engine_integration 6行）を再スキャンし、sha256整合・provenance必須フィールド・禁止プロバイダ／ライセンスを再確認。時系列コンタクトシート（`/tmp/ar-batch-check-p2/chrono-montage.png`、13資産をgenerated_at最終値順に配置）を作成し目視スキャンした上で、疑わしい2ファミリー（core-energy `#ABFFFF` / enemy `#973FA5`+`#C71A23`）をPython(PIL/numpy)で独立測色。一時ファイルは `/tmp/ar-batch-check-p2/` に保存（対象ディレクトリは汚していない）。

### sha256整合性チェック（前提確認）

`game/_generated/textures/` 上の実ファイルのsha256を独立計算し、MANIFEST最新revision行のsha256と全件一致を確認（IMG-01=`fd37ec4d...`／IMG-06=`cc268039...`／IMG-07=`79010cf4...`／IMG-08=`90d6c040...`／IMG-04=`83f63b80...`／IMG-03=`6ea85387...`）。ディスク上の資産とMANIFESTのprovenance記録に齟齬なし。

### 指摘1（iteration1）の解消確認 — IMG-06 UPG-03クリスタル: 解消

独立測色（クリスタル本体クロップをhue150-215&sat≥0.15&val≥0.5でマスクし、rim/panelクラスタを除外——iteration1のk-means手法とは別実装）:

| 資産 | hue | sat | val | dist to `#ABFFFF` |
|---|---|---|---|---|
| IMG-04 essence（既承認） | 181.1° | 0.250 | 0.911 | 32.8 |
| IMG-08 core-hp rev3（既承認） | 184.9° | 0.232 | 0.904 | 38.4 |
| IMG-06 UPG-03 rev3（本チェック対象） | 181.1° | **0.248** | 0.875 | 45.8 |

彩度0.542（iteration1指摘時点）→0.248まで低下し、IMG-04(0.250)・IMG-08(0.232)と同一の家族レンジに復帰したことを独立測定で確認。目視（`/tmp/ar-batch-check-p2/chrono-montage.png`）でもcore-energy 4資産（MDL-05/IMG-04/IMG-08/IMG-06）は淡いシアンで統一され、iteration1で視認された「IMG-06だけ明確に濃いターコイズ」という3段階の色分離は解消。**この指摘は解消と判定し、再指摘しない。**

### 指摘2（iteration1）の解消確認 — IMG-07 body/accent比: 部分的解消（残差あり・再指摘）

独立測色（MDL-03/04と同一のhue-window+sat>0.25手法、iteration1のMANIFEST自己申告値とは独立に再計算）:

| 資産 | body(`#973FA5`)画素比 | accent(`#C71A23`)画素比 | body/accent比 |
|---|---|---|---|
| MDL-03（marauder render） | 90.82% | 9.18% | 9.90 |
| MDL-04（warbeast render） | 89.51% | 10.49% | 8.54 |
| IMG-07 全体 rev2 | 78.24% | 21.76% | 3.60 |
| IMG-07 左（Marauder） | 85.02% | 14.98% | 5.67 |
| IMG-07 右（Warbeast） | **72.15%** | **27.85%** | **2.59** |

iteration1指摘時点（body42.8-63.8%/accent29.4-44.0%、比0.97-2.17）から大幅改善し、MANIFESTの自己申告値（overall78.24/21.76、Marauder83.57/16.43、Warbeast74.43/25.57）とも独立測定でほぼ一致（測定手法完全一致のため誤差3ポイント以内）。しかし3Dモデル実測比（約9:1）にはなお届いておらず、**特にWarbeast側は3Dモデル(8.54)の約1/3の比(2.59)** で、accent(赤)領域が3Dモデルの約2.5倍の面積比を占める。目視（`/tmp/ar-batch-check-p2/chrono-montage.png`のMDL-04とIMG-07右半分を並置）でも、MDL-04は「紫の身体に小さな赤いホーン/目」だが、IMG-07のWarbeast側は脚・胴体にまで赤が目立って広がり、3Dモデルと並べたときに配色バランスが異なる個体に見える。

**判定**: design/assets.md IMG-07行の文字どおりの受け入れ条件（「二足 vs 四足のシルエット差だけで256px縮小後も即判別できること」）は本チェックでも視認確認済みで満たされている——シルエット可読性自体は不合格ではない。一方、本チェック観点1（スタイル一致・パレット逸脱）の観点では、実際にプレイヤーが見る3Dモデルとウェーブ予告HUDアイコンの間で色面積バランスが視覚的に異なる個体に見える状態は解消しておらず、iteration1からの継続指摘として維持する。すでに4回の生成試行（3回破棄）を経ており、プロンプトのみでの形状・色比率・意匠（角/爪/牙）の同時制御は難度が高く収束していないことは art-director 自身の開示（追加開示事項5）でも認められている。これ以上プロンプト再生成のみで追い込むのは費用対効果が低いと判断し、**再生成の方向性を変える具体的な指示**を付けて CONCERNS とする（下記 retryInstruction）。

### 再確認（変化なし・非blocker、既存判定を維持）

- **MDL-05（core-crystal）の彩度**: 独立再測色（背景差分+輝度上位60%）でhue179.2°/sat0.172/val0.830、`#ABFFFF`との距離61.7（iteration1測定sat0.161・距離ベースの水準と一致、無変化）。既存判定（3イテレーション審査済みの個別レビューでnon-blocker、本バッチチェックiteration1でも再生成対象外と明記）を維持し、再指摘しない。
- **IMG-03のhueオフセット（MDL-01/02比 約9-12°シアン方向）**: 本チェック対象の再生成が発生していないため再測色は実施せず、iteration1の非blocker開示を維持。
- **IMG-05（icon-achievements）の家族色**: 本チェックで新規に測色（未実施だった点を追加検証）。ACH-03クロスヘア（`#973FA5`ファミリー）はhue292.1°/sat0.618・距離0.8で良好。ACH-04同心円（`#15ACC1`ファミリー）はhue188.2°/sat0.907・距離30.9で許容基準（distance<40）内。両方とも既存ファミリーとの新規ドリフトなし。

### provenance/plan_tier（再スキャン）

MANIFEST全35行をPythonで再スキャン: `must_replace:true` 0件、`shippable_route:false` 0件（`shippable_route`フィールドを持つ7行は全てSFX/BGM行で値は`true`）、禁止プロバイダ・禁止モデル（gpt-image-2/bria-rmbg/ElevenLabs Free/fal経由Meshy/Mixamo等）の痕跡0件（provider値は`elevenlabs:music-v2`/`elevenlabs:sfx-v2`/`fal:flux-2-pro`/`fal:ideogram-v3-transparent`/`meshy:direct-image-to-3d`の5種のみ、全てMeshy直APIでfal経由Meshyは不使用のためライセンス継承未検証リスクは非該当）。`plan_tier`は全29生成行に実測値あり（prepaid 14 / pro+ 8 / starter 7）。`cost_estimated:true`は生成データ全29行に付与（クレジット→USD換算・ElevenLabs Music APIのcost無ヘッダ等、既存disclosure通りの保守見積であることを維持）。gltf-transform validate結果（5モデル全数）を再確認し全て `info: No errors found.`（`model-warbeast`のみtangent-space生成に関する非blocking WARNING 1件、glTF標準の想定内挙動でエラーではない）。新規のprovenance指摘は無し。

## 指摘要約（優先度順）

1. **[要対応・非緊急] IMG-07（icon-enemy-indicator.png・revision2）Warbeast側のaccent面積比が3Dモデル比で約2.5倍** — 独立測色: Warbeast側body72.15%/accent27.85%（3Dモデル比 body89.51%/accent10.49%）。iteration1の指摘（body/accent逆転）は大幅改善したが目標水準には未到達。優先度は「非緊急」——シルエット可読性（design/assets.mdの文字どおりの受け入れ条件）は満たしており、ゲームプレイのブロッカーではない。

### retryInstruction（IMG-07・優先度: 非緊急）

これまでの4回の試行はいずれも**プロンプトのみ**での形状／色比率／モンスター意匠（角・爪・牙）の同時制御であり、3回が何らかの要件を退行させて破棄されている（費用対効果が低下）。次回試行は方針を変え、**ローカル決定論的後処理でaccent領域の面積自体を縮小する**ことを提案する:
1. 生成（プロンプトはrevision2の採用版seed 20260754のものを再利用してよい・再生成不要）→ 既存のHSVクラスタ抽出（紫body cluster / 暗紺色→赤accent cluster）を再利用。
2. accent clusterのマスクに対し `cv2.erode`（または `scipy.ndimage.binary_erosion`）を1〜2px適用してから`#C71A23`へremapする——爪先・角の先端・目のような「小さな点」は生き残るが、脛や胴体の縁を這うような線幅の広いaccent領域は縮小され、bodyクラスタ（紫）に明け渡される。
3. 縮小後にWarbeast側で独立測色（本レビューと同一のhue-window+sat>0.25手法）を再実行し、body/accent比が最低6:1（MDL-04実測8.54の70%水準）に達するか確認。達しない場合はerode回数を1段階増やして再試行（最大2回まで）。
4. 2回のerode試行でも6:1に届かない場合は、それ以上の後処理・再生成は行わず、本指摘は「対応済み（改善したが目標未達）」として本ファイルに記録し、Checkpoint Cで開示のうえ受け入れる（review-loops.mdの費用対効果に基づく打ち切り判断——4回のプロンプト試行+後処理2回試行の合計を持ってこの資産の追い込みは十分と扱う）。

## disclosures（gates.md AR-ASSET観点6準拠）

- IMG-07（revision2）: Warbeast側のbody/accent比が3Dモデル実測（約9:1）に対し約2.6:1と、家族内一貫性の観点でなお開示対象。4回の生成試行（3回破棄）を経た残差であり、上記retryInstructionの後処理アプローチで次回改善を試みるが、それでも収束しない場合はCheckpoint Cで「HUD予告アイコンと実際の3Dモデルの配色バランス差」として個別開示すること（`[BLOCKER]`ではなく通常の既知課題として提示可）。
- 既存disclosure（cost_estimated:true全29行／MDL全5点の非多様体頂点は軽微／IMG-03のhueオフセット9-12°／MDL-05の彩度が家族内最淡）は変更なしのため再掲のみで重複開示はしない。

## 対応

（art-director記入待ち）

