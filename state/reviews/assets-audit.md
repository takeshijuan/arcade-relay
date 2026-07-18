# Assets Audit（qa-lead）

対象: `game/_generated/MANIFEST.jsonl`（engine=unity）/ `state/budget.txt` / `.claude/docs/assets-config.md`

## Assets Audit iteration 1 — CONCERNS
- 日時: 2026-07-13T21:00:00Z
- 監査範囲: MANIFEST 全43エントリ（IMG-01〜04 / SFX-01〜06 / BGM-01 / MDL-01,02 / ANM-01〜03、各リビジョン含む）

### 1) コスト合算 vs 予算

- `state/budget.txt` = **$100**
- MANIFEST `cost_usd` 全エントリ合算（リビジョン含む実測合算・実際にAPI課金が発生した全行を加算） = **$2.03044**（design/assets.md 記載の「約$2.03」と一致）
- 内訳: 画像(IMG-01〜04, 全リビジョン合算) $0.60 / SFX(01〜06) $0.2762 / BGM-01 $0.335（不採用試行1回分含む） / MDL-01 $0.40 / MDL-02 $0.30 / ANM-01〜03 $0.12
- **overBudget = false**（$100 に対し2%未満の消費、余裕大）

### 2) ライセンスフラグ抽出（Checkpoint 開示対象）

| フラグ | 該当資産 | 状態 |
|---|---|---|
| ElevenLabs「Studio Games」条項（商用×マルチプラットフォーム出荷はEnterprise相談要） | SFX-01〜06, BGM-01（全7エントリの `license_note` に明記済み） | 開示済み・継続フラグ |
| Ideogram: アプリ内AI生成表記条項 | IMG-01〜04（`fal:ideogram-v3-transparent` = Ideogram v3 バックエンド使用） | **未開示（gap）** — MANIFEST 各エントリに ElevenLabs 同様の `license_note` が無い。Checkpoint 提示前に art-director へ追記依頼を推奨 |
| 米国AI出力の著作権不確定 → 人間関与記録が防御材料 | 全資産 | MANIFEST の revision 履歴に人間/エージェントの選別理由・retouch内容が詳細に記録されており（IMG-01/03 HSV補正、SFX 4変種選定理由、MDL scale補正等）、防御材料として十分機能している（良好） |
| （3D）Meshy plan_tier 確認 | MDL-01, MDL-02, ANM-01〜03 | 全エントリ `plan_tier:"pro+"` 記録済み。`state/asset-routing.json` preflight実測（balance 3100〜3032 credits）とも整合。Free プラン（CC BY 4.0/商用不可）不使用を確認 — **問題なし** |
| （3D）Tripo Free プラン | MDL-02 リグのフォールバック先として試行 | HTTP403（クレジット不足）で**未使用に終わった**（成果物に混入なし）。plan_tier は preflight で "unknown" のままだが未使用のため出荷リスクなし |
| （3D）Hunyuan3D Territory除外（EU/UK/韓国） | 該当なし | 本バッチは Meshy のみ使用、Hunyuan3D不使用 — N/A |
| gpt-image-2 / rembg bria-rmbg / MusicGen・audiocraft / placeholder-nc | 該当なし | 全数不使用・不検出（画像は fal ideogram ネイティブ透過、音楽は elevenlabs:music-v2） — **問題なし** |

### 3) アルファ全数検証（画像4点・MANIFEST記載の全画像）

Pillow による全画素スキャン（ImageMagick `identify -format mean` でのブランク検知も併用）:

| ファイル | mode | size | 4隅alpha | 完全不透明% | 完全透過% | 白不透明(r,g,b>240)% | 判定 |
|---|---|---|---|---|---|---|---|
| img-01-hero-concept.png | RGBA | 1024x1024 | [0,0,0,0] | 35.59% | 61.79% | 0.02% | PASS |
| img-02-swarmer-concept.png | RGBA | 1024x1024 | [0,0,0,0] | 34.66% | 63.22% | 0.00% | PASS |
| img-03-crystal-icon.png | RGBA | 512x512 | [0,0,0,0] | 43.10% | 55.82% | 0.00% | PASS |
| img-04-hit-vfx.png | RGBA | 512x512 | [0,0,0,0] | 30.24% | 64.44% | 3.80%（ヒットVFXの白コア部分。背景ではなく意図された発光表現） | PASS |

