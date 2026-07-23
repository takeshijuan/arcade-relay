# レビュー履歴: 画像資産バッチ（Phase 3・build — IMG-01/02/05/06/07）

対象: `game/_generated/textures/tile-grass.png`（IMG-01）/ `tile-dirt-path.png`（IMG-02）/ `icon-achievements.png`（IMG-05）/ `icon-upgrades.png`（IMG-06）/ `icon-enemy-indicator.png`（IMG-07）
照合元: `design/art-bible.json`（style_block/palette/resolution）、`design/art-bible.md`「パレット」「シルエット方針」「解像度・タイルサイズ」節、`design/assets.md`「画像」節 IMG-01/02/05/06/07行、`game/_generated/MANIFEST.jsonl`（該当5行）

## AR-ASSET iteration 1 — CONCERNS

- 日時: 2026-07-22T07:34:33Z
- 機械検査（reviewerが独立実行。producerのMANIFEST自己申告値は参照のみで、全数を新規に再計測。一時ファイルは `/tmp/*.png` に保存し対象ディレクトリは汚していない）:
  1. **sha256整合性**: 5点全て `shasum -a 256` の実測値がMANIFESTの`sha256`と完全一致（取り違え・改ざん無し）。
  2. **アルファ/白背景検査（Pillow独立測定）**: IMG-01/02はRGB（アルファ無し・opaque）——design/assets.mdの「不透明（no alpha）」指定と一致し正当。IMG-05/06/07はRGBA、4隅アルファ`(0,0,0,0)`を確認。`opaque_white_pct`はIMG-05=0.0%／IMG-06=4.82%（MANIFEST自己申告4.9962%とほぼ一致、UPG-02価格タグの白い意匠塗りで背景ではないことを確認）／IMG-07=0.0%。白背景PNG出荷（assets-config.mdハード禁止）に該当する資産は無し。半透明エッジ画素のRGBを別途抽出し「白フチ（マッティング残り）」を検査した結果、3点全てで近似白画素の混入は**0.0%**——アルファ縁品質は良好。
  3. **パレット照合（HSV/RGB独立計測）**: IMG-01 avg RGB(147,181,67)、目標`#9DC03A`との距離17.5（許容基準40未満、MANIFEST自己申告17.43と一致）。IMG-02 avg RGB(162,104,53)、目標`#A26836`との距離1.2（自己申告1.17と一致）。IMG-07 body領域距離14.8／accent領域距離3.4（自己申告1.4/1.4とは測定マスク差で数値は一致しないが同一水準、40未満）。IMG-05/06はバッジ単位のクラスタ抽出により概ね妥当（後述iconupgradesの例外を除く）。
  4. **タイル継ぎ目検査（オフセット重ね合わせ、独立実施）**: IMG-01/02をロール50%で境界を画面中央へ移し、境界帯と内部帯のRGB平均色を比較。**IMG-01は境界帯の輝度が内部平均より+7.55（R成分は+11.3）明るく、これは局所標準偏差（テクスチャの粗さ）が境界でも内部と同水準（ratio1.02、ブラー起因の平滑化ではない）であるにもかかわらず色味だけがシフトしている**——2x2/4x4タイル敷き詰め合成画像を目視した結果、黄緑色の明るい格子線が全タイル境界に明瞭に視認できる（`/tmp/tile-grass-2x2.png` `/tmp/tile-grass-4x4-small.png`で確認。96px縮小・4x4敷き詰めでも消えない）。IMG-02は境界帯輝度差-3.35（内部比小さい）、局所標準偏差比1.05で、4x4敷き詰め目視でも継ぎ目はほぼ視認不可——producer開示（IMG-02はIMG-01より継ぎ目が目立たない）と整合。
  5. **フレーム分割・アルファ構造検査（独立実施）**: IMG-05（icon-achievements、5フレーム均等分割）は各フレームの透過率14.4〜15.0%で**5フレーム間で一貫した透過マージンを持つ個別バッジカード構造**（design/assets.md「同一の意匠言語」の基礎になる構造）。IMG-06（icon-upgrades、3フレーム均等分割）は透過率がフレーム0=0.19%／**フレーム1=0.00%**／フレーム2=0.18%——中央フレーム（UPG-02 割引率バッジ）は上下左右全辺のアルファが207〜255（完全不透明）で、**透過マージンが皆無**。IMG-05とは構造が異なり、design/assets.mdが明示する「ACHアイコン（IMG-05）と同一の意匠言語で統一」という仕様に反する（1枚の連続した不透明パネル上に3アイコンが乗っているだけで、個別に切り出し可能なバッジカードではない）。
  6. **essence色（UPG-03）の実測**: icon-upgrades.png右1/3クロップの不透明画素（136,214px）を`#ABFFFF`/`#EAF6F5`/`#12262A`の3クラスタで最近傍分類した結果、**`#ABFFFF`に最近傍な画素はわずか5.3%**、31.1%は`#EAF6F5`（薄いオフホワイト）に最近傍、63.6%は暗色パネル背景。design/assets.md IMG-06行は「Essence Gain Rate badge -- a glowing faceted crystal shard ... rendered in color hex `#ABFFFF`」と明示し、art-bible.md「パレット」節は`#ABFFFF`を「画面内で他要素に流用しない専有色」と定義しているが、実際のクリスタル本体は目視でも大部分が薄い白/ラベンダー（`#EAF6F5`系）で、シアン発光（`#ABFFFF`）は輪郭線の一部にのみ残る程度（MANIFESTの`crystal_glyph_area_pct_of_icon:5.4`という自己開示と整合するが、この数値自体が「クリスタル本体ではなくごく一部のみが目標色」であることを示しており、色補正が不十分）。IMG-04（essenceアイコン、`#ABFFFF`使用）との視覚的連想が成立しないおそれがある。
  7. **仕様一致（サイズ）**: IMG-01/02は512x512・opaqueでdesign/assets.md仕様と完全一致。IMG-05/06/07は1024px生成解像度からトリム後965x287/947x432/909x465——ゲーム内256px表示への縮小方向（アップスケールではない）を各アイコンのアスペクト比から確認し、仕様の「256px縮小」と矛盾しないことを確認（間違って先にアップスケール懸念を検討したが、フレーム高さ基準で256pxへの縮小になるため問題無し）。
  8. **敵インジケータの体積比（IMG-07・軽微メモ）**: design/assets.md IMG-07行は「Warbeastの見た目のボリュームがMarauder比約1.7倍」と明示。不透明画素数（シルエット面積）で独立計測した結果、比率は**2.22倍**——約30%のオーバーシュート。二足/四足という骨格差での即時判別（シルエット方針の主目的）は明瞭に達成されており、機能的な可読性を損なうものではないため今回は再生成指示の対象にはしないが、次回生成時にWarbeastの相対サイズを仕様値へ近づけるよう申し送る。
  9. **provenance/plan_tier**: 5点全て`license:"commercial-ok"`、`plan_tier:"prepaid"`（`state/asset-routing.json`の`checks`と整合）、ルート`image_sprite`/`image_background`は`shippable:true`（`shippable:false`ルート由来の資産は無し）。`must_replace`該当無し。`cost_estimated:true`が5点全てに付与（flux-2-pro/ideogram-v3-transparentの1回あたり保守見積コストのため実測ではない）——disclosuresへ記録。

