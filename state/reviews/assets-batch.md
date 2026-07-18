# レビュー履歴 — assets-batch（バッチ横断一貫性チェック / style drift検出 pass 1、engine=unity）

このartifactは個別資産（IMG-xx / MDL-xx 単体）の仕様一致・アルファ縁・provenance等の反復審査を扱わない（それらは `state/reviews/assets-images.md` / `assets-images-prototype.md` / `assets-models-prototype.md` / `assets-audio*.md` が正本で、既に複数iterationにわたり判定済み）。本ファイルは **画像資産（IMG-01〜04）と3Dモデルのレンダリングプレビュー（preview-hero.png=MDL-01, preview-swarmer.png=MDL-02）を `game/_generated/MANIFEST.jsonl` の生成順に並べたときの、バッチ横断のパレット逸脱・画風ブレの時系列劣化** に判定対象を限定する。

## AR-ASSET iteration 1 — CONCERNS

- 日時: 2026-07-13T10:15:00Z
- 対象: 画像資産 IMG-01〜04（各資産の最終有効revision）+ 3Dモデルレンダリングプレビュー preview-hero.png（MDL-01由来）/ preview-swarmer.png（MDL-02由来）。`design/art-bible.json` の `palette`（13色）・`style_block`（flat cel-shaded + bold 2-3px dark-navy outline + AO陰影 + 役割別単色ブロッキング）を基準に照合した。
- 検査方法: Pillow+numpy によるRGBA/アルファ抽出、8/16刻み量子化パレットクラスタ抽出＋13色パレットへのRGBユークリッド距離計算、境界画素のHSV分布集計（連続グラデーション vs 離散フラットブロッキングの判別）、`find game -iname "*toon*" -o -iname "*outline*"` によるUnityプロジェクト内トゥーン/アウトラインシェーダ資産の有無確認、`qa/evidence/asset-integration-hero-visual.png`（Integrate実測RenderTexture screenshot、2026-07-13T09:03生成）とのクロスチェック、`git show <commit>:<path>` によるIMG-01の3リビジョン（rev1/rev2/rev3）の抽出・並列比較、Readツールによる目視確認。
- 生成順タイムライン（`generated_at` 昇順、各資産の実効時刻）:
  1. IMG-03（crystal icon）rev2最終: `2026-07-10T06:28:10Z`
  2. IMG-01（hero concept）rev1（image-to-3D入力として実際に使用された版）: `2026-07-10T06:20:01Z` — 以後 rev2: `06:42:34Z`、rev3（現行出荷版）: `2026-07-11T00:00:00Z`
  3. IMG-02（swarmer concept）rev2最終: `2026-07-10T06:44:00Z`
  4. MDL-01（hero model、ジオメトリはIMG-01 rev1由来のまま全revisionで不変）: `2026-07-10T06:22:03Z`〜（Integrate検証 revision:4: `06:22:03Z`起点、screenshot証跡は`2026-07-13T09:03Z`更新）
  5. MDL-02（swarmer model）: `2026-07-10T06:22:45Z`〜
  6. IMG-04（hit vfx）: `2026-07-12T23:51:45Z` — Phase 3 buildバッチで約2日空けて追加生成（他の画像/3D資産と別セッション）

### 観点別結果

**【最優先・新規エスカレーション】MDL-01/MDL-02 のレンダリング結果が style_block の必須要件（flat cel-shaded + 太いダークネイビーアウトライン）を満たしていない — CONCERNS（既知事項の継続、最新Integrate証跡で未解決を裏付け）**

`preview-hero.png` / `preview-swarmer.png`（Blender headlessレンダー）および `qa/evidence/asset-integration-hero-visual.png`（Unity実機RenderTexture screenshot、Integrate実施者による2026-07-13T09:03生成の最新証跡）のいずれも、なめらかな方向性ライティングによるPBRグラデーション陰影のみで構成されており、`style_block` が要求する「flat cel-shaded color fill with clean bold 2-3px dark-navy outlines on every silhouette edge」「single-hue color blocking per role」は視認できない。

- HSV分布による定量確認（プレビュー画像を8刻み量子化）: hero側は青系統だけで6段階以上の連続的な明度クラスタに分散しており（例: rgb(64,128,184)〜rgb(72,136,192)の近接クラスタが複数）、離散的な「2-3色のフラットブロッキング」ではなく連続グラデーションであることを裏付ける。swarmer側も同様（enemy_shadow/hero_secondary/enemy_primaryの3クラスタにまたがる連続分布）。
- `game` プロジェクト内を `find -iname "*toon*" -o -iname "*outline*"` で検索した結果、`game/Library/PackageCache/`（Unity標準パッケージのサンプル同梱物）以外にトゥーン/アウトラインシェーダ資産は存在しない。MDL-01 revision:4 の Integrate 記録も `"unity_material_urp_fix": "pass (shader Universal Render Pipeline/Lit; ...)"` と明記しており、標準PBR/Litシェーダがそのまま使われていることをMANIFEST自身が裏付けている。
- `design/art-bible.md`「3D スタイル方針」節は、アウトラインを「コンセプト画側に描き込んだ状態でimage-to-3D生成することでベイクする」方針を採用し、専用トゥーンシェーダ/ポストプロセスは「実装余力があれば任意検討（必須要件ではない）」と位置付けている。しかし実際の生成結果ではこのベイクが機能しておらず、テクスチャ・レンダリングいずれにも輪郭線が転写されていない。
- **本件は新規発見ではない**: `state/reviews/assets-models-prototype.md` の AR-ASSET iteration 1〜4（2026-07-10〜11）で同一欠陥が繰り返し検出・定量確認（iteration4ではHSV value<0.15画素0.00%という直接測定）され、「3Dモデル単体の再生成では解消見込みが低く、Integration側でのURP Outlineポストプロセス適用を『任意』から『実質必須』に格上げする」申し送りが4回連続でなされている。本pass-1バッチチェックは、**Phase 3 build のIntegrate実施（2026-07-13T09:03、iteration4の申し送りより後）でもなおこの格上げが実行されず、標準Litシェーダのまま出荷候補の状態に留まっている**ことを、最新のin-engine screenshotで独立に再確認した点が新規の付加情報である。
- 影響: `design/art-bible.json` のスタイルロックは2D/3D共通の単一ルールとして定義されているが、実際の出荷候補資産では **HUD/UIスプライト（IMG-01〜04）は正しくcel-shaded + 太い輪郭線で描画される一方、プレイ画面の主役である3Dキャラクター（Hero/Swarmer）だけがスタイル逸脱**した状態になる。プレイヤーが最も長時間見る要素（自機・敵）で画風が破綻するリスクが高く、Checkpoint C提示前に対応要否を人間判断へ上げるべき優先度の高い事項と判断する。

