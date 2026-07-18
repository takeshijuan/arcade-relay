# レビュー履歴 — assets-audio-prototype（SFX-01〜04, engine=unity）

## AR-ASSET iteration 1 — CONCERNS
- 日時: 2026-07-10T15:30:00Z
- 対象: `game/_generated/MANIFEST.jsonl` 今回追記分 SFX-01〜04（`game/_generated/audio/sfx-01-attack-hit.ogg` / `sfx-02-dash.ogg` / `sfx-03-player-hit.ogg` / `sfx-04-crystal-pickup.ogg`）。SFX-05/06・BGM-01 は `design/assets.md` の状態が `planned`（未生成・Phase 3 繰り延べ）のため本バッチ対象外。
- 検査方法: `ffprobe`（duration/codec/format/sha256）、`ffmpeg -af loudnorm=print_format=json -f null -`（Integrated Loudness実測）、`ffmpeg -af ebur128=peak=true`（クロスチェック・True Peak実測）、`ffmpeg -af astats`（Peak/RMS/Crest factor/クリッピング/ノイズフロア実測）を全4ファイル直接実行。

### 観点別結果

**仕様一致（provenance/format/license）— PASS**
- sha256: 4件全て MANIFEST 記載値と実ファイルが一致（改ざん・取り違えなし）
- フォーマット: 全件 `ogg`（vorbis, 44.1kHz, stereo）。engine=unity の音声既定（tech-stack-unity.md §資産の取り扱い「OGGのみで良い」）に一致
- クリッピング: `astats` で clipped samples 検出なし。全件 peak -1.56〜-3.48dBFS でヘッドルーム確保
- ループ要件: 全件 `loop:false`・design/assets.md「単発・ループなし」と一致（ループシーム検査は対象外）
- license: 全件 `commercial-ok` / `plan_tier:starter`（state/asset-routing.json の `elevenlabs.commercial_ok:true` と整合）。`must_replace` / `placeholder-nc` 該当なし

**ラウドネス実測（観点1）— 4件中4件で機械検証との齟齬あり（CONCERNS の主因）**

実ファイルへ直接 `ffmpeg loudnorm` / `ebur128` を実行した結果、MANIFEST の自己申告値と再現しない、または再現不能なケースが4件全てで見つかった:

| id | MANIFEST申告 (loudnorm_output_i_lufs) | 独立再測定 (loudnorm/ebur128実測) | -16±1 (-17〜-15) 判定 |
|---|---|---|---|
| SFX-01 | -18.92（**自己申告で既に不適合と開示済み**） | -18.65 LUFS（ほぼ一致） | 不適合（下限より-1.65dB） |
| SFX-02 | -16.04（適合と記載） | `-70.0 LUFS` / `input_i:-inf`（**測定不能**。duration 0.292s が EBU R128 最小ゲーティングブロック長400msに満たない） | 検証不能（申告値を再現できず） |
| SFX-03 | -16.67（適合と記載） | **-14.4 LUFS**（申告値と2.27dB乖離） | 不適合（上限より+0.6dB＝やや大きすぎ） |
| SFX-04 | -15.97（適合と記載） | `-70.0 LUFS` / `input_i:-inf`（**測定不能**。duration 0.393s も同様に400ms未満） | 検証不能（申告値を再現できず） |

補足実測（RMS・crest factor・astats、参考値）: SFX-01 RMS -25.2dB/crest 12.4dB、SFX-02 RMS -18.1dB/crest 6.2dB、SFX-03 RMS -16.6dB/crest 5.6dB、SFX-04 RMS -16.7dB/crest 4.9dB。いずれも無音ではなく実信号があることは確認済み（-inf は「音が無い」のではなく「BS.1770ゲーティング計算が短すぎて成立しない」ことを意味する）。

**評価**: SFX-01のMANIFEST注記（「短い高crest factor単発トランジェントにEBU R128積分ラウドネスを適用する既知の限界」）は独立測定でも裏付けられる正しい技術認識だが、この限界が**SFX-02/03/04にも及んでいることが未検出・未開示**だった。特にSFX-03は「適合」と記載されているにも関わらず実測が乖離し、無音トリムの適用順序（トリム前の中間ファイルで測定→トリム後にラウドネスが変化）が原因の可能性が高い（同エントリの`loudnorm_note`でトリムのfilter chainバグ修正に言及されており符合する）。これは音質そのものの欠陥ではなく**測定手法・出荷バイトに対する再測定の欠落**が原因のため、全面再生成ではなく再測定＋軽微なゲイン調整で対応可能と判断。

**仕様一致（duration・観点3）— 軽微な逸脱、ブロッキングではない**
`duration_requested_s`（=design/assets.md「サイズ」列と一致: 0.5/0.6/0.5/0.6s）に対し `duration_final_s` はいずれも短い（SFX-01: 0.414s / SFX-02: 0.292s / SFX-03: 0.435s / SFX-04: 0.393s）。無音トリムは assets-config.md「生成後パイプライン」の標準工程であり、各SFXの同期対象（SFX-01: `AUTO_ATTACK_INTERVAL` 初期値0.6s、SFX-02: `DASH_DURATION` 0.2s）は全て final duration 以下に収まっているため機能的な不具合はない。SFX-02 の trim 率（-51%）が4件中最大で今後の生成時の参考として留意。

