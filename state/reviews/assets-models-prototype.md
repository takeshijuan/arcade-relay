# レビュー履歴 — assets-models-prototype（MDL-01, MDL-02, ANM-01〜03, engine=unity）

## AR-ASSET iteration 1 — CONCERNS
- 日時: 2026-07-10T06:42:00Z
- 対象: `game/_generated/MANIFEST.jsonl` 今回追記分の3D資産 = MDL-01（`game/_generated/models/model-hero.fbx`）、MDL-02（`game/_generated/models/model-swarmer.fbx`）、ANM-01〜03（`game/_generated/anims/anim-hero-{attack,idle,run}.fbx`）。ANM-04 は `design/assets.md` の状態が `must-replace`（MDL-02リグ未完了のため未生成）のため実ファイルが存在せず本バッチ対象外（既知の未解決事項として後述）。IMG-01〜03・SFX-01〜04 は別artifact（`assets-images-prototype.md` / `assets-audio-prototype.md`）で判定済みのため本ファイルでは扱わない（重複判定を避ける）。
- 検査方法: `shasum -a 256`（provenance照合）、Blender 5.1.2 headless（`bpy`によるFBX再インポート・ポリ数/頂点数/マテリアル/ボーン/アクション/バウンディングボックス・非多様体/境界エッジ/縮退面の構造検査、`bmesh`によるオブジェクト単位の内訳）、Blender headless → GLB変換 + `npx @gltf-transform/cli validate`（独自再現、MANIFEST自己申告への非依存検証）、Blenderでのバックフェースカリング有効レンダリング（法線異常・浮遊ジオメトリの視覚的実証）、生バイト列でのPNG/JPEGマジックナンバー走査とFBX内部の`RelativeFilename`/`Filename`文字列抽出（テクスチャ参照整合性検査）、Pillowによるパレット色距離・HSV色相分析（量子化クラスタ比較）。

### 観点別結果

**provenance（sha256照合）— PASS**
5ファイル全て（MDL-01, MDL-02, ANM-01, ANM-02, ANM-03）でMANIFEST記載sha256と実ファイルのshasum -a 256実測値が完全一致。改ざん・取り違えなし。

---

**【最優先・全5ファイル共通】テクスチャ参照が破損・未同梱（観点1: 仕様準拠）— FAIL（ブロッキング）**

5ファイル全てのFBXバイナリを直接調査した結果、参照テクスチャが実体として一切同梱されていないことを確認した:

| ファイル | FBX内部の参照パス | 実ファイル有無 |
|---|---|---|
| model-hero.fbx | `.../game/_generated/models/textures/packed/texture_0`（絶対パス、拡張子なし） | 存在しない |
| model-swarmer.fbx | `.../game/_generated/models/textures/packed/Image_0`, `Image_2`（絶対パス、拡張子なし） | 存在しない |
| anim-hero-attack.fbx | `/tmp/Animation_Attack_withSkin.fbm/texture_0.png`（**生成環境のローカル`/tmp`絶対パス**） | 存在しない（他環境では原理的に到達不能） |
| anim-hero-idle.fbx | `/tmp/Animation_Idle_withSkin.fbm/texture_0.png` | 同上 |
| anim-hero-run.fbx | `/tmp/Animation_Running_withSkin.fbm/texture_0.png` | 同上 |

検証手順と根拠:
1. `game/_generated/` 配下を再帰探索した結果、`textures/`ディレクトリ・PNG/JPG/TGAいずれも一切存在しない（previews/とimages/の既存資産以外に画像ファイルなし）。
2. 各FBXの生バイト列に対しPNGシグネチャ（`\x89PNG\r\n\x1a\n`）・JPEG SOIマーカー（`\xFF\xD8\xFF`）をバイト単位で走査した結果、**5ファイル全てで検出数0**。テクスチャがFBX内部にバイナリ埋め込み（embedded media）されている可能性も排除。
3. Blender headlessで5ファイルを再インポートし`bpy.data.images`を確認した結果、参照される`texture_0` / `Image_0` / `Image_2` はいずれも `size=(0,0)`, `packed=False`、ロード試行で`Error: Image '...' does not have any image data`。

**影響**: `game/_generated/previews/preview-hero.png` / `preview-swarmer.png`（生成パイプライン内のどこかの時点で作られた着色済みプレビュー）は存在するため、テクスチャデータ自体は生成過程で一度は存在していたはずだが、最終的にリポジトリへ配置されたFBX一式にはテクスチャファイルが同梱されておらず、参照パスも「このワークツリー内の存在しないディレクトリへの絶対パス」または「生成環境のローカル`/tmp`」という、Unity取込時に**確実に解決不能**なパスになっている。tech-stack-unity.md「資産の取り扱い」節の取込手順（`game/Assets/Generated/` へコピーしUnityインポートに任せる）をそのまま実行すると、5モデル全てが無テクスチャ（Unity既定のmissing-textureマゼンタ/白）で取り込まれ、`design/art-bible.json` の`style_block`・パレットが一切反映されない状態になる。これはスタイル逸脱以前の**資産未完成（出荷不能）**の問題である。

**注記**: MANIFESTの`validator.gltf_validator:"pass"`は本レビューでもBlender→GLB独自変換で再現確認できた（下記参照）が、glTF-transform validateはスキーマ構造の妥当性のみを検証し、参照外部ファイルの実在は検査対象外のため、この欠陥を検出できない。「validator pass」の記載だけでは資産完成を保証しないことが今回の検査で判明した。

**再検査（独自変換）: gltf-transform validate — 5ファイル全てPASS（構造面）**
Blender headlessでFBX→GLB変換し `npx @gltf-transform/cli validate --format md` を独自実行（MANIFEST自己申告に依存しない再現）。結果は5ファイル全て `No errors found`。警告として全リグ付き4ファイル（hero/attack/idle/run）で `NODE_SKINNED_MESH_NON_ROOT`（severity 1）が検出されたが、これはBlenderのgltfエクスポータがArmature+Meshの階層をglTF化する際の既知の一般的挙動であり、Unity側のネイティブFBXインポート経路（本検査のGLB変換を経由しない）には影響しないと判断（参考情報・非ブロッキング）。

---

**【MDL-01固有】素性不明の巨大アイコスフィアオブジェクトが混入（観点2: 予算・構造の「浮遊ジオメトリ」）— FAIL（ブロッキング）**

`model-hero.fbx` を独自インポートしたところ、キャラクター本体メッシュ（`char1`）とは別に、**マテリアル未割当・Armature非アタッチの80ポリゴン`Icosphere`オブジェクト**が同梱されていることを発見した。MANIFESTにはこのオブジェクトの記載が一切ない。

- バウンディングボックス: X −0.634〜0.634m / Y,Z −0.667〜0.667m（≒1.27×1.33×1.33m）、中心はワールド原点 — これはキャラクター全体（bbox 1.268×1.333×1.800m）をほぼ丸ごと包含するサイズ
- `hide_render=False`（デフォルトで描画される）、マテリアルスロット空（Unity取込時は既定/エラーマテリアルが割り当てられ可視化される可能性が高い）
- companion 3ファイル（anim-hero-attack/idle/run.fbx）には存在しない → model-hero.fbx固有の混入物
- MANIFESTの`polycount:47254`は本オブジェクトの80 triを含んだ合計値（本体メッシュ単体は47174 tri）であることを独自インポートで確認。予算超過（≤50,000）にはなっていないが、意図しないジオメトリが予算を消費している

**視覚的実証**: バックフェースカリング有効の非トゥーンマテリアルでレンダリングしたところ（`/tmp`の一時レンダリング画像で確認、4方向）、キャラクターの腰から下がこの巨大アイコスフィア内部に完全に埋没し、外部から見えない状態であることを確認した。Icosphereを除去して同条件でレンダリングすると、キャラクター本体は全周（6方向）で欠損・黒塗り（法線反転由来の裏面カリング穴）のない健全なシルエットで描画されることを確認済み（後述の法線検査参照）。

**推定原因**: Meshyのauto-rig API呼び出し時にリグ用スケールキャリブレーション参照として内部生成される球体が、rigging成果物のFBXエクスポート時に取り除かれず残存した可能性が高い（`character_height_meters=1.8`のような身長基準入力を扱うAPIの副産物として典型的なパターン）。

---

**【MDL-01】非多様体エッジ・法線検査（観点2）— 情報レベル（非ブロッキング、ただし自己申告値の解釈を訂正）**

独自Blender検査で `char1` メッシュの境界エッジ（1面にのみ属するエッジ = オープン/非水密）を6,614本検出（MANIFEST自己申告6,854本と近い値・同一種類の問題を指しているとみられる）。内訳を独自に分解した結果、**3面以上に接続する真の意味での破損トポロジ（縮退/T字型非多様体）は0本**で、全て「単純な開口境界」であることを確認した（`bmesh`で `nonmanifold_edges (>2 faces)=0`, `boundary_edges (used by 1 face)=6614`）。

さらに`normals_make_consistent`適用前後の面法線比較では28.567%の面で反転が検出されたが、これは開口境界を含む非水密メッシュに対するBlenderの自動法線統一アルゴリズムの既知の不安定性（連結成分の分割・外側判定不能）に起因する可能性が高いと判断し、上記のバックフェースカリングレンダリング（Icosphere除去後、6方向）で実際に検証した結果、**キャラクター本体表面に黒塗り・裏面カリング穴は一切確認できなかった**。したがって「法線反転が広範囲に存在し可視破綻を起こしている」という懸念は独自レンダリングで否定される。開口境界自体（腹部・脇下・兜内部等の非可視面である可能性が高い）は技術的には「非多様体」の一種だが、可視破綻のエビデンスがないため本レビューではブロッキングとして扱わない（将来のUnity内リアルタイム影生成・コリジョンメッシュ生成時に問題化する可能性はゼロではなく、Integration時の要観察事項として記録する）。

