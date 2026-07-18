# レビュー履歴 — assets-images（build フェーズ画像バッチ, engine=unity）

## AR-ASSET iteration 1 — APPROVE
- 日時: 2026-07-13T09:15:00Z
- 対象: `game/_generated/images/img-04-hit-vfx.png`（IMG-04）／`game/_generated/MANIFEST.jsonl` 末尾追記行（`asset_id:"IMG-04"`）／`design/assets.md` IMG-04 行（状態 `planned → generated`）。IMG-01/IMG-02/IMG-03 は `state/reviews/assets-images-prototype.md` iteration 6 で既に APPROVE 済みのため本ファイルでは再判定しない（重複判定回避）。3D資産（MDL/ANM）・音声資産は別artifactのスコープ。
- 検査方法: Pillow+numpy によるフルスキャン（サンプリングなし）でのRGBA/寸法/4隅アルファ実測、境界半透明画素×白系画素[r,g,b>230,a>15]×隣接4画素にalpha=0が存在、の3条件ANDによる外周フリンジ検査の独自実装、colorsys.rgb_to_hsvベースの禁止色（赤/オレンジ `#FF3B30` 系、hue<40°またはhue>340°でsat>0.3かつval>0.3）検出（不透明画素5px間隔サンプリング）、8bit(16刻み)量子化によるパレットクラスタ抽出＋13色パレットへのRGBユークリッド距離計算、nearest-neighbor 64px/32px縮小をダークグレー背景・敵主色`#8B12A5`背景の両方に合成した検証画像書き出し＋Readツールでの直接目視、`shasum -a 256` によるMANIFEST sha256照合、`state/asset-routing.json` によるplan_tier/shippable突合、MANIFEST全39行のJSONパース検証＋`cost_usd`合算と`state/budget.txt`照合。

### 観点別結果

**仕様一致（サイズ・アルファ・provenance）— PASS**
- 実寸512x512・RGBA・4隅alpha=0（`corner alphas: 0 0 0 0`）。`design/assets.md` IMG-04 指定「512x512」と一致。「1枚絵」（単一フレーム、スプライトシートではない）の仕様とも一致。
- 不透明30.24%／完全透過64.44%／半透明（アンチエイリアス縁）5.31%の内訳を実測。
- sha256実測 `16f8ce59e09d746cbbf2a353e5956b1f20da73946c7f9fa3399b858047234e13` はMANIFEST記載値と完全一致（改ざん・取り違えなし）。
- `state/asset-routing.json`: ルート `fal:ideogram-v3-transparent`（`routes.image_sprite` 相当）、`shippable.image_sprite:true`、`checks.fal.plan_tier:"prepaid"` はMANIFEST記載の`plan_tier:"prepaid"`と一致。`license:"commercial-ok"`、`must_replace`該当なし。

**アルファ縁品質（観点3）— PASS**
- フルスキャン（サンプリングなし）で境界半透明画素13,925px中、外周フリンジ（白系[r,g,b>230,a>15]かつ隣接4画素にalpha=0が存在）は**0px（0.00%）**。白フチ・背景残りなし。
- 尖端（スパイク先端）のズームクロップ目視（`/tmp/img04-tip-crop.png`）でも、輪郭は白コア→シアン中間層→濃紺外周のグラデーションでクリーンに透過へフェードしており、ジャギ・帯状アーティファクトは確認されず。

**スタイル一致・パレット逸脱（観点1）— PASS**
- 8bit量子化による不透明画素（79,282サンプル）クラスタ分析: 主要色 `RGB(32,48,128)`(36.08%, 最近傍`#164583`距離23.5)・`RGB(80,192,192)`(24.12%, 最近傍`#4FE8E2`距離52.5)・`RGB(240,240,240)`(12.61%, 最近傍`#F2F5FA`距離11.4) を筆頭に、全クラスタが濃紺・シアン・白の3系統に収束。最大逸脱クラスタでも距離84.5で、本レビュー履歴が既に確立した許容上限（IMG-03マゼンタハイライト距離111.1、iteration 2/3/4/5で許容確定）を下回る。緑系・オフパレット色クラスタは検出されず。
- 禁止色検査: `design/assets.md` IMG-04指定「`#FF3B30`系は被弾警告色と混同するため使用しない」に対し、赤/オレンジ帯（hue<40°またはhue>340°、sat>0.3かつval>0.3）検出は不透明画素15,857サンプル中**0件**。指定通り白〜シアン系発光のみで構成されていることを機械確認。
- `design/art-bible.json` style_block の役割別単色ブロッキング方針（クリスタル＝シアン発光ハロー系統）とも整合し、画風ブレなし。