**白背景PNG（`ハード禁止事項`）0件を確認。** 全4点 RGBA・4隅完全透過・ImageMagick mean値も0.22〜0.28で黒/白ブランク（<0.02 / >0.98）に該当せず、実コンテンツを保持している。

### 3b) 3D資産 MANIFEST 必須フィールド・provenanceGaps

| 種別 | 該当 | 詳細 |
|---|---|---|
| 必須フィールド記録漏れ（`bbox_authoring_m`） | ANM-01, ANM-02, ANM-03（**全リビジョン, revision 1〜5共通**） | アニメクリップは MDL-01 のメッシュを共用するため独自ジオメトリを持たないが、`bbox_authoring_m` が一度も記録されていない。MDL-01 のbboxへの参照注記もない。軽微だが自己完結性の観点で記録漏れ |
| 必須フィールド記録漏れ（`plan_tier`/`bbox_authoring_m`/`license`） | MDL-01 revision4, MDL-02 revision3, ANM-01/02/03 revision4（Integrateフェーズの追記revision） | 「ファイル本体無変更・sha256同一」と本文に記載されるのみで、`revision_of_sha256` フィールドも無く、該当行単体では旧revisionへの機械的な紐付けができない。人間可読の記述はあるが機械監査上は前revision参照が必要 |
| 必須フィールド記録漏れ（`bbox_authoring_m`/`validator`） | ANM-01/02/03 revision5（記録改善revision） | 同上の理由で該当行単体では自己完結しない |
| `shippable:false` 由来 | 該当なし（MANIFEST中に `shippable` フィールド自体が0件） | N/A |
| `cost_estimated:true` | MDL-01 rev1($0.4), MDL-02 rev1($0.3), ANM-01 rev1($0.06), ANM-02 rev1($0.06), ANM-03 rev1($0) | いずれも「Meshyクレジット数 × $0.02/credit 保守見積」という `cost_basis` の根拠記載あり。プロバイダ請求の実測確定値ではなく見積りである点は開示継続が必要 |
| must_replace 継続 | MDL-02（rig未完了、quadruped auto-rig非対応。Meshy 422 / Tripo 403） | `design/assets.md` に Checkpoint B 追認済み（2026-07-13、S-21でコードモーション代替に確定）で状態語彙 `must-replace` を正しく使用。ANM-04（未生成、code-motion代替）も同様に開示済み。**Checkpoint C でも再開示が必要** |

### 総合所見

- 予算超過なし、ハード禁止ライセンス（gpt-image-2/rembg bria-rmbg/MusicGen等）該当0件、白背景PNG0件、Meshy Free プラン不使用確認済み — 重大な出荷ブロッカーは無い
- CONCERNS 理由: (1) Ideogram AI生成表記条項が画像4点のMANIFEST上で未開示、(2) ANM-01〜03 の `bbox_authoring_m` 恒常的記録漏れ、(3) Integrate/記録改善系の追記revisionが `plan_tier`/`license`/`bbox_authoring_m`/`validator`を自己完結的に持たず前revision参照に依存、(4) MDL-02/ANM-04 の must-replace が Checkpoint C 提示まで解消見込みなし（既知・開示済みだが未解消のまま）
- 対応: （未定。art-director / creative-director への申し送り事項。本監査は qa-lead の独立監査であり `design/assets.md`・MANIFEST への直接修正は行っていない）