MDL-02（swarmer）も同種の境界エッジ3,720本を検出（MANIFEST自己申告と一致）。同様にバックフェースカリングレンダリング（6方向）で可視破綻なしを確認済み。

---

**【MDL-01/MDL-02共通】スケール・向き（観点3）— PASS（独自再現）**

- MDL-01: 独自Blenderインポートでのbbox実測 = 幅1.268m・高さ1.800m・奥行1.333m（軸変換考慮後）。MANIFEST `bbox_authoring_m`（幅1.268/高さ1.800/奥行1.333）と**完全一致**。hero_height_m=1.8（レンジ1.6–2.0）に一致。
- MDL-02: 独自Blenderインポートでのbbox実測 = 幅0.897m・高さ0.959m・奥行1.461m。MANIFEST `bbox_authoring_m`（幅0.897/高さ0.959/奥行1.461）と**完全一致**。target（肩高1.0m・体長1.4m）に対し高さ-4.1%・奥行+4.3%の乖離があるが、MANIFEST自身が「単一uniform scaleでは2ターゲットを同時に満たせないための幾何平均妥協」と開示済みで、乖離幅も軽微なため許容。
- up軸+Y・前方軸+Z（Meshy既定）はMANIFEST記載通りで、独自インポート時のBlender軸変換（Y-up→Z-up変換の整合）からも矛盾は確認されなかった。
- 備考: assets-config.md はFBX+armatureのreimportにスケールドリフトの既知の限界があると注記しているが、MDL-01（リグ付き）・MDL-02（無リグ）とも本レビューの独自再計測がauthoring値と小数点3桁まで一致しており、今回のケースでは有意なドリフトは観測されなかった。

---

**【MDL-01/MDL-02共通】ポリゴン予算（観点2）— PASS（独自再現）**
- MDL-01: 独自計測47,254 tri（char1 47,174 + Icosphere 80）≤ 50,000（hero budget, art-bible.md）。※Icosphere除去が必須のため、除去後は47,174 triとなり引き続き予算内。
- MDL-02: 独自計測18,192 tri ≤ 20,000（enemy budget, art-bible.md独自基準）。

---

**【ANM-01〜03】リグ・クリップ（観点4）— PASS（独自再現、一部要記録改善）**
- 3ファイルともボーン数24・ボーン名がMDL-01と一致することを独自確認。
- 各ファイルにアクションが1本ずつ存在し（Attack/Idle/running）、開始・中間・終了フレームで複数ボーン（Hips/Spine/UpLeg×2/Arm×2/Head）の位置・回転が明確に変化していることを確認 — 静止/空クリップではなく実モーションが焼き込まれていることを実証。idle/runは始点と終点のポーズがほぼ一致しループ破綻がないことも確認。
- **[軽微・非ブロッキング指摘]** ANM-01（attack）の生クリップ長は84フレーム@30fps=2.8秒。design/assets.md は「再生長はAUTO_ATTACK_INTERVAL初期値0.6sを超えない（超える場合は発動間隔に合わせ再生速度をスケーリング）」と規定しており、超過時の対応方針自体は既に想定済みだが、MANIFESTには`duration_s`/`frame_count`/`fps`のいずれも記録されていないため、Integration側が正しいスケール係数（0.6/2.8≈0.214倍速、すなわち約4.7倍速再生が必要）を導出する手がかりがMANIFEST上にない。次回以降のANM系MANIFESTエントリに生クリップ長を記録することを推奨する（再生成不要・記録追加のみ）。
- **[軽微・非ブロッキング指摘]** anim-hero-{attack,idle,run}.fbxに同梱されているメッシュ（55,499 tri）はmodel-hero.fbxの最終メッシュ（decimate後47,254 tri）より約17%重く、単体ではhero予算50,000 triを超過する（未decimateのメッシュがそのまま残存）。tech-stack-unity.md「資産の取り扱い」節の標準手順（アニメーションFBXは同一スケルトンでインポートしリターゲット）に従う限り、このメッシュはUnity上で描画に使われず実害は生じない設計だが、MANIFESTにその旨（「anim系FBXの同梱メッシュは描画用途では使用不可・retarget専用」）が明記されていないため、Integration時に誤って独立モデルとしてインポートされるリスクの芽がある。記録追加を推奨（再生成不要）。

---

**【MDL-02固有】色相のパレット逸脱（観点5: スタイル一致）— CONCERNS**

`preview-swarmer.png`（実FBXから直接テクスチャ抽出できなかったため、生成パイプラインが出力した唯一の着色済みプレビュー画像を使用。上記テクスチャ欠落問題のため実テクスチャそのものでの再検証は次回再提出時に必須）に対しPillowで不透明前景画素を12刻み量子化してクラスタ集計した結果、支配的クラスタは色相240〜264°（青紫〜藍色）に集中していた。対して`design/art-bible.json`のパレット`enemy_primary_purple`（`#8B12A5`）の色相は≈289.4°（同ファイル`notes.crystal_magenta_revision`にも記載の実測値）であり、**約25〜49°の色相ずれ**が確認された（最近傍パレット色との距離51.7〜90.3、参考: hero側の最良クラスタは距離24.6〜26.6と大幅に良好）。

一方、image-to-3D入力元の`img-02-swarmer-concept.png`（本レビュー対象外・別バッチ承認済み）の主要色（155,80,202）は色相≈276.9°で目標289.4°との差は約12.5°に収まっており、**2Dコンセプト画からimage-to-3D変換の過程で紫→青紫方向へ色がシフトしている**ことが定量的に裏付けられる。P-03（群れ密度の圧力の視認性）はhero＝青／enemy＝紫という役割別の明確な色分離に依存する設計（style_block参照）であり、enemy側が青寄りに寄ることはhero色との識別性を損なうリスクがある。

**留保**: 本測定はレンダリング済みプレビュー画像に基づくものであり、Meshyのプレビュー用ライティング（ブルー寄りの環境光の可能性）による見かけ上の色相シフトを完全には排除できない。次回テクスチャ同梱後、無地/ニュートラル照明下でのアンリット・アルベド直接サンプリングによる再検証を最優先で行うこと。

---

**【MDL-01固有】太いダークネイビーのアウトラインがテクスチャ上で確認できない（観点5: スタイル一致）— CONCERNS**

`style_block`は「clean bold 2-3px dark-navy outlines on every silhouette edge」を明示的に要求している。`preview-hero.png`をズームして確認したところ、白トリムのハイライトストライプ（`#F2F5FA`系、装甲パネルの縁）は明確に再現されているが、シルエット全周を縁取る太いダークネイビーの輪郭線は視認できず、代わりに滑らかな方向性ライティングによるPBRグラデーション陰影が主体になっている。`design/art-bible.md`「3Dスタイル方針」節は「太いアウトライン線はコンセプト画側に描き込んだ状態でimage-to-3D生成することでベイクする」という方針を採用しているが、実際の生成結果ではこのベイクが期待通りに機能していない可能性が高い（image-to-3Dツールがコンセプト画の視点依存シルエット線を3D表面テクスチャへ正しく転写できないという、既知のパイプライン限界と整合する現象）。

色相自体（hue≈200-214°、目標207.9°）はhero側で良好に一致しており、配色ブロッキングの大枠は踏襲されているため**REJECTではなくCONCERNS**とするが、`art-bible.md`が「任意検討」と位置付けているURP Outlineポストプロセスは、実質的にこの資産では「必須（バックアップ手段ではなく唯一の手段）」に格上げが必要と考えられる。Integration側への申し送り事項として記録する（3Dモデル自体の再生成では解決しにくい問題のため、後段のポストプロセス対応を推奨）。

### 総合判定: CONCERNS
理由: 全5ファイル共通のテクスチャ参照破損（未同梱・存在しない絶対パス参照）という出荷不能レベルの欠陥を機械検査で特定した。加えてMDL-01には未文書化の浮遊ジオメトリ（Icosphere）混入という追加のブロッキング欠陥がある。これらはいずれも「スタイルロック自体の欠陥」や「ライセンス違反」ではなく、明確な原因（テクスチャファイルの配置漏れ／エクスポート後処理でのゴミオブジェクト除去漏れ）を伴う再現可能な技術的欠陥であり、具体的な再生成・再エクスポート指示で解消可能と判断したためREJECTではなくCONCERNSとする。ポリ数・ボーン数・スケール・アニメクリップの実在性・非多様体の可視影響など、その他の機械検査項目は独自検証でも概ねPASSしている。

### 再生成指示（優先度順）

1. **[全5件（MDL-01, MDL-02, ANM-01, ANM-02, ANM-03）・優先度最高]** テクスチャファイルをFBXへ正しく同梱すること。推奨: (a) FBXエクスポート時に「Embed Media（バイナリ埋め込み）」オプションを有効化し、テクスチャをFBX内部に完全内包する、または (b) 参照パスをリポジトリ相対パス（例: `game/_generated/models/textures/model-hero-albedo.png`）に修正した上で当該テクスチャファイルを実際にそのパスへ配置する。生成環境ローカルの`/tmp`絶対パスや存在しない絶対パスを参照に残さないこと。修正後は本レビューで実施した検査（バイト列でのPNG/JPEGシグネチャ検出 or Blender再インポートでの`image.size != (0,0)`確認）を再現し、MANIFESTに再検証結果を記録すること。