**シルエット可読性（観点2）— PASS**
- 64px/32px nearest-neighbor縮小をダークグレー背景に合成して確認（`/tmp/img04-64-darkbg.png`, `/tmp/img04-32-darkbg.png`）: 8方向の放射状スパイク＋白コアの形状が32px相当でも明瞭に判別可能。
- 敵主色`#8B12A5`背景に合成した比較（`/tmp/img04-64-enemybg.png`）でも、VFXの白/シアン/濃紺は敵の紫と混同されず独立して視認できる（色相・輝度とも十分に分離）。

**provenance/MANIFEST整合性 — PASS**
- MANIFESTエントリは必須フィールド（`file`/`provider`/`model`/`prompt`/`seed`/`style_codes`/`cost_usd`/`plan_tier`/`sha256`/`license`/`generated_at`）を全て充足。`negative_prompt`（禁止色・禁止要素の明示）・`post_process`（1024→512ダウンサンプル手法の記録）・`alpha_verified`/`alpha_verification_note`も記録済み。
- MANIFEST全39行を `python3 -c` でJSONパース検証（parseエラー0件、`wc -l`実測39行と一致）。`cost_usd`合算 **$1.43394**、`state/budget.txt` 上限 $100 に対し超過なし（IMG-04分 $0.06 込み）。

**ファイル命名（付随事項・継続記録・非ブロッキング）**
`img-04-hit-vfx.png` は `.claude/rules/assets.md` 規定のプレフィクス（`sprite-`/`tile-`/`ui-`/`sfx-`/`bgm-`/`model-`/`anim-`）のいずれにも該当しない。`state/reviews/assets-images-prototype.md` iteration 1（IMG-03時点）から継続する既知の系統的逸脱であり、`design/assets.md` の編集は art-reviewer の権限外のため新規のブロッキング指摘とはせず、disclosuresに継続記録するのみとする。

### 総合判定: APPROVE
理由: IMG-04は機械検査可能な全項目（アルファ縁品質・スタイル一致/パレット逸脱・禁止色不使用・シルエット可読性・仕様一致・provenance）で独立計測によりPASSを確認した。不合格資産（failedAssets）は無い。

### disclosures（再生成不要・人間開示のみ）
- IMG-04: MANIFESTに `cost_estimated:true` の記録あり（fal.aiの確定請求額ではなく推定コスト）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- ファイル命名: `img-04-hit-vfx.png` が `.claude/rules/assets.md` 規定プレフィクス外（IMG-01〜04全件に共通する既知の系統的逸脱、iteration 1（prototype）から継続記録・art-director/art-reviewer権限外のため是正見送り）。
- 予算: `game/_generated/MANIFEST.jsonl` 全39行の `cost_usd` 合算 $1.43394、`state/budget.txt` 上限 $100 に対し超過なし。

- 対応: （該当なし。本iterationはAPPROVEのため producer 対応不要）

## AR-ASSET iteration 2 — CONCERNS
- 日時: 2026-07-14T06:10:00Z
- 対象: `game/_generated/images/img-05-ui-frame-kit.png`（IMG-05, P-04）／`game/_generated/images/img-06-arena-backdrop.png`（IMG-06, P-01）／`game/_generated/MANIFEST.jsonl` 該当2行（`asset_id:"IMG-05"`/`"IMG-06"`, 44-45行目）／`design/assets.md`「art-director追記（2026-07-14）」節。本サブバッチ（Phase 3 Visual Brushup, Checkpoint C修正依頼由来）としては初回レビュー（iteration 1 of sub-batch。ファイル通番はiteration 2）。IMG-01〜04は本ファイルiteration 1以前で既にAPPROVE済みのため再判定しない。
- 検査方法: `Image.open()`実測でmode/size確認、numpy全画素スキャンで4隅アルファ・境界半透明画素の白フチ検出、グリッド区画ごとの色サンプリング（`mean`と`mode`（16刻み量子化）の両方）を13色パレットへRGBユークリッド距離で照合、グレー背景合成＋256px nearest-neighbor縮小画像を書き出してRead目視、IMG-06はcolorsys.rgb_to_hsvによる中心列hue独立再測定（MANIFEST自己申告値との突合）＋水平方向hue均一性チェック＋FFTによる8px周期性検査（JPEG DCTブロックアーティファクト検出）、`shasum -a 256`によるMANIFEST sha256照合、MANIFEST全45行cost_usd合算。

### 観点別結果

