# レビュー履歴: 音声資産バッチ（Phase 3 build生成分 — BGM-01）

対象: `game/_generated/audio/bgm-main-theme.ogg`（BGM-01）
照合元: `design/assets.md`（## BGM 節・BGM-01行 + 補足 + 「生成時の実施メモ（BGM-01）」節）、`game/_generated/MANIFEST.jsonl`（26行目・BGM-01追記分。総行数は本レビュー時点で並走中の IMG-01/02/05/06/07 生成により31行まで増加中だが、対象はBGM-01の1行のみ）、`state/asset-routing.json`（`checks.elevenlabs`・`shippable.bgm`）、`.claude/docs/tech-stack-unity.md`「資産の取り扱い」（engine=unity: OGGのみ）。
※ SFX-01〜06は前バッチ（`state/reviews/assets-audio-prototype.md` iteration1/2 APPROVE済み）で判定済みのため本バッチの対象外。IMG系は別バッチ・別レビューで判定する（本レビュー実行中に並走生成が進んでいるのを確認したが対象外として除外）。

## AR-ASSET iteration 1 — APPROVE

- 日時: 2026-07-22T07:25:11Z
- 機械検査（ffmpeg/ffprobe実測。MANIFEST自己申告値を鵜呑みにせず全項目を独立再計測。一時出力は `/tmp/ar-audio-review/` に生成し対象ファイルは非破壊）:
  - **sha256突合**: `shasum -a 256 game/_generated/audio/bgm-main-theme.ogg` 実測値 `665605e64d78910759f6f6a55ad5637063974c8028c7d3a0b667a8f1574766fd`（64文字）が MANIFEST 記載値と完全一致。改ざん・取り違え無し。
  - **ffprobe（フォーマット/長さ）**: `codec_name=vorbis`, `sample_rate=44100`, `channels=2`, `format=ogg`, `bit_rate=145121`, `duration=38.400000`。design/assets.md 記載の「38.4秒（1ループ）」と完全一致（差分0ms）。engine=unity の既定形式「OGGのみ」（tech-stack-unity.md）に適合、WAV/M4A不要。
  - **ラウドネス実測**（`ffmpeg -af loudnorm=print_format=json -f null -`）: `input_i=-15.98 LUFS` / `input_tp=-2.82 dBTP` / `input_lra=5.70`。**-16 LUFS ±1（許容域-15〜-17）に収まる**。MANIFEST自己申告値（`measured_I_LUFS:-15.98`, `measured_TP_dBTP:-2.82`）と完全一致。クリッピング無し（TP<0dBTP）。
  - **無音検査**（`ffmpeg -af silencedetect=noise=-45dB:d=0.1 -f null -`）: 全38.4秒でヒット0件。MANIFESTの`silencedetect_full_track_neg45dB: zero hits`主張と一致（フェードアウト/無音跳躍が無いことを独立確認）。
  - **ループシーム検査（独立再現・2手法で実施）**:
    1. まず `ffmpeg -f concat -safe 0 -c copy` によるコンテナレベル自己2連結（`/tmp/ar-audio-review/doubled.ogg`）でPCMデコード後の継ぎ目を検査したところRMS段差20.21dB相当の見かけ上の異常が出たが、これはOgg Vorbisのストリームをコンテナレベルでstream copy連結する手法自体がデコーダのオーバーラップ処理をリセットして生じる**測定手法側のアーティファクト**と判断（デコード連続性が保証されない既知の問題）。判定には採用しない。
    2. 代わりに本番の再生方式に忠実な検査（単一ファイルを直接PCMへデコードし、末尾フレーム→先頭フレームの実際のラップアラウンド点を直接解析）を実施: ラップ点でのサンプル差分260（トラック全体のサンプル差分: 平均519.8・中央値370.0・99パーセンタイル2392・最大9551 — ラップ点の差分はこれらより明確に小さくクリック無し）。ラップ点前後50msウィンドウのRMS段差3.84dB（トラック全体の隣接50msウィンドウRMS段差: 平均4.00・中央値3.25・90パーセンタイル7.62 — ラップ点の段差はp90以下で異常な段差ではない）。この独立測定はMANIFESTの自己申告値（`seam_max_sample_delta:260`, `rms_step_scan_post_encode.seam_step_db:3.24`）と近接し、ループ検証合格を裏付ける。
  - **仕様一致（design/assets.md突合）**: ファイル名・P-xx(P-03)参照・尺(38.4秒)・ループ要件(seamless)・BPM(100)/キー(D major)が全て一致。
  - **音要件突合**: ジャンル（light fantasy orchestral、pizzicato strings + woodwind lead + light brass + glockenspiel/triangle）・BPM100・D majorはdesign/assets.mdプロンプト草案と一致。`force_instrumental`は当初仕様の`force_instrumental:true`パラメータではなく、composition_plan使用時のAPI制約（422 "force_instrumental can only be used with prompt"）により空lyric lines(`lines:[]`)で代替実装——この技術的回避はdesign/assets.md「生成時の実施メモ（BGM-01）」本文とMANIFEST両方に整合して開示済みであり齟齬ではない。歌声の有無を機械的に検出するツールは本環境に無いため聴感確認はMANIFEST自己申告（no vocals present）に依拠する旨を記録する（ブロッカーとはしない — 技術的回避の記録と自己申告の整合性は確認済み）。
  - **provenance必須フィールド**（assets-config.md Provenance節）: file/asset_id/kind/provider/model/prompt/seed_note/generation_attempts/cost_usd/cost_estimated/cost_usd_all_attempts/plan_tier/shippable_route/duration_spec_s/duration_actual_s/loop/loop_type/loop_edit_method/loop_validation/loudness_method/measured_I_LUFS/measured_TP_dBTP/format/sample_rate/channels/bpm/key/sections/force_instrumental/license/license_note/sha256/generated_at を記録済み（音声資産として要求水準を上回る記録密度）。
  - **プロバイダ/ライセンス検査**: `provider:"elevenlabs:music-v2"`、`model:"music_v2"`（`POST /v1/music` REST直、禁止されている公式MCP経由ではない — assets-config.md Primary経路と一致）。`plan_tier:"starter"`は`state/asset-routing.json` `checks.elevenlabs.plan_tier=starter, commercial_ok=true`のpreflight実測と一致（Freeプラン出荷禁止のハード制約はクリア）。`license:"commercial-ok"`。`must_replace`/`placeholder-nc`該当なし。ルート`bgm`は`state/asset-routing.json` `shippable.bgm:true`（degradedルート/フォールバック未使用）。
  - **予算**: BGM-01単体`cost_usd=0.096`（採用分のみ）。`state/budget.txt`（$20上限）に対し無視できる範囲。なお design/assets.md「集計と予算」節が示す全資産cost_usd合算値（$5.5979）を`game/_generated/MANIFEST.jsonl`から独立再計算したところ**$5.9779**（差分約$0.38）となり、design/assets.md本文が自認する「前回集計時の記録誤差の可能性」を裏付けた。ただし$20上限に対しどちらの値でも大幅に残枠があり予算超過は無い。BGM-01固有の欠陥ではないため再生成対象にはしないが、art-directorは次回assets.md更新時にMANIFEST直接合算値へ訂正することを推奨する。
- 指摘要約: BGM-01は品質観点（ラウドネス/ループ/フォーマット/仕様一致/音要件突合/provenance/ライセンス）の全項目で合格。再生成対象なし。
- 開示事項（再生成では直らない・disclosures）: (1) `cost_usd`は`cost_estimated:true`（ElevenLabs `/v1/music`レスポンスにcredit/costヘッダが無いため$0.15/分のレート換算による保守見積）。(2) ElevenLabs「Studio Games」条項（商用×マルチプラットフォーム出荷はEnterprise相談要）がMANIFEST `license_note`に開示済み — Checkpointでの人間提示が必要（assets-config.md Checkpointライセンスフラグ節）。
- 対応: （該当なし — BGM-01はAPPROVE。開示事項はCheckpointで人間へ提示）

---