**再生成指示（優先度最高）**: 3Dモデル自体の再生成（Meshy呼び出しのやり直し）はこれまで3回試みても解消しなかった経路のため推奨しない。代わりに以下を推奨する。
1. **[Integration側・最優先]** Hero/Swarmerマテリアルへ軽量アウトライン手法（例: インバートハル法によるバックフェース法線押し出しの単色シェーダパス、またはURPのフルスクリーンpostprocessによる法線/深度エッジ検出アウトライン）を追加適用し、シェーダを `Universal Render Pipeline/Lit` 単体から「Lit + Outlineパス」構成へ変更する。色はダークネイビー（`#12081F` または `#164583` 系、2-3px相当）に固定。art-bible.mdの「任意検討」を「実質必須」へ格上げする申し送りをそのまま実行に移す。
2. **[art-director・優先度中・併用推奨]** 今後 IMG-01/IMG-02 相当の3Dモデル用コンセプト画を再生成する機会があれば、輪郭線がimage-to-3Dのテクスチャ転写で消失しないよう、プロンプトへ「輪郭線はテクスチャのalbedoに焼き込まれた高コントラストの黒い縁取り線として明示的に描画され、3D変換後もUV展開されたテクスチャ上に幅2-3px相当で保持される」旨の指示を追加し、変換後に albedo テクスチャを直接サンプリングしてvalue<0.2の輪郭画素比率を計測する検証ステップを生成後パイプラインに追加する（過去のiteration4で実施した検証手法をルーティン化）。ただし(1)の対応で解決する場合はこちらは必須ではない。

---

**【中・新規】MANIFEST Integrate-phase 追記行（MDL-01 revision:4 / MDL-02 revision:3 / ANM-01〜03 revision:4）で `license` / `plan_tier` / `sha256` / `cost_usd` が欠落 — 記録改善指摘**

`game/_generated/MANIFEST.jsonl` の該当5行（asset_id: MDL-01 rev4, MDL-02 rev3, ANM-01/02/03 rev4）は `revision_reason` 内で「ファイル本体の変更なし（sha256同一、メタデータのみの追記revision — IMG-02 revision2/SFX-04 revision2 と同型）」と説明しているが、実際のキー集合を比較すると **IMG-02 revision:2（行19-20）や SFX-04 revision:2（行16）は `license`/`plan_tier`/`cost_usd`/`sha256` を引き続き記録している** のに対し、上記5行はこれらのフィールドを一切持たない（`asset_id`/`file`/`generated_at`/`revision`/`revision_of_sha256`/`revision_reason`/`validator` のみ）。「同型」という記述は正確ではなく、provenance追跡の一貫性が本5行のみ崩れている。

これはassets-config.mdの必須フィールド定義（3D資産は`license`等も含め必須）に照らすと記録漏れに相当するが、ファイル本体自体は変更されておらず元の生成エントリ（MDL-01初回行・MDL-02初回行・ANM-01〜03初回行）に完全なprovenanceが既に存在するため、**出荷可否には影響しない**（再生成不要、記録追記のみで解消可能）。

**再生成指示（優先度中・記録追加のみ）**: 該当5行へ、参照元revision（`revision_of_sha256`が指す各資産の直近の完全provenanceエントリ）から `license`/`plan_tier`/`cost_usd`（Integrate作業自体はコスト$0のため`cost_usd:0`）を転記すること。

---

**【低・確認のみ】IMG-04（hit vfx、Phase 3 buildで約2日後に追加生成）— PASS（パレット/画風ブレなし）**

主要クラスタは `rgb(32,48,128)`（`#164583`最近傍・距離23.5）・`rgb(80,192,192)`（`#4FE8E2`最近傍・距離52.5）・`rgb(240,240,240)`（`#F2F5FA`最近傍・距離11.4）で、指定色「白系ハイライト＋シアン#4FE8E2寄りの発光」（`design/assets.md` IMG-04記載）と一致し、アルファ・寸法（512x512, RGBA, 4隅透過）も仕様通り。ダークネイビー輪郭線もIMG-01〜03と同系統の色で存在する。**唯一の観察事項（非ブロッキング）**: 星形の各光条にジグザグの二重線（細いギザギザ輪郭）が使われており、キャラクター資産（IMG-01/02）の滑らかな単一2-3px輪郭線とは描線処理が異なる。`style_block`はVFX用の描線規約を明示していないため仕様違反ではないが、今後VFX系資産を追加する場合の描線スタイル一貫性の参考として記録する。

---

**【既知・継続開示のみ・変化なし】IMG-01のトリムハイライト色ラベンダー寄りシフト／IMG-01(2D)とMDL-01(3D)のキャラクターデザイン差分**

- IMG-01現行rev3のトリムハイライト（`rgb(192,176,240)`前後、パレット`#F2F5FA`との距離約85.8）が白から淡いラベンダーへ寄っている件は `state/reviews/assets-images-prototype.md` AR-ASSET iteration 6 で既に独立検証・許容確定済み（既存許容基準=IMG-03マゼンタハイライト距離111.1の範囲内、役割別色分けの混同なしと判断）。本pass-1チェックでも同一クラスタ・同距離を再確認したのみで新規劣化はない。
- IMG-01は rev1（image-to-3Dへ実際に入力された痩身・フラットカラーの意匠）→ rev2（丸みを帯びたグロス表現＋緑の設置影ブロブ）→ rev3（現行、いかつい"パワードスーツ"的な意匠、設置影解消）と3回意匠が変化しているが、MDL-01のジオメトリはrev1由来のまま全revisionで不変（`state/reviews/assets-models-prototype.md`のrevision_reasonが明記する通り「ジオメトリ/リグ生成プロンプトは初回revisionと同一」）。したがって**現行出荷2Dコンセプト画（rev3）と現行出荷3Dモデルは、同じ「hero」でも異なるプロポーション/意匠を指す状態**になっている。この事実は`assets-images-prototype.md` iteration3で「provenanceの実体乖離」として既に開示・許容済み（3D入力としての実害はrev1使用時点で確定済みのため無し、と判断されている）。本pass-1チェックでは事実関係を再確認したのみで、新規の再生成対象とはしない（IMG-01を再度3D入力として使う計画は無いため実害は限定的と判断するが、Checkpoint Cでの開示継続を推奨）。

**【既知・継続開示のみ】MDL-02 quadruped未リグ（`must_replace:true`）／ANM-04未生成**

色相・パレット一致自体は`assets-models-prototype.md` iteration2〜4で独立検証済みPASS（目標`#8B12A5`との差0.01–0.03°）。リグ未完了はCheckpoint B（2026-07-13）で「build フェーズで静的メッシュとして取込み、接近表現はコードモーションで代替する」旨が追認済み（`design/assets.md` MDL-02行）。本pass-1チェックのスコープ（パレット/画風）には抵触しないため対象外、継続開示のみ。

### 総合判定: CONCERNS