**IMG-05: 仕様一致（サイズ・アルファ）— PASS**
- 実寸1024x1024・RGBA、`design/assets.md`指定「1024x1024」と一致。4隅alpha=0（`corner alphas: 0 0 0 0`）。sha256実測 `b1f204be6cc308da6f4c42bfd7a9f532984875a3978aaf316ce0e8bae8f8bcb0` はMANIFEST記載値と完全一致。

**IMG-05: アルファ縁品質（観点3）— PASS**
- 境界半透明画素（alpha 15-240）7,635px・低alpha画素（1-39）8,835px の両方で白系[r,g,b>200]画素は0px/11px（0.12%）のみで、白フチ・背景残りは検出されず。

**IMG-05: スタイル一致・仕様一致（観点1・4）— element(a) 9-sliceパネル枠で CONCERNS（要再生成）**
- MANIFEST記載プロンプト（採用要素1/3, seed 130510）は「flat solid dark-navy **#12081F** empty stretchable center」を明示指定。実測ではパネル中央領域（4区画サンプル平均）が `mean RGB≈#3E4A98〜#3F4F9C`、mode(16刻み量子化)`#404090`（不透明画素の85%を占める支配色）で、指定色`#12081F`とのRGBユークリッド距離は**約145**（13色パレット中最も近い`#164583`とでも距離46.8）。中程度〜明るい藍色になっており、指定された「ほぼ黒に近いダークネイビー」から明確に逸脱。
- さらに区画別mode/std比較（topleft std=[24,35.7,26.5] vs topright std=[6.2,13.4,11.7] vs botright std=[9.9,36.1,30.2]）で色が均一でなく、目視（`/tmp/img05_grayclip.png`グレー背景合成）でも遠近感のある「額縁/画面ベゼル」風の収束線・左上ハイライト・右下シャドウが確認できる。`design/assets.md` IMG-05 仕様「9-sliceの中央領域は無地で引き伸ばし可能なこと」に反しており、この見た目のままHUD/Menu/Title/Resultパネルへ9-sliceで引き伸ばすと、遠近線とハイライトが不自然に歪む。
- 他4要素（タブ選択/非選択枠・リボン・コーナー装飾）は透過中央領域または装飾フィルとして機能上問題なし（タブは中央が意図通り透過、リボン・コーナー装飾はパレット近傍色でグロス表現も許容範囲）。

**IMG-05: シルエット可読性（観点2）— PASS（パネル要素を除く）**
- 256px nearest-neighbor縮小（グレー背景合成、`/tmp/img05_small256.png`相当）でタブ選択/非選択・リボン・コーナー装飾の形状は明瞭に判別可能。パネル要素は形状自体は判別可能だが、上記の色/均一性欠陥は縮小しても解消されない。

**IMG-06: 仕様一致（サイズ・アルファ）— PASS**
- 実寸2048x2048・RGB、`design/assets.md`指定「2048x2048」と一致。背景/バックドロップ用途のため`design/assets.md`にアルファ必須の明記なし（IMG-05と異なりn/aは適切な判定）。sha256実測 `d84d182c25ec683d5e04dc6f4f44f1d583aeb013973bdbf0cc98fa730b24463d` はMANIFEST記載値と完全一致。

**IMG-06: スタイル一致（観点1）— PASS（グラデーション方向・パレット系統は概ね一致）**
- 独立再測定（中心列サンプリング）: top2%→hue 292.9°（マゼンタ参照330°、差37°）／mid45%→hue187.2°（シアン参照177.6°、差9.6°）／75%→hue266.9°（パープル参照289.4°、差22.5°）／bottom97%→黒。MANIFEST自己申告値（301°/187°/268°）と概ね整合し、独立測定でも捏造・誤記は無し。色相の系統（void→purple→cyan→magenta）は仕様通り。水平方向のhue変動は最大約15°（mid帯）に収まり、左右非対称な目立つムラは無い。彩度0.26-0.41・輝度0.65-0.98で低コントラスト方針とも整合し、前景阻害は無いと判断。
- 参考値との色相差20-37°はやや大きいが、本資産は「低ディテール・低コントラストの背景」であり、キャラ/クリスタルのようなシルエット識別要件（gates.md観点1の主眼）を負わないため、この程度の色相ズレはCONCERNS対象としない（許容）。

