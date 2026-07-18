# レビュー履歴 — assets-audio（SFX-05/06, BGM-01, engine=unity）

## AR-ASSET iteration 1 — APPROVE
- 日時: 2026-07-13T10:20:00Z
- 対象: `game/_generated/MANIFEST.jsonl` 追記分 `BGM-01`（`bgm-01-main-loop.ogg`）/ `SFX-05`（`sfx-05-wave-start.ogg`）/ `SFX-06`（`sfx-06-upgrade-purchase.ogg`）。SFX-01〜04 は `state/reviews/assets-audio-prototype.md` iteration 3/4 で既に APPROVE 確定済みのため本バッチ対象外。
- 照合元: `design/assets.md`（SFX/BGM節・サイズ列・BPM/キー・ループ要件）、`design/art-bible.json`（本バッチは音声のため palette/style_block 非該当。scale/resolution も非該当）、`.claude/docs/tech-stack-unity.md`「資産の取り扱い」（音声=OGGのみ）、`state/asset-routing.json`（routes.sfx/routes.bgm/shippable）、`state/engine.txt`=`unity`。
- 検査方法（全て実ファイルへ直接実行。MANIFEST自己申告値の転記は行わず独立再測定）:
  - `shasum -a 256`（実ファイル vs MANIFEST `sha256` 突合）
  - `ffprobe -show_entries format=duration,format_name -show_entries stream=codec_name,sample_rate,channels`
  - `ffmpeg -af loudnorm=I=-16:TP=-1.0:LRA=11:print_format=json -f null -`（Integrated Loudness / True Peak実測）
  - `ffmpeg -af astats`（Peak/RMS level, Number of NaNs/Infs/denormals, Max/Min level でクリッピング検査）
  - BGM-01のみ: 実ファイルを2連結（`concat` filter）した120秒素材を作り、継ぎ目（t=60.0s）前後500msのRMS段差、継ぎ目前後40ms窓の最大サンプル差分を、曲中の他の高エネルギー区間（t=30.0s）・静音寄り区間（t=10.0s）と比較してクリック検出

### 検査結果

**provenance/sha256整合 — PASS**
3件全て実ファイルのsha256がMANIFEST記載値と完全一致（改ざん・取り違えなし）:
`BGM-01`=`57635389...` / `SFX-05`=`1802dea8...` / `SFX-06`=`b9643092...`。

**仕様一致（フォーマット・観点3）— PASS**
3件とも `ogg`（vorbis, 44.1kHz, stereo）。`tech-stack-unity.md`「音声: Unity は Ogg Vorbis / WAV をネイティブ対応。OGGのみで良い」に一致（M4A非同梱も正しい＝phaser専用要件を混入させていない）。`state/asset-routing.json` の `routes.sfx=elevenlabs:sfx-v2` / `routes.bgm=elevenlabs:music-v2` / `shippable.sfx:true` / `shippable.bgm:true` と一致し、`shippable:false` ルート由来の資産なし。

**duration実測（観点3）**

| id | design/assets.md 指定 | duration_requested_s (MANIFEST) | 独立再測定 duration | 判定 |
|---|---|---|---|---|
| BGM-01 | 60s（ループ、brief許容60〜90s） | 60 | **60.000000s** | 完全一致・PASS |
| SFX-05 | 1.2s | 1.2 | **1.200045s** | 実質完全一致・PASS |
| SFX-06 | 0.5s | 0.5 | **0.241497s** | **-51.7%短縮。指定値と不一致** |

SFX-06 は無音トリム後の最終尺が指定の半分以下（0.241497s / 0.5s）。ただし `design/assets.md` の SFX-06 説明には SFX-01（`AUTO_ATTACK_INTERVAL`）・SFX-02（`DASH_DURATION`）のような上限を規定するゲーム側同期定数の明記がなく、単発のMenu購入確定音として機能面の破綻はない。かつこの逸脱幅（-51.7%）は同一バッチ手法（ElevenLabs SFX v2 + 無音トリム）で既に `state/reviews/assets-audio-prototype.md` iteration 1〜4 が非ブロッキングと判定した `SFX-02`（0.6s→0.292s、-51.3%）と同水準であり、新規のリスクパターンではない。**REJECT/再生成対象とはしないが、`design/assets.md` にはこの逸脱が明文化されていない**（BGM-01の`loop_edit_method`/`force_instrumental_deviation`は同ファイルに開示済みだがSFX-06のduration逸脱は未記載）ため disclosures に記録する。

**ラウドネス実測（観点1）**

| id | 独立再測定 input_i / input_tp | MANIFEST申告 | 再現性 | -16±1(-17〜-15)判定 |
|---|---|---|---|---|
| BGM-01 | -16.03 LUFS / -4.51 dBTP | -16.03 / -4.51 | 完全一致 | 適合 |
| SFX-05 | -16.13 LUFS / -0.80 dBTP | -16.13 / -0.80 | 完全一致 | 適合 |
| SFX-06 | `-inf`（測定不能）/ -1.18 dBTP | null（測定不能と申告） / -1.18 | 完全一致（測定不能の再現含む） | 測定不能＝duration 0.241497s が EBU BS.1770 最小ゲーティングブロック400ms未満のため物理的に不能。`assets-audio-prototype.md`（SFX-02/04）で既に確立した非ブロッキング事項と同一パターン。代替指標のRMS実測 -17.01dB（astats独立測定）はMANIFEST申告のRMS-17.00dBと一致し、SFX-02(-17.26dB)/SFX-04(-16.72dB)とバッチ内一貫した水準 |

