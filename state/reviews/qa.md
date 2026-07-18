## QA-PLAY iteration 1 — APPROVE
- 日時: 2026-07-12T23:17:37Z
- 指摘要約: 重大バグなし。中優先度の既知の妥協点2件を記録（MDL-02 swarmerのアニメーション未実装＝rig_type=none縮退による静止移動、BootLoaderのend-to-end PlayMode検証未整備）。いずれも acceptance 判定・重大バグには該当せず APPROVE。
- 対応: （該当なし・iteration 1 で APPROVE のため revise 不要）

## QA-PLAY iteration 2 — APPROVE（フルQA round 1・全story回帰）
- 日時: 2026-07-13T11:47:10Z
- 対象: state/stories.yaml 全23story（prototype S-01〜S-11 + build S-12〜S-23）の acceptance 回帰検証。build 相当（ForgeBuild.BuildMac）exit0、EditMode 160/160、PlayMode 109/109、いずれも exit0・console/ログエラー0（NullReferenceException/MissingReferenceException 0件）。Title→Menu→Game→Result→Menu 1周（`ResultSceneTests.FullLoop_...`）、メタ進行永続化（保存→新規インスタンスLoad一致／破損4種（パース不能・save_version欠落=スキーマ不正ケース・ゼロ版・未来版）→.bak退避+`[SaveCorruption]`1回+recovered=true）を全てPlayModeテストで確認。9枚のスクリーンショットをmagick機械検知（SUSPECT_BLANK 0件）→全件Readで目視、8枚は判読可能・意図した被写体を確認、1枚（qa-swarmer-closeup.png）は被写体欠落を確認。
- 指摘要約（優先度順）:
  1. [minor / gameplay-engineer] `QaPlayEvidenceTests.Capture_Swarmer_Closeup` のQA証跡スクリーンショットが空/地平線のみで swarmer モデルが写っていない。S-23 で追加された `ArenaCameraRig.Update()` の毎フレーム位置上書きが、テストのカメラ手動配置を無効化する回帰。swarmer自体の健全性は `EnemyVisualMotionSceneTests`（機械検証・pass）で別途確認済みのため acceptance には非影響。詳細・再現手順は `qa/report.md`「フルQA round 1 追記」中・軽微バグ#1。
  2. [継続・新規ではない] S-20/S-22 の南側可視性残存制約（ARENA_RADIUS南端の完全カバー未達）。既に state/reviews/s-20.md・state/active.md で開示・Checkpoint C 申し送り済みのため今回新規指摘としては計上せず、ピラー検証所見(P-01/P-03)で確認継続を記録。
  3. [開示のみ] S-01/S-08/S-19 が stories.yaml 上 status=review のまま（CR-CODE手続き上の持ち越し）。acceptance自体は全通過。
- 判定根拠: 重大バグ0・acceptance 23/23 pass（fail 0）のため review-loops.md 合格基準を満たし APPROVE。中優先度バグ1件はQA証跡の内容欠落でありゲームプレイ・acceptance判定に影響しないため非ブロッキング。
- 対応: （qa-lead 発行のiteration。gameplay-engineer側の対応は次回サイクルまたはPolishで記入予定）

## QA-PLAY iteration 3 — APPROVE（フルQA round 2・Phase 3 Visual Brushup 全33story回帰）
- 日時: 2026-07-14T15:07:39Z
- 対象: state/stories.yaml 全33story（prototype S-01〜S-11 + build S-12〜S-33。今回追加分 S-22改修・S-24〜S-33）の acceptance 回帰検証。build相当（ForgeBuild.BuildMac）exit0、EditMode 181/181（round1の160から+21）、PlayMode 142/142（round1の109から+33）、いずれもexit0・console/ログエラー0（NullReferenceException/MissingReferenceException/InternalErrorShader 0件）。Title→Menu→Game→Result→Menu 1周、メタ進行永続化（保存→新規インスタンスLoad一致／破損4種→.bak退避+`[SaveCorruption]`1回+recovered=true）を全てPlayModeテストで再確認。12枚のスクリーンショットをmagick機械検知（SUSPECT_BLANK 0件）→全件Readで目視、11枚は判読可能・意図した被写体を確認、1枚（qa-swarmer-closeup.png）はround1既報の被写体欠落バグが継続。
- 指摘要約（優先度順）:
  1. [minor / gameplay-engineer / 継続] `QaPlayEvidenceTests.Capture_Swarmer_Closeup` の証跡スクリーンショットにswarmerモデルが写っていない回帰（`ArenaCameraRig.Update()`によるカメラ位置上書き）。round1 iteration2から継続、未対応。swarmer健全性自体は`EnemyVisualMotionSceneTests`で機械検証済みでacceptance非影響。
  2. [開示のみ・重要・コード外] S-27後半〜S-33の実装一式が1Password SSH署名ブロッカーによりgit commit未達のままworking treeに残存（`git status --short`250件超）。QA自体はworking treeに対し実施し機能面は全て確認済みだが、Checkpoint Cで最優先の人間申し送り事項とすべき。
  3. [開示のみ] `state/stories.yaml` S-24/S-25のstatus表記が`todo`のまま（実体はAR-ASSET iteration4でAPPROVE済み・S-26/S-30が依存実装済み）。記帳漏れ、acceptance内容自体は充足。
  4. [継続・新規ではない] S-20/S-22 南側可視性残存制約（ARENA_RADIUS南端の完全カバー未達）。既存開示継続、新規指摘としては非計上。
- 判定根拠: 重大バグ0・acceptance 33/33 pass（fail 0）のためreview-loops.md合格基準を満たしAPPROVE。中・軽微バグ1件（継続・証跡内容欠落のみ）は非ブロッキング。プロセスリスク（git commit未反映）はQA-PLAY判定対象（実際に動くgame/working tree）には影響しないため非ブロッキングとしつつ、Checkpoint Cへの最優先申し送り事項として明記。
- 対応: （qa-lead発行のiteration。gameplay-engineer/tech-directorへの対応は次サイクルで記入予定）