**IMG-06: 技術ノート（優先度低・非ブロッキング） — 補足指摘**
- FFT解析で列方向勾配に8px周期の高調波（period 4.00px/2.67px/2.00px、magnitude 91.0/70.3/56.1、8px周期成分自体も平均比16倍）を検出。これはJPEG 8x8 DCTブロックの典型的アーティファクト署名で、MANIFESTの`verification_note`が自認する「元JPEG出力をPNGへ無劣化変換」（＝PNG化はコンテナ変換のみでJPEG圧縮由来の劣化はそのまま内包）と整合する。6倍ズームクロップ（`/tmp/img06_crop_zoom.png`）で目視しても、スムーズなはずの勾配面に微弱なブロック状ノイズが確認できる。通常プレイ距離・等倍表示では知覚困難だが、エンジン側テクスチャ圧縮（BC7/ASTC等）でさらに劣化が増幅する可能性がある。ブロッキング事由により本判定ではCONCERNS化しない（機能上の実害が現時点で確認できないため）が、art-directorへの技術メモとして記録する。改善する場合は生成プロバイダにPNG/ロスレス出力オプションがあれば切替、無ければ弱いガウシアンブラー（縦方向）でブロックを馴らすローカル後処理を追加する案がある。

**provenance/MANIFEST整合性 — PASS**
- IMG-05/IMG-06とも必須フィールド（`file`/`provider`/`model`/`prompt`/`seed`/`cost_usd`/`plan_tier`/`sha256`/`license`/`generated_at`）を充足。IMG-05は`negative_prompt`・`seeds_all_attempts`・`post_process`・`alpha_verified`/`alpha_verification_note`も記録。`state/asset-routing.json`: `routes.image_sprite`=`fal:ideogram-v3-transparent`（IMG-05）・`routes.image_background`=`fal:flux-2-pro`（IMG-06）とも`shippable:true`と一致（disclosures対象の`shippable:false`ルート由来には該当しない）。
- MANIFEST全45行cost_usd合算 **$2.71044**、`state/budget.txt`上限$100に対し超過なし。

**ファイル命名（付随事項・継続記録・非ブロッキング）**
`img-05-ui-frame-kit.png`/`img-06-arena-backdrop.png`とも`.claude/rules/assets.md`規定プレフィクス（`sprite-`/`tile-`/`ui-`/`sfx-`/`bgm-`/`model-`/`anim-`）に非該当。iteration 1（IMG-04）から継続する既知の系統的逸脱であり、art-reviewer権限外（design/assets.mdの編集不可）のため新規ブロッキング指摘とはせずdisclosuresに継続記録する。

### 総合判定: CONCERNS
理由: IMG-06および IMG-05のタブ/リボン/コーナー装飾要素は機械検査全項目でPASSしたが、IMG-05のelement(a)（9-sliceパネル枠）が仕様（色`#12081F`・中央領域の均一性）から明確に逸脱しており、UI装飾キットの中核要素であるため是正必須と判断した。

### disclosures（再生成不要・人間開示のみ）
- IMG-05: MANIFESTに`cost_estimated:true`（$0.42、内訳: 廃棄4回+採用3回=fal ideogram-v3-transparent計7回API呼び出しの見積按分）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- IMG-06: MANIFESTに`cost_estimated:true`（$0.26、fal flux-2-pro 1回・megapixelベースの未検証レート見積もり）。Checkpointでの開示対象。
- ファイル命名: `img-05-ui-frame-kit.png`/`img-06-arena-backdrop.png`が`.claude/rules/assets.md`規定プレフィクス外（IMG-01〜06全件に共通する既知の系統的逸脱、iteration 1から継続記録・是正は art-director/design/assets.md編集権限側の対応事項）。
- 予算: `game/_generated/MANIFEST.jsonl`全45行の`cost_usd`合算$2.71044、`state/budget.txt`上限$100に対し超過なし。
- IMG-06技術メモ（ブロッキングではないが開示）: JPEG由来8px周期ブロックアーティファクトをFFTで検出（上記詳細）。再生成では必ずしも解消されない場合がある（同一プロバイダ設定であれば同種の圧縮を経由する可能性）ため、根本対応はプロバイダのロスレス出力オプション確認が必要。