## Assets Audit iteration 2 — CONCERNS
- 日時: 2026-07-15T00:00:00Z
- 監査範囲: `game/_generated/MANIFEST.jsonl` 全46行（S-21〜S-33 の Build フェーズ追加分を含む最新スナップショット。iteration 1（2026-07-13、43行時点）以降に IMG-04, BGM-01, SFX-05/06, MDL-02 revision4, IMG-05（+revision2）, IMG-06 の8行が追加）。ファイル実体（`game/_generated/images|audio|models|anims/` 全12ファイル）を独立に sha256 再計算し、対応する MANIFEST 最新revisionの `sha256` フィールドと突合（画像6/音声7/モデル2/アニメ3=計18ファイルのうち、`sha256` フィールドが存在する15件は全て一致。MDL-01 revision4・MDL-02 revision3/4 の3行は後述の通り `sha256` フィールド自体が欠落——独立検証では「ファイル本体は直前revisionのsha256と一致し無変更」と確認）。

### 1) コスト合算 vs 予算

- `state/budget.txt` = **$100**
- MANIFEST `cost_usd` 全46行合算（実際にAPI課金が発生した全行を加算。0円の追記revision/メタデータ訂正revisionはそのまま0として合算） = **$2.95044**
- 内訳（iteration1の$2.03044から+$0.92の増分）: 画像 IMG-01〜04・全リビジョン $1.02 / IMG-05（初回$0.42+revision2 $0.24）$0.66 / IMG-06 $0.26 / SFX-01〜04 $0.2762 / SFX-05 $0.1846 / SFX-06 $0.0769 / BGM-01 $0.335 / MDL-01 $0.40 / MDL-02 $0.30 / ANM-01〜03 $0.12
- **overBudget = false**（$100 に対し約3%の消費。余裕大）
- 独立検算（python3 `json.loads`全行の `cost_usd` を合算）: `2.9504400000000004` ≈ $2.95044、design/assets.md「集計と予算」節の見積もり($1.02音声+約$2.00画像+3D)とも整合レンジ内

### 2) ライセンスフラグ抽出（Checkpoint 開示対象）

| フラグ | 該当資産 | 状態 |
|---|---|---|
| ElevenLabs「Studio Games」条項（商用×マルチプラットフォーム出荷はEnterprise相談要） | SFX-01〜06, BGM-01（全13エントリ、SFX-05/06の新規2件含め `license_note` に明記済み） | 開示済み・継続フラグ |
| Ideogram: アプリ内AI生成表記条項 | IMG-01, IMG-02, IMG-03, IMG-04, IMG-05（全リビジョン含む。`fal:ideogram-v3-transparent` = Ideogram v3 バックエンド使用） | **未解消（iteration1から継続するgap）** — 新規追加された IMG-04・IMG-05（revision2含む）にも ElevenLabs 同様の `license_note` が付与されていない。IMG-01 revision3 のメタデータ内で「fal-ai/ideogram/v3/generate-transparent の実APIパラメータには style_type/style_codes が存在しない」ことが判明済みだが、これは Ideogram バックエンド自体の使用有無とは無関係で開示義務は変わらない。Checkpoint C 提示前に art-director へ追記依頼を推奨 |
| （新規）fal `flux-2-pro`（画像背景生成） | IMG-06 | assets-config.md にflux-2-pro固有のCheckpoint開示義務の記載なし。Ideogramバックエンドではないため上記フラグは非該当（N/A）。cost_basis はfal公表の確定レート未記載のため保守見積り（`cost_estimated:true`）である旨は開示継続 |
| 米国AI出力の著作権不確定 → 人間関与記録が防御材料 | 全資産 | 新規資産（IMG-04〜06, BGM-01, SFX-05/06, MDL-02 revision3/4）についても選定理由・retouch内容・Integrate時の是正内容がMANIFESTに詳細記録されており、防御材料として引き続き十分機能している（良好・iteration1から劣化なし） |
| （3D）Meshy plan_tier 確認 | MDL-01, MDL-02（全revision）, ANM-01〜03（revision1〜3, 5） | 記録がある行は全て `plan_tier:"pro+"`。Free プラン（CC BY 4.0/商用不可）不使用を確認 — 問題なし。ただし MDL-01 revision4・MDL-02 revision3/4・ANM-01〜03 revision4 は `plan_tier` フィールド自体が欠落（下記3b参照） |
| （3D）Tripo Free プラン | MDL-02 リグのフォールバック先として試行 | iteration1から状況変化なし。HTTP403（クレジット不足）で未使用のまま（成果物に混入なし） |
| （3D）Hunyuan3D Territory除外（EU/UK/韓国） | 該当なし | 本バッチも Meshy のみ使用、Hunyuan3D不使用 — N/A |
| gpt-image-2 / rembg bria-rmbg / MusicGen・audiocraft / placeholder-nc | 該当なし | 全46行走査で該当0件（`license` フィールドは値がある全行 `commercial-ok`、`placeholder-nc` 文字列一致0件） — 問題なし |