理由: IMG-01〜04は個別資産としては既存レビュー（assets-images.md / assets-images-prototype.md）で反復審査済みで、本pass-1のバッチ横断チェックでも新規のパレット逸脱・画風ブレは検出されなかった（IMG-04を含め時系列で一貫）。一方で **MDL-01/MDL-02 のレンダリング結果（Blenderプレビューおよび最新のUnity実機screenshot）が style_block の中核要件（flat cel-shaded + 太いダークネイビーアウトライン）を満たしていない状態が、4回の既存レビューでの申し送りにもかかわらずPhase 3 buildの現時点まで未解決** であることを最新証跡で確認した。この欠陥は個別資産のブロッキング不合格（REJECT）に値する性質ではなく、明確な原因（Integration側のシェーダ構成が標準PBR/Litのままでアウトラインパスが無い）を伴う技術的欠陷であり、対応経路も明確（シェーダ追加）なため REJECT ではなく CONCERNS とするが、**プレイ中最も視認時間が長い自機/敵キャラクターにおけるスタイルロック不適合**という影響範囲の大きさから、Checkpoint Cで人間へ明示的に開示すべき最優先事項として扱う。加えてMANIFEST Integrate-phase行のprovenance記録漏れ（中優先度、記録追記のみで解消可）を新規指摘とする。

### 再生成指示（優先度順、詳細は各節を参照）

1. **[最優先]** MDL-01/MDL-02（Hero/Swarmerマテリアル）へIntegration側でアウトラインシェーダ/ポストプロセスを追加適用し、`design/art-bible.json` style_block の輪郭線要件を満たすこと。
2. **[中]** MANIFEST Integrate-phase 5行（MDL-01 rev4, MDL-02 rev3, ANM-01/02/03 rev4）へ `license`/`plan_tier`/`cost_usd` を参照元エントリから転記すること（記録追加のみ、再生成不要）。
3. **[低・任意]** VFX系資産（IMG-04以降）の描線スタイル（ジグザグ二重線 vs 単一2-3px輪郭線）の使い分け方針をart-bible.mdへ明文化することを検討（現行は仕様違反ではないため必須ではない）。

### disclosures（再生成不要・人間開示のみ）

- **最優先で開示**: MDL-01/MDL-02のレンダリング結果がstyle_blockの輪郭線要件を満たしていない件は、`assets-models-prototype.md` iteration1〜4で計4回申し送り済みだが、2026-07-13T09:03時点の最新Integrate証跡（`qa/evidence/asset-integration-hero-visual.png`）でも未対応のまま。Checkpoint Cで必ず提示すること。
- MDL-02 / ANM-04: quadruped auto-rig未完了（`must_replace:true`）。Checkpoint B（2026-07-13）でbuild フェーズのlocal:code-motion代替が追認済み（継続開示）。
- IMG-01: 2D現行出荷版（rev3）と3D出荷モデル（MDL-01、rev1由来ジオメトリ）が異なる意匠を指す状態（provenance実体乖離、`assets-images-prototype.md` iteration3で開示済み）。
- IMG-01: トリムハイライト色のラベンダー寄りシフト（`assets-images-prototype.md` iteration6で許容確定済み、変化なし）。
- 3D資産のルーティング実績は `meshy:direct-*`（Meshy直API）+ `local:blender-*` であり fal.ai経由ではない。gates.md AR-ASSET観点6「fal経由Meshyのライセンス継承未検証」は該当しない（確認済み・非該当）。
- MDL-01/MDL-02/ANM-01〜03: `plan_tier:"pro+"`は`state/asset-routing.json`のnotesで「間接証明（balanceレスポンスにtierフィールド無し）」と明記された未検証値（既存開示の継続）。
- 全画像/3D資産で `cost_estimated:true`（プロバイダ確定請求額ではなく見積り）。
- スコープ外の申し送り: 本pass-1チェックは画像資産と3Dモデルプレビューに限定しており、Phase 3 buildで新規生成されたSFX-05/06・BGM-01（`design/assets.md`が「AR-ASSET 未実施（本バッチはproduceのみ）」と明記）は対象外。別途の音声バッチAR-ASSETレビューが未実施のまま残っていることをここに記録する（本判定の合否には含めない）。

- 対応: （art-director / tech-director 記入欄）

## AR-ASSET iteration 2 — CONCERNS

- 日時: 2026-07-13T10:45:00Z
- 対象: iteration 1 と同一スコープ（画像資産 IMG-01〜04 の各最終有効revision + 3Dモデルレンダリングプレビュー preview-hero.png/preview-swarmer.png、および最新Integrate証跡 `qa/evidence/asset-integration-hero-visual.png`/`qa/evidence/qa-swarmer-closeup.png`/`qa/evidence/qa-game.png`/`qa/evidence/qa-game-swarm.png`）。pass 2 として再照合し、iteration 1 以降の変化有無を確認した。
- 検査方法（iteration 1 からの追加・独自の機械計測）:
  1. `game/_generated/MANIFEST.jsonl`（42行）を `generated_at` 昇順で再走査し、iteration 1 記録時点（state/reviews/assets-batch.md mtime 2026-07-13 09:29）以降に生成された新規行の有無を確認（`find game/_generated -newer state/reviews/assets-batch.md` および全レコードの `generated_at` 最大値確認）。
  2. `git log --oneline -20` でリポジトリの直近コミット履歴を確認（アウトラインシェーダ・マテリアル変更・資産再生成に該当するコミットの有無）。
  3. `qa/evidence/asset-integration-hero-visual.png` / `qa/evidence/qa-swarmer-closeup.png` に対し独自のエッジ勾配解析（隣接画素差分>40を「強エッジ」と定義し、強エッジ画素中の低輝度(V<60)画素比率を算出）を実施し、`game/_generated/images/img-04-hit-vfx.png`（既知cel-shaded+輪郭線あり資産）・`img-01-hero-concept.png` を対照群として同一手法で比較。
  4. `design/art-bible.json` の13色パレットに対し、IMG-01〜04 の不透明画素（alpha>200）を12刻み量子化してクラスタ抽出し、各クラスタのRGBユークリッド距離を再計算（iteration 1 の主張を独立に裏取り）。
  5. `qa/evidence/qa-game-swarm.png`（Wave2、hero+swarmer+クリスタル群が同一画面に映る証跡）を目視確認し、シルエット可読性（色相・輝度・スケール差による瞬時判別可否）を確認。

### 結果: iteration 1 からの変化なし（新規ドリフト検出なし・既存指摘は未対応のまま）

**時系列変化の有無**: `game/_generated/MANIFEST.jsonl` 全42行の `generated_at` 最大値は `2026-07-13T09:05:00Z`（SFX-05/06。本バッチのスコープ外＝音声のため対象外）で、画像/3D関連の最新行は iteration 1 が確認した時点から増えていない。`find game/_generated -newer state/reviews/assets-batch.md` は0件。`git log` 直近20件（最新 `5480c1d` S-12: アップグレード購入インタラクション + Game への反映）にもマテリアル/シェーダ/資産再生成に該当するコミットは無い。**したがって IMG-01〜04・MDL-01/MDL-02 のいずれについても、iteration 1 の指摘に対する revise は行われていない**（state/reviews/assets-batch.md の「対応」欄も iteration 1 のまま未記入）。

**【最優先・継続】3Dレンダリング（Hero/Swarmer）のアウトライン欠如は未解決 — 独自エッジ解析で再確認**