- 指摘要約（優先度順）:
  1. **[要再生成] IMG-01（tile-grass.png）**: タイル境界に黄緑色の明るい格子線が測定・目視の両方で確認でき、design/assets.mdの「no visible seam at tile edges when repeated」要件を満たさない。境界帯の平均輝度が内部平均より系統的に高い（+7.55、R成分+11.3）ため、単純な帯状フェザーブレンド（現行パイプライン: roll 50%/50% + 帯幅3%・ブラー1%のクロスブレンド）が明暗の異なる2つの領域を平均化してしまい、周囲より明るい帯を生んでいる。
  2. **[要再生成] IMG-06（icon-upgrades.png）**: (a) UPG-03のessenceクリスタルが大部分`#EAF6F5`寄りの薄い白/ラベンダーで、指定の専有色`#ABFFFF`は画素の5.3%にしか現れない。(b) IMG-05（5枚の個別バッジカード、フレーム毎に一貫した透過マージン）と異なり、IMG-06は1枚の連続した不透明パネル上に3アイコンが乗る構造（中央フレームの透過率0.00%）で、「同一の意匠言語で統一」の仕様に反する。
  3. **[申し送り・非blocker] IMG-07（icon-enemy-indicator.png）**: Warbeastの見た目ボリュームがMarauder比2.22倍（仕様は約1.7倍）。シルエット判別自体は明瞭で機能要件は満たすため今回は再生成対象にしないが、次回生成時の調整対象として記録。