### 3) アルファ全数検証（画像6点・MANIFEST記載の全画像、独立再計測）

`game/_generated/images/` 配下の実ファイル6点全てに対し PIL+numpy でフルスキャン（サンプリングなし）。各ファイルの実sha256は対応する asset_id の最新revision（IMG-01 rev3 / IMG-02 rev2 / IMG-03 rev2 / IMG-04 rev1 / IMG-05 rev2 / IMG-06 rev1）の MANIFEST `sha256` と完全一致を確認済み。

| ファイル | mode | size | 4隅alpha | 不透明% | 透過% | 半透明(AA縁)% | 白不透明(r,g,b>240)% ※全画素比 | 判定 |
|---|---|---|---|---|---|---|---|---|
| img-01-hero-concept.png | RGBA | 1024x1024 | [0,0,0,0] | 35.59% | 61.79% | 2.62% | 0.02% | PASS |
| img-02-swarmer-concept.png | RGBA | 1024x1024 | [0,0,0,0] | 34.66% | 63.22% | 2.12% | 0.00% | PASS |
| img-03-crystal-icon.png | RGBA | 512x512 | [0,0,0,0] | 43.10% | 55.82% | 1.08% | 0.00% | PASS |
| img-04-hit-vfx.png | RGBA | 512x512 | [0,0,0,0] | 30.24% | 64.44% | 5.31% | 3.80%（ヒットVFXの白コア。意図された発光表現、iteration1と同値） | PASS |
| img-05-ui-frame-kit.png | RGBA | 1024x1024 | [0,0,0,0] | 24.08% | 73.61% | 2.30% | 0.40%（タブ/リボンの白トリムハイライト。MANIFEST自己申告0.13%は閾値r,g,b>245使用のため定義差、白背景残存ではない） | PASS |
| img-06-arena-backdrop.png | **RGB（アルファチャンネル無し）** | 2048x2048 | n/a | n/a | n/a | n/a | 白(r,g,b>240)混入 0.00% | PASS（下記注記） |

**白背景PNG（`ハード禁止事項`）0件を確認。** IMG-01〜05 は全数 RGBA・4隅完全透過・白背景残存なし（iteration1のPASS判定を維持、新規追加のIMG-04/05含め健全）。IMG-06 はアルファチャンネルを持たない RGB 画像だが、`design/assets.md` IMG-06 行・MANIFEST `alpha_verified:"n/a (opaque background art, no transparency required per design/assets.md IMG-06 spec)"` の通り Unity Skybox/背景バックドロップ用の意図的な不透明アートであり（`kind:"concept_art"`、スプライト/UI画像ではない）、`assets-config.md` ハード禁止事項の「白背景PNGの出荷禁止」は文言上「スプライトは全数アルファチャンネル機械検証」と対象をスプライトに限定しているため本資産は対象外と判断。実測でも白(r,g,b>240)画素0.00%・4象限とも滑らかな紫→シアン→マゼンタのグラデーションのみで白背景の混入自体は無い。**軽微な指摘として記録**: `.claude/rules/assets.md` の「全画像はアルファチャンネル必須」という一般文言とは字面上矛盾するため、IMG-06 のようなopaque背景アートを例外とする旨をassets-config.mdまたはrules/assets.mdに明記することを推奨（AR-ASSET/art-directorへの申し送り、ブロッカーではない）。

### 3b) 3D資産 MANIFEST 必須フィールド・provenanceGaps