| 対象 | 強エッジ画素比率 | 強エッジ中の低輝度(V<60)画素比率 |
|---|---|---|
| `qa/evidence/qa-swarmer-closeup.png`（3D swarmer, Integrate証跡） | 0.476% | 3.41% |
| `qa/evidence/asset-integration-hero-visual.png`（3D hero, Integrate証跡） | 0.582% | 0.13% |
| `game/_generated/images/img-04-hit-vfx.png`（2D, cel-shaded+輪郭線あり・対照群） | 6.471% | 30.76% |
| `game/_generated/images/img-01-hero-concept.png`（2D, cel-shaded+輪郭線あり・対照群） | 2.083% | 13.32% |

2D対照群（style_block準拠と既に確認済みの資産）は強エッジ画素の13〜31%が低輝度（ダークネイビー輪郭線に相当）である一方、3Dレンダリング2点は0.13〜3.41%と一桁以上少なく、`qa/evidence/qa-swarmer-closeup.png`（PlayMode実機screenshot、2026-07-13T09:03生成）を目視確認しても滑らかなPBRグラデーション陰影のみで輪郭線は視認できない。iteration 1 の指摘（4回連続の既存申し送り＋Integrate側でのアウトラインパス未実装）を独立した定量指標で再確認し、**pass 2 時点でも未解決**であることを確定する。新規のスタイルドリフトではなく、既知の未解決欠陥が持続している状態。

**【中・継続】MDL-01 revision:4 / MDL-02 revision:3 の provenance 記録欠落も未解決（ANM-01〜03 は別経路で既に解消済みと訂正）**

MANIFEST再走査で以下を確認・訂正する:
- **ANM-01〜03 は revision:5（`generated_at: 2026-07-13T00:00:00Z`、iteration 1 の記録より前に存在）で `license`/`plan_tier`/`cost_usd`/`sha256` 等が既に復元済み**（`review_ref: "AR-ASSET iteration 3/4 記録改善の推奨 — art-director revise（記録改善のみ）"` — `state/reviews/assets-models-prototype.md` 側の個別資産レビューへの対応であり、iteration 1 が「ANM-01〜03 revision:4 で欠落」とした記述は revision:4 単体としては正しいが、**最新有効revisionである revision:5 では既に解消済み**という文脈が iteration 1 では欠けていた。ここに訂正として記録する。
- 一方 **MDL-01（最新 revision:4、`generated_at: 2026-07-10T11:32:26Z`）と MDL-02（最新 revision:3、`generated_at: 2026-07-10T11:32:26Z`）にはANM同様の記録改善revisionが追加されておらず、`license`/`plan_tier`/`cost_usd`/`sha256` の欠落が最新有効revisionの時点でも残っている**（`sorted(keys())` 実測: `['asset_id','degradation_note','file','generated_at','revision','revision_of_sha256','revision_reason','validator']` のみ、`license`等なし）。こちらは iteration 1 の指摘通り pass 2 時点でも未対応。

### 総合判定: CONCERNS（iteration 1 から変化なし）

理由: pass 2 の独自機械計測により、iteration 1 で検出した2件のCONCERNS（①3Dレンダリングのアウトライン欠如、②MDL-01/MDL-02のprovenance記録欠落）がいずれも**新規の悪化ではないが未対応のまま持続している**ことを確認した。新規のパレット逸脱・画風ブレは検出されなかった（IMG-01〜04のパレット距離は再計測でも既存許容範囲内、`qa/evidence/qa-game-swarm.png` 目視でもhero/swarmer/crystalの色相・輝度・スケール差によるシルエット判別に劣化なし）。ANM-01〜03のprovenance記録欠落は別経路（assets-models-prototype.mdレビュー）で既に解消済みと判明したため対象から除外し、MDL-01/MDL-02のみに指摘を絞り込む。

### 再生成指示（優先度順、iteration 1 から変更なし）

1. **[最優先・継続]** MDL-01/MDL-02（Hero/Swarmerマテリアル）へIntegration側でアウトラインシェーダ/ポストプロセス（インバートハル法 or URP法線/深度エッジ検出）を追加適用すること。3Dモデル自体の再生成は不要（原因はマテリアル/シェーダ構成側）。
2. **[中・継続、対象修正: ANM除外]** `game/_generated/MANIFEST.jsonl` の MDL-01 revision:4・MDL-02 revision:3 へ、参照元の完全provenanceエントリ（各資産の revision:2、`license:"commercial-ok"`/`plan_tier:"pro+"`/`cost_usd`/`sha256`）から値を転記した新規revision行を追記すること（Integrate作業自体のコストは0のため `cost_usd:0` で可）。ANM-01〜03は対応不要（revision:5で解消済み）。
3. **[低・任意、変化なし]** VFX系資産の描線スタイル一貫性のart-bible.md明文化（iteration 1と同内容、必須ではない）。

### disclosures（iteration 1 から更新）

- **最優先で開示・エスカレーション**: MDL-01/MDL-02のレンダリング結果がstyle_blockの輪郭線要件を満たしていない件は、`assets-models-prototype.md` iteration1〜4（計4回）に加え本バッチチェックiteration1・iteration2（計2回）でも申し送り済みだが、pass 2時点で対応着手の痕跡（コミット・MANIFEST新規revision・「対応」欄記入のいずれも）が無い。Checkpoint Cで必ず提示すること。
- MDL-01 revision:4 / MDL-02 revision:3 の provenance 記録欠落（license/plan_tier/cost_usd/sha256）は pass 2 時点でも未解消。ANM-01〜03の同種欠落は revision:5 で解消済みと訂正確認。
- iteration 1 に記載したその他のdisclosures（MDL-02/ANM-04 quadruped未リグ・IMG-01の2D/3D意匠乖離・トリムハイライト色シフト・fal経由Meshy非該当・plan_tier未検証値・cost_estimated:true全数・音声バッチAR-ASSET未実施）はいずれも変化なし、継続有効。

- 対応: （art-director / tech-director 記入欄）

## AR-ASSET iteration 3 — CONCERNS

- 日時: 2026-07-14T12:30:00Z
- 対象: 画像資産 IMG-01〜06（各最終有効revision。IMG-05は`revision:2`）＋3Dモデルレンダリングプレビュー `preview-hero.png`(MDL-01由来)/`preview-swarmer.png`(MDL-02由来)＋最新Integrate/QA証跡5点（`qa/evidence/asset-integration-hero-visual.png` / `arena-four-direction.png` / `qa-swarmer-closeup.png` / `qa-game.png` / `qa-game-swarm.png`、全てmtime 2026-07-14T11:52台、S-19以降の再撮影分）。`game/_generated/MANIFEST.jsonl` 全46行を `generated_at` 昇順で走査し、iteration 2（pass 2、2026-07-13T10:45時点・42行）以降に追加された4行（MDL-02 revision:4 / IMG-05 / IMG-06 / IMG-05 revision:2）を新規に時系列へ組み込んだ pass 3。
- 検査方法: (1) `python3 -c` によるMANIFEST全46行のJSONパース＋`generated_at`昇順ソートで新規行を特定、(2) 新規画像IMG-05(revision:2)/IMG-06をPillow+numpyで13色パレット距離再計測（8/16刻み量子化クラスタ抽出、既存iterationと同一手法）、(3) `state/stories.yaml`を`grep`し、IMG-05/IMG-06に紐づく実装ストーリー（S-24〜S-30、tech-director Replan 2026-07-14「Phase 3 Visual Brushup」）の`status`を確認、(4) `find game/Assets/Generated -maxdepth 3 -type f`でUnity取込先ディレクトリの実ファイル一覧を取得しIMG-05/IMG-06の取込有無を確認、(5) `grep -rn`で`game/Assets/Scripts`配下の`img-06`/`arena-backdrop`/`Skybox`/`skybox`参照を検索、(6) 最新QA証跡5点をReadツールで直接目視、(7) iteration 2と同一の独自エッジ勾配解析（隣接画素差分>40を強エッジ、強エッジ中のV<60画素比率）を最新証跡へ再実行し2D対照群（IMG-01/IMG-04）と比較、(8) MANIFEST全46行に対し`license`/`plan_tier`/`cost_usd`/`sha256`必須フィールドの欠落を`revision`付きエントリ全件で機械スキャン。