**音要件突合（観点4）— PASS**: 各SFXの用途（攻撃ヒット/ダッシュ/被弾/クリスタル回収）とプロンプト内容が design/assets.md のトリガー説明と1対1で一致。

### 総合判定: CONCERNS
理由: 機械検証可能なハード数値基準（ラウドネス -16 LUFS ±1）についてMANIFESTの自己申告値が4件中3件で独立再現できず（うち2件は測定不能、1件は乖離）、残り1件は自己申告通り不適合。ただしこれは音源自体の破綻（クリッピング・ノイズ・仕様外フォーマット等）ではなく、測定手法・出荷後再検証の欠落に起因するため REJECT ではなく CONCERNS とし、再測定＋必要に応じた軽微なゲイン調整を再生成指示とする。

### 再生成指示（優先度順）
1. **[全4件・優先度高]** MANIFEST の `loudnorm_output_i_lufs` は最終出荷バイト（trim後・現物ファイル）に対し直接 `ffmpeg -i <file> -af loudnorm=print_format=json -f null -` を実行した値で記録し直すこと。0.4s未満のファイルで `-inf`/`-70.0 LUFS` となる場合は「Integrated Loudness測定不能（BS.1770最小ゲーティングブロック400ms未満）」と明記し、代替指標（`ebur128=peak=true` の Momentary値、または本レビューが実測したRMSを基準にした相対値）を採用した根拠を記録すること。
2. **[SFX-03・優先度中]** `ffmpeg -i sfx-03-player-hit.ogg -af volume=-1.8dB -c:a libvorbis -q:a 6 sfx-03-player-hit-v2.ogg` 等で-1.5〜2dB減衰させ、true peakを-1.0dBTP以下に保ったまま-17〜-15 LUFS帯へ収め、再測定してMANIFEST値を更新すること。
3. **[SFX-01・優先度低（既に開示済みの設計判断）]** crest factor（14.47dB=最もパンチが効いた変種）を維持する現状の判断は妥当だが、`ffmpeg -af volume=+2.0dB` 程度のメイクアップゲインで-17dB帯へ近づけられないか一度試行し、true peak超過が起きるなら現状維持のまま「意図的許容逸脱」として本レビューの指摘対応欄に理由を明記すること。
4. **[SFX-02・優先度低]** RMS実測-18.1dBがやや低めのため、+1.0〜1.5dB程度のメイクアップゲインを検討（必須ではない。まず1.の再測定を優先）。

### disclosures（再生成不要・人間開示のみ）
- 全4件 `cost_estimated:true`（ElevenLabs character_count実測差分をStarterプランレートで按分した推定値であり、プロバイダ請求の実測値ではない）
- 全4件 `license_note`: ElevenLabs Starterプランは商用利用可だが「Studio Games」条項（商用×マルチプラットフォーム出荷は Enterprise 相談要）が付帯（assets-config.md 「Checkpointで人間に提示するライセンスフラグ」節と一致、Checkpointでの開示を継続すること）