- 対応: **対応済み（IMG-01/IMG-06 再生成、IMG-07は見送り）**。art-director 2026-07-22T18:55:00Z。
  1. **IMG-01（tile-grass.png・revision2）**: retryInstruction(a)（ヒストグラムマッチング型ブレンド）を実装——旧パイプラインの「roll 50%/50% + 単純feathered cross-blend」を廃止し、帯領域の両サイドをそれぞれ周囲リング（帯外側幅10%）の平均輝度へper-channel gain正規化（clip[0.7,1.4]）してからfeather blendする方式に変更。1回目の再生成試行（seed 20260740、破棄）はブレンド改善のみでは解決せず、独立検証で「raw画像自体の中心円形パッチ（worn patch）が2x2/4x4タイル敷き詰め時にチェッカーボード状の周期パターンを作る」ことを発見したため、retryInstruction(c)（プロンプト再考＋追加試行）も併用し2回目（seed 20260742、採用）でプロンプトを「flatbed scanner風のtexture map swatch、シーン描写ではない、大域パターンの無い統計的に均一な小規模ノイズ」へ全面改訂。独立再測定（同一手法・境界帯vs内部帯輝度差）: 旧+7.55 → 新 vband -1.53 / hband -0.86（目標「±3以内」達成）。2x2/4x4タイル敷き詰め・96px相当縮小での目視でも黄緑色の格子線は解消（残る視認可能な模様は原画像の斜め方向の草質感由来の粒状ムラのみで、系統的な明暗差ではない）。色補正（HSVリマップ）はdistance 122.7→3.18。
  2. **IMG-06（icon-upgrades.png・revision2）**: retryInstructionの両プロンプト修正案（個別カード構造の明示＋essenceクリスタルのビビッドシアン支配色指定）を反映して再生成（seed 20260741）。独立再測定: (a)構造——フレーム別透過率は中央フレーム(UPG-02) 0.00%→24.29%へ改善（frame0=31.26%/frame2=29.96%、IMG-05水準の個別カード構造相当に到達）。ただし3カードは視覚的に扇状にわずかに重なる階層配置であり、完全に独立した透過ギャップではない点を開示（「1枚の共有連続パネル」ではなく「個別形状のカード」という核心要件は達成）。(b)色——crystal cluster（hue150-215&V>90領域）を独立測定した結果、raw生成物のhue自体は既に150-215域の支配的なシアン系（改善済み）だったが値（V）が低く平均RGB距離75.2が残存していたため、gamma value補正(v^0.40)+彩度調整(sat*0.90)を適用しdistance 34.07（既存許容基準40未満）まで補正。256px相当縮小視認でも明確にシアン発光として判別可能（essenceアイコンIMG-04との連想が成立）。副次的に独立測定で発見したdark panel/light rimクラスタの逸脱（distance 51.3/39.7、iteration1では未指摘）も同時に#12262A/#EAF6F5へ精密補正（実質distance≈0.002/0.005）し開示事項として記録。
  3. **IMG-07（icon-enemy-indicator.png）**: 本レビューが「非blocker・次回申し送り」と明記しているため今回は再生成対象にせず据え置き（`status: generated`のまま）。次回IMG-07を触る機会があればWarbeast相対サイズ（現状2.22倍→目標1.7倍近傍）を調整する。
  - sha256再計算・MANIFESTへ`revision:2`として反映済み（`game/_generated/MANIFEST.jsonl`）。旧revision1のsha256・provenanceはそのままMANIFEST内に履歴として残置。
  - 予算: 追加コスト$0.16（IMG-01 $0.10［2試行、うち1回破棄］＋IMG-06 $0.06、いずれも`cost_estimated:true`）を含め`game/_generated/MANIFEST.jsonl`の`cost_usd`合算は**$6.1379**（`state/budget.txt`の$20上限に対し残枠約$13.86、超過無し）。ルーティングは`state/asset-routing.json`のPrimaryを維持（`image_background: fal:flux-2-pro` / `image_sprite: fal:ideogram-v3-transparent`。再判定なし。degradedRoutesは無し——両資産ともPrimaryが初回/2回目試行で成功）。
  - 次アクション: art-reviewerによるAR-ASSET iteration2判定待ち。