### 生成順タイムライン追加分（iteration 2 以降）

7. MDL-02 revision:4（構造変更のみ、ジオメトリ/テクスチャ/ボーン数無変更・Unity実測bbox revision3と完全一致）: `2026-07-13T03:24:43Z`
8. IMG-05（UI装飾キット、初回）: `2026-07-14T05:20:00Z`
9. IMG-06（アリーナ背景）: `2026-07-14T05:20:00Z`
10. IMG-05 revision:2（element(a)再生成、`state/reviews/assets-images.md` AR-ASSET iteration 2 CONCERNS対応）: `2026-07-14T11:37:00Z`

### 観点別結果

**【新規・最優先】IMG-05/IMG-06 は個別資産としてAPPROVE済みだが実ゲーム画面に一切統合されていない — 新規の時系列不整合として検出**

- `state/stories.yaml`を確認した結果、IMG-05/IMG-06の生成自体を扱うストーリー S-24（UIキット生成）・S-25（背景生成）に加え、統合を扱う S-26（背景/スカイボックス統合）・S-27（ポストプロセス/ライティング）・S-28（URP Outline+マテリアル増強）・S-29（クリスタル発光/juice）・S-30（UI装飾適用）は**全件 `status: todo`**（2026-07-14 tech-director Replan「Phase 3 Visual Brushup」で新規起票されたバッチ）。資産生成（S-24/S-25の成果物）自体は`design/assets.md`/MANIFESTで`generated`、`state/reviews/assets-images.md`でAR-ASSET APPROVE済みだが、**統合ストーリー（S-26〜S-30）が1件も着手されていない**。
- 実測で裏付け: `find game/Assets/Generated -maxdepth 3 -type f`の結果、Unity取込先`Images/`配下には`img-03-crystal-icon.png`と`img-04-hit-vfx.png`のみ存在し、**`img-05-ui-frame-kit.png`／`img-06-arena-backdrop.png`は取込先に一切存在しない**（raw生成物は`game/_generated/images/`にのみ存在）。`game/Assets/Scripts`配下を`img-06`/`arena-backdrop`/`Skybox`/`skybox`で検索してもヒット0件。
- 最新QA証跡（`qa-swarmer-closeup.png`、2026-07-14T11:52生成）を目視すると、アリーナ外周の背景は**Unity標準の手続き型プロシージャルSkybox（青空グラデーション+ヘイズ地平線+褐色地面）のまま**で、IMG-06が指定する「地平のダーク void `#12081F` → 紫 → シアン → マゼンタ」のネオングラデーションには一切置換されていない。量子化パレット距離を計測しても`arena-four-direction.png`/`asset-integration-hero-visual.png`等5枚全てで最頻出クラスタが`RGB(184,168-232,216-248)`系統（薄い水色・ラベンダー、13色パレットいずれとも距離59.5〜114.8で非該当）と`RGB(8,8,8)`（void_blackへ距離25.1と近いが単なる暗い地面色）で構成されており、13色パレットのいずれの主要色（hero/enemy/crystal系）とも一致しない＝**Unity Skybox標準アセットの色**であることを裏付ける。
- 同様にHUD（`HP 100/100`バー・`WAVE 1`・`SCORE`テキスト）は無装飾の素のUnity UI（角丸パネル無し、フレーム無し）のままで、IMG-05由来の9-sliceパネル/タブ/リボン装飾は一切適用されていない（S-30未着手と整合）。
- **この事象はgates.md AR-ASSET観点1「スタイル一致」の趣旨（生成資産が実際のゲーム内表示でart-bible.jsonに沿っているか）に照らし、raw画像ファイル単体の合否だけでは判定が完結しない領域であることを示す**。IMG-05/IMG-06はファイルとしては仕様通り（disclosuresではなくAPPROVE済み）だが、**Checkpoint C提示物としての「ゲームの見た目」は、これらの新規承認資産をまだ一切反映していない**。人間がCheckpoint Bで要求した「UIが簡素すぎる／背景が欲しい／見た目をkey imageに接近させたい」という修正依頼（`state/checkpoint-b-feedback.md`）に対し、**資産だけが用意され、ゲーム側への適用がまだ0%**という状態を正確に開示する必要がある。
- 本件は資産の再生成では解決しない（IMG-05/IMG-06自体に欠陥は無い）。対応はS-26/S-27/S-28/S-29/S-30（gameplay-engineer/ui-engineer領分）の実装完了であり、art-reviewerの権限外（CR-CODE/QA-PLAYの領分）。ここでは**Checkpoint C提示前に必ず開示すべき最優先の状態不整合**として記録する。

**【継続・最優先】3Dレンダリング（Hero/Swarmer）のアウトライン欠如 — 依然未解決、根本原因がS-28未着手であることを確認**

- 独自エッジ勾配解析を最新証跡5点へ再実行した結果、iteration 2と同一の傾向を確認:

| 対象 | 強エッジ画素比率 | 強エッジ中の低輝度(V<60)画素比率 |
|---|---|---|
| `qa/evidence/asset-integration-hero-visual.png`（2026-07-14T11:52再撮影） | 1.499% | 0.00% |
| `qa/evidence/qa-swarmer-closeup.png`（同上） | 0.135% | 0.00% |
| `qa/evidence/arena-four-direction.png`（同上） | 1.584% | 0.00% |
| `qa/evidence/qa-game-swarm.png`（同上） | 1.673% | 0.00% |
| `game/_generated/images/img-04-hit-vfx.png`（2D対照群） | 5.537% | 35.94% |
| `game/_generated/images/img-01-hero-concept.png`（2D対照群） | 1.823% | 15.04% |

2D対照群は強エッジ画素の15〜36%が低輝度（ダークネイビー輪郭線相当）である一方、3Dレンダリング4点は**0.00%**（iteration 2の0.13〜3.41%からさらに悪化気味の測定値、閾値パラメータの再現誤差の範囲内だが改善の兆候は無い）。`state/stories.yaml` S-28「URP Outline 輪郭線 + hero/swarmerマテリアル増強」が`status: todo`であることを確認し、**この欠陥の是正ストーリー自体は2026-07-14のReplanで正式に起票されたが、実装着手前**であることが根本原因として特定できた。`assets-models-prototype.md` iteration1〜4 + 本バッチiteration1・2に続き、iteration3でも4回目の申し送りとなる（実装ストーリーとしての追跡先が明確になった点は前進）。