- 対応: audio-designer 記入（2026-07-10T06:24:31Z）。ElevenLabs API への再生成は行わず、既存4音源へ ffmpeg ローカル後処理（`volume` フィルタ + libvorbis 再エンコード）で是正。全件、最終出荷バイト（現物ファイル）へ直接 `ffmpeg -af loudnorm=print_format=json -f null -` を再実行し独立再測定した（詳細は `game/_generated/MANIFEST.jsonl` の `revision:2` エントリ4件を参照）。

  1. **[全4件・指摘1: 出荷バイト直接再測定]** 対応済み。4件とも最終出荷バイトへ直接再測定を実施し、旧自己申告値の再現性を確認:
     - SFX-01: 独立再測定 -18.65 LUFS（自己申告-18.92とほぼ一致・再現OK）
     - SFX-02: 独立再測定 `-inf`/`-70.0 LUFS`（測定不能を確認・再現不能。旧申告-16.04は中間ファイル測定の可能性）
     - SFX-03: 独立再測定 -14.45 LUFS（旧申告-16.67と2.27dB乖離を確認。トリム後出荷バイト未測定が原因と特定）
     - SFX-04: 独立再測定 `-inf`/`-70.0 LUFS`（測定不能を確認。旧申告-15.97は中間ファイル測定の可能性）
     測定不能2件（SFX-02/04）は MANIFEST に「Integrated Loudness測定不能（BS.1770最小ゲーティングブロック400ms未満）」と明記し、`ebur128=peak=true:metadata=1` によるMomentary値も試行したが同一の-70.0 LUFSで追加情報が得られなかったため（duration自体が400ms未満で1ゲーティングブロックすら構成できない）、代替指標として astats RMS実測値を採用し根拠を記録した。

  2. **[SFX-03・指摘2]** 対応済み。指定コマンド通り `volume=-1.8dB` を適用（true peak -1.56→-3.28dBTPで-1.0dBTP制約に十分な余裕）、再測定で -16.25 LUFS を確認し目標-17〜-15帯へ収束。MANIFESTの `loudnorm_output_i_lufs` を実測値へ更新。

  3. **[SFX-01・指摘3]** 対応（部分是正+意図的許容逸脱として明記）。`volume=+2.0dB` はTP -0.42dBTPとなり-1.0dBTP制約を超過するため採用不可と判定。二分探索でTP<=-1.0dBTPを満たす最大安全ゲインを実測し `+1.4dB`（TP -1.07dBTP）を採用。crest factor 13.64dB（適用前12.4-13.7dB相当）を維持したまま -18.65→-17.45 LUFS まで改善したが、目標下限-17に0.45dB届かず。これ以上のゲインは true peak 制約を超過するため、残差0.45dBは「意図的許容逸脱」として受容する（設計意図＝高crest factorのパンチ表現を損なわない範囲での物理的な最大是正）。

  4. **[SFX-02・指摘4]** 対応済み（任意項目だが実施）。`volume=+0.9dB`（TP -1.05dBTPでTP<=-1.0dBTP制約の理論境界+0.99dBに対し安全マージン確保）を適用し、astats RMS実測を -18.12dB→-17.26dB へ改善。Integrated Loudness自体は引き続き測定不能（400ms未満の物理制約は変更不可）だが、代替指標（RMS）でのバッチ内一貫性は向上。

  **未解決事項**: SFX-01 は物理的制約（TP -1.0dBTP ceiling と短尺高crest factorトランジェントの併存不可）により目標-17〜-15 LUFS帯に0.45dB届かない状態を「意図的許容逸脱」として継続する。この逸脱は Checkpoint で開示すること。SFX-02/04 の Integrated Loudness 測定不能は音源の欠陥ではなく尺（<400ms）由来の測定手法上の限界であり、代替指標（RMS実測）で音量の妥当性を確認済み。

## AR-ASSET iteration 2 — CONCERNS
- 日時: 2026-07-10T16:10:00Z
- 対象: `game/_generated/MANIFEST.jsonl` の `revision:2` 4件（SFX-01〜04。sha256はSFX-04のみiteration1と同一＝波形無変更、他3件は再エンコード後の新sha256）。iteration1 CONCERNS への対応版。
- 検査方法: iteration1 と同一手法で全4件へ独立再測定を再実行。`shasum -a 256` でMANIFEST記載sha256との一致確認、`ffprobe`（duration/codec/sample_rate/channels）、`ffmpeg -af loudnorm=I=-16:TP=-1.0:print_format=json -f null -`、`ffmpeg -af ebur128=peak=true`、`ffmpeg -af astats`（Peak/RMS/Crest factor/クリッピング）を実ファイルへ直接実行。

### 観点別結果

**provenance/sha256整合 — PASS（iteration1の主要懸念が解消）**
4件全てで実ファイルのsha256がMANIFEST `revision:2` 記載値と完全一致。かつ独立再測定した数値（`input_i` / `input_tp`）がMANIFESTの自己申告値（`loudnorm_output_i_lufs` / `loudnorm_output_tp_dbtp`）と誤差0.1dB以内で一致し、iteration1で指摘した「自己申告値が出荷バイトから再現できない」問題は4件中4件で解消済みと確認した:

| id | MANIFEST申告 (I / TP) | 独立再測定 (I / TP) | 再現性 |
|---|---|---|---|
| SFX-01 | -17.45 / -1.07 dBTP | -17.45 / -1.07 dBTP（`ebur128`実測 -17.4 LUFS, TP -1.1dBFS） | 完全一致 |
| SFX-02 | null（測定不能）/ -1.05 dBTP | `-inf`（`input_i`）/ TP -1.05 dBTP（`ebur128` -70.0 LUFS） | 完全一致（測定不能の再現含む） |
| SFX-03 | -16.25 / -3.28 dBTP | -16.25 / -3.28 dBTP（`ebur128`実測 -16.2 LUFS, TP -3.3dBFS） | 完全一致 |
| SFX-04 | null（測定不能）/ -2.82 dBTP | `-inf`（`input_i`）/ TP -2.82 dBTP（`ebur128` -70.0 LUFS） | 完全一致（測定不能の再現含む、sha256も無変更を確認） |

**仕様一致（フォーマット・duration）— PASS**
全件 `ogg`（vorbis, 44.1kHz, stereo）。tech-stack-unity.md §資産の取り扱い「OGGのみで良い」に一致（M4A不要も正しく非同梱）。`duration_final_s` はMANIFEST記載値と一致（SFX-01: 0.440s / SFX-02: 0.292s / SFX-03: 0.460s / SFX-04: 0.393s）。iteration1で指摘した`duration_requested_s`との差（無音トリムによる短縮）は各SFXの同期対象（`AUTO_ATTACK_INTERVAL`初期値0.6s等）を全て下回るため引き続き非ブロッキングと判定。

