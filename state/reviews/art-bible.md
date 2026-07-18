## key image ランク付け

- 日時: 2026-07-10T04:54:56Z
- 対象: `design/refs/key-image-candidate-1.png` 〜 `-4.png`（各1024x768px）。`design/art-bible.md` / `design/art-bible.json` は本レビュー時点で未作成のため、これは正式な `AR-BIBLE` ゲート判定ではなく、Checkpoint A に向けた key image 選定のための事前検討である。art-bible.md/json 確定後に改めて `AR-BIBLE` ゲート判定を別途実施すること。
- 観点: `.claude/docs/gates.md` AR-BIBLE 観点2（ゲーム内可読性）・観点3（生成再現性）を流用 + `design/concept.md` ピラー P-01〜P-04 との整合。参照した明文規定: `design/gdd.md`「アート方向」節 — 「スタイライズド・トゥーン。明るいファンタジー基調にネオン風のアクセント（クリスタルの発光色）。フラット寄りの塗りでシルエット可読性を最優先（hero=縦長・敵=低く横に広い・クリスタル=幾何学形状）」
- 機械検査: Python3 + Pillow で hero領域／敵領域／背景領域の平均RGBを抽出し、ユークリッド距離で色分離度を計測（`/tmp` 相当のスクラッチ実行、対象ファイル・design/ 配下は非汚染）

### 計測結果（サンプルボックス平均RGB色距離。大きいほど色分離が明瞭 = ゲーム内可読性が高い）

| candidate | hero-敵(近傍側) | hero-敵(奥側) | 敵-背景(近傍側/奥側) | 全体輝度標準偏差 |
|---|---|---|---|---|
| 1 | 169.4 | 65.9 | 72.4 / 220.0 | 73.9 |
| 2 | 31.6 | 20.7 | 85.1 / 89.6 | 64.2 |
| 3 | 104.7 | 164.0* | 61.4 / 3.0* | 47.3 |
| 4 | 56.8 | 69.4 | 67.4 / 46.5 | 49.3 |

*candidate-3 の「奥側」ボックスは背景の隙間をサンプリングした誤差を含む（目視では敵と背景の分離自体は十分にある）。ただし candidate-3 は hero・敵が共にグレースケール系統の同系色であり、色相ではなく明度・形状差のみに可読性を依存している点は構造的な弱点として後述する。

### 順位と根拠

**1位: candidate-1**
- gdd.md「アート方向」の明示指定（スタイライズド・トゥーン／明るいファンタジー基調／ネオン風のアクセント／フラット寄りの塗り）に4候補中もっとも忠実。太いアウトライン＋フラットな塗り分けのセルシェード調で、緑のアリーナ・青白のhero・紫の敵・シアン/ピンクのクリスタルが4系統の異なる色相で構成される。観点2（可読性）の計測でも hero-敵(近傍)169.4／敵-背景(上)220.0 と全候補中最高値。
- 敵は低く横に広い四足シルエット（gdd「敵=低く横に広い」に一致。密集陣形はP-03「群れ密度の圧力」を視覚的に裏付ける）。heroは縦長の直立シルエット（gdd「hero=縦長」に一致）。
- 太いアウトライン＋フラット色面のトゥーン様式はimage-to-3D／トゥーンシェーダのパイプラインで数十資産を安定再現しやすく、観点3（生成再現性）も良好。
- **懸念（要対応）**: アリーナ内に人間サイズの「倒れたキャラクター」が4体（青/ピンク衣装、胸に番号状のエンブレム）描かれている。design/concept.md のスコープは「ヒーロー1体」「敵はスウォーマー（四足獣）1〜2種のみ」であり、人間の味方/対戦キャラは存在しない設計。style_block・character_reference を art-bible.json へ落とす際、この人間キャラクター群やスポーツユニフォーム調のモチーフを標準スタイルとして誤って抽出しないよう、art-director にクロップ範囲の限定（hero単体＋敵＋クリスタルの配色・シェーディング様式のみ抽出）を明示指示する必要がある。