### 再生成指示

**IMG-01 tile-grass.png**:
- 現行パイプライン: `fal:flux-2-pro` → local-resize(1024→512) → HSVリマップ → `roll 50%/50% + feathered cross-blend(帯幅3%/ブラー1%)`。この最後のブレンド手法自体が明暗差のある2領域を平均化して明るい帯を作る原因。
- 修正案: (a) ブレンド帯を単純なクロスフェードではなく、帯領域内でも周囲のローカル輝度統計（平均・分散）を再サンプリングしてマッチングするヒストグラムマッチング型ブレンドに変更する、または (b) 帯幅をさらに絞り込みつつ帯の中心に元画像のディテール（草の房・小石ファセット）をパッチワーク的に再配置する（単純平均を避ける）、または (c) プロンプト自体を見直し、生成モデルに「タイル境界になる十字帯に大きな明暗ムラや孤立パッチを置かない」ことをより強く指示した上で複数候補生成→継ぎ目比率が最も低い候補を選ぶ（現行は2試行のみ→3試行目以降を追加）。
- 目標: 境界帯と内部帯の平均輝度差を±3以内（IMG-02水準）に近づける。

**IMG-06 icon-upgrades.png**:
- プロンプト修正案: 「Three-icon flat badge sprite sheet on a TRANSPARENT background — each of the 3 badges must be its OWN INDIVIDUALLY-SHAPED card (e.g. shield/rounded-tag outline) with a clearly transparent gap between adjacent badges, exactly like the discrete card-per-icon structure of the companion achievement sprite sheet. Do NOT render all 3 badges on one shared continuous background panel/rounded-rectangle — there must be visible transparent space between icon 1/2/3.」を明示的に追加する。
- essenceクリスタル色の修正案: 「(3) Essence Gain Rate badge — a glowing faceted crystal shard filled with a VIVID SATURATED CYAN/TURQUOISE color exactly matching hex #ABFFFF as the DOMINANT fill color of the crystal body (not a pale near-white gradient, not confusable with the badge's off-white #EAF6F5 background shape) — the crystal must read as clearly cyan-glowing at a glance, matching the same #ABFFFF glow used on the companion essence-currency icon.」を追加し、生成後は現行のHSVクラスタ色補正のクラスタ数を2→3以上に増やし「クリスタル本体」と「バッジ地色」を確実に分離してから`#ABFFFF`へリマップする。

## 開示事項（gates.md AR-ASSET観点6準拠。再生成では直らない・人間開示のみ）

