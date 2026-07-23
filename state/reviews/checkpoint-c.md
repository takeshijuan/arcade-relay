# CD-CHECKPOINT 履歴 — Checkpoint C（完成品受け渡し）

対象: Crystal Bastion（仮）/ engine=unity / stage=build → done 判定前の最終ゲート。
判定者: creative-director。観点は .claude/docs/gates.md CD-CHECKPOINT（ビジョン一貫性・提示品質・正直さ）。

## CD-CHECKPOINT iteration 1 — CONCERNS
- 日時: 2026-07-23（時刻: 本セッションは Bash 不使用のため `date -u` の機械取得ができず、ISO8601 の秒精度は未取得。日付は実行環境コンテキストの currentDate に基づく）
- 判定: **CONCERNS**（人間へ提示可。ただし下記の revise 対象＝「既知の課題」を要約冒頭で個別警告することが条件）
- ビジョン一貫性（観点1）: 合格。P-01〜P-04 の4ピラー全てが QA 報告のピラー検証表で ok。特に P-02（二種の役割分担）は S-22 で Arc Emitter の AoE 半径バグ（RadiusM=3f が SpotOffsetZ=3f と一致し命中区間幅0＝実質無効）を 4f へ是正し「範囲低火力」の役割が実機能するようになったことを QA が回帰テストで pass 確認済み（未解決事項 JSON 内の旧 S-22 [blocker]「RadiusM 未適用」は、より新しい QA ラウンド 2026-07-22T20:39 で解消済み＝歴史的 finding）。スコープ逸脱（タワー3種目・敵3種目・マップ切替・移設）の混入なし。
- 提示品質（観点2）: 合格水準。qa/report.md が「何を作ったか／acceptance 全通過／既知の妥協点」を5分で判断可能な粒度で提示。スクリーンショット6枚を目視確認済み・ピンク/黒抜けなし。
- 正直さ（観点3）: 合格（隠蔽なし）。下記の [BLOCKER] と縮退は qa/report.md「既知の妥協点」・state/active.md・stories.yaml note・未解決事項 JSON に全て開示済み。要約冒頭での個別警告を条件に提示可とする。
- revise 対象（Checkpoint C 提示物の「既知の課題」冒頭へ個別転記すること・箇条書きに埋没させない）:
  1. **[BLOCKER][INFRA] git commit 不能**: 1Password SSH signing agent 障害でセッション全体を通じ `git commit` が成立せず、S-14 fix・S-22〜S-26・batch-verify 修正・資産取込の一部が working tree 上のみに存在（この worktree の HEAD は S-07 相当まで）。QA は working tree の実コードを検証済み（＝実際に遊べる内容は完成・green）だが、git 上の完成コミットが存在しない。署名バイパスは規約禁止のためエージェントでは解決不可＝人間が 1Password サインイン復旧後にコミット確定する必要がある（完成品受け渡しの完全性に関わる最重要事項）。
  2. **[縮退: 実資産→プレースホルダ] IMG-05/06/07（実績/UPG/敵インジケータ UI アイコン）未 Integrate**: 生成・AR-ASSET 承認済みだが Assets/Resources/Generated/ へ未取込。該当 UI はテキスト/プレースホルダ図形で表示（ピンク破綻なし・acceptance はテキスト表示で充足）。意図されたアイコングラフィックは未反映。
  3. **[縮退: 機能ギャップ] 音量スライダーが Game シーン BGM 本体に反映されない**: S-19 BgmController の既知制約。設定パネルは別 AudioSource インスタンスのため、プレイ中のリアルタイム音量変更が Game の BGM へ伝播しない。
  4. 実機（ビルド済み .app）でのマウス実操作プレイは未実施（サンドボックス制約・GUI 自動操作不可）。検証は PlayMode InputTestFixture 擬似発行のみ。
  5. 資産全 29 行 cost_estimated:true（Meshy クレジット→USD／ElevenLabs レート換算は保守見積・実測でない）。予算 $6.44/$20・超過なし・must-replace なし。
  6. ライセンスフラグ: ElevenLabs「Studio Games」条項（商用×マルチプラットフォーム出荷は Enterprise 相談要）／ Ideogram AI 生成表記条項／ Meshy Pro 確認済み（plan_tier=pro+）／米国純 AI 出力の著作権不確定 → color_correction の人手リタッチ記録が防御材料。
  7. CR-CODE MAX_ITER(2) 到達で非 APPROVE の story 多数（S-10/13/14/15/16/17/18/19/20/22/23/24/25/26）。内訳は (a) 上記1に起因する COMMIT_FAILED プロセス完全性 finding、(b) 後続 QA で解消済みの歴史的 finding、(c) 正当理由明記済みの見送り。review-loops.md 上パイプライン継続は正当（「findings 解消 or 正当理由の明記」の後者）。
- 対応: 上記 revise 対象を Checkpoint C 要約の「既知の課題」冒頭へ個別転記して人間提示する。MAX_ITER=1 のため本 iteration をもって提示可（REJECT ではないため再判定ループは発生しない）。