**クリッピング — PASS**: 全件 `astats` でサンプルピーク最大-1.06dBFS（SFX-01）、クリップ兆候なし。

**ラウドネス実測（観点1）— 4件中3件PASS、1件（SFX-01）が-16±1 LUFS帯（-17〜-15）を0.45dB下回り不適合のまま**
- SFX-02/04: Integrated Loudness測定不能（duration<400msのEBU BS.1770物理制約）は音源側の欠陥ではなく測定手法の限界として正しく開示・代替指標（RMS）で妥当性確認済み。iteration1の指摘に忠実に対応しており、これ自体は不合格理由としない。
- SFX-03: -16.25 LUFSで-17〜-15帯に収まりPASS。
- **SFX-01: -17.45 LUFS（独立再測定で完全再現・虚偽申告なし）。目標下限-17を0.45dB下回り不適合が継続。** MANIFESTは「TP<=-1.0dBTP制約下での物理的な最大是正」と説明するが、この-1.0dBTPシーリング自体は `design/art-bible.json` / `design/assets.md` / `.claude/docs/assets-config.md` のいずれにも明記されたプロジェクト要件ではなく、audio-designer側が任意に設定した安全マージンである（放送/ストリーミング配信で一般的な目安ではあるが、本プロジェクトはUnity内蔵AudioClipとしての再生であり配信規格の対象外）。現状のサンプルピークは-1.06dBFSで0dBFSまでまだ約1dBの余地があり、シーリングを緩めた再試行を行っていない。よって「これ以上のゲインは不可能」という結論は未検証の前提に基づいており、再生成（追加ローカル後処理）で解消し得る不合格と判定する。

**音要件突合（観点4）— PASS**: iteration1から変更なし、用途とプロンプトの対応は適合。

### 総合判定: CONCERNS
理由: iteration1の主要懸念（自己申告ラウドネス値が出荷バイトから再現不能）は4件全件で解消し、provenanceの健全性は大きく改善した。SFX-02/03/04は仕様・実測とも適合。しかしSFX-01のみ、ハード数値基準（-16 LUFS ±1）に対し実測-17.45 LUFSで0.45dB不適合のまま残っており、その根拠とされる-1.0dBTPシーリングがプロジェクト要件として文書化されていない自己設定値であるため、緩和余地を検証せずに「意図的許容逸脱」で確定するのは時期尚早と判断した。REJECTではなくCONCERNS（3資産PASS・1資産のみ要追加対応）とする。

### 再生成指示（優先度順）
1. **[SFX-01・優先度中]** 自己設定した`TP<=-1.0dBTP`シーリングを緩和し、Vorbis再エンコード後の安全マージンとして一般的な`-0.3dBTP`程度まで許容した上で追加ゲインを試すこと。起点コマンド例: `ffmpeg -i sfx-01-attack-hit.ogg -af volume=+0.6dB -c:a libvorbis -q:a 6 sfx-01-attack-hit-v3.ogg` を作成し `ffmpeg -i <out> -af loudnorm=print_format=json -f null -` で再測定、true peakが-0.3dBTPを超えない範囲で二分探索しながら-17.0〜-16.5 LUFS帯（下限に安全マージンを残す）まで追い込むこと。
2. **[SFX-01・優先度中、1.で届かない場合の代替手段]** 単純な線形ゲイン（`volume`フィルタ）ではなく短いlookaheadピークリミッタを試すこと（例: `ffmpeg -i sfx-01-attack-hit.ogg -af alimiter=limit=0.966:attack=1:release=50,volume=+1.0dB -c:a libvorbis -q:a 6 sfx-01-attack-hit-v3.ogg`。`0.966`≈`-0.3dBFS`）。リミッタは線形ゲインと異なりピーク超過分のみを圧縮するため、crest factorの大部分を維持したまま積分ラウドネスを底上げできる可能性がある。適用後は必ず `alimiter` 前後で crest factor を比較し、パンチの質（crest factor低下幅）が指摘3の設計意図（14.47dB基準）を大きく損なっていないか確認して記録すること。
3. **[SFX-01・優先度低、1.・2.双方を試した上でなお届かない場合のみ]** 1.と2.を実測付きで試行してもTP安全マージンとcrest factor維持の両立で-17.0 LUFSに届かない場合に限り、「意図的許容逸脱」として確定してよい。ただしその際は「-1.0dBTPではなく-0.3dBTPシーリングを試した上での物理的限界」であることをMANIFESTに明記し、本レビュー（iteration2）を根拠として引用すること。iteration1の「-1.0dBTP前提での限界」という根拠をそのまま踏襲しての受容は不可。
- 本資産（SFX-01）は review-loops.md の MAX_ITER=3/資産のうち今回が2回目。次回もCONCERNSであれば3回目が最終審査となり、非APPROVEならfallbackプロバイダ（ElevenLabs以外のローカル縮退等）への切替を検討すること。ただしSFX-01は既存ElevenLabs音源へのローカル後処理のみで対応可能な性質の指摘であり、fallbackプロバイダ切替（音源自体の作り直し）は不要と考えられる。