2. **[MDL-01・優先度高]** `model-hero.fbx`から素性不明の`Icosphere`オブジェクト（80 tri、マテリアル未割当、Armature非アタッチ、bbox≒1.27×1.33×1.33mでキャラクター全体を包含）を除去して再エクスポートすること。Blenderでの対応例: `bpy.data.objects.remove(bpy.data.objects["Icosphere"], do_unlink=True)` をエクスポート前処理に追加。除去後、独自インポートで`mesh_count==1`となることを確認すること。

3. **[MDL-02・優先度中]** テクスチャ同梱修正後、無地/ニュートラル照明でのアンリット・アルベド直接レンダリング（Blender headless、Emission相当のシェーダーで直接テクスチャ色を可視化）を行い、主要色相が目標`enemy_primary_purple`（`#8B12A5`, hue≈289.4°）に対し引き続き25°以上のずれを示す場合、image-to-3Dプロンプトへ色相固定の明示的指示（例: "preserve exact source albedo hue #8B12A5 (magenta-purple), do not shift toward blue or indigo"）を追加して再生成するか、IMG-03と同様のHSV色相帯域補正retouchをテクスチャに適用すること。

4. **[MDL-01・優先度低]** 太いダークネイビーのシルエットアウトラインがテクスチャに反映されていない件は、3Dモデル単体の再生成では解決見込みが低いため、Integration側でのURP Outlineポストプロセス適用を「任意」から「実質必須」に格上げするようart-bible.md/Integration申し送りへ反映することを推奨（art-reviewerの編集権限外のため本指摘のみ記録、対応はart-director/tech-directorへ）。

5. **[ANM-01〜03・優先度低・記録改善のみ]** MANIFESTへ各クリップの`duration_s`（またはframe_count・fps）を追記し、ANM-01については実装時に必要な速度スケール係数（AUTO_ATTACK_INTERVAL 0.6s ÷ 実測2.8s ≈ 0.214倍）を計算できるようにすること。また、anim系FBXに同梱されるメッシュはdecimate前の重量級コピー（55,499 tri、hero予算超過）であり描画用途に使わないことをMANIFESTまたはassets.mdへ明記すること。

### disclosures（再生成不要・人間開示のみ）
- MDL-01, MDL-02, ANM-01, ANM-02: `cost_estimated:true`（Meshyクレジット→USD換算は`state/asset-routing.json`記載の$0.02/credit保守見積であり、プロバイダ確定請求額ではない）。ANM-03は`cost_usd:0`（rigging taskに無償同梱、確定額）。
- MDL-01, MDL-02, ANM-01〜03: `plan_tier:"pro+"`は`state/asset-routing.json`のnotesで「間接証明（balanceレスポンスにtierフィールド無し）」と明記された未検証値。
- MDL-02: 既に`must_replace:true`・`degraded_route`として自己開示済み（quadruped auto-rigがMeshy直API・Tripo直APIの両方で失敗——422 Pose estimation failed / 403 credit不足。ローカル縮退Blender+Rigifyは未着手）。本レビューが新たに追加したテクスチャ欠陥とは独立した既存の未解決事項として、Checkpointでの開示を継続すること。
- ANM-04（approach_loop）: `design/assets.md`上ステータス`must-replace`のまま未生成（MDL-02リグ未完了が原因）。本バッチにファイル自体が存在しないため機械検査対象外だが、Checkpoint Bまでに（a）Tripoクレジット補充後の再試行、または（b）`local:code-motion`によるコード側代替実装、のいずれかの決着が必要な既知の未解決事項として申し送る。
- 3D資産のルーティング実績は`state/asset-routing.json`上`meshy:direct-*`（Meshy直API）であり、fal.ai経由ではない。gates.md AR-ASSET観点6の「fal経由Meshyのライセンス継承未検証」は本バッチには該当しない（確認済み・非該当）。

- 対応: （art-director 記入欄）

## AR-ASSET iteration 2 — CONCERNS
- 日時: 2026-07-10T16:15:00Z
- 対象: `game/_generated/MANIFEST.jsonl` の revision:2 追記分 = MDL-01（`model-hero.fbx`, sha256 `17c85e80...`）、MDL-02（`model-swarmer.fbx`, sha256 `8bcd0855...`）、ANM-01/02/03（`anim-hero-{attack,idle,run}.fbx`）。全5ファイルとも art-director revise（`review_ref: "AR-ASSET (3D batch) 不合格対応"`）による iteration 1 CONCERNS 対応版。ANM-04 は引き続き未生成（対象外・disclosures参照）。
- 検査方法（独自再現、iteration 1 と同一方針で MANIFEST 自己申告に非依存）: (1) `shasum -a 256` で on-disk ファイルと MANIFEST revision:2 エントリの sha256 突合、(2) 生バイト列 PNG シグネチャ走査（`\x89PNG\r\n\x1a\n` カウント）、(3) Blender 5.1.2 headless（`bpy.ops.import_scene.fbx` デフォルト設定）でのオブジェクト内訳・画像 packed 状態・ポリ数・ボーン数/名・アクション確認、(4) Blender headless → GLB 変換 + `npx @gltf-transform/cli validate`/`inspect`（Khronos 標準 +Y-up canonical bbox の取得。これは MANIFEST 自身が validator 手法として明記する手順と同一）、(5) Armature/Mesh オブジェクトの `obj.scale`/`matrix_world.to_scale()` をフレーム1・中間・最終で採取、(6) swarmer base color テクスチャを Blender 経由で PNG 抽出し Pillow で HSV 色相分布を再計測。

### 観点別結果

**provenance（sha256照合）— PASS**
on-disk 5ファイル全てで MANIFEST revision:2 の sha256 と完全一致（改ざん・取り違えなし）。

---

**【iteration 1 指摘1 再検証】テクスチャ埋め込み — PASS（解消確認）**
5ファイル全てで PNG シグネチャが4件検出（旧revisionは0件）。Blender 再インポートでも全画像 `size=(2048,2048)`, `packed_file` 非None を確認。model-hero.fbx / model-swarmer.fbx / anim-hero-{attack,idle,run}.fbx いずれも base_color・metallic・roughness・normal の4テクスチャが正しく embed されている。旧revisionの `/tmp/meshy_redo/...` や存在しない絶対パス参照は残存しているが（FBX内 `RelativeFilename`/`FileName` フィールド自体は変更されていない）、Content blob 側に実データが埋め込まれているため Unity 標準インポート（embedded media 優先）で解決不能パスにフォールバックすることはない。**指摘1は解消**。

**【iteration 1 指摘2 再検証】MDL-01 の素性不明 Icosphere — PASS（解消確認）**
Blender 独自再インポートで `model-hero.fbx` のオブジェクトは `Armature` と `char1` の2つのみ（`Icosphere` 消失、mesh_count=1）。ポリ数は 47,174 tri（旧: 47,254 = 47,174 + Icosphere 80）で Icosphere 分のみ減少しており除去内容と整合。**指摘2は解消**。

**【iteration 1 指摘3 再検証】MDL-02 色相ずれ — PASS（解消確認、実測値で追認）**
`model-swarmer.fbx` から抽出した `swarmer_tex_base_retouched.png`（2048x2048）に対し独自に HSV 色相解析（不透明・saturation>0.15・value>0.05 の262,144画素サンプル）を実施した結果、mean hue = **289.37°**、median = 289.47°（71.5%が288–300°バケットに集中）。目標 `enemy_primary_purple` #8B12A5（hue≈289.4°）との差は **0.03°**（旧revisionは25〜49°ずれ）。**指摘3は解消**。

**【gltf-transform validate 再検証】5ファイル全て ERROR 0 — PASS（変化なし）**
Blender→GLB独自変換 + `npx @gltf-transform/cli validate` で5ファイル全て ERROR セクション「No errors found」を確認（exit 0）。WARNING は `MESH_PRIMITIVE_GENERATED_TANGENT_SPACE` と `NODE_SKINNED_MESH_NON_ROOT`（いずれもseverity 1）のみで iteration 1 と同一。Unity ネイティブFBXインポート経路には影響しないと判断（iteration 1 判断を継続）。

---

**【新規検出・最優先ブロッキング】ANM-01/02/03: Armatureオブジェクトの未適用スケール0.01がFBXに焼き込まれ、キャラクターが約100分の1に縮小 — FAIL（ブロッキング）**

`anim-hero-attack.fbx` / `anim-hero-idle.fbx` / `anim-hero-run.fbx` の3ファイル全てで、Blender再インポート直後の `Armature` オブジェクトの `obj.scale` = **(0.01, 0.01, 0.01)**（`matrix_world.to_scale()` も同値）を確認した。アクションのフレーム1・中間フレーム・最終フレームいずれで採取しても同一値であり、ポーズアニメーションの一時的な値ではなく**オブジェクトのベース変換に恒久的に焼き込まれた縮小スケール**である。