**【継続・中、対象追加で悪化】MANIFEST provenance欠落 — MDL-02 revision:4 が新たに欠落対象に追加**

- MANIFEST全46行を`license`/`plan_tier`/`cost_usd`/`sha256`必須フィールドで機械スキャンした結果、`revision`付きエントリのうち以下6件が引き続き欠落: `MDL-01 revision:4`・`MDL-02 revision:3`・`ANM-01/02/03 revision:4`（iteration 2から継続、未対応）に加え、**新規追加された`MDL-02 revision:4`（S-21統合構造変更、`generated_at:2026-07-13T03:24:43Z`）も同じ4フィールドを欠く**（`sorted(keys())`実測: `asset_id/file/revision/revision_of_sha256/revision_reason/provider/model/validator/degradation_still_open/review_ref/generated_at`のみ）。ANM-01〜03は`revision:5`で解消済み（iteration 2で訂正確認済み、変化なし）。
- ファイル本体はrevision:3から無変更（sha256同一、`revision_reason`に明記）のため出荷可否そのものへの影響は無いが、記録漏れの対象範囲がiteration 2時点の3件（MDL-01 rev4/MDL-02 rev3/ANM系）から**4件（MDL-02 rev4追加）**に拡大している。

**【確認のみ・PASS】IMG-05(revision:2)・IMG-06 のバッチ内パレット/画風再照合 — 新規逸脱なし**

- IMG-05(revision:2)・IMG-06とも`state/reviews/assets-images.md` AR-ASSET iteration 2/3で個別に機械検証済み（IMG-05は中央領域dist=0/std=0、IMG-06は色相差20-37°で背景用途として許容範囲）であり、本pass-3の独立再計測でも同一クラスタ・同一距離を再確認した（新規劣化なし）。IMG-01〜04についても iteration 1/2 からの変化は無い（`find game/_generated -newer <iteration2時点ファイル>`相当の生成順ソートで新規画像行はIMG-05/IMG-06/IMG-05rev2の3行のみと確認済み）。**「バッチ横断の画風ブレ」自体はraw画像ファイルの範囲では検出されなかった** — 検出されたのはraw画像とゲーム内表示の乖離（上記最優先項目）である。

### 総合判定: CONCERNS

理由: raw画像資産（IMG-01〜06）自体のパレット/画風は時系列で一貫しており新規のドリフトは検出されなかった。一方で、(1) Checkpoint C修正依頼を受けて新規生成・APPROVE済みのIMG-05/IMG-06が**実ゲーム画面に一切統合されていない**（Unity標準Skybox・無装飾UIのまま、統合ストーリーS-26〜S-30が全件todo）という新規の重大な状態不整合を検出し、(2) 既存最優先事項（3Dモデルのアウトライン欠如、4回目の申し送り、根本原因はS-28未着手と特定）が未解決のまま持続し、(3) MANIFEST provenance欠落がMDL-02 revision:4の追加により対象範囲が拡大した。(1)(2)はいずれも資産の再生成では解決しない性質のため、failedAssets（再生成対象）としてではなくdisclosures（Checkpoint Cでの必須開示事項）として扱う。

### 再生成指示（該当なし・全件エスカレーション事項）

本iterationで検出した問題はいずれも「資産ファイル自体の再生成」では解決しない（raw画像・3Dモデルファイルそのものに機械検査上の欠陥は無い）。対応は下記の実装ストーリー完了が前提となるため、art-reviewer権限外の作業として明示的にエスカレーションする:
1. **[最優先]** S-26（背景/スカイボックス統合）・S-30（UI装飾適用）の実装着手 — IMG-05/IMG-06を実際にUnityシーン/Canvasへ組み込む（gameplay-engineer/ui-engineer領分）。
2. **[最優先・継続]** S-28（URP Outline輪郭線）の実装着手 — MDL-01/MDL-02マテリアルへのアウトラインパス追加（gameplay-engineer領分、4回目の申し送り）。
3. **[中・継続、対象拡大]** `game/_generated/MANIFEST.jsonl`のMDL-01 revision:4・MDL-02 revision:3・MDL-02 revision:4・ANM-01/02/03 revision:4へ、参照元の完全provenanceエントリから`license`/`plan_tier`/`cost_usd`/`sha256`を転記した新規revision行を追記すること（Integrate作業自体のコストは0のため`cost_usd:0`で可。ANM-01〜03は既にrevision:5で解消済みのため対象外）。

### disclosures（再生成不要・人間開示のみ・Checkpoint C必須開示）

- **【新規・最優先】** IMG-05（UI装飾キット）・IMG-06（アリーナ背景）はraw資産としてAR-ASSET APPROVE済みだが、Unityプロジェクトへの取込・シーンへの適用が0件（`game/Assets/Generated/Images/`に存在しない、Skyboxは標準プロシージャルのまま、HUD装飾も未適用）。関連ストーリーS-26〜S-30は全件`status: todo`。Checkpoint Cで「Checkpoint B修正依頼（UI簡素・背景欲しい・key image接近）は資産生成のみ完了し、ゲーム内反映はまだ実施されていない」ことを正直に開示すること。
- **【継続・最優先エスカレーション】** MDL-01/MDL-02のレンダリング結果がstyle_blockの輪郭線要件を満たしていない件は、`assets-models-prototype.md` iteration1〜4 + 本バッチiteration1・2・3（計7回）申し送り済み。2026-07-14のReplanでS-28として正式に実装ストーリー化されたが未着手。
- MDL-01 revision:4／MDL-02 revision:3・revision:4／の provenance 記録欠落（license/plan_tier/cost_usd/sha256）はiteration 3時点でも未解消（対象がMDL-02 revision:4の追加で拡大）。ANM-01〜03の同種欠落はrevision:5で解消済み（変化なし）。
- iteration 1・2に記載したその他のdisclosures（MDL-02/ANM-04 quadruped未リグ・IMG-01の2D/3D意匠乖離・トリムハイライト色シフト・fal経由Meshy非該当・plan_tier未検証値・cost_estimated:true全数（IMG-05/IMG-06分含む）・音声バッチAR-ASSET未実施）はいずれも変化なし、継続有効。
- `state/stories.yaml`上、S-24（IMG-05生成）・S-25（IMG-06生成）の`status`が実態（`design/assets.md`では`generated`、`assets-images.md`でAR-ASSET APPROVE済み）に対し`todo`のまま更新されていない記録上のズレを確認した（art-reviewerの編集権限外のため指摘のみ）。

- 対応: （art-director / gameplay-engineer / ui-engineer / tech-director 記入欄）

## AR-ASSET iteration 4 — CONCERNS