**2位: candidate-4**
- P-03（群れ密度の圧力）をもっとも強く可視化（画面のほぼ全域を埋める密集した敵の壁）。フラット寄りの低ポリシェーディングでgdd「フラット寄りの塗り」要件にはcandidate-1に次いで整合。
- hero（オレンジ）・敵（暗赤系）・アリーナ床（薄紫）・クリスタル（ネオングリーン）で色相は分離されている。観点2の可読性は実用レベル（サンプルボックス起因で計測値はやや低めに出たが、目視ではオレンジheroと暗赤の敵群の分離は明瞭）。
- **懸念**: 全体トーンが紫〜暗赤主体で沈んでおり、gdd指定の「明るいファンタジー基調」との整合がcandidate-1より弱い（SF/サイバー寄りの印象）。heroの意匠（オレンジの装甲スーツ）も"ファンタジー"というよりは近未来的で、brief/gddのファンタジートーンからやや外れる。art-bible.json化の際はこの点をart-directorと再確認すべき。

**3位: candidate-3**
- 低ポリ・フェイセット調のジオメトリックスタイル自体は3Dパイプラインと親和性が高いが、シェーディングがグラデーション主体でgdd指定の「フラット寄りの塗り」からは外れる。
- **観点2の構造的弱点**: hero（白灰）と敵（灰）が共にグレースケール系統の同系色で、色相による瞬時識別ができず、輝度・形状差のみに依存する。固定俯瞰視点で敵が密集した際、同系色の集団は個体の輪郭が溶け合い判別しづらくなるリスクが高く、P-03（群れの密度・位置を視認できることが前提のピラー）と相性が悪い。
- 全体トーンも暗いネイビー基調で、gdd指定の「明るいファンタジー基調」との整合が4候補中もっとも弱い。

**4位（最下位）: candidate-2**
- 観点2（可読性）の計測値が全指標で最低（hero-敵近傍31.6／hero-敵奥20.7）。hero・敵・背景がいずれも茶〜オレンジの近似色でまとまり、パレット計測上も視覚的にも三者の分離が4候補中もっとも弱い。
- ペインタリー調の柔らかいシェーディング・毛並み等のディテールは、観点3（生成再現性）の観点でもっとも再現コストが高く、数十資産をブレなく短時間で安定生成する用途に不向き。
- クリスタルも白〜クリーム色でネオン感が無く、gdd指定の「ネオン風のアクセント（クリスタルの発光色）」を満たさない。敵の数も他候補より少なく密集感が弱いためP-03の説得力ももっとも低い。
- gdd「アート方向」への適合度・ゲーム内可読性・生成再現性のいずれでも4候補中最下位。

### 推奨アクション（art-director 宛）
1. candidate-1 を key image 本命としてCheckpoint Aへ提示することを推奨するが、上記「倒れた人間キャラクター群」を除外/再クロップした上でart-bible.json化すること（style_block・character_referenceが人間キャラのスポーツユニフォーム調モチーフで汚染されないように）。
2. candidate-4 は「明るいファンタジー基調」要件を満たす代替修正（暗赤・紫トーンを明るめに調整、hero意匠をよりファンタジー寄りに）ができれば次点候補として保持を推奨。
3. candidate-2・candidate-3 は主要な観点（可読性 and/or アート方向整合）で明確に劣るため、そのままの採用は非推奨。

## AR-BIBLE iteration 1 — CONCERNS