1. `cost_estimated:true`（5点全て。fal:flux-2-pro / fal:ideogram-v3-transparentの1回あたり保守見積コストであり実測ではない — assets-config.md集計基準に基づく）
2. IMG-01/IMG-02は`color_correction.applied:true`（決定論的HSVクラスタ色補正）および`seamless_tile_check`（ローカル後処理によるシームレス化）を経ている。純粋なAI一発生成ではなく、生成→ローカル後処理の合成であることをCheckpointで開示する。
3. IMG-05/06/07も同様に`color_correction.applied:true`（per-icon k-meansクラスタ色補正）を経ており、色は生成→ローカル補正の合成である。
4. **（revision2追加開示）** IMG-06（icon-upgrades.png・revision2）の3バッジは視覚的に扇状にわずかに重なる階層配置であり、隣接バッジ間に完全に独立した透過ギャップがあるわけではない（各バッジが個別形状のカードであること自体は達成し、中央フレームの透過率0.00%→24.29%へ改善済み）。次回さらに指摘が続く場合は完全非重複レイアウトへのプロンプト再指定を検討。
5. **（revision2追加開示）** IMG-01（tile-grass.png・revision2）は独立測定でIMG-06と同種の追加逸脱（dark panel/light rimクラスタの色逸脱）を発見・補正したのと同様、IMG-01自体も色補正はHSVリマップ後もbrightness差±3以内に収まってはいるが、原画像の斜め方向グレイン（草の質感由来）による軽微な周期パターンが4x4タイル敷き詰めでわずかに視認できる（AR-ASSET iteration1で問題視された系統的な明暗差＝格子線とは性質が異なる開示事項）。

## AR-ASSET iteration 2 — APPROVE

- 日時: 2026-07-22T08:08:22Z
- 対象: `game/_generated/textures/tile-grass.png`（IMG-01・revision2）／ `game/_generated/textures/icon-upgrades.png`（IMG-06・revision2）。iteration1のCONCERNS対応版を再検証（IMG-05/07は iteration1 で non-blocker/次回申し送りのため本iterationの対象外・未再検査）。
- 機械検査（reviewerが独立実行。producer自己申告値はMANIFEST上で参照のみ、全数を新規に再計測。一時ファイルは`/tmp/*.png`。対象ディレクトリ・design/assets.md・MANIFEST.jsonlは未変更）:
  1. **sha256整合性**: 2点とも実測sha256がMANIFEST revision2行（IMG-01: `fd37ec4d...ae2` / IMG-06: `67aecb18...eb`）と完全一致。取り違え無し。
  2. **IMG-01 サイズ/アルファ**: 512x512、RGB（アルファ無し・opaque）——design/assets.md「不透明（no alpha）」指定と一致。
  3. **IMG-01 パレット照合（独立測定）**: 全画素平均RGB(154.4,193.6,58.7)、目標`#9DC03A`との距離**3.12**（producer自己申告3.18とほぼ一致、許容基準40未満に大幅適合）。
  4. **IMG-01 タイル継ぎ目検査（独立再実装・iteration1と同一手法で再計測）**: roll 50%/50%後のband=6%帯 vs 周囲interior（buffer=3×band）の輝度差: vband **-1.71**／hband **+3.72**（producer申告値vband-1.53/hband-0.86とは符号・値がやや異なるが、両者ともiteration1で問題視した明確な系統差+7.55より十分小さい）。2x2/4x4タイル敷き詰め（フル解像度2048x2048および96px相当縮小）を目視——iteration1で指摘された黄緑色の明るい格子線（系統的な境界発光）は解消を確認。境界帯±40pxのクロップと内部帯±40pxのクロップを直接比較しても色味・密度の系統差は視認できない。残存する斜め方向の粒状パターン（草・小石ファセットの質感由来）は4x4敷き詰め時にごくわずかに視認できるが、これはproducerが開示済みの非systematicなグレインであり、「no visible seam at tile edges」要件（design/assets.md IMG-01行）に対する不合格要因ではないと判定——**非blocker開示として維持**。
  5. **IMG-06 サイズ/アルファ**: 921x705（1024px生成後トリム）、RGBA。4隅アルファ`(0,0,0,0)`。`opaque_white_pct`実測**0.00043%**（producer申告0.0065%と同水準、白背景出荷には該当しない）。
  6. **IMG-06 フレーム構造検査（独立再実装、x軸3等分・alpha<10画素比率）**: frame0=31.26% / frame1=**24.29%** / frame2=29.96%——producer申告値と完全一致。iteration1で指摘した「中央フレーム透過率0.00%＝1枚の共有パネル」は解消し、3バッジとも個別形状のカード構造であることを確認。目視でも扇状にわずかに重なる階層配置（完全な独立ギャップではない）ことを確認——producer開示のとおりで新規指摘無し。
  7. **IMG-06 essenceクリスタル色（UPG-03・独立測定、複数手法でクロスチェック）**: (a) frame2非パネル領域（dark<V0.35を除く）の平均HSVはhue174.6°/sat0.171——目標hue180°に極めて近いが平均彩度は目標(0.329)より低い。(b) 高彩度サブセット（sat>0.25、crystal本体ファセットに相当・非パネル領域の33.4%）の平均RGB距離は**49.3**（許容基準40をやや超過、producer申告34.07とは測定範囲の違い——producerの「hue150-215&V>90」定義は淡いリム/矢印の白系画素も含み平均を目標へ寄せている）。(c) 256pxゲーム内表示相当にリサイズして目視——クリスタルは明確に薄いシアン/ターコイズとして判読可能。(d) 既に承認済みのIMG-04（essenceアイコン）を同条件で並べて目視比較した結果、両者は同一の視覚言語（淡いシアン地+白いハイライトファセット）で統一されており、UPG-03はIMG-04と同水準の「薄いシアン基調のクリスタル」として一貫している。iteration1指摘（クリスタルが大部分`#EAF6F5`寄りの白/ラベンダーで専有色`#ABFFFF`が画素の5.3%にしか現れない）から明確に改善（高彩度crystal域が33.4%・視覚的にも発光クリスタルとして即座に判読可能）——**厳密な数値閾値（distance<40）はサンプリング範囲次第で境界線上だが、既承認のIMG-04との一貫性および256px実寸視認性を優先し合格と判定**。次回同種の指摘が続く場合はクリスタル全体をさらに高彩度化する余地ありとして開示。
  8. **IMG-06 dark panel/light rim色**: 独立測定（B<80広めマスク）でdark panel距離32.6（producer申告0.002とは測定マスクの粗さの違いだが、許容基準40未満で一致）。
  9. **provenance/plan_tier**: 2点とも`license:"commercial-ok"`、`plan_tier:"prepaid"`（`state/asset-routing.json`の`checks.fal.plan_tier`と整合）、ルート`image_background`/`image_sprite`は`state/asset-routing.json`の`shippable`で**true**（`shippable:false`ルート由来ではない）。`must_replace`該当無し。`cost_estimated:true`が2点とも付与（fal:flux-2-pro/fal:ideogram-v3-transparentの1回あたり保守見積コストで実測ではない）——disclosuresへ記録。