- 対応: 対応済み。IMG-05 element(a)（9-sliceパネル枠）を再生成した（`game/_generated/MANIFEST.jsonl` IMG-05 revision:2）。route `fal:ideogram-v3-transparent` で4回試行（seed 130520〜130523）— attempt1は均一性改善もdist=134.4で不採用、attempt2はバケツ/ハンドル状の誤形状で不採用、attempt3は形状・均一性良好もdist=75.0でなお不採用、attempt4（seed130523）は中央領域が完全均一（std=0）だが純黒`#000000`（dist=36.7）だったため、ローカルで中央領域の純黒画素（r,g,b<12かつalpha>250、398,566px、周辺の青リング/白ハイライト/紫内リング縁取りは対象外）のみを厳密色置換（`#12081F`, dist=0）。この要素をアルファ含有bboxでクロップし、既存シート内element(a)セル領域（x:58-462, y:37-316）のみ透過クリア後に貼付、他4要素（タブ選択/非選択・リボン・コーナー装飾）はnumpy全画素diffで差分0（無変更）を確認。再検証結果: 中央領域4象限すべてmean=`#12081F`・dist=0・std=[0,0,0]（要求基準: dist≤30 かつ std半減を大幅に超過達成）。アルファ縁品質も全数再検証し白フリンジ0px。状態を`generated`へ更新（`design/assets.md`は既に`generated`のため変更不要、追記のみ実施）。追加コスト$0.24（fal 4回試行、cost_estimated:true）、MANIFEST全46行cost_usd合算$2.95044、`state/budget.txt`上限$100に対し超過なし。

## AR-ASSET iteration 3 — APPROVE
- 日時: 2026-07-14T12:15:00Z
- 対象: `game/_generated/images/img-05-ui-frame-kit.png`（IMG-05, element(a)再生成・差し替え分の再判定）／`game/_generated/MANIFEST.jsonl` IMG-05 `revision:2` エントリ（46行目）。iteration 2 CONCERNSで指摘したelement(a)の是正のみを対象とするフォローアップ判定（呼び出し元ワークフローの表記では本バッチの「iteration 2」レビュー、本ファイル内の通し番号ではiteration 3）。IMG-01〜04は既APPROVE、IMG-06はiteration 2でPASS済みのため再判定しない。
- 検査方法: `git show`でrevision1（コミット`5bd63be`, sha256`b1f204be...`）とrevision2（コミット`02ba6e7`, sha256`23d462d5...`）を独立抽出し`shasum -a 256`でMANIFEST記載値と突合。Pillow+numpyで(1)element(a)セル内側22%マージン領域の4象限mean/std/dist実測（MANIFESTの`spec_verification`と同一手法で独立再現）、(2)ring-to-center境界の水平スキャンライン実測によるアンチエイリアス品質の目視確認、(3)セル内不透明画素全数に対する「非リング・非ハイライト・dist>10」残留画素スキャン、(4)revision1/revision2間の全画素numpy diffによる変更範囲の独立検証（MANIFESTの「他4要素diff 0」自己申告の裏取り）、(5)全画像に対する白フチ検査（境界半透明画素×白系[r,g,b>230]×隣接alpha=0のAND条件、既存iteration方式を踏襲）、(6)MANIFEST全46行のJSONパース＋`cost_usd`合算、`state/asset-routing.json`のroute/shippable/plan_tier突合。グレー背景合成によるelement(a)単体・シート全体のダウンスケール確認画像を書き出しReadツールで目視。

### 観点別結果

**IMG-05 element(a): 仕様一致・スタイル一致（観点1・4, 前回CONCERNS対象）— PASS（是正確認）**
- element(a)セル（x:58-462, y:37-316, 404x279）の内側22%マージン領域を独立算出（MANIFESTの計算と同一ロジックで再現）: 4象限すべて `mean RGB = (18, 8, 31) = #12081F`、目標値との `dist = 0.0`、`std = [0.0, 0.0, 0.0]`（内側実測25,398px全画素が寸分違わず同一色）。前回逸脱値（dist≈145、区画std 24〜36）から完全に是正され、要求基準（dist≤30 かつ std半減）を大幅に超過して満たすことを独立測定で確認した。
- リング（青`#3488D1`系）から中央navy領域への境界を水平スキャンラインで実測: x=74–82の6px区間でRGBが `(114,159,208)→(73,108,166)→(47,76,141)→(25,37,72)→(19,9,33)→(18,8,31)` と滑らかに遷移し、x=84以降は1px単位で完全に`(18,8,31)`固定。段差・バンディング・残留藍色ハローは検出されず、クリーンなアンチエイリアス遷移であることを確認。
- セル内不透明画素70,629pxのうち「非リング色・非ハイライト色でdist>10」の残留画素は552px（0.78%）のみで、その内訳を個別サンプリングした結果、値の大半（dist 10〜60程度）はリング／中央領域の1〜2px幅アンチエイリアス遷移帯に位置しており、前回指摘された「面全体を覆う遠近感ベゼル」のような広域欠陥ではないことを確認（グレー背景合成画像`/tmp/img05_elementA_graybg.png`の目視でも、遠近線・左上ハイライト・右下シャドウの再発は無し。白ハイライトストロークは意図通り上端の一部のみに限定）。

