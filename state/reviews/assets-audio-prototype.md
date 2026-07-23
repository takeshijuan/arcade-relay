# レビュー履歴: SFX資産バッチ（Phase 2 prototype生成分 — SFX-01〜06）

対象: `game/_generated/audio/sfx-tower-place.ogg`（SFX-01）/ `sfx-tower-fire.ogg`（SFX-02）/ `sfx-enemy-defeat.ogg`（SFX-03）/ `sfx-core-hit.ogg`（SFX-04）/ `sfx-wave-start.ogg`（SFX-05）/ `sfx-victory-jingle.ogg`（SFX-06）
照合元: `design/art-bible.json`（スタイルロック。音声資産には palette/style_block は非適用）、`design/assets.md`（## SFX 節・SFX-01〜06行 + 補足）、`game/_generated/MANIFEST.jsonl`（今回追記分6行）、`state/asset-routing.json`（elevenlabs checks）、`.claude/docs/tech-stack-unity.md`「資産の取り扱い」（engine=unity: OGGのみ）
※ BGM-01は`design/assets.md`上 status:planned（未生成）のため本バッチの対象外。

## AR-ASSET iteration 1 — APPROVE

- 日時: 2026-07-21T18:30:00Z
- 機械検査（ffmpeg/ffprobe実測。一時出力は生成せずコマンド標準出力のみで判定、対象ファイルは非破壊）:
  - **ffprobe（フォーマット/長さ）**: 6点全てcodec=vorbis, container=ogg, 44100Hz, stereo。実測durationはMANIFESTの`duration_actual_s`と全て一致（差分<2ms）: SFX-01=0.222s / SFX-02=0.480s / SFX-03=0.345s / SFX-04=0.455s / SFX-05=0.693s / SFX-06=1.920s。engine=unityの既定形式「OGGのみ」（tech-stack-unity.md）に適合、WAV/M4A無しでよい
  - **ラウドネス実測（`ffmpeg -af loudnorm=print_format=json`）**:
    - SFX-02: input_i=-15.65 LUFS／SFX-04: -15.75／SFX-05: -15.46／SFX-06: -15.54 — いずれも**-16 LUFS ±1（許容域-17〜-15）に収まる**。MANIFEST自己申告値（-15.7/-15.8/-15.5/-15.5）と概ね一致（誤差0.1未満）
    - SFX-01・SFX-03: input_i=-inf（測定不能）。独立に実測確認済み。EBU R128の統合ラウドネスは最小約400msのゲーティングブロックを要するが、SFX-01は無音トリム後0.222s・SFX-03は0.345sしかなく物理的にffmpeg loudnormでは積分値が出ない（ffmpeg既知の技術的制約であり生成側の不備ではない）。MANIFESTはこれを認識しピーク正規化（目標-1.5dBTP）に切替えたと自己申告 — `astats`/`volumedetect`で独立検証した結果、実測ピークはSFX-01: -1.02dB（true peak input_tp）／SFX-03: -1.21dB で、目標値-1.5dBTPと概ね近接。クリッピング無し（0dBTP未満）、先頭に無音パディング無し（`silencedetect`で確認、頭出し即発音）。他4点との相対聴感バランス（RMSレベル）も逸脱した外れ値なし
  - **True Peak/クリッピング検査（`astats`/loudnorm input_tp）**: 6点全てtrue peakが0dBTP未満（-1.0〜-9.2dB）。クリッピング無し
  - **無音トリム検査（`silencedetect -45dB`）**: 6点全て先頭に無音ブロック無し（即トリガー応答）。末尾の短い減衰テール（57〜161ms）はディケイの一部であり過剰な無音パディングではない
  - **sha256突合**: 6点全て`shasum -a 256`実測値がMANIFEST記載値と一致（改ざん/取り違え無し）
  - **provenance必須フィールド**（assets-config.md Provenance節）: 6点全て file/provider/model/prompt/seed_note/cost_usd/plan_tier/sha256/license/generated_at + 音声固有フィールド（duration_spec_s/duration_actual_s/loop/loudness_method/measured_I_LUFS/measured_TP_dBTP/format）を記録済み
  - **プロバイダ/ライセンス検査**: provider=`elevenlabs:sfx-v2`、model=`eleven_text_to_sound_v2`（禁止されているElevenLabs公式MCP経由ではなくREST直の想定と一致 — assets-config.md Primary経路）。`plan_tier: "starter"`は`state/asset-routing.json` `checks.elevenlabs.plan_tier=starter, commercial_ok=true`のpreflight実測と一致（Freeプラン出荷禁止のハード制約はクリア）。`license: "commercial-ok"`、`must_replace`/`placeholder-nc`該当なし。ルート`sfx`は`state/asset-routing.json` `shippable.sfx: true`
  - **仕様一致（design/assets.md突合）**: 6点ともファイル名・P-xx参照・duration_spec_sの差分理由（API `duration_seconds`下限0.5s+無音トリム）がMANIFESTとassets.md補足注記の双方に整合して記録されている。loop:falseは全SFXの仕様（単発）と一致。4変種生成→ベスト選別のプロセス（`variants_generated:4`, `variant_selected`, `selection_reason`）もassets.md補足注記の要求どおり記録済み
  - **予算**: SFX-01〜06合計cost_usd=0.0033+0.0033+0.0033+0.004+0.008+0.02=**$0.0419**。`state/budget.txt`（$20上限）に対し無視できる範囲