- Armatureの rest-pose（edit bone）ローカル座標自体は MDL-01 と完全一致（例: Hips head `[0.004270, -0.014498, 0.659596]` は両者で小数点6桁まで一致）——**骨格データそのものは破損していない**。純粋にオブジェクトTransformのScaleだけが0.01のまま未適用（unapplied）で書き出されている。
- 実害の実測: Blender headless で FBX→GLB 変換し `npx @gltf-transform/cli inspect` で取得した glTF標準（+Y-up, メートル）canonical bbox は3ファイルとも `bboxMin=(-0.00003, 0, -0.00001)` `bboxMax=(0.00003, 0.00011, 0.00001)` ——**キャラクター全高が約0.00011m（0.11mm）**。Unityへこのままインポートした場合、AnimatorでMDL-01（実寸約1.13m、後述）にリターゲットしない限り、このFBXを単体基準にすると視認不能なサイズになる。
- 原因推定: iteration 1 対応の「テクスチャ埋め込み修正」再エクスポート処理（Meshy元タスクからの再ダウンロード→Blenderでのマテリアル再構築→`export_scene.fbx(embed_textures=True, path_mode='COPY')`）が、MDL-01本体とは異なる由来（rig taskの`running_fbx_url`等、別エンドポイントからの再ダウンロード）のソースファイルに対して実行された際、Object Scale の unapply（`transform_apply(scale=True)`相当の処理）が抜け落ちた可能性が高い。MDL-01自体は `obj.scale=(1,1,1)` で正常なため、このバグは ANM-01〜03 の再エクスポート手順に固有。
- **iteration 1 では未検出**の新規回帰。iteration 1 のレビューはテクスチャ欠落問題を最優先し、ANM系の独自スケール検証まで踏み込んでいなかったため見逃されていた可能性がある。

**再生成指示（優先度最高）**: ANM-01/02/03 の3ファイルについて、Blender再エクスポート前に対象Armatureオブジェクト（および子のchar1メッシュオブジェクト）を選択し `bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)` を実行してScaleを`(1,1,1)`に焼き込んでから `export_scene.fbx(embed_textures=True, path_mode='COPY')` を実行すること。エクスポート後、本レビューと同じ手順（Blender再インポート→`obj.scale`実測 かつ Blender→GLB変換→`gltf-transform inspect`のbboxMax）で自己検証し、`obj.scale==(1,1,1)` かつ bbox高さがMDL-01修正後の値（下記参照）と整合することを確認してから再提出すること。

---

**【新規検出・優先度高】MDL-01: 実寸高さ1.133mで art-bible.json の要求レンジ[1.6, 2.0]（目標1.8m）を約37%下回る — FAIL（ブロッキング）**

`model-hero.fbx` を Blender headless で標準インポート（追加パラメータなし）し、そのまま Blender公式glTFエクスポータでGLB変換の上 `npx @gltf-transform/cli inspect` で取得した glTF標準（+Y-up, メートル）canonical bboxは `bboxMin=(-0.26575, 0, -0.13102)` `bboxMax=(0.26575, 1.13333, 0.13125)` ——**全高 1.13333m**。MANIFEST `bbox_authoring_m.height_m: 1.8` および `design/art-bible.json` `scale.hero_height_range_m: [1.6, 2.0]`（目標 `hero_height_m: 1.8`）に対し **約37%（0.667m）不足**し、要求レンジの下限1.6mにも届いていない。

- 独自Blender再インポート（デフォルトパラメータ）での `char1` メッシュ `dimensions` = `[0.5315, 0.2622, 1.1334]`、`Armature` `dimensions` = `[0.4667, 0.2454, 1.2256]` と3系統の測定（生dimensions／GLB canonical bbox／Armatureのrest-pose骨格座標）が相互に整合しており、単一ツールの軸変換バグではないと判断した。Armature rest-pose座標（Hips head Z≈0.66、LeftFoot tail Z≈0.02）から算出される脚の長さ（約0.64m）・体幹プロポーションも、全高約1.13mの人型として構造的に自然であり、ポーズ由来（前傾姿勢等でのbbox圧縮）ではなく**ジオメトリ自体が小さい**ことを示している。
- `obj.scale`（Armature/char1とも`(1,1,1)`）は正常であり、ANM-01〜03のような単純な「未適用オブジェクトスケール」ではない（この点はANM系と異なり原因が別）。revision_reasonが「無変更の証跡」として提示するArmature dimensions `[0.4667, 0.2454, 1.2256]` は本レビューでも同値を独自確認できたが、この値自体が1.8m相当のキャラクターの妥当なArmature bboxとして裏付けられたことは一度もなく（Armatureの各ボーン包絡は関節位置ベースでメッシュ全体を包含しない場合がある）、「revision間でdimensionsが不変＝スケール健全」という旧revisionの検証ロジックは、そもそも絶対値としての1.8m一致を示す根拠になっていなかった点を指摘する。
- **留保（assets-config.md の既知の制約を踏まえて）**: assets-config.md「生成後パイプライン」は「FBX+armatureのreimportにスケールドリフトの既知の限界がある」ため authoring-time計測を第一情報源とすべきと注記しており、本測定はpost-export reimportに基づく。しかし観測された乖離幅（37%）は同ファイルが言及する「既知のドリフト」の想定範囲（通常サブパーセント〜数%オーダー）を大きく超えており、単純な数値誤差として片付けられる規模ではないと判断した。

**再生成指示（優先度高）**: (a) 元のBlender authoring シーン（image-to-3D 生成直後、エクスポート前）で改めてキャラクター高さを実測し、1.8m（レンジ1.6–2.0m）を満たしていたか一次証跡（スクリーンショットまたはBlenderコンソールのdimensions出力ログ）を確認する。(b) 満たしていなかった場合はジオメトリ自体を目標スケールへ補正（現状の約1.589倍）した上で再エクスポートする。(c) 満たしていた場合は「authoring→texture-embed-fix」のBlender再インポート/再エクスポート過程でスケールが変質した工程を特定し、修正すること。(d) いずれの場合も再エクスポート後、本レビューと同一手順（Blender→GLB変換→`gltf-transform inspect`のbboxMax.y）で1.6–2.0m範囲内であることを自己検証し、MANIFESTの`bbox_authoring_m`を実測値で更新して再提出すること。(e) ANM-01〜03とスケルトンを共有するため、モデルとアニメーション4ファイル全てで同一の補正係数を適用し整合を保つこと（別々に補正すると相対スケールが崩れUnity上でretarget時に不整合が生じる）。

---

**【未変更・iteration 1から継続】その他の観点 — 変化なし（参考）**
- ポリゴン予算: MDL-01 47,174 tri ≤50,000 / MDL-02 18,192 tri ≤20,000。ジオメトリ自体は無変更のためPASS継続。
- 非多様体境界エッジ（MDL-01 約6,614本 / MDL-02 約3,720本）: ジオメトリ無変更のため iteration 1 の可視破綻なし判定を継続（非ブロッキング）。
- ANM-01〜03 のボーン数24・ボーン名一致・実モーション焼き込みはジオメトリ/アクションデータ無変更のため継続PASS（ただしオブジェクトスケール0.01の影響で実際のワールドスケールは上記の通り破綻）。

### 総合判定: CONCERNS
理由: iteration 1 で指摘した3件（テクスチャ埋め込み破損・Icosphere混入・MDL-02色相ずれ）は全て独自再現検査で解消を確認できた（art-directorの対応は的確）。一方で、今回の独自スケール検査で **ANM-01/02/03 に未適用オブジェクトスケール0.01（約100分の1に縮小、iteration 1 未検出の新規回帰）**、および **MDL-01 の実寸高さが目標比37%不足（1.133m、要求レンジ1.6–2.0m未達）**という、いずれも観点3（スケール・向き）に関わる2件の新規ブロッキング欠陥を機械検査で特定した。原因が特定可能で再生成・再エクスポート手順の修正で解消できる技術的欠陥であり、スタイルロック自体の欠陥やライセンス違反ではないためREJECTではなくCONCERNSとする。**iteration 3（MAX_ITER到達）が次回のため、MDL-01/ANM-01〜03は次回不合格でfallbackプロバイダ切替（review-loops.md）の対象になる**ことをart-directorへ明示する。

### 再生成指示（優先度順）
1. **[ANM-01, ANM-02, ANM-03・優先度最高]** Armatureオブジェクトの未適用Scale (0.01,0.01,0.01) を `bpy.ops.object.transform_apply(scale=True)` 等で(1,1,1)に焼き込んでから再エクスポート。上記詳細参照。
2. **[MDL-01・優先度高]** 実寸高さ1.133m→目標1.8m（レンジ1.6–2.0m）への補正。authoring時点の一次証跡確認→ジオメトリ補正またはエクスポート工程修正→本レビュー手法での自己検証→MANIFEST `bbox_authoring_m` 更新。ANM-01〜03と同一係数で整合させること。
3. **[MDL-02・優先度中・継続、対応済み]** iteration 1 指摘（色相ずれ）は独自実測で解消確認。追加対応不要。
4. **[MDL-02・優先度中・既存継続、変更なし]** quadruped auto-rig未完了（`must_replace:true`）は本iterationのスコープ外のまま継続（disclosures参照）。

### disclosures（再生成不要・人間開示のみ）
- MDL-02: quadruped auto-rig未完了（`must_replace:true`, `degraded_route`: Meshy直API=HTTP422、Tripo直API=HTTP403クレジット不足）。iteration 1から状況変化なし。ANM-04（approach_loop）も同理由で未生成のまま。
- MDL-01: 太いダークネイビーのシルエットアウトラインがテクスチャ上で確認できない件（iteration 1 CONCERNS 優先度低）は、テクスチャ内容自体が今回変更されていないため未解消のまま。3Dモデル単体の再生成では解決見込みが低く、Integration側でのURP Outlineポストプロセス適用を「任意」から「実質必須」に格上げする申し送りを継続する。
- MDL-01, MDL-02, ANM-01〜03: `plan_tier:"pro+"`は`state/asset-routing.json`のnotesで「間接証明（balanceレスポンスにtierフィールド無し）」と明記された未検証値（iteration 1から継続）。
- 3D資産のルーティング実績は`state/asset-routing.json`上`meshy:direct-*`（Meshy直API）であり、fal.ai経由ではない。gates.md AR-ASSET観点6の「fal経由Meshyのライセンス継承未検証」は本バッチには該当しない（確認済み・非該当、iteration 1から継続）。
- revision:2エントリの`cost_usd:0`/`cost_estimated:false`（再ダウンロード＋ローカルBlender処理のみで追加Meshyクレジット消費なし）はMeshy balance変化なしの自己申告に基づく。本レビューでは第三者的なMeshy balance照会は行っていない（未検証・確度は自己申告どまり）。