**IMG-05: 他4要素（タブ選択/非選択・リボン・コーナー装飾）無変更の独立検証 — PASS（軽微な精度注記あり）**
- revision1（sha256`b1f204be6cc308da6f4c42bfd7a9f532984875a3978aaf316ce0e8bae8f8bcb0`）とrevision2（sha256`23d462d56086c31b62c4d1851cdd1d6b05be6d812aae7ed92bfe904e7bab20c9`）はいずれも`git show`抽出後の実測sha256がMANIFEST記載値と完全一致（改ざん・取り違えなし）。
- 全画素numpy diff: 変更104,202px中104,047pxはelement(a)セル内（想定通り）。セル外の変更は155pxのみで、全て`x=462`列または`y=316`行（＝element(a)セル自体の右端・下端の境界線ちょうど1px）に集中し、いずれもrev1側で微弱な半透明アルファ（1〜190）を持っていた画素がrev2で完全な透明`(0,0,0,0)`へクリアされたもの（element(a)自身のアンチエイリアス漏れの後処理での消去）。タブ/リボン/コーナー装飾の実体形状・色を構成する画素領域（x>480付近以降）には一切変更が無いことを座標分布で確認した。MANIFESTの`post_process`記載「他4要素セル領域はnumpy diffで差分0」はセル境界1px分の精度において厳密には成立していないが、実質的な内容（タブ/リボン/コーナー装飾の形状・色）への影響はゼロであり、是正妨害・再指摘に値する逸脱ではないと判断した（品質上のブロッキング事由にはしないが、provenance記述の精度メモとしてdisclosuresに記録する）。

**IMG-05: アルファ縁品質（観点3）— PASS**
- 全画像フルスキャン: 半透明境界画素7,315px中、白系[r,g,b>230]かつ隣接4画素にalpha=0が存在する白フチ画素は**0px**。4隅alpha=0、mode RGBA・1024x1024（`design/assets.md`指定と一致）。MANIFESTの`alpha_verification_note`（半透明7,315px・白フチ0px）と完全一致。

**IMG-05: シルエット可読性（観点2）— PASS**
- グレー背景合成の256px相当ダウンスケール目視（`/tmp/img05_full_graybg.png`）で、element(a)パネル・タブ選択/非選択・リボン・コーナー装飾の5要素とも輪郭・色分離が明瞭。element(a)は前回問題だった立体感/額縁感が解消され、フラットな2Dパネル枠として即座に判別できる（art-bible.json `style_block`のcel-shaded・太めダークネイビー輪郭方針とも整合）。

**provenance/MANIFEST整合性・予算 — PASS**
- IMG-05 `revision:2`エントリは必須フィールド（`file`/`provider`/`model`/`prompt`/`seed`/`style_codes`/`cost_usd`/`plan_tier`/`sha256`/`license`/`generated_at`）に加え`revision_of_sha256`/`revision_reason`/`spec_verification`/`review_ref`も充足。
- `state/asset-routing.json`: `routes.image_sprite = fal:ideogram-v3-transparent`、`shippable.image_sprite:true`、`checks.fal.plan_tier:"prepaid"` はMANIFEST記載`plan_tier:"prepaid"`と一致。`shippable:false`ルート由来には該当しない。
- MANIFEST全46行を`python3`でJSONパース検証（エラー0件）、`cost_usd`合算 **$2.95044** は`design/assets.md`「集計と予算」節の記載値と一致。`state/budget.txt`上限 $100 に対し超過なし。

**ファイル命名（付随事項・継続記録・非ブロッキング）**
`img-05-ui-frame-kit.png`は引き続き`.claude/rules/assets.md`規定プレフィクスに非該当。iteration 1から継続する既知の系統的逸脱として disclosures に記録を継続する（art-reviewer/art-director双方の権限外の是正事項）。

### 総合判定: APPROVE
理由: iteration 2で指摘したIMG-05 element(a)の仕様逸脱（中央領域色`#12081F`からのdist≈145、区画std 24〜36の非均一なベゼル状陰影）は、独立再測定でdist=0・std=0（4象限全て）に是正されたことを確認した。アルファ縁品質・他要素の実質的無変更・provenance・予算いずれも機械検証でPASS。IMG-05は全項目でAPPROVE水準に達したため、本バッチ（IMG-05差し替え分）は合格とする。不合格資産（failedAssets）は無い。