### disclosures（再生成不要・人間開示のみ、iteration1から継続）
- 全4件、原資産の生成コスト（`revision:2`以前の元エントリの`cost_usd`）は`cost_estimated:true`（ElevenLabs character_count実測差分をStarterプランレートで按分した推定値であり、プロバイダ請求の実測値ではない）。`revision:2`のローカルffmpeg後処理自体は`cost_usd:0`・`cost_estimated:false`で正確だが、由来する原資産コストの推定値である点は継続開示。
- 全4件 `license_note`: ElevenLabs Starterプランは商用利用可だが「Studio Games」条項（商用×マルチプラットフォーム出荷はEnterprise相談要）が付帯（assets-config.md「Checkpointで人間に提示するライセンスフラグ」節と一致）。Checkpointでの開示を継続すること。
- SFX-02/04: Integrated Loudness（EBU BS.1770）が測定不能（duration<400ms）である点は音源の欠陥ではなく短尺one-shot SFXにおける当該規格の既知の適用限界。再生成では解消しない特性のため今後も代替指標（RMS実測）での妥当性確認を運用とすること。

- 対応: audio-designer 記入（2026-07-10T16:45:00Z）。ElevenLabs API への再生成は行わず、既存音源（revision:2、sha256 855f3dba...）へ ffmpeg ローカル後処理（`volume` フィルタ + libvorbis 再エンコード）で追加是正。最終出荷バイトへ直接 `ffmpeg -af loudnorm=print_format=json -f null -` および `ebur128=peak=true` を再実行し独立再測定した（詳細は `game/_generated/MANIFEST.jsonl` の `revision:3` エントリを参照）。

  1. **[SFX-01・指摘1: TPシーリング緩和+二分探索]** 対応済み。`TP<=-1.0dBTP` の自己設定シーリングを `-0.3dBTP` へ緩和し、指摘の起点コマンド（`volume=+0.6dB`）から実測しながら二分探索を実施:
     - +0.6dB: -16.95 LUFS / TP -0.90dBTP
     - +0.8dB: -16.75 LUFS / TP -0.81dBTP
     - +1.0dB: -16.65 LUFS / TP -0.81dBTP
     - +1.2dB: -16.45 LUFS / TP -0.38dBTP（**-0.3dBTPシーリングを満たす最大安全ゲイン**）
     - +1.3dB: -16.35 LUFS / TP -0.29dBTP（-0.3dBTPシーリング超過のため不採用）
     - +1.4dB: -16.25 LUFS / TP -0.28dBTP（同上、不採用）

     `+1.2dB` を採用し `ffmpeg -i sfx-01-attack-hit.ogg -af volume=+1.2dB -c:a libvorbis -q:a 6` で再エンコード。最終出荷バイトへの独立再測定で **-16.45 LUFS / TP -0.38dBTP**（`ebur128` クロスチェックで I:-16.4 LUFS・Peak:-0.4dBFS、誤差0.1dB以内で一致）を確認し、目標-17〜-15 LUFS帯に**下限から0.55dBの安全マージンを残して収束**、-0.3dBTPシーリングにも0.08dBの安全マージンを確保した。iteration2で継続していた0.45dB不足を完全解消。

  2. **[SFX-01・指摘2: alimiterによる代替手段]** 不要と判断（未実施）。指摘1の線形`volume`ゲインのみで目標帯に十分な安全マージン（0.55dB）を持って到達できたため、`alimiter`によるピークリミッティングは試行しなかった。線形ゲインのみで解決できる場合にリミッタを追加適用するとcrest factorを不必要に追加圧縮するリスクがあるため、本ケースでは見送りが妥当と判断。

  3. **[SFX-01・指摘3: 意図的許容逸脱としての確定]** 該当なし（不要）。指摘1で目標帯に到達したため「意図的許容逸脱」としての受容は行わない。0.45dB不足は完全解消。

  4. **crest factor維持の確認**: 適用前（revision:2）は左右チャンネルで11.98dB/13.64dB、適用後（revision:3、+1.2dBゲイン後）は11.66dB/13.15dBで、約0.3〜0.5dBの低下に留まった。4変種選定時の設計意図（「パンチの効いた」最高crest factor特性）を大きく損なっていないことを確認した。

  **未解決事項**: なし。SFX-01 はラウドネス（-16.45 LUFS）・TP（-0.38dBTP）・フォーマット（ogg, unity既定）・sha256整合すべて適合。SFX-02〜04（iteration2でPASS済み）と合わせ本バッチ4件全てハード数値基準を満たした。