クリッピング: 3件とも `astats` Overall の Max level < 1.0（BGM: 0.550 / SFX-05: 0.911 / SFX-06: 0.832）、Number of NaNs/Infs/denormals は全件0。

**ループ品質（観点2・BGM-01のみ）— PASS（独立再現）**
自己2連結した120秒素材の継ぎ目（t=60.0s）を独立検査:
- 継ぎ目前500ms窓RMS -19.23dB、継ぎ目後500ms窓RMS -20.24dB（差分約1.01dB。MANIFEST申告の`seam_rms_delta_db: 1.12`と近似・独立再現）
- 継ぎ目±40ms窓の最大サンプル差分 6250〜7029 → 曲中の高エネルギー区間（t=30.0s、同40ms窓）の最大差分6803〜7479と同水準（継ぎ目が突出していない）。静音寄り区間（t=10.0s）の最大差分381〜392と比べ明らかに"通常の楽曲内変動"レンジに収まり、異常なクリックは検出せず
- ピークレベルも継ぎ目付近-8.7〜-8.9dBFSでクリッピング兆候なし

MANIFESTの`loop_verification`（`click_detected:false`、`verdict:PASS`）を独立手法で追試し、同じ結論（クリック/RMS段差異常なし）を確認した。

**音要件突合（観点4）**
- BGM-01: `bpm:128`・`key:"A minor"` は design/assets.md「BPM/キー」列と一致。ジャンル記述（driving electro, four-on-the-floor, synth bass, arpeggiated lead, instrumental）もbrief.md「音の方向」と一致。ただし BPM/キー/ボーカル有無の**聴感上の一致確認は本レビューのツール群（ffmpeg/ffprobe）では検証不能**（tempo/key検出は範囲外）。composition_planのnegative_global_styles（vocals除外）とMANIFESTの開示記録に基づく判断であり、独立した音響解析による裏付けではない点を明記する。
- SFX-05（ウェーブ開始）: プロンプト内容「低域パルス+短いライザー」が design/assets.md のトリガー説明と1対1で一致。P-03参照も一致。
- SFX-06（購入確定）: プロンプト内容「短い2音の上昇コンボ」が design/assets.md のトリガー説明と1対1で一致。P-04参照も一致。

**license/provenance — PASS**
3件とも `license:"commercial-ok"` / `plan_tier:"starter"`。`state/asset-routing.json` の `checks.elevenlabs`（`tier:"starter"`, `commercial_ok:true`）と整合し、ElevenLabs Freeプランでの出荷（ハード禁止事項）に該当しない。`must_replace` 該当なし。MANIFEST必須フィールド（file/provider/model/prompt/seed or seed_note/cost_usd/sha256/license/generated_at）は3件とも充足。

### 総合判定: APPROVE
理由: sha256整合・フォーマット（unity既定=OGGのみ）・ラウドネス（-16±1 LUFS、測定不能ケースは既知の物理的制約として非ブロッキング）・BGM-01ループ品質（独立2連結テストでクリック/RMS段差なしを再現確認）・license/provenanceの全観点で3資産とも問題を検出しなかった。SFX-06のduration逸脱（-51.7%）は同一バッチ手法で既に非ブロッキング判定済みのSFX-02（-51.3%）と同水準かつ機能上の同期制約を持たないため再生成は不要と判断したが、`design/assets.md`に未記載であるため disclosures に記録する。

### failedAssets
なし（3資産とも再生成指示を要する不合格なし）。

### disclosures（再生成不要・人間開示のみ）
1. **SFX-06 duration逸脱の文書化漏れ**: `design/assets.md`指定0.5sに対し実測0.241497s（-51.7%）。音源自体は無音トリム後の正当な仕上がりで機能破綻なし（同期対象となるゲーム側定数の指定なし）だが、`design/assets.md`にはBGM-01の`loop_edit_method`/`force_instrumental_deviation`と異なりこの逸脱が明記されていない。再生成では解決しない文書ギャップのため disclosures とする。
2. **cost_estimated:true**（BGM-01/SFX-05/SFX-06 全3件）: `cost_usd`はElevenLabs character_count実測差分をStarterプランレート按分・Eleven Music固定レートで按分した見積もりであり、プロバイダ請求の実測確定値ではない。
3. **license_note（ElevenLabs Starter「Studio Games」条項）**: 商用利用は可だが、商用×マルチプラットフォーム出荷は Enterprise 相談が必要な条項が付帯（`.claude/docs/assets-config.md`「Checkpointで人間に提示するライセンスフラグ」節）。3件とも該当し、Checkpointでの開示継続が必要。
4. **BGM-01ループはffmpeg編集による人工生成**: composition_plan任せの自然なシームレスループでは再現されず、末尾150ms×冒頭150msの三角窓クロスフェード編集でループ点を作成した（`MANIFEST.jsonl` `loop_edit_method`）。本レビューが独立再現検証しPASSしているため品質上の問題はないが、「AI生成モデルが自然に作ったループ」ではなく「編集で保証したループ」である事実は開示に値する。
5. **force_instrumental_deviation**: `force_instrumental:true`は`composition_plan`併用時にAPI仕様上使用不可（422）のため、`positive_global_styles`/`negative_global_styles`でのinstrumental強制・vocals除外に代替した（`MANIFEST.jsonl`開示済み）。機能的検証はテキストプロンプト設計に基づくものであり、本レビューのツール群では聴感上のボーカル混入有無を音響的に確認できていない。

- 対応: 該当なし（本iterationはfailedAssets 0件・APPROVEのためreviser対応不要。disclosuresはCheckpoint提示時の人間開示事項として引き継ぐ）。