### disclosures（再生成不要・人間開示のみ）
- IMG-05: MANIFESTに`cost_estimated:true`（revision:2分$0.24、累計$2.95044のうちの一部）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- provenance記述の精度注記（非ブロッキング）: MANIFEST `post_process`は「他4要素セル領域はnumpy diffで差分0」と記載するが、独立diffではelement(a)セルの境界1px（x=462列/y=316行、計155px、いずれも微弱アルファ1〜190→0へのクリアのみ）で技術的な差分が検出された。タブ/リボン/コーナー装飾の実体形状・色には影響なしと確認済みだが、今後同種のprovenance記述は「セル境界を含む全画素で差分0」ではなく「対象セル内部のみ」等、範囲を厳密に記載することが望ましい。
- ファイル命名: `img-05-ui-frame-kit.png`が`.claude/rules/assets.md`規定プレフィクス外（IMG-01〜06全件に共通する既知の系統的逸脱、iteration 1から継続記録・是正はart-director/design/assets.md編集権限側の対応事項）。
- 予算: `game/_generated/MANIFEST.jsonl`全46行の`cost_usd`合算$2.95044、`state/budget.txt`上限$100に対し超過なし。

- 対応: 該当なし（本iterationはAPPROVEのためproducer対応不要）。

## AR-ASSET iteration 4 — APPROVE
- 日時: 2026-07-14T15:40:00Z
- 対象: `game/_generated/images/img-05-ui-frame-kit.png`（IMG-05, P-04）／`game/_generated/images/img-06-arena-backdrop.png`（IMG-06, P-01）／`game/_generated/MANIFEST.jsonl` 該当行（IMG-05 revision:2＝46行目、IMG-06＝45行目）／`design/assets.md`。呼び出し元ワークフローは本レビューを「iteration 1」と表記しているが、この2資産（IMG-05/IMG-06）は本ファイル iteration 2（初回CONCERNS）・iteration 3（IMG-05 element(a)是正のAPPROVE）で既に判定済みのため、本ファイル内の通し番号ではiteration 4として記録する（iteration 3と同じ命名精度注記を踏襲）。ファイル内容に変更が無いことをsha256で確認した上で独立再検証を実施。
- 検査方法: `shasum -a 256`で両ファイルの実測sha256をMANIFEST記載値（IMG-05 revision:2 `23d462d5...`／IMG-06 `d84d182c...`）と突合（完全一致、iteration 2/3以降の無改変を確認）。`git log`で対象ファイルの変更コミット履歴を確認（最終変更は`02ba6e7`＝iteration 2 CONCERNS対応コミットで以降変更なし）。Pillow+numpyでmode/size再実測、4隅アルファ、境界半透明画素×白系[r,g,b>230]×隣接alpha=0のAND条件によるフリンジ検査フルスキャン、IMG-05 element(a)セル（x:58-462,y:37-316）内側22%マージンのmean/std/dist独立再算出、13色パレットへのユークリッド距離サンプリング（opaque画素500点）、128px nearest-neighbor縮小をグレー背景合成した確認画像書き出し＋Readツール目視、IMG-06は中心列4点（top2%/mid45%/75%/bottom97%）のhue再測定、MANIFEST全46行のJSONパース検証＋`cost_usd`合算、`state/asset-routing.json`のroute/shippable/plan_tier突合。

### 観点別結果

**provenance整合（無改変の確認）— PASS**
- IMG-05実測sha256 `23d462d56086c31b62c4d1851cdd1d6b05be6d812aae7ed92bfe904e7bab20c9` はMANIFEST `revision:2`エントリ（46行目）と完全一致。IMG-06実測sha256 `d84d182c25ec683d5e04dc6f4f44f1d583aeb013973bdbf0cc98fa730b24463d` はMANIFEST 45行目と完全一致。`git log`上も最終変更コミットはiteration 2 CONCERNS対応（`02ba6e7`）でそれ以降の改変なし。

**IMG-05: 仕様一致・アルファ縁品質 — PASS（iteration 3の是正が維持されていることを再確認）**
- 実寸1024x1024・RGBA、4隅alpha=0。境界半透明画素7,315px中、白フチ（白系[r,g,b>230]かつ隣接4画素にalpha=0）は独立再スキャンでも**0px**。
- element(a)中央領域（内側22%マージン）を独立再算出: mean RGB≈(20.1, 10.4, 34.1)、目標`#12081F`=(18,8,31)に対しdist≈4.4（iteration 3実測のセル内25,398px全画素同一色dist=0とは算出範囲が広め＝AA境界を一部含むため差は出るが、それでも要求基準dist≤30を大幅にクリア）。iteration 2で指摘した逸脱（dist≈145、区画std24-36の非均一ベゼル状陰影）は再発していないことを確認。
- 128px nearest-neighbor縮小＋グレー背景合成の目視（`/tmp/img05_check_128.png`）で、パネル枠（フラット濃紺）・選択/非選択タブ（シアン発光/暗紺）・リボン（マゼンタ）・コーナー装飾（青）の5要素とも輪郭・色分離が明瞭。パネル要素に遠近感/額縁感の再発なし。