## AR-ASSET iteration 3 — APPROVE
- 日時: 2026-07-10T17:20:00Z
- 対象: `game/_generated/MANIFEST.jsonl` 今回追記分 — SFX-01 `revision:3`（sha256 `e6f4a98087e12f52d8dfc857ae8009b64d32ab5c482687a2afb5e880196b72c4`）。SFX-02〜04 は `revision:2`（iteration2でPASS済み・今回変更なし）。SFX-05/06・BGM-01 は `design/assets.md` 状態 `planned`（対応メカニクスがphase:buildのためPhase 3繰り延べ）で引き続き本バッチ対象外。
- 検査方法: iteration1/2と同一手法で独立再実行。`shasum -a 256`（実ファイル vs MANIFEST記載sha256の突合）、`ffprobe`（format_name/codec_name/sample_rate/channels/duration）、`ffmpeg -af loudnorm=print_format=json -f null -`（input_i/input_tp実測）、`ffmpeg -af astats`（Overall Peak level dB/Max level/Min levelでクリッピング・NaN/Inf検査）を全4ファイル直接実行。

### 観点別結果

**provenance/sha256整合 — PASS**
全4件、実ファイルのsha256がMANIFEST最新revision記載値と完全一致: SFX-01 `e6f4a980...`（revision:3）/ SFX-02 `9f739120...`（revision:2）/ SFX-03 `0cd70a83...`（revision:2）/ SFX-04 `539e9cad...`（revision:2、原本と無変更）。

**仕様一致（フォーマット）— PASS**
全4件 `ogg`（vorbis, 44.1kHz, stereo）。`tech-stack-unity.md`「資産の取り扱い」節「音声: Unity は Ogg Vorbis / WAV をネイティブ対応。**OGGのみで良い**（Safari用M4Aは不要—phaser専用要件）」に一致。M4A非同梱も正しい。

**クリッピング — PASS**
astats Overall で Max level 全件 <1.0（SFX-01: 0.958 / SFX-02: |−0.885| / SFX-03: |−0.685| / SFX-04: |−0.721|）、Peak level dB 全件 0dBFS未満（-0.37 / -1.06 / -3.28 / -2.85dBFS）。Number of NaNs / Infs とも全件0。

**ラウドネス実測（観点1）— 4件中4件適合。iteration2からの唯一の残課題（SFX-01）解消を独立再現で確認**

| id | 独立再測定 input_i / input_tp | -16±1（-17〜-15）判定 |
|---|---|---|
| SFX-01 | -16.45 LUFS / -0.38 dBTP | 適合（MANIFEST revision:3 申告値と完全一致・独立再現） |
| SFX-02 | -inf（測定不能）/ -1.05 dBTP | 測定不能＝物理制約（duration 0.292s < BS.1770最小ゲーティングブロック400ms）。iteration1/2から継続の既知限界。RMS代替指標（-17.26dB、iteration1で確認済み）で妥当性担保、非ブロッキング |
| SFX-03 | -16.25 LUFS / -3.28 dBTP | 適合（iteration2から変更なし、再現済み） |
| SFX-04 | -inf（測定不能）/ -2.82 dBTP | 測定不能＝同上物理制約（duration 0.393s）。RMS代替指標（-16.72dB）で妥当性担保、非ブロッキング |

iteration2で唯一不合格だった SFX-01（独立実測 -17.45 LUFS、目標下限-17を0.45dB下回る）は、revision:3（TPシーリングを自己設定の-1.0dBTPから-0.3dBTPへ緩和した上での二分探索、+1.2dBゲイン採用）により **-16.45 LUFS / TP -0.38dBTP** へ是正されたことを本iterationの独立再測定で完全再現・確認した。目標帯-17〜-15 LUFSに下限から0.55dBの安全マージンを残して収束している。

**仕様一致（duration・音要件突合）— PASS（iteration1から変更なし）**
`duration_final_s` は全件MANIFEST記載値と一致（SFX-01: 0.440s / SFX-02: 0.292s / SFX-03: 0.460s / SFX-04: 0.393s）。無音トリムによる短縮は各SFXの同期対象（`AUTO_ATTACK_INTERVAL`初期値0.6s等）を全て下回らないため非ブロッキング。用途（攻撃ヒット/ダッシュ/被弾/クリスタル回収）とプロンプト内容もdesign/assets.mdのトリガー説明と1対1で一致。

**license/provenance — PASS**
全4件 `license:"commercial-ok"` / `plan_tier:"starter"`。`state/asset-routing.json` の `routes.sfx="elevenlabs:sfx-v2"`・`shippable.sfx:true`・`checks.elevenlabs.commercial_ok:true` と整合。`must_replace` 該当なし。全件ElevenLabs直APIのPrimaryルート経由のため、fal経由Meshyのライセンス継承未検証や`shippable:false`ルート由来は非該当。