- 対応（art-director、revision:3として対応済み。詳細は `game/_generated/MANIFEST.jsonl` の当該4エントリ `revision:3` を参照）:
  1. **[ANM-01/02/03・優先度最高・対応済み]** 単純な `transform_apply(scale=True)` では解消しないことを実測で確認した（export直後は `armature.scale==(1,1,1)` だが、再インポートすると`(0.01,...)`に戻る再現バグを検出）。根本原因を追加調査した結果、Armatureオブジェクト自身のActionに**pose.bonesとは別のOBJECT-levelなlocation/rotation_euler/scale fcurve**（各2キーフレーム・frame1と最終フレームで同値=非アニメーション的定数）が残存しており、`bake_anim=True`でのフレーム評価時にこの残存fcurveが`armature.scale`を`0.01`へ再適用していたことを特定した（reviewer推定「再エクスポート手順でのunapply抜け」とは異なる、より具体的な原因）。対応: 該当9本のfcurve（location×3, rotation_euler×3, scale×3）をActionから削除した上で、MDL-01と同一の補正係数F=1.588162195358737をArmature.scaleへ設定し`transform_apply`でedit-bone rest poseへbake、再エクスポート。pose.bonesのモーションキーフレーム（frame_range: attack[1,85]/idle[1,121]/run[1,20]、ボーン名24種）は一切変更していないことを差分比較で確認済み。再エクスポート後、独自にBlender再インポート→`obj.scale==(1,1,1)`、Blender→GLB変換→`gltf-transform validate`エラー0、を確認（下記MANIFEST validatorに記録）。
  2. **[MDL-01・優先度高・対応済み]** 一次証跡（authoring-time スクリーンショット等）は本セッションでは遡って取得できなかったため、(a)は実施不能と判断し(b)の経路（ジオメトリを目標スケールへ補正）を選択した。指摘通りの補正係数（現状比約1.589倍、厳密には F=1.8/1.1333854975646318=1.588162195358737）をArmature.scaleへ乗算し、`transform_apply(scale=True)`でchar1メッシュ・edit-boneへ焼き込んでから再エクスポート。再エクスポート後、独自にBlender→GLB変換→`gltf-transform inspect`相当（本環境ではBlender evaluated-depsgraph world bbox実測、gltf-transform validateは構造検証のみのためinspectの代替として採用）でheight=1.799969m（レンジ[1.6,2.0]内・目標1.8mとの差0.003%）を確認し、MANIFESTの`bbox_authoring_m`を実測値へ更新した。
  3. **[ANM-01〜03とMDL-01のスケール整合・優先度最高・対応済み]** 指摘(e)の通り、4ファイル全てに同一係数F=1.588162195358737を適用し、24種のボーン名がMDL-01と全ファイルで完全一致することを`diff`で機械確認済み（Unity retarget時の相対スケール不整合を回避）。
  4. **[非該当/継続]** MDL-02のquadruped auto-rig未完了、MDL-01のアウトラインテクスチャ不在は本バッチの指摘対象外（変更なし、disclosures参照）。
  - 検証済みだが未解決のまま残る事項: (a)の一次証跡（authoring-time計測ログ）は取得できなかったため、「Meshy生成時点から1.8m未達だったのか、その後の工程で縮小したのか」の根本原因切り分けは行えていない。今回の対応は結果（現在のファイルが1.8mになる）を保証する補正的対応であり、Meshy側の生成条件（`character_height_meters=1.8`パラメータ指定が実際に機能しているか）の恒久検証は次回生成時の申し送り事項とする。

## AR-ASSET iteration 3 — APPROVE
- 日時: 2026-07-10T16:45:00Z
- 対象（MAX_ITER到達回・review-loops.md 3/資産）: `game/_generated/MANIFEST.jsonl` の revision:3 追記分 = MDL-01（`model-hero.fbx`, sha256 `d0a71f3a...`）、ANM-01/02/03（`anim-hero-{attack,idle,run}.fbx`, sha256 `25cb64db...`/`0ddf52c8...`/`550d8771...`）。MDL-02（`model-swarmer.fbx`）は本iterationで無変更（revision:2のまま、sha256 `8bcd0855...`）— iteration 2で独自実測PASS済みの資産として据え置き確認のみ実施。ANM-04は引き続き未生成（disclosures参照）。
- 検査方法（独自再現、MANIFEST自己申告に非依存。iteration 1/2と同一方針を継続）: (1) `shasum -a 256` でon-diskファイルとMANIFEST revision:3（swarmerはrevision:2）エントリのsha256突合。(2) Blender 5.1.2 headless（`bpy.ops.import_scene.fbx`デフォルト設定）でオブジェクト内訳・画像packed状態・ポリ数/tri数・ボーン数・ボーン名・マテリアル数を独自スクリプトで採取。(3) Blender 5.x レイヤードAction API（`action.layers[].strips[].channelbags[].fcurves`）でobject-level fcurve（pose.bones以外のchannel）を独自抽出し、data_path・keyframe値・補間タイプ・ハンドルを直接検査（iteration 2で使ったAPIが本環境では`AttributeError: 'Action' object has no attribute 'fcurves'`で失敗したため、本iterationで新API対応スクリプトへ更新して再実施）。(4) `bpy.context.scene.frame_set()`でアクション開始・中間・終了フレームを走査し、評価済みdepsgraph上の`Armature.matrix_world.to_scale()`とworld空間mesh bbox（evaluated mesh頂点をworld行列変換）を毎フレーム相当で実測。(5) Edit Mode下で`Hips`/`LeftFoot`等のedit-bone rest position（head/tail座標）をMDL-01と3本のANMファイル間で直接比較しスケルトンスケール整合性を検証。(6) Blender headless→GLB変換＋`npx @gltf-transform/cli validate --format md`を独自実行（5ファイル全て）。(7) 埋め込みテクスチャをBlender経由でPNG抽出し目視レンダリング検証、および`bpy.ops.render.render`によるEEVEE簡易ライティングレンダー（800x800、サンライト単灯）でモデル全体のスタイル整合を再確認。

### 観点別結果（iteration 2 で検出した2件のブロッキング不合格の再検証を最優先で実施）

**provenance（sha256照合）— PASS**
5ファイル全て（MDL-01, ANM-01, ANM-02, ANM-03revision:3、MDL-02 revision:2）でon-disk実測shasum -a 256とMANIFEST記載sha256が完全一致。改ざん・取り違えなし。

---

**【iteration 2 指摘1 最終再検証】ANM-01/02/03: Armatureの未適用スケール0.01問題 — PASS（解消確認、ただし修正手法の記録に軽微な不整合あり）**

独自Blender再インポートで3ファイルとも `Armature.scale == (1.0, 1.0, 1.0)`、かつ`frame_samples`（各クリップのstart/mid/endフレーム）全てで`matrix_world.to_scale() == (1.0, 1.0, 1.0)`を確認（旧revisionの0.01からの回帰は解消）。

さらにEdit Mode下のrest-pose bone座標を独自比較した結果、`Hips`（head Z≈1.047545）・`LeftFoot`（head/tail）ともMDL-01と3本のANMファイルすべてで小数点6桁まで完全一致することを確認し、スケルトンスケールの相対整合（Unity retarget時に必要な条件）も独立に実証した。

**軽微な記録不整合（非ブロッキング）**: MANIFESTの`revision_reason`/`validator.root_cause`は「Armatureオブジェクト自身のActionに残存していたobject-level location/rotation_euler/scaleの9本のfcurveを**削除**した」と記載しているが、独自にBlender 5.xレイヤードAction APIで直接fcurveを抽出したところ、当該9本（location×3, rotation_euler×3, scale×3）は**削除されておらず現存**していた。ただし各fcurveの2キーフレーム値が旧来の不正値（scaleなら0.01）から中立値（location=0.0, rotation_euler=0.0, scale=1.0）へ**上書き修正**されており、補間タイプは全チャンネルLINEAR・2キーフレームとも同一値のため補間区間中のオーバーシュートは理論上発生しない（この点も独自に全フレーム相当のsceneサンプリングで裏付け済み、常時1.0/0.0を維持）。したがって**機能的には修正は有効**であり、実害はない。しかしMANIFESTの技術的な修正手法説明（「fcurve削除」）が実際のファイル状態（「fcurve現存・値のみ上書き」）と一致しておらず、将来のメンテナ・Integration担当がこの記述を信じてfcurve不在を前提にすると誤解を招く。次回MANIFEST記述時は実際の操作内容（値の上書きか削除か）を正確に記載することを推奨する（regenerationは不要、記録訂正のみ）。

---

**【iteration 2 指摘2 最終再検証】MDL-01: 実寸高さ不足（旧1.133m→目標1.8m） — PASS（解消確認）**

Blender独自再インポート＋evaluated depsgraph world bbox実測で `char1` メッシュの高さ = **1.799969m**（`world_dimensions_zup`のZ成分）。`dimensions`プロパティでも同一値（1.7999690771102905m）を確認。`design/art-bible.json` `scale.hero_height_range_m: [1.6, 2.0]`（目標1.8m）に対し**目標との差0.0017%**で完全に収まっている。