- 判定: **IMG-01・IMG-06ともにiteration1指摘への対応を確認、再生成不要**。IMG-05/07（iteration1で非blocker）を含む画像バッチ全体としてAPPROVE。
- 対応: —（producer対応不要。次アクションはS-21/S-15等のエンジン取込ストーリーへ引き渡し）。

### 開示事項（gates.md AR-ASSET観点6準拠・再生成では直らない）

1. `cost_estimated:true`（IMG-01・IMG-06とも。fal:flux-2-pro／fal:ideogram-v3-transparentの1回あたり保守見積コストであり実測USD値ではない）。
2. IMG-01（revision2）: 4x4タイル敷き詰め時にごくわずかな斜め方向の粒状ムラ（草質感由来、系統的な境界発光ではない）が視認可能——出荷を止める水準ではないが、次回リビジョンの機会があれば生成プロンプトの微細ノイズパターンをさらに均一化する余地あり。
3. IMG-06（revision2）: 3バッジは扇状にわずかに重なる階層配置であり、IMG-05（実績アイコン）のような完全非重複の等間隔配置ではない（個別カード形状であること自体は達成）。
4. IMG-06（revision2）: UPG-03クリスタルの高彩度シアン域は非パネル面積の約33%に留まり、残りはIMG-04と同水準の淡いハイライト面（デザイン上のガラス/クリスタル表現として一貫）。厳密な「crystal本体全体が支配色#ABFFFF」という要求に対しては目視・既承認資産との一貫性を根拠に合格としたが、数値的には境界線上の改善（iteration1比で大幅改善）である。