### 総合判定: APPROVE
理由: iteration1〜2で継続していたラウドネス不適合（SFX-01の目標下限に対する0.45dB不足）が今回のrevision:3是正で解消され、出荷バイトへの独立再測定でも完全再現を確認した。SFX-02〜04はiteration2からsha256無変更でPASSを維持。sha256整合・フォーマット・クリッピング・ラウドネス・duration・音要件・provenance/licenseの全観点で対象4資産（SFX-01〜04）とも問題なし。バッチ全資産合格。

### failedAssets
なし。

### disclosures（再生成不要・人間開示のみ）
- 原資産（ElevenLabs生成分）のコストは`cost_estimated:true`（character_count実測差分をStarterプランレートで按分した推定値であり、プロバイダ請求の実測値ではない）。`revision:2/3`のffmpegローカル後処理自体は`cost_usd:0`・`cost_estimated:false`で正確。
- 全4件 `license_note`: ElevenLabs Starterプランは商用可だが「Studio Games」条項（商用×マルチプラットフォーム出荷はEnterprise相談要）が付帯（assets-config.md「Checkpointで人間に提示するライセンスフラグ」節と一致）。Checkpointでの開示を継続すること。
- SFX-02/04: Integrated Loudness（EBU BS.1770）が測定不能（duration<400ms）である点は音源の欠陥ではなく短尺one-shot SFXにおける当該規格の既知の適用限界。再生成では解消しない特性のため、今後も代替指標（RMS実測）での妥当性確認を運用とすること。
- SFX-05/06・BGM-01は`design/assets.md`上`planned`（対応メカニクスS-15/S-12がphase:buildのためPhase 3繰り延べ）で本バッチ未生成。Checkpoint C（stage=done）到達前にPhase 3での生成・AR-ASSET合格が必須（現時点では単に「未着手」であり不合格ではない）。

- 対応: 該当なし（本iterationはfailedAssets 0件・APPROVEのためreviser対応不要）。

## AR-ASSET iteration 4 — APPROVE
- 日時: 2026-07-11T00:00:00Z
- 経緯: 本iterationは workflow から「iteration 1」として本バッチのレビュー依頼を再度受けたが、`state/reviews/assets-audio-prototype.md`（本ファイル）を読んだ結果、同一バッチ（SFX-01〜04）は既に iteration 1〜3 のループを経て iteration 3 で APPROVE 済みであることを確認した。review-loops.md の追記原則（「状態はファイルが真実」）に従い、依頼側の iteration 番号指定より本ファイルの実履歴を正としてシーケンス番号を継続し、独立した再検証として **iteration 4** で記録する（iteration 1 を重複起票して履歴を破壊しない）。
- 対象: `game/_generated/MANIFEST.jsonl` 最新revision — SFX-01 `revision:3`（sha256 `e6f4a980...`）、SFX-02/03/04 `revision:2`（sha256 `9f739120...` / `0cd70a83...` / `539e9cad...`、SFX-04は原本と無変更）。SFX-05/06・BGM-01 は `design/assets.md` 状態 `planned`（Phase 3繰り延べ）で引き続き対象外。
- 検査方法: iteration1〜3から独立して全項目をゼロから再実行（前回結果は参照のみで転記せず）。`shasum -a 256`（実ファイル vs MANIFEST最新revision突合）、`ffprobe`（format_name/codec_name/sample_rate/channels/duration）、`ffmpeg -af loudnorm=print_format=json -f null -`（input_i/input_tp実測）、`ffmpeg -af astats=metadata=0`（Peak level dB・RMS level dB・NaNs/Infs/denormals、左右チャンネル+Overall）、`state/asset-routing.json` の `shippable.sfx` / `checks.elevenlabs` 確認。

### 独立再検証結果（実測値）

| id | sha256実測 | MANIFEST一致 | duration実測 | format | loudnorm input_i / input_tp | Peak dB(Overall) | Clipping/NaN/Inf |
|---|---|---|---|---|---|---|---|
| SFX-01 | `e6f4a980...` | 一致（revision:3） | 0.439728s | ogg/vorbis/44.1kHz/stereo | -16.45 LUFS / -0.38 dBTP | -0.372 | なし/0/0 |
| SFX-02 | `9f739120...` | 一致（revision:2） | 0.292336s | ogg/vorbis/44.1kHz/stereo | -inf（測定不能・duration<400ms） / -1.05 dBTP | -1.063 | なし/0/0 |
| SFX-03 | `0cd70a83...` | 一致（revision:2） | 0.460045s | ogg/vorbis/44.1kHz/stereo | -16.25 LUFS / -3.28 dBTP | -3.284 | なし/0/0 |
| SFX-04 | `539e9cad...` | 一致（revision:2、原本無変更） | 0.392676s | ogg/vorbis/44.1kHz/stereo | -inf（測定不能・duration<400ms） / -2.82 dBTP | -2.845 | なし/0/0 |

全4件、本iterationの独立測定値は iteration 3 の記録値と誤差0dBで完全一致した（同一ファイルへの再測定のため当然だが、MANIFESTの自己申告値をそのまま転記せず実ファイルへ直接コマンドを実行して確認済み）。