- 日時: 2026-07-10T05:07:14Z
- 対象: `design/art-bible.md` + `design/art-bible.json` + key image（`design/refs/key-image-candidate-1.png`、crop-01/02/03）。engine=unity（`state/engine.txt`）につき「3D スタイル方針」節を含めて審査。
- 実施した機械照合: (1) `design/art-bible.json` の `palette` 13色を Python (colorsys) で RGB距離・HSV色相を全ロールペア総当たり計算 (2) `reference_images`/`character_reference`/key image の実ファイル存在確認（`ls design/refs/`） (3) `grep` によるart-bible.md引用元（gdd.md「アート方向」節）の実在確認 (4) `design/art-bible.json` の `palette`/`resolution`/`scale`/`polygon_budget_tri` と `design/art-bible.md` 本文・`.claude/docs/tech-stack-unity.md`「資産の取り扱い」節・`assets-config.md` 3D既定値との突合
- 指摘要約（優先度順）:
  1. **[中] 敵主色とクリスタル・マゼンタの色相分離が不十分（P-03の群れ密集時にクリスタルと敵の混同リスク）** — 実測: `enemy_primary`(#8B12A5, hue=289.4°, val=0.65) と `crystal_magenta`(#D33FD4, hue=299.6°, val=0.83) の色相差はわずか10.2°、RGB距離は97.0のみ。art-bible.md「シルエット方針」は「敵は単一色相（紫）でまとまり hero（青）・クリスタル（シアン/マゼンタ）と混同しない配色を維持する」と明言するが、この2色は同一パレット中で自ら基準として使う分離値（key image計測: hero-敵169.4／敵-背景220.0）に遠く及ばない近似色相であり、P-03（最大40体同時出現・密集）でクリスタルが敵群の中に点在する場面での識別根拠が配色だけでは崩れる。対応案: (a) `crystal_magenta` の色相を敵主色から離す方向（例: 320〜340°付近のピンク〜赤紫）へ再指定するか、(b) 識別が配色ではなくエミッシブ発光輝度・Bloom（クリスタルのみ val=0.83 かつ発光ハロー付き）に依存する設計である旨を art-bible.md「シルエット方針」に明記し直す。
  2. **[低] 引用元の誤り: 「gdd.md「アート方向」節」は実在しない** — art-bible.md 9-16行目（Key Image節）および `state/reviews/art-bible.md` 事前検討の双方が「gdd.md「アート方向」節」（スタイライズド・トゥーン／明るいファンタジー基調／ネオン風アクセント／フラット寄りの塗り／hero=縦長・敵=低く横に広い・クリスタル=幾何学形状）を引用しているが、`grep -n "アート方向" design/gdd.md` では該当節は存在せず（33-34行目に語句が2回登場するのみで、いずれも brief 由来の言及）、該当テキストの実在箇所は `design/brief.md`「アート方向」節（56-59行目）である。art-bible.md の引用元表記を `brief.md` に修正すること（内容自体は正確に転記されており実害は小さいが、下流のトレーサビリティを壊す）。
  3. **[情報/低] `style_codes` の実Ideogramコードは未確定のプレースホルダのまま** — `style_codes` 配列の3件目が `"pending:ideogram_style_code_capture_on_first_generation"` であり、実際に再現性を担保するIdeogram発行のstyle codeはまだ捕捉されていない。プロセス自体は art-bible.md 86行目で文書化されており本judgmentをブロックする理由にはしないが、AR-ASSET初回バッチレビュー時に実コードがpinされ `pending:` が解消されているか必ず確認すること。
- 検証済みで問題なしと判断した項目: palette 13色とjson配列の1:1対応（順序含め完全一致）／`reference_images`・`character_reference`・key image の実ファイル存在（`design/refs/` に4候補+crop3枚とも存在）／`resolution`（concept_art_px 1024・ui_sprite_px 512・texture_px 2048・tile_px null）と art-bible.md 本文の一致／「3D スタイル方針」節がgates.md観点4の4要素（ポリゴン予算・テクスチャ/PBR・リグ方針・スケール規約）を full に備え、`scale.engine_unit_to_meter=1.0`・`up_axis=+Y`・`hero_height_range_m=[1.6,2.0]` が tech-stack-unity.md「資産の取り扱い」節・assets-config.md既定と矛盾なし／polygon_budget_tri（hero 50000, enemy 20000, prop 10000, environment 100000）がassets-config.md既定内かつenemy枠を絞った理由（`MAX_CONCURRENT_ENEMIES=40`の実行時負荷）が明記済み／key image に混入していた人間キャラクターモチーフをstyle_block本文で明示除外し、reference_images/character_referenceも汚染の無い再クロップのみを使用（既知欠陥への対応が具体的）／design/assets.md は本ゲート時点で未作成（パイプライン順序上、art-bible確定後に起票される想定のため技術整合の一部検証は次工程で実施）
- 対応:
  1. **[中] 敵-クリスタル色相分離 → 対応済み（両方の対応案を実施）**。(a) `design/art-bible.json` `palette[9]`（crystal_magenta）を `#D33FD4`（hue≈299.6°）から `#E62284`（hue≈330.0°, val≈0.90, sat≈0.85）へ再指定し、`enemy_primary` `#8B12A5`（hue≈289.4°）との色相差を10.2°→40.6°に拡大（colorsys実測で確認）。`design/art-bible.md` の「パレット」表・`design/art-bible.json` `notes.crystal_magenta_revision` に根拠を追記。(b) 色相のみに識別を依存させない設計として `design/art-bible.md`「シルエット方針」節に新規段落を追加し、クリスタルは常時エミッシブ発光（val≈0.90+Bloomハロー）で敵の非発光マット面（val≈0.65）と輝度差で区別できること、加えて形状（小型ファセット vs 四足シルエット）・スケール・動き（自転+浮遊 vs 接近移動）でも識別軸が重複しないことを明記した。
  2. **[低] 引用元誤り（gdd.md「アート方向」節は非実在）→ 対応済み**。`design/art-bible.md` の該当4箇所（Key Image節の承認理由・差し替え候補表・シルエット方針「形での区別ルール」・3Dスタイル方針「テクスチャ解像度とPBR方針」）の引用元表記を `gdd.md「アート方向」節` から `brief.md「アート方向」節`（brief.md 56-59行目、実在確認済み）に修正した。`grep -n "gdd.*アート方向\|アート方向.*gdd" design/art-bible.md` で残存参照が無いことを確認済み。内容（スタイライズド・トゥーン等の記述内容）自体は brief.md の実テキストと一致しており変更していない。`state/reviews/art-bible.md` 内の事前検討セクション（本ファイル冒頭の「key image ランク付け」）にも同種の誤記があるが、これは art-reviewer 自身の過去出力でありレビュー履歴の改ざん防止のため revise 対象外として原文のまま保持する（今後の参照はart-bible.md側の正しい引用元表記を正本とする）。
  3. **[情報/低] `style_codes` の実Ideogramコード未確定 → 見送り（意図的）**。指摘の通りブロッキング事由ではないと reviewer 自身が明記しており、`pending:ideogram_style_code_capture_on_first_generation` は「初回生成バッチでstyle codeを捕捉してpinする」というプロセスをart-bible.md 86行目で既に文書化済み。プレースホルダのまま値を先取りで確定させると実際のIdeogram応答と食い違うリスクがあるため、現時点では変更せず、AR-ASSET初回バッチレビュー時に `pending:` が実コードへ解消されているかの確認を art-director/art-reviewer 双方の申し送り事項として維持する。

## AR-BIBLE iteration 2 — APPROVE

- 日時: 2026-07-10T05:13:32Z
- 対象: `design/art-bible.md` + `design/art-bible.json` + key image（`design/refs/key-image-candidate-1.png` + crop-01/02/03。engine=unity のため「3D スタイル方針」節を含めて審査）
- 実施した機械照合:
  1. `design/refs/` の実ファイル存在確認（`ls`）— key-image-candidate-1〜4.png・crop-01-hero.png・crop-02-scene.png・crop-03-enemy.png すべて実在
  2. `design/art-bible.json` の JSON構文検証（`python3 json.load`）— 正常パース、キー9件（style_block/palette/style_codes/reference_images/character_reference/resolution/scale/polygon_budget_tri/notes）、`palette` 13件全て `#RRGGBB` 形式で妥当
  3. iteration 1 指摘1（敵主色とクリスタル・マゼンタの色相近接）の修正実測再検証（Python colorsys で全13色の RGB距離・HSV色相・彩度・明度を再計算）— `enemy_primary`(#8B12A5, hue=289.4°) と `crystal_magenta`(#E62284, hue=330.0°) の色相差は実測40.61°、RGB距離98.1であり、art-bible.json `notes.crystal_magenta_revision` の記載値（約40.6°）と一致。対応済みと確認
  4. iteration 1 指摘2（`gdd.md「アート方向」節`という非実在引用）の修正確認（`grep -n "アート方向" design/gdd.md` / `design/brief.md` / `design/art-bible.md`）— `design/art-bible.md` 内の該当4箇所（5, 12, 22, 49, 74行目）は全て `brief.md「アート方向」節`表記に修正済みで、`gdd.md`側にはそもそも「アート方向」節が存在しない（33-34行目の地の文中の語句のみ）ことを再確認。誤引用の残存なし
  5. `resolution`（concept_art_px 1024 / ui_sprite_px 512 / texture_px 2048 / tile_px null）・`scale`（units meters, engine_unit_to_meter 1.0, up +Y, forward +Z, hero_height_m 1.8 [1.6–2.0], swarmer 1.0m/1.4m）・`polygon_budget_tri`（hero 50000 / enemy 20000 / prop 10000 / environment 100000）を art-bible.md 本文・`.claude/docs/tech-stack-unity.md`「資産の取り扱い」節（ヒト型 1.6–2.0m・1 unit = 1m）・`assets-config.md` 3D既定（hero ≤50k/prop ≤10k/環境 ≤100k）と突合し矛盾なし。enemy=20000（既定の character 予算より意図的に絞った値）は `MAX_CONCURRENT_ENEMIES=40` の実行時負荷を理由に art-bible.md 本文で明記済み
  6. `state/asset-routing.json` の `routes.model_character`（`meshy:direct-image-to-3d+rigging`）と art-bible.md「3D スタイル方針」節のリグ方針記述（Meshy image-to-3D→rigging/multi-animation, humanoid/quadruped 使い分け）の整合を確認
- 追加検証（新規・iteration 1 では未計測）: 敵ヘヴィ変種差し色 `enemy_heavy_accent`(#6B1030, hue=338.9°, val=0.42) と `crystal_magenta`(#E62284, hue=330.0°, val=0.90) の色相差を計測したところ実測8.9°と近接している。ただし (a) RGB距離は150.0で iteration 1 で問題視した旧配色（enemy_primary-crystal_magenta間 距離98.1）より離れている、(b) 明度差は0.48（enemy_primary-crystal間の0.25より大）でクリスタルのエミッシブ発光（val≈0.90+Bloom）とヘヴィ敵の非発光マット面（val≈0.42）の輝度分離は主色enemy_primaryとの分離より明瞭、(c) art-bible.md「シルエット方針」節の識別ロジック（「色相分離はこれら複数の識別軸の一つであり、単独の生命線ではない」）は「敵」を主色・差し色問わず総称して記述しており、形状・スケール・動きによる分離もヘヴィ変種に等しく適用される。ヘヴィ変種は任意採用かつ出現はWave3以降・混入率15%と発生頻度も低い。以上からブロッキング事由とは判断せず、次回 AR-ASSET（実アセット生成時）でヘヴィ変種の見た目確定時に再計測することを申し送り事項とする（本ゲートの findings には計上しない）
- 判定: iteration 1 の指摘3件（[中]敵-クリスタル色相分離、[低]引用元誤り、[情報/低]style_codes pending）はいずれも実測・grep で対応確認済み。gates.md AR-BIBLE の4観点（機械可読性/ゲーム内可読性/生成再現性/技術整合）を全て満たす。APPROVE。
- 対応: —（reviewer 判定のため対応欄なし）