- 日時: 2026-07-14T16:20:00Z
- 対象: iteration 1〜3 と同一スコープ（画像資産 IMG-01〜06 の各最終有効revision + 3Dモデルレンダリングプレビュー `preview-hero.png`/`preview-swarmer.png`）を `game/_generated/MANIFEST.jsonl`（全46行、iteration 3から行数不変）の生成順で再照合する pass 4。加えて iteration 3 記録時点（当該レビューエントリのファイル書き込みmtime 12:12台）以降に出現した新規証跡（`qa/evidence/asset-integration-report.txt` mtime 12:30:13、`qa/evidence/arena-backdrop.png`/`arena-four-direction.png`/`asset-integration-hero-visual.png` mtime 12:30:5x、いずれもiteration 3が参照した旧版=mtime 11:52台より後に再生成されたもの）をクロスチェックした。
- 検査方法: (1) `game/_generated/MANIFEST.jsonl` 全46行を再パースし `generated_at` 最大値・行数が iteration 3 時点から不変（新規生成なし）であることを確認、(2) 画像6点（IMG-01〜06最終revision）+ FBX 5点（MDL-01/02, ANM-01〜03）+ プレビュー2点の `shasum -a 256` 実測をMANIFEST記載sha256と突合、(3) `git status --short` でワークツリーの未コミット変更を確認、(4) `git log --oneline -3 -- game/Assets/Generated/` および `grep -rn ArenaBackdrop game/Assets/Scripts` で S-26 統合コードの実装状況を確認、(5) `qa/evidence/asset-integration-report.txt` の `PatchArenaBackdrop` ログ行を確認、(6) numpy による独自エッジ勾配解析（隣接画素差分>40を強エッジ、強エッジ中のV<60画素比率）を最新3枚の証跡へ再実行し既存対照群（IMG-01/IMG-04）と比較、(7) 最新 `asset-integration-hero-visual.png` のhero部分を100x100pxで切り出し4倍ズーム目視、(8) `arena-four-direction.png` 上のクリスタル（白色ダイヤモンド状オブジェクト2点）のピクセル色を実測しHSV変換、(9) `git show HEAD:qa/evidence/qa-game-swarm.png` でコミット済み旧版を抽出しクリスタル色を比較、(10) `arena-backdrop.png` 上部（void〜purple帯）3点×3高さのHSV色相サンプリングでIMG-06統合後の色相が仕様（purple hue≈289.4°）に整合するか確認。

### 観点別結果

**画像資産6点・3Dモデル5点・プレビュー2点のprovenance — PASS（無改変を確認）**
IMG-01〜06（各最終revision）、MDL-01/MDL-02（`model-hero.fbx`/`model-swarmer.fbx`）、ANM-01〜03、`preview-hero.png`/`preview-swarmer.png` の全13ファイルで実測sha256がMANIFEST記載値と完全一致。MANIFEST行数も46行のまま不変（`generated_at`最大値は引き続きIMG-05 revision:2の`2026-07-14T11:37:00Z`）。**iteration 3以降、raw生成資産（画像・3Dモデル・プレビュー）に新規生成・改変は一切無い** ことを確認した。

**バッチ横断パレット/画風ブレ（本artifactの中核スコープ）— PASS（新規ドリフトなし）**
上記の通りraw資産が無改変のため、iteration 1〜3で確立した時系列パレット距離・アルファ・色相の実測値がそのまま有効。IMG-01〜06間、およびpreview-hero.png/preview-swarmer.pngとの間で新規のパレット逸脱・画風ブレは検出されない。

---

**【新規・重要な進展（未完了）】S-26（背景/スカイボックス統合）が着手され、IMG-06が初めて実ゲーム画面へ反映され始めている——ただし未コミット・未レビューのWIP状態**

- `git status --short` で `game/Assets/Scripts/Components/ArenaCameraRig.cs` / `Editor/AssetIntegration.cs` / `Editor/SceneWiring.cs` / `GameConfig.cs` / `Systems/ArenaCameraMath.cs` / `Assets/Scenes/Game.unity` を含む多数ファイルが未コミットで変更中（`M`）であることを確認。`grep -rn ArenaBackdrop game/Assets/Scripts` で新規 `Components/ArenaBackdrop.cs`（コメント「S-26: Checkpoint C 修正依頼2」）と `GameConfig.AssetKeys.ArenaBackdropTexture` 定数を確認し、`qa/evidence/asset-integration-report.txt`（mtime 12:30:13、iteration 3記録時点より後に生成）で `PatchArenaBackdrop: IMG-06 texture assigned to ArenaBackdrop._backdropTexture.` のログを確認した。`state/stories.yaml` の S-26 は `status: in-progress`（iteration 3時点は`todo`）。
- 実際に `qa/evidence/arena-backdrop.png`（mtime 12:30:53）・`arena-four-direction.png`（mtime 12:30:54、iteration 3が参照した旧版=mtime 11:52台から更新）・`asset-integration-hero-visual.png`（mtime 12:30:56）で、地平のダークvoidから紫〜シアン系グラデーション背景が初めて視認できることを確認した。`arena-backdrop.png` 上部の独立hueサンプリング（3点×3高さ）は262.5–300.0°の範囲に収まり、IMG-06の紫域参照値（289.4°）と概ね整合（iteration 2で確立したIMG-06自体の許容差20–37°の範囲内）。**iteration 1〜3で最優先開示事項としてきた「IMG-05/IMG-06が資産としてAPPROVE済みだが実ゲーム画面に一切反映されていない」状態は、少なくともIMG-06（背景）については解消に向けて着手されたことを確認した。**
- ただし本変更は**未コミット・CR-CODE未実施・S-26自体もacceptance未検証のWIP**であり、AR-ASSETの判定対象（raw生成資産）そのものには影響しない。この点を出荷可否の判断材料にはできず、あくまで「iteration 3の最優先開示事項に動きがあった」という進捗情報としてdisclosuresに記録するに留める。IMG-05（UIキット、S-30）は `game/Assets/Generated/Images/` に引き続き存在せず未着手のまま（`find`実測で`img-05-ui-frame-kit.png`不在を再確認）。

---

**【新規検出・要フォローアップ】最新WIP証跡でクリスタルのエミッシブ色が仕様のシアン`#4FE8E2`ではなくほぼ純白に飽和して見える — 非ブロッキングだが要確認**