また、独自レンダリング（EEVEE、800x800、サンライト単灯）でモデル全体を可視化した結果、Icosphereのような不要オブジェクトの再混入や、スケール補正由来の変形・破綻は視覚的に確認できなかった。オブジェクトリストは`['Armature', 'char1']`のみで、iteration 1で指摘したIcosphereも引き続き非存在を確認（**継続PASS**）。

---

**【継続確認】gltf-transform validate — 5ファイル全てERROR 0（PASS、変化なし）**
Blender→GLB独自変換＋`npx @gltf-transform/cli validate --format md`で5ファイル全て「No errors found」を確認（MDL-01, ANM-01, ANM-02, ANM-03, MDL-02）。WARNINGは`MESH_PRIMITIVE_GENERATED_TANGENT_SPACE`と`NODE_SKINNED_MESH_NON_ROOT`（いずれもseverity 1）のみでiteration 1/2と同一、Unity ネイティブFBXインポート経路には影響しないと判断（継続）。

**【継続確認】テクスチャ embed — 5ファイル全て維持（PASS、変化なし）**
MDL-01: hero_tex_{base,metallic,normal,roughness}.png 全て`size=(2048,2048)`・`packed_file`非None。ANM-01/02/03も同一4テクスチャが維持され、fcurve修正のための再エクスポート後も破損・欠落なし。MDL-02（無変更）も4テクスチャ維持を再確認。

**【継続確認】ポリゴン予算・ボーン数 — PASS（変化なし）**
MDL-01: 47,174 tri（char1単体、budget 50,000 tri以内）、bone_count 24。ANM-01/02/03: 同梱メッシュ55,499 tri（description欄=「retarget専用・描画非使用」の記録改善は未対応のまま継続、下記disclosures参照）、bone_count 24（MDL-01と名称完全一致）。MDL-02: 18,192 tri（budget 20,000 tri以内、enemy budget）。

**【継続確認】MDL-01 スタイル一致（ダークネイビーアウトライン欠如） — CONCERNS継続（非ブロッキング、Integration申し送り事項）**
独自レンダリング（EEVEE、簡易ライティング）およびBlender経由での`hero_tex_base.png`直接抽出のいずれでも、`style_block`が要求する「太い2-3pxダークネイビーのシルエット輪郭線」は確認できなかった（白トリムのハイライトストライプは明瞭に再現されているが、輪郭線自体は無し）。テクスチャ内容がrevision2から無変更（MANIFEST記載通り）のため、iteration 1からの継続事項として変化なし。3Dモデル単体の再生成では解消見込みが低いため、引き続きIntegration側でのURP Outlineポストプロセス適用の「実質必須」格上げを申し送る（art-reviewerの編集権限外）。

**【継続確認】MDL-02 色相・テクスチャ — PASS（本iteration対象外、無変更のため独自再検査は省略し前回実測値を継続採用）**
本iterationではMDL-02自体はrevision変更なし（sha256完全一致）。iteration 2で独自HSV実測により色相ずれ解消（目標289.4°との差0.02°）を確認済みのため、本iterationでの重複再測定は行っていない。

### 総合判定: APPROVE
理由: iteration 2で検出した2件のブロッキング欠陥（① ANM-01/02/03のArmature未適用スケール0.01による約100分の1縮小、② MDL-01の実寸高さ1.133m＝目標比37%不足）は、いずれも独自の機械検査（Blender headless再インポート、evaluated depsgraph world bbox実測、edit-bone rest position比較、全フレームサンプリングでのスケール推移確認）で**解消を確認**した。iteration 1で検出した3件（テクスチャ参照破損、Icosphere混入、MDL-02色相ずれ）も引き続き解消状態を維持している。gltf-transform validateは5ファイル全てエラー0、ポリゴン予算・ボーン数・スケルトン整合も全て独自検証でPASS。新規に検出したのはMANIFEST記述と実ファイル状態の軽微な不一致（fcurve「削除」と記載されているが実際は「値の上書き」）のみで、これは機能的実害のない記録上の問題であり、レンダリング・全フレームサンプリングでオーバーシュートが無いことも直接確認済みのため再生成対象としない。残る指摘（MDL-01のアウトライン欠如、ANM系同梱メッシュの重量超過に関する記録改善、MDL-02/ANM-04のmust_replace）はいずれもiteration 1/2から継続する非ブロッキング事項であり、3Dモデル単体の再生成では解消しない（Integration側対応 or 記録追加のみで足りる）性質のため、disclosuresとして開示するにとどめAPPROVEとする。review-loops.md のMAX_ITER（3/資産）に本iterationが該当するが、全ブロッキング指摘が解消されたためfallbackプロバイダ切替は不要と判断する。

### 再生成指示
なし（本iterationでブロッキング不合格資産なし）。

### 記録改善の推奨（再生成不要、次回MANIFEST更新時に反映を推奨）
1. **[ANM-01/02/03・優先度中]** `revision_reason`/`validator.root_cause`の「object-level fcurveを削除」という記述を、実際の操作内容（fcurveは現存し、キーフレーム値をLINEAR補間の中立値へ上書き）に合わせて訂正すること。
2. **[ANM-01〜03・優先度低・iteration 1から継続]** MANIFESTへ各クリップの`duration_s`/`frame_count`/`fps`を明記すること（現状`clip_length_note`にframe_rangeのみ記載でfpsが無く、AUTO_ATTACK_INTERVAL用の再生速度スケール係数を導出する手がかりが引き続き不足）。
3. **[ANM-01〜03・優先度低・iteration 1から継続]** 同梱メッシュ（55,499 tri、hero予算超過）が描画用途では使用不可・retarget専用である旨をMANIFESTまたはassets.mdへ明記すること。

### disclosures（再生成不要・人間開示のみ）
- **MDL-01**: 太いダークネイビーのシルエットアウトラインがテクスチャ上で確認できない件（iteration 1から継続、独自レンダリングで再確認）。3Dモデル単体の再生成では解決見込みが低く、Integration側でのURP Outlineポストプロセス適用を「任意」から「実質必須」に格上げする申し送りを継続する。
- **MDL-02 / ANM-04**: quadruped auto-rig未完了・`must_replace:true`（Meshy直API rigging=HTTP422、Tripo直API=HTTP403クレジット不足。fallback chain両方失敗済み）。design/assets.mdの資産状態欄自体が`must-replace`と明記された既知の設計許容状態であり、本iterationのrevisionスコープ外。Integration/build時にTripoクレジット補充後の再試行、またはlocal:code-motionによるコード側代替実装が必要な未解決事項として申し送りを継続する。
- **MDL-01, ANM-01〜03**: `plan_tier:"pro+"`は`state/asset-routing.json`のnotesで「間接証明（balanceレスポンスにtierフィールド無し）」と明記された未検証値（iteration 1から継続）。
- **MDL-01, ANM-01〜03のrevision:3**: `cost_usd:0`/`cost_estimated:false`（Meshy再呼び出しなし・既存出力へのローカルBlender幾何補正のみ）は自己申告に基づく。本レビューではMeshy balanceの第三者照会は行っていない（未検証）。
- 3D資産のルーティング実績は引き続き`meshy:direct-*`（Meshy直API）+`local:blender-*`であり、fal.ai経由ではない。gates.md AR-ASSET観点6の「fal経由Meshyのライセンス継承未検証」は本バッチには該当しない（確認済み・非該当、iteration 1から継続）。

- 対応（art-director、2026-07-13、記録改善のみ・ファイル本体無変更のためAPPROVE判定は無影響。詳細は `game/_generated/MANIFEST.jsonl` ANM-01/02/03 の `revision:5` を参照）:
  1. **[ANM-01/02/03・優先度中・対応済み]** `revision_reason`/`validator.root_cause`の「object-levelのfcurve9本を削除した」という誤記述を訂正した。実際の操作内容（fcurveは削除されておらず現存し、キーフレーム値をLINEAR補間の中立値（location=0.0/rotation_euler=0.0/scale=1.0）へ上書きした）を`fcurve_description_correction`フィールドとして`revision:5`に明記し、旧`revision:3`記述の誤りを正式に訂正した。
  2. **[ANM-01〜03・優先度低・対応済み]** `duration_s`/`frame_count`/`fps`を`revision:5`へ追記した: ANM-01(attack)=frame_range[1,85]→85フレーム/fps30/duration_s=2.8s（AUTO_ATTACK_INTERVAL 0.6s との再生速度スケール係数≈0.214も併記）、ANM-02(idle)=121フレーム/fps30/duration_s=4.0s、ANM-03(run)=20フレーム/fps30/duration_s≈0.633s。fpsはiteration 1で reviewer が示した「84フレーム@30fps=2.8s」の推定値を踏襲（Meshy出力の既定フレームレートとして一貫使用、他計測との矛盾なし）。design/assets.md スケルタルアニメーション節にも同値を転記済み。
  3. **[ANM-01〜03・優先度低・対応済み]** 同梱メッシュ（`char1`、55,499 tri、hero予算50,000超過）が「Unity取込では`animationType=Humanoid`のAnimation-only importとして扱われ描画に使用されない・retarget専用」である旨を`revision:5`の`bundled_mesh_note`フィールドと`design/assets.md`スケルタルアニメーション節の注記の両方に明記した。
  4. **[非該当/継続]** MDL-02のquadruped auto-rig未完了・ANM-04未生成は本レビューの指摘対象外（disclosures参照、状況変化なし）。Checkpoint B（2026-07-13）で build フェーズの local:code-motion 代替が正式決定済み（`design/assets.md` ANM-04行・`state/stories.yaml` S-21）。