| 種別 | 該当 | 詳細 |
|---|---|---|
| 必須フィールド記録漏れ（`bbox_authoring_m`） | ANM-01, ANM-02, ANM-03（**全リビジョン、revision 1〜5、iteration1から継続・最新revision5でも未解消**） | MDL-01のメッシュを共用し独自ジオメトリを持たないためbbox実測が無いのは理解できるが、MDL-01のbboxへの参照注記すら無いまま5世代を経ても記録されていない。軽微だが自己完結性の観点で未解消のまま |
| 必須フィールド記録漏れ（`plan_tier`/`bbox_authoring_m`/`license`/`sha256`） | MDL-01 revision4、MDL-02 revision3・revision4、ANM-01/02/03 revision4（Integrateフェーズの追記revision、計7行） | 「ファイル本体無変更・sha256同一」と本文に記載されるのみで、`sha256` フィールド自体が行に存在しない（iteration1指摘時点では`revision_of_sha256`フィールドの欠如のみ指摘していたが、今回の独立監査で`sha256`フィールド自体も欠落していることを追加確認）。当該行単体では機械的な sha256 突合ができず、file整合性は「前revisionのsha256と一致」という人間可読の記述および今回実施した独立ファイルハッシュ突合でのみ担保されている。iteration1指摘から3行（MDL-02 revision4含む）増加 |
| `shippable:false` 由来 | 該当なし（MANIFEST中に `shippable` フィールド自体が0件、iteration1から変化なし） | N/A |
| `cost_estimated:true`（3D資産） | MDL-01(初回, $0.40), MDL-02(初回, $0.30), ANM-01(初回, $0.06), ANM-02(初回, $0.06), ANM-03(初回, $0) | iteration1から変化なし。いずれも「Meshyクレジット数 × $0.02/credit 保守見積」が根拠。プロバイダ請求の実測確定値ではない点は開示継続が必要 |
| must_replace 継続 | MDL-02（rig未完了、quadruped auto-rig非対応。Meshy 422 / Tripo 403。revision4まで継続） | `design/assets.md` に Checkpoint B 追認済み・S-21でコードモーション代替に確定、状態語彙 `must-replace` を正しく使用。revision4（S-21統合構造変更）でもmust_replace:trueを維持し `degradation_still_open` で継続開示。ANM-04（未生成、code-motion代替、状態`must-replace`）も同様に開示済み。**Checkpoint C でも再開示が必要（未解消のまま出荷段階に到達）** |

### 総合所見

- 予算超過なし（$2.95044 / $100、約3%消費）、ハード禁止ライセンス該当0件、白背景PNG0件（スプライト5点全数PASS）、Meshy Free プラン不使用確認済み — 重大な出荷ブロッカーは無い。iteration1からの新規追加8行（IMG-04〜06・BGM-01・SFX-05/06・MDL-02 revision4）も同水準の健全性を確認
- CONCERNS 理由（iteration1からの継続・悪化分）: (1) Ideogram AI生成表記条項が画像5点（IMG-01〜05、新規のIMG-04/05含む）でMANIFEST上**引き続き未開示**、(2) ANM-01〜03 の `bbox_authoring_m` が最新revision5でも**引き続き記録漏れ**、(3) Integrate/記録改善系の追記revisionが `plan_tier`/`license`/`bbox_authoring_m`/`sha256` を自己完結的に持たない行が7行に**増加**（iteration1指摘時4行から+3行）、(4) MDL-02/ANM-04 の must-replace が**引き続き未解消**のまま Build フェーズ終盤（S-21revision4）に到達 — Checkpoint C で必ず再開示すること。新規の軽微指摘: (5) IMG-06（アルファ無しRGB背景アート）が rules/assets.md の一般文言「全画像はアルファチャンネル必須」と字面上矛盾するため例外規定の明記を推奨（ブロッカーではない）
- 対応: （未定。art-director / creative-director への申し送り事項。本監査は qa-lead の独立監査であり `design/assets.md`・MANIFEST への直接修正は行っていない）