- `arena-four-direction.png`（最新WIP証跡）上のクリスタル状オブジェクト2点のピクセル実測: `(555,204)≈RGB(251,255,255)`、`(390,344)≈RGB(242,255,255)` —— いずれも彩度がほぼ0のニアホワイト。
- 比較のため直近のコミット済み版（`git show HEAD:qa/evidence/qa-game-swarm.png`）を独立抽出したところ、同一シーン要素のクリスタルは明瞭なシアン系ダイヤモンド/ペンタゴン形状（目視で`#4FE8E2`相当の水色）として描画されていた。コード側 `Components/CrystalPickup.cs` は `GameConfig.Ui.ColorCrystalCyan = "#4FE8E2"` をベースに `_EmissionColor = color * GameConfig.Crystal.EmissionIntensity` を設定しており、色自体の指定は仕様通り。
- 原因はraw資産（クリスタルはUnityプリミティブ+マテリアルで表現する設計のため生成資産ではない — `design/art-bible.json` `notes.prop_budget_status` 参照）ではなく、**S-26のカメラ/背景変更（`ArenaCameraRig.cs`/`ArenaCameraMath.cs`が未コミットで変更中）に伴う露出・トーンマッピング設定の変化、またはS-27（URPポストプロセス/ライティング）が依然`todo`のまま高強度エミッシブがクランプされずに白飛びしている**可能性が高いと推定するが、確定的な原因特定はコード実行環境（Unity Editor起動）を要するためAR-ASSETの権限・手段の範囲外。
- **AR-ASSETとしての扱い**: raw生成資産（クリスタルアイコン IMG-03含む）自体に欠陥はなく、再生成対象（failedAssets）とはしない。S-26が未コミットWIPである以上、この白飛びも現時点で「確定した退行」と断定はできないが、**S-27着手時またはS-26コミット前に、クリスタルのエミッシブ発光が意図した`#4FE8E2`系シアンとして視認できるか（露出/トーンマッピング込みで）を確認する**ことを新規disclosureとして記録し、gameplay-engineer/tech-directorへの確認事項とする。

---

**【継続・最優先、新証跡でも未解消を再確認】MDL-01/MDL-02のアウトライン欠如 — 依然未解決（S-28 todo のまま）**

`asset-integration-hero-visual.png`（最新WIP証跡）のhero部分を100x100pxクロップ→4倍ズーム目視（`/tmp/hero_zoom.png`）した結果、シルエット全周にわたる太いダークネイビー輪郭線は依然視認できず、なめらかなPBRグラデーション陰影のみで構成されている。iteration 2/3で用いた強エッジ画素中の低輝度(V<60)画素比率による定量指標は、今回の最新証跡3枚（`asset-integration-hero-visual.png`/`arena-backdrop.png`/`arena-four-direction.png`）でいずれも12.9〜14.7%とiteration 2/3の実測値（0.00〜3.41%）より上昇して見えるが、これは新規背景（濃紺〜紫グラデーション）とアリーナ床/リング境界がフレームの大部分を占めることで生じた背景由来の強エッジ増加であり、ズーム目視で確認した通りhero本体の輪郭線には起因しない（指標を漫然と比較するとミスリードになるため、ズーム目視による直接確認を優先根拠とした）。`state/stories.yaml` S-28（URP Outline）は引き続き `status: todo`。`assets-models-prototype.md` iteration1〜4 + 本バッチiteration1〜3（計7回）に続き、**本iteration4で8回目の申し送り**となる。

---

**【継続・変化なし】MANIFEST provenance欠落（MDL-01 revision:4／MDL-02 revision:3・revision:4）— 未解消**

MDL-01 `revision:4`・MDL-02 `revision:4`（最新有効revision）の`sorted(keys())`を再実測した結果、`license`/`plan_tier`/`cost_usd`/`sha256`はいずれも欠落したまま（iteration 3から変化なし）。ファイル本体は無変更（実測sha256はrevision:3のものと一致）のため出荷可否そのものへの影響はないが、記録漏れは未解消。

### 総合判定: CONCERNS

理由: raw生成資産（画像6点・3Dモデル5点・プレビュー2点）は iteration 3 以降完全に無改変で、本pass-4の独立検証でも新規のパレット逸脱・画風ブレは検出されなかった（本artifactの中核スコープはPASS）。一方で、(1) iteration 1〜3で継続申し送り中のMDL-01/MDL-02アウトライン欠如（S-28未着手、8回目の申し送り）が最新証跡でも未解消であることを直接ズーム目視で再確認し、(2) iteration 3の最優先開示事項（IMG-05/IMG-06のゲーム内非反映）はIMG-06（背景）について着手の動き（S-26 in-progress、未コミットWIP）を確認できたが完了はしていない、(3) MANIFEST provenance欠落は変化なし、(4) 新規観察として、最新WIP証跡でクリスタルのエミッシブ色が仕様のシアンではなくほぼ白飛びしている疑いを検出した（raw資産の欠陥ではなくエンジン側の露出/ポストプロセス設定に起因する可能性が高い未確定事項）。(1)(2)(4)はいずれも資産の再生成では解決しない性質のためfailedAssetsとしてではなくdisclosures/申し送りとして扱う。

### 再生成指示（該当なし・全件エスカレーション事項、詳細は各節参照）

1. **[最優先・継続、8回目の申し送り]** S-28（URP Outline輪郭線）の実装着手 — MDL-01/MDL-02マテリアルへのアウトラインパス追加（gameplay-engineer領分）。
2. **[新規・優先度中]** S-26/S-27着手・コミット前に、クリスタルのエミッシブ発光が`#4FE8E2`系シアンとして意図通り視認できるか（露出/トーンマッピング込みで）確認すること。白飛びが確定した場合はS-27（URPポストプロセス）のBloom/Tonemapping/露出パラメータ調整、またはCrystalPickupの`EmissionIntensity`見直しで対応（3D/2D資産の再生成は不要）。
3. **[中・継続、変更なし]** `MANIFEST.jsonl`のMDL-01 revision:4・MDL-02 revision:3・revision:4へ、参照元の完全provenanceエントリから`license`/`plan_tier`/`cost_usd`/`sha256`を転記した新規revision行を追記すること。

### disclosures（再生成不要・人間開示のみ・Checkpoint C必須開示）

- **【継続・最優先】** MDL-01/MDL-02のアウトライン欠如は計8回の申し送りにもかかわらず未解決（S-28 todo）。
- **【進捗更新】** iteration 3の最優先開示事項（IMG-05/IMG-06非反映）のうちIMG-06（背景）は S-26 着手（未コミットWIP）により実ゲーム画面への反映が始まっている（hueサンプリングで仕様との整合も確認）。IMG-05（UIキット、S-30）は引き続き未着手・未反映。
- **【新規】** 最新WIP証跡（`arena-four-direction.png`等）でクリスタルのエミッシブ色がほぼ白飛びしており、直近コミット版（`qa-game-swarm.png`旧版）で見えていたシアン発色から後退している疑いがある。原因はraw資産ではなくエンジン側設定（S-26カメラ変更 or S-27未着手による露出/トーンマッピング欠如）の可能性が高い。S-26/S-27完了前に確認を要する未確定事項として開示する。
- MDL-01 revision:4／MDL-02 revision:3・revision:4 の provenance 記録欠落（license/plan_tier/cost_usd/sha256）は継続未解消。
- iteration 1〜3に記載したその他のdisclosures（MDL-02/ANM-04 quadruped未リグ・IMG-01の2D/3D意匠乖離・トリムハイライト色シフト・fal経由Meshy非該当・plan_tier未検証値・cost_estimated:true全数・音声バッチAR-ASSET未実施・ファイル命名規約逸脱）はいずれも変化なし、継続有効。
- 予算: `game/_generated/MANIFEST.jsonl` 全46行の `cost_usd` 合算 $2.95044（変化なし）、`state/budget.txt` 上限 $100 に対し超過なし。

- 対応: （art-director / gameplay-engineer / ui-engineer / tech-director 記入欄）