## AR-ASSET iteration 4 — APPROVE
- 日時: 2026-07-11T01:35:01Z
- 経緯: 本iterationは新規invocation（呼び出し元プロンプトの指示表記は「iteration 1」だったが、本ファイルに既にiteration 1〜3のレビュー履歴が存在するため、review-loops.md の追記原則（既存履歴を失わない・ledger連番の整合）に従い実際の連番であるiteration 4として記録する）。iteration 3（2026-07-10T16:45:00Z）でMAX_ITER到達によりAPPROVE済みのMDL-01・ANM-01〜03（revision:3）とMDL-02（revision:2、無変更）を対象に、on-disk資産が引き続き同一内容であることを独自の機械検査で再確認した。MANIFEST.jsonlには本レビュー後にrevision:4（MDL-01/02, ANM-01〜03）が追加されているが、いずれも`revision_reason`が「エンジン取込後検証（gates.md AR-ASSET ※節 — Integrate フェーズの責務）」と明記されファイル本体sha256は無変更（メタデータ追記のみ。Unity Avatar.isValid/PlayModeテスト等の取込後検証はIntegrate実施者の責務でAR-ASSET判定対象外）のため、本iterationの判定対象は revision:3（MDL-02のみrevision:2）のファイル本体のまま据え置く。
- 対象: `game/_generated/MANIFEST.jsonl` の該当エントリ = MDL-01（`model-hero.fbx`, revision:3, sha256 `d0a71f3a...`）、MDL-02（`model-swarmer.fbx`, revision:2, sha256 `8bcd0855...`）、ANM-01/02/03（`anim-hero-{attack,idle,run}.fbx`, revision:3, sha256 `25cb64db.../0ddf52c8.../550d8771...`）。ANM-04は`design/assets.md`上`must-replace`のまま未生成のため実ファイルなし（対象外・disclosures参照）。IMG-01〜03・SFX-01〜04は別artifactで判定済みのため対象外。
- 検査方法（MANIFEST自己申告・過去レビュー記述に非依存の独自再実行）: (1) `hashlib.sha256`でon-diskファイル実測とMANIFEST該当revisionのsha256を突合。(2) Blender 5.1.2 headless（新規スクリプト、`bpy.ops.import_scene.fbx`デフォルト設定）で5ファイルを個別に再インポートし、オブジェクト内訳・ポリ数（`polygon.vertices`からtriangle-fan近似で独自算出）・マテリアル数・ボーン数/名・ワールド空間bbox（`matrix_world @ v.co`で頂点変換後にmin/max算出）・Armature/Meshオブジェクトの`obj.scale`・Action数とframe_rangeを独自スクリプトで採取しJSON出力。(3) 同スクリプト内でBlender公式gltfエクスポータによりFBX→GLB変換し、`npx @gltf-transform/cli validate --format md`（version 4.4.1）を5ファイル全てで独自実行。(4) MDL-02のbase colorテクスチャをBlender経由でPNG抽出しPillowでHSV色相を独自再計測（不透明・saturation>0.15・value>0.05画素、256×256リサイズ後全画素サンプル）。(5) MDL-01のbase colorテクスチャを同様に抽出し、明度分布（HSV value）を独自集計してダークネイビーアウトラインの有無を定量確認。

### 観点別結果（独自再現）

**provenance（sha256照合）— PASS**
5ファイル全て（MDL-01, MDL-02, ANM-01, ANM-02, ANM-03）でon-disk実測sha256とMANIFEST記載revisionのsha256が完全一致。iteration 3以降のファイル改変・取り違えなし。

**gltf-transform validate — 5ファイル全てERROR 0（PASS、独自再現）**
Blender→GLB独自変換＋`npx @gltf-transform/cli validate --format md`で5ファイル全て「No errors found.」を確認。WARNINGはMDL-02のみ`MESH_PRIMITIVE_GENERATED_TANGENT_SPACE`（severity 1、非ブロッキング）、他4ファイル（MDL-01, ANM-01〜03）は同warningに加え`NODE_SKINNED_MESH_NON_ROOT`（severity 1）を検出（iteration 1〜3と同一内容・severityで新規劣化なし）。

**ポリゴン予算・ボーン・スケール — PASS（独自再現、iteration 3と数値一致）**
- MDL-01: bone_count=24、tri=47,174（≤50,000 hero budget）、オブジェクトは`Armature`/`char1`のみ（Icosphere非存在を再確認）、ワールドbbox高さ=1.79992m（レンジ1.6–2.0m内、目標1.8mとの差0.005%）、`obj.scale`はArmature/char1とも(1,1,1)。テクスチャ4枚（base/metallic/normal/roughness）全て2048×2048・`packed_file`非None。
- MDL-02: bone_count=0（未リグ、既存degraded_route/must_replaceと整合・新規劣化ではない）、tri=18,192（≤20,000 enemy budget）、ワールドbbox=(幅0.897m, 奥行1.461m, 高さ0.959m) — MANIFEST `bbox_authoring_m`と実測一致。テクスチャ4枚（`swarmer_tex_base_retouched`含む）全て2048×2048・packed。
- ANM-01/02/03: いずれもbone_count=24でMDL-01と一致、`obj.scale`は全て(1,1,1)（iteration 2で検出された0.01縮小回帰は非再発）、Action1本ずつ・frame_range=[1,85]/[1,121]/[1,20]（MANIFEST `clip_length_note`と一致）。同梱メッシュ`char1`はいずれもtri=55,499（decimate前の重量級コピー、描画非使用・retarget専用 — iteration 1/3から継続の記録改善未対応事項、下記参照）。テクスチャ4枚とも維持。

**MDL-02色相 — PASS（独自再実測、iteration 2/3の実測値を追認）**
`swarmer_tex_base_retouched.png`をBlender経由で抽出しPillowで独自HSV解析した結果、mean hue=289.387°・median=289.474°（サンプル65,536画素全件が閾値条件を満たし採用）。目標`enemy_primary_purple` #8B12A5（hue≈289.4°）との差は**0.01°**。iteration 2実測（289.37°）・iteration 3記載（289.41°）とも一致し、色相補正の恒久性を確認。

**MDL-01ダークネイビーアウトライン — CONCERNS継続（非ブロッキング、独自定量確認で追認）**
`hero_tex_base.png`をBlender経由で抽出し、262,144画素サンプルでHSV value分布を独自集計した結果、value<0.15（黒に近い暗色＝輪郭線候補）の画素は**0.00%**、value<0.25（濃紺〜暗色を広めに許容）でも**0.33%**のみで、value平均は0.627（明るいテクスチャ主体）。`style_block`が要求する「全シルエット縁の太い2-3pxダークネイビーアウトライン」がテクスチャ内に定量的にほぼ存在しないことを独自データで再確認した。iteration 1〜3の目視ベース指摘（「視認できない」）を数値で裏付ける形となった。3Dモデル単体の再生成では解消見込みが低い性質は変わらないため、引き続き非ブロッキングのCONCERNS/Integration申し送りとして扱う（下記disclosures参照）。

### 総合判定: APPROVE
理由: iteration 1〜3で検出・解消確認済みの全ブロッキング欠陥（テクスチャ参照破損、Icosphere混入、MDL-02色相ずれ、ANM系Armatureスケール0.01回帰、MDL-01実寸高さ不足）について、本iterationで独自に再実行した機械検査（sha256突合・Blenderヘッドレス再インポート・gltf-transform validate・HSV色相/明度解析）が全て一致する結果を再現し、新規の回帰・劣化は検出されなかった。gltf-transform validateは5ファイル全てエラー0、ポリゴン予算・ボーン数・スケール・テクスチャ埋め込みも全項目独自再現でPASS。唯一の非ブロッキング事項（MDL-01のダークネイビーアウトライン欠如）は本iterationで定量データにより改めて確認したが、iteration 1から一貫してIntegration側ポストプロセス対応が推奨される性質であり、3Dモデル自体の再生成対象ではないためdisclosuresとして開示するに留めAPPROVEとする。MDL-02のリグ未完了（must_replace）・ANM-04未生成も既存の開示済み事項であり、再生成指示（プロンプト修正）では解消不能（Meshy quadruped非対応・Tripoクレジット不足という経路上の制約）なためfailedAssetsではなくdisclosuresとして扱う。

### 再生成指示
なし（本iterationでブロッキング不合格資産・新規劣化なし）。

### 記録改善の推奨（再生成不要、iteration 1/3から継続）
1. **[ANM-01〜03]** 同梱メッシュ（55,499 tri、hero予算50,000超過）が描画用途では使用不可・retarget専用である旨をMANIFESTまたはdesign/assets.mdへ明記すること。
2. **[ANM-01〜03]** `duration_s`/`frame_count`/`fps`をMANIFESTに明記し、AUTO_ATTACK_INTERVAL用の再生速度スケール係数を導出しやすくすること。
3. **[design/assets.md]** MDL-01・ANM-01〜03の資産状態語彙（現状`generated`）をAR-ASSET APPROVE確定後の状態（`approved`）へ更新するかはart-director判断だが、iteration 3でAPPROVE済み・本iterationで再確認済みである旨を反映することを推奨（art-reviewerの編集権限外のため推奨のみ）。