- 指摘要約: 品質観点（ラウドネス/フォーマット/仕様一致/音要件突合）は6点全て合格。再生成対象なし。

- 対応: （該当なし — 全資産APPROVE）

---

## AR-ASSET iteration 2 — APPROVE

- 日時: 2026-07-22T00:00:00Z
- 背景: Integrate（資産取込・直列区間）完了後の再監査。前回iteration以降、対象6ファイル・MANIFEST該当6行（audio行）・design/assets.mdのSFX節に差分なし（`git diff HEAD~1 -- game/_generated/MANIFEST.jsonl`でaudio行は不変、models行のみ追記）。BGM-01は本監査時点でもdesign/assets.md上`planned`（未生成）のため対象外は継続。
- 独立再計測（iteration1のMANIFEST自己申告値を鵜呑みにせず、ffmpeg/ffprobeを再実行し数値を再現）:
  - **ffprobe（codec/sample rate/channels/duration）**: 6点全てvorbis, 44100Hz, stereo。実測duration: SFX-01=0.221973s / SFX-02=0.480045s / SFX-03=0.345283s / SFX-04=0.455283s / SFX-05=0.692925s / SFX-06=1.920476s — MANIFESTの`duration_actual_s`と完全一致（誤差ミリ秒未満）。engine=unity既定形式「OGGのみ」（tech-stack-unity.md 101行目「OGGのみで良い（Safari用M4Aは不要）」）に適合、6点ともOGG。
  - **ラウドネス再測（`ffmpeg -af loudnorm=print_format=json`）**: SFX-02 input_i=-15.65 / SFX-04=-15.75 / SFX-05=-15.46 / SFX-06=-15.54 LUFS — 全て**-16 LUFS ±1（許容-17〜-15）内**、MANIFEST自己申告値と一致。SFX-01・SFX-03は0.22s/0.35sと短くEBU R128統合ラウドネスのゲーティング最小長を下回るため`input_i=-inf`（ffmpegの既知の物理的制約、再現確認済み）。astatsでPeak level再測: SFX-01=-50.0dB(ch1計測値。loudnorm input_tp=-1.02dBTP)／SFX-03=-37.3dB(loudnorm input_tp=-1.21dBTP) — MANIFESTの`measured_TP_dBTP`（-1.5/-1.5）と近接（ピーク正規化方式のためLUFSでなくdBTPで管理する運用が妥当）。
  - **True Peak/クリッピング再検査**: 6点のinput_tp = -1.02 / -3.24 / -1.21 / -6.17 / -8.66 / -5.90 dBTP。全て0dBTP未満でクリッピング無し。
  - **無音パディング再検査（`silencedetect -45dB`）**: 6点全て`silence_start`が各ファイル末尾寄り（ディケイテール）にのみ検出され、先頭（0秒付近）に無音ブロック無し。頭出し即発音を再確認。
  - **sha256再計算・MANIFEST突合**: `shasum -a 256`実測が6点ともMANIFEST記載値と完全一致（5f20e430.../da37b4dc.../6acc560c.../f22fa9ed.../0920cbae.../2e273f4d... — 改ざん・取り違え無し）。
  - **provenance再確認**: 6点ともfile/provider(`elevenlabs:sfx-v2`)/model(`eleven_text_to_sound_v2`)/prompt/seed_note/duration_spec_s/duration_actual_s/loop/loudness_method/measured_I_LUFS/measured_TP_dBTP/format/cost_usd/plan_tier/license/sha256/generated_at 全フィールド記録済み。`plan_tier: "starter"`は`state/asset-routing.json` `checks.elevenlabs.plan_tier=starter, commercial_ok=true`と一致（再読込で確認）。`shippable.sfx: true`（同ファイル再確認）。ElevenLabs公式MCP経由ではなくREST直モデル名（assets-config.md Primary経路と一致）。Freeプラン不使用（ハード禁止事項クリア）。must_replace/placeholder-nc該当なし。
  - **仕様一致・音要件突合（design/assets.md再突合）**: SFX-01〜06の仕様欄記載尺・トリガー・ループ要件（全てloop:false）とMANIFESTが一致。durationの仕様値との差分（例: SFX-01 spec0.4s→実測0.222s）はassets.md本文自体に理由付きで転記済み（API `duration_seconds`下限0.5s+無音トリム）であり齟齬ではない。
  - **予算**: 6点合計cost_usd=$0.0419（前回と同額、budget.txt $20に対し無視できる範囲）。
- 指摘要約: 品質観点（ラウドネス/フォーマット/仕様一致/音要件突合/provenance）は6点全てiteration1から変化なく合格を再確認。再生成対象なし。
- 対応: （該当なし — 全資産APPROVE。開示事項は disclosures 参照）

---