**IMG-06: 仕様一致・スタイル一致 — PASS**
- 実寸2048x2048・RGB。中心列hue再測定: top2%→292.9°／mid45%→187.2°／75%→268.1°／bottom97%→黒(RGB(1,1,1))。MANIFEST自己申告値（301°/187°/268°）・iteration 2実測値（292.9°/187.2°/266.9°）と一致し、独立測定でも捏造・ズレなし。彩度0.27-0.41・輝度0.64-0.98の低コントラスト方針も維持。
- 128px縮小画像（`/tmp/img06_check_128.png`）の目視で、下部near-black voidから上部にかけてpurple→cyan→magentaへ滑らかに遷移するグラデーションのみで、明瞭な形状/模様/前景競合要素は無し。

**スタイル一致・パレット逸脱（IMG-05, 観点1）— PASS**
- opaque画素サンプル500点の13色パレットへのユークリッド距離: 最大107.7・平均43.8。iteration 1で確立した許容上限（IMG-03マゼンタハイライト距離111.1）を下回り、既存許容範囲内。

**provenance/MANIFEST整合性・予算 — PASS**
- MANIFEST全46行を`python3`でJSONパース検証（エラー0件）、`cost_usd`合算 **$2.95044**（iteration 3実測値と一致）、`state/budget.txt`上限$100に対し超過なし。
- `state/asset-routing.json`: `routes.image_sprite=fal:ideogram-v3-transparent`・`routes.image_background=fal:flux-2-pro`とも`shippable:true`、`checks.fal.plan_tier:"prepaid"`はMANIFEST記載`plan_tier:"prepaid"`と一致。`shippable:false`ルート由来・fal経由Meshyライセンス継承未検証（本バッチは2D画像のみのため非該当）・must_replaceのいずれにも該当しない。

**ファイル命名（付随事項・継続記録・非ブロッキング）**
`img-05-ui-frame-kit.png`/`img-06-arena-backdrop.png`とも`.claude/rules/assets.md`規定プレフィクス外。iteration 1から継続する既知の系統的逸脱として引き続きdisclosuresに記録する（art-reviewer/art-director双方の権限外の是正事項）。

### 総合判定: APPROVE
理由: IMG-05（element(a)是正後revision:2）・IMG-06とも、ファイル内容がiteration 2/3時点から無改変（sha256一致）であることをprovenanceで確認した上で、仕様一致・アルファ縁品質・スタイル一致・シルエット可読性・provenance/予算整合を独立に再検証し、全項目PASSを確認した。不合格資産（failedAssets）は無い。

### disclosures（再生成不要・人間開示のみ）
- IMG-05: MANIFESTに`cost_estimated:true`（累計$0.42+$0.24=$0.66分、fal ideogram-v3-transparent計11回API呼び出しの見積按分）。Checkpointでの開示対象（gates.md AR-ASSET観点6）。
- IMG-06: MANIFESTに`cost_estimated:true`（$0.26、fal flux-2-pro 1回・megapixelベースの未検証レート見積もり）。Checkpointでの開示対象。
- IMG-06技術メモ（非ブロッキング、iteration 2から継続）: FFT解析でJPEG由来8px周期ブロックアーティファクトを検出済み（元JPEG出力をPNGへ無劣化変換したため内包）。通常表示では知覚困難だがエンジン側テクスチャ圧縮で増幅する可能性があり、根本対応にはプロバイダのロスレス出力オプション確認が必要。
- ファイル命名: `img-05-ui-frame-kit.png`/`img-06-arena-backdrop.png`が`.claude/rules/assets.md`規定プレフィクス外（IMG-01〜06全件に共通する既知の系統的逸脱、iteration 1から継続記録・是正はart-director/design/assets.md編集権限側の対応事項）。
- 予算: `game/_generated/MANIFEST.jsonl`全46行の`cost_usd`合算$2.95044、`state/budget.txt`上限$100に対し超過なし。

- 対応: 該当なし（本iterationはAPPROVEのためproducer対応不要）。