### disclosures（再生成不要・人間開示のみ）
- **MDL-01**: 太いダークネイビーのシルエットアウトラインがテクスチャ上にほぼ存在しない（value<0.15画素0.00%、独自定量確認）。iteration 1から継続。3Dモデル単体の再生成では解決見込みが低く、Integration側でのURP Outlineポストプロセス適用を「任意」から「実質必須」に格上げする申し送りを継続する。
- **MDL-02 / ANM-04**: quadruped auto-rig未完了・`must_replace:true`（Meshy直API rigging=HTTP422 "Pose estimation failed"、Tripo直API=HTTP403クレジット不足。fallback chain両方失敗済み）。design/assets.mdの資産状態欄が`must-replace`と明記された既知の設計許容状態。再生成プロンプト修正では解消不能（プロバイダ側の技術的非対応・アカウント側のクレジット不足という経路上の制約のため）。Integration/build時のTripoクレジット補充後の再試行、またはlocal:code-motion（コード側代替実装）での決着が必要。
- **MDL-01, MDL-02, ANM-01〜03**: `plan_tier:"pro+"`は`state/asset-routing.json`のnotesで「間接証明（balanceレスポンスにtierフィールド無し）」と明記された未検証値。
- **MDL-01, MDL-02（revision:1相当分）**: 初回生成コスト（`cost_usd:0.4`/`0.3`等）は`cost_estimated:true`（Meshyクレジット→USD換算が`state/asset-routing.json`記載の$0.02/credit保守見積であり、プロバイダ確定請求額ではない）。revision:2以降の追加コストは`cost_usd:0`/`cost_estimated:false`（再ダウンロード＋ローカルBlender処理のみ、Meshy balance変化なしの自己申告に基づく。第三者照会は未実施）。
- 3D資産のルーティング実績は`meshy:direct-*`（Meshy直API）+`local:blender-*`であり、fal.ai経由ではない。gates.md AR-ASSET観点6の「fal経由Meshyのライセンス継承未検証」は本バッチには該当しない（確認済み・非該当）。
- revision:4（MDL-01/02, ANM-01〜03）はIntegrate実施者によるエンジン取込後検証の記録（Unity Avatar.isValid=true, PlayModeテスト3/3 pass, RenderTexture証跡等）であり、gates.md AR-ASSET ※節により本ゲートの判定対象外（参考情報として確認したのみ）。MDL-01revision:4はUnity取込時に別のスケール解釈差（authoring 1.8m→Unity実測2.0729m）が新規検出されModelImporter.globalScaleで是正済みである旨が記録されているが、これはUnity側インポート設定の問題でありAR-ASSET（authoring-time計測）の判定を変更するものではない。

- 対応（art-director、2026-07-13）: 本iterationの再生成指示は無し。「記録改善の推奨」3件は iteration 3 の対応欄（本ファイル該当iteration参照）で `game/_generated/MANIFEST.jsonl` ANM-01/02/03 `revision:5`（duration_s/frame_count/fps、bundled_mesh_note、fcurve記述訂正）として一括対応済み。推奨3.「MDL-01・ANM-01〜03の資産状態語彙を`approved`へ更新」も対応済み（`design/assets.md` 3Dモデル節・スケルタルアニメーション節の該当行を`generated`→`approved（AR-ASSET iteration 3/4 APPROVE確定）`へ更新）。disclosures（MDL-01アウトライン欠如のIntegration申し送り、MDL-02/ANM-04のmust_replace、plan_tier未検証、Meshyクレジット→USD見積の未検証性）はいずれも3Dモデル単体の再生成では解消しない性質のため対応不要と判断し見送り、開示事項として次のCheckpoint（C）へそのまま引き継ぐ。design/assets.md のうち game/_generated/MANIFEST.jsonl に未記載の3D資産（models）は本セッション時点で無し（MDL-03は設計上生成なし・ANM-04はroute=localでAssetGen対象外に確定済み）ため、本バッチでの新規3D生成は実施しなかった。

## art-director 対応記録 — style drift 再検証（2026-07-13、Phase3 build Integrate 由来）

- 日時: 2026-07-13T09:30:00Z（呼び出し元: full-build.js ワークフロー、art-director revise 指示）
- 経緯: Phase3 Integrate 実施後のUnity実機screenshot（`qa/evidence/asset-integration-hero-visual.png`, 2026-07-13T09:03生成）とレンダリングプレビュー（`game/_generated/previews/preview-hero.png` / `preview-swarmer.png`）のいずれも `design/art-bible.json` `style_block` が要求する「flat cel-shaded color fill with clean bold 2-3px dark-navy outlines」を満たしていないという指摘（failedAssets: `model-hero.fbx`＝MDL-01, `model-swarmer.fbx`＝MDL-02）を受理した。本ファイルの iteration 1〜4（2026-07-10〜11）で既に同一事象（MDL-01のダークネイビーアウトライン欠如）が4回申し送られ、いずれも「3Dモデル単体の再生成では解消しない」非ブロッキングCONCERNS/disclosureとして総合APPROVE確定済みであり、Phase3 Integrate後も未解消のまま持ち越されたことを確認した（新規劣化ではなく既知事項の継続）。MDL-02（swarmer）についても色相自体は解消済み（iteration 2/3/4でPASS、目標#8B12A5との差0.01–0.03°）だが、輪郭線欠如はMDL-01と同根の未解決事項として本指摘で改めて明示された。

- 検討: 呼び出し元から提示された retryInstruction は (1) Integration側での軽量アウトラインシェーダ（インバートハル法バックフェース法線押し出し、またはURP法線/深度エッジ検出ポストプロセス）追加を主対応とし `design/art-bible.md` の「任意検討」を「実質必須」へ格上げすること、(2) 将来のIMG-01系コンセプト画再生成機会があれば輪郭線焼き込みの明示指示とalbedo value<0.2画素比率の検証ステップを追加すること、の2点を推奨し、3Dモデル自体のMeshy再呼び出しは「これまで3回試みても輪郭線がテクスチャに転写されず解消しなかったため非推奨」と明記していた。

### 対応（対応/見送りを個別に明記）

1. **[MDL-01/MDL-02 の Meshy 再生成（3Dモデル本体の作り直し）] 見送り。** 理由: iteration 1〜4で独自機械検査（HSV value分布実測: MDL-01 albedo の value<0.15画素比率0.00%、iteration 4）により、輪郭線がalbedoテクスチャへ転写されないことが定量的かつ再現的に確認済みであり（image-to-3Dパイプラインが2Dコンセプト画の視点依存シルエット線を3D表面テクスチャへ正しく転写できないという既知の限界）、retryInstruction 自体が追加のMeshy呼び出しを明示的に非推奨としている。同一アプローチの5回目の試行は期待値が低く、`state/asset-routing.json` のMeshyクレジット（balance 3100、$0.02/credit保守見積）を消費する正当な根拠に乏しいと判断した。よって本タスクでは Meshy/fal 等への新規API呼び出しを一切行っていない（`state/budget.txt` $100上限・MANIFEST.jsonl合算に変更なし、追加コスト$0）。
2. **[`design/art-bible.md`「3D スタイル方針」節の格上げ] 対応済み。** URP Outlineポストプロセス（インバートハル法バックフェース法線押し出し、またはURP法線/深度エッジ検出）の適用を「実装余力があれば任意検討」から「実質必須（3Dスタイル準拠を実現する唯一の手段）」へ明文で格上げし、色をダークネイビー系パレット色 `#12081F` を基準に2-3px相当幅で固定する指示を追記した。4回のレビューで独自定量確認された「albedo焼き込みでは解消しない」という技術的根拠（value<0.15画素比率0.00%）も明記した。併せて、実装は Integration 工程（gameplay-engineer/ui-engineer 担当、CR-CODE 対象のUnityコード変更）側で行う旨を明記し、art-director の生成物（FBX/テクスチャ）だけでは満たせない要件であることを明示した。
3. **[Integration側シェーダー実装そのもの] 見送り（art-director の役割外としてエスカレーション）。** art-director は画像/3D資産の生成・検証（curl直叩き・Blender headless検証・MANIFEST記録）を担当し、Unity C#/シェーダーコードの実装は担当しない（contract.md §2 Delegation Map — gameplay-engineer/ui-engineer の領分。Must NOT Do: 音声同様に他agentの領分への越権はしない）。art-bible.md の格上げにより次の build story（Integrate/QA-PLAY 実施者）が「実質必須」要件として実装に着手できる状態にした上で、未解決事項として呼び出し元（full-build.js）へ構造化報告する。
4. **[将来のIMG再生成時の輪郭線焼き込み検証ステップ追加] 対応（プロトコル明記のみ・本タスクでは新規IMG生成なし）。** `design/assets.md` には現時点でIMG-01/IMG-02の再生成計画は無く（既にgenerated/approved済み、brief.mdの画像上限に対し過不足なし）、本タスクのfailedAssetsもIMGではなくMDL（model-hero.fbx/model-swarmer.fbx）のみを対象としているため、今回IMG再生成は実施しない。将来同種のimage-to-3D入力コンセプト画を再生成する必要が生じた場合に備え、`design/art-bible.md` 3Dスタイル方針節に「輪郭線をalbedoへ高コントラストな黒縁として焼き込む明示指示」と「変換後のvalue<0.2画素比率計測による検証ステップ」を申し送り事項として追記した。

- 予算・生成実績: 本タスクでは新規API呼び出し（Meshy/fal/Ideogram等）を一切行っていない。`game/_generated/MANIFEST.jsonl` への新規追記なし（既存MDL-01/MDL-02の revision:4/3 から変更なし）。`state/budget.txt`（$100上限）に対する追加消費$0。
- 未解決事項（Checkpoint Cへ継続開示）: MDL-01/MDL-02 の太いダークネイビーアウトライン欠如は、3Dモデル本体としては本タスクでは解決されないまま（art-bible.md方針格上げのみ実施、実装はIntegration工程待ち）。MDL-02のquadruped未リグ（must-replace、iteration 1〜4から継続）は本指摘とは独立した既存の別課題として引き続き開示を継続する。