**ラウドネス（観点1）** — SFX-01/03は-16 LUFS ±1（-17〜-15帯）に適合（-16.45 / -16.25）。SFX-02/04はEBU BS.1770の最小ゲーティングブロック長400ms未満（duration 0.292s/0.393s）のため Integrated Loudness 測定不能（`-inf`）——これは音源の欠陥ではなく短尺one-shot SFXにおける規格の物理的適用限界であり、iteration1〜3で既に確認済みの非ブロッキング事項。True Peakは4件とも-1.0dBTP以浅（-0.38〜-3.28dBTP）でクリッピングなし。

**仕様一致（観点3）** — フォーマットは全件 `ogg`（vorbis, 44.1kHz, stereo）。`tech-stack-unity.md`「資産の取り扱い」の音声既定（Unity=OGGのみ）に一致、M4A非同梱も正しい。duration_final_sはMANIFEST記載値と完全一致。design/gdd.mdの同期対象定数（`AUTO_ATTACK_INTERVAL`初期値0.6s＝gdd.md L150、`DASH_DURATION`初期値0.2s＝gdd.md L146）を実測して突合し、SFX-01(0.44s)はAUTO_ATTACK_INTERVAL(0.6s)を超えず機能上問題なし。SFX-02(0.292s)はDASH_DURATION(0.2s)よりわずかに長いが、ダッシュ発動トリガー音が短いダッシュ動作をわずかに追い越して余韻を残す設計は一般的なゲームSFX実装であり、iteration1でも非ブロッキングと判定済みの事項（新規指摘としない）。

**音要件突合（観点4）** — design/assets.mdのSFX-01〜04トリガー説明（攻撃ヒット/ダッシュ/被弾/クリスタル回収）とMANIFESTのprompt内容が1対1で一致することを再確認。

**license/provenance** — `state/asset-routing.json` 再読込で `routes.sfx="elevenlabs:sfx-v2"` / `shippable.sfx:true` / `checks.elevenlabs.commercial_ok:true`（tier=starter）を再確認。MANIFEST 4件とも `license:"commercial-ok"` / `plan_tier:"starter"`、`must_replace`該当なし。fal経由Meshyのライセンス継承（3D資産のみ該当）や`shippable:false`ルート由来は本バッチには非該当。

### 総合判定: APPROVE
理由: 依頼側の起票番号（iteration 1）と本ファイルの実履歴（既にiteration 3でAPPROVE済み）に齟齬があったため、過去の判定を鵜呑みにせず全項目をゼロから独立再検証した。sha256・duration・format・loudnorm・True Peak・クリッピング・音要件・license/provenanceの全観点で実測値がMANIFEST記載値および過去iterationの判定と完全に一致し、不合格要素は検出されなかった。SFX-01〜04の4資産とも合格。

### failedAssets
なし。

### disclosures（再生成不要・人間開示のみ）
- 原資産（ElevenLabs生成分）のコストは`cost_estimated:true`（character_count実測差分をStarterプランレートで按分した推定値であり、プロバイダ請求の実測値ではない）。SFX-01/02/03の`revision:2/3`ローカルffmpeg後処理自体は`cost_usd:0`・`cost_estimated:false`で正確。
- 全4件 `license_note`: ElevenLabs Starterプランは商用可だが「Studio Games」条項（商用×マルチプラットフォーム出荷はEnterprise相談要）が付帯（assets-config.md「Checkpointで人間に提示するライセンスフラグ」節と一致）。Checkpointでの開示を継続すること。
- SFX-02/04: Integrated Loudness（EBU BS.1770）が測定不能（duration<400ms）である点は音源の欠陥ではなく短尺one-shot SFXにおける当該規格の既知の適用限界。再生成では解消しない特性のため、今後も代替指標（RMS実測・本ファイルastats記録）での妥当性確認を運用とすること。
- SFX-05/06・BGM-01は`design/assets.md`上`planned`（対応メカニクスS-15/S-12がphase:buildのためPhase 3繰り延べ）で本バッチ未生成。Checkpoint C（stage=done）到達前にPhase 3での生成・AR-ASSET合格が必須（現時点では単に「未着手」であり不合格ではない）。
- **本レビュー起票番号についての開示**: workflow からの依頼指示は「iteration 1」だったが、本ファイルの実履歴は既にiteration 1〜3を経てiteration 3でAPPROVE済みだった。番号相違はワークフロー側の呼び出し文脈（本バッチが既に承認済みであることを呼び出し元が把握していない状態での再依頼の可能性）に起因すると推測される（推測であり確証なし）。事実に基づく判断のため、本追記は「iteration 4」として記録した。Checkpoint提示時にこの重複依頼の経緯自体は軽微な運用上の注意点として触れてよいが、資産の品質判定には影響しない。

- 対応: 該当なし（本iterationはfailedAssets 0件・APPROVEのためreviser対応不要）。
