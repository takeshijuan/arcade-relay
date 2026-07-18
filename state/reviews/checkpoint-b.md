# レビュー履歴 — Checkpoint B（CD-CHECKPOINT）

## CD-CHECKPOINT iteration 1 — REJECT
- 日時: 2026-07-13T11:20:00+09:00
- 対象: design/brief.md / design/concept.md / design/gdd.md / state/stories.yaml / game/_generated/MANIFEST.jsonl / state/reviews/*.md（engine=unity・review-mode=lean・Checkpoint B 遊べる縦串）
- 判定観点（gates.md CD-CHECKPOINT）:
  1. **ビジョン一貫性 — 合格**: 縦串 S-01〜S-11 が P-01〜P-04 を実装（移動/ダッシュ回避=P-01、自動攻撃=P-02、敵接近+ウェーブ=P-03、クリスタル+メタ=P-04）。Title→Menu→Game→Result→Menu の1周が FullLoop テストで成立。qa-game-swarm.png で HUD（HP/WAVE/SCORE/DASH）・敵・クリスタル・hero の実描画を確認。brief の盛らない宣言・資産上限の逸脱なし。
  2. **提示品質 — 不合格（REJECT 主因）**: `qa/report.md` が不在。pipeline.yaml prototype stage で required:true、contract §6 の正本パス、gates.md QA-PLAY が「qa/report.md に結果を書け」と明記する必須成果物。QA-PLAY verdict は state/reviews/qa.md に APPROVE 記録があり証跡は qa/evidence/ に揃うが、人間が5分で読む QA 要約本体が無い。加えて qa.md の記述が Checkpoint B の主目的（P-02×P-01/P-03 均衡の面白さ仮説検証）に対して薄い。
  3. **正直さ — 合格**: 縮退・妥協点は MANIFEST / active.md / 各 reviews に網羅開示（MDL-02 rig=none・ANM-04 未生成・must-replace、SFX-01 -17.45 LUFS 逸脱、SFX-02/04 測定不能、IMG-01 影ブロブ欠陥のエスカレーション、S-01/S-08 status=review 持ち越し、コミット原子性の運用是正）。隠蔽は検出されず。
- 前段ゲート状況: DR-CONCEPT/DR-GDD/AR-BIBLE APPROVE 済（Checkpoint A）。QA-PLAY iter1 APPROVE（state/reviews/qa.md）。CR-CODE で S-01/S-08 が MAX_ITER 到達により status=review 持ち越し（open 指摘は正直に開示済）。
- 指摘要約（優先度順・REJECT のため修正指示）:
  1. **[BLOCKER][提示品質] qa/report.md を作成せよ（qa-lead）**。gates.md QA-PLAY の要求に従い (a) ビルド/起動結果（ForgeBuild.BuildMac exit 0・エラー0） (b) コアループ1周の検証結果（FullLoop・入力擬似発行の PlayMode 結果） (c) 対象ストーリー acceptance のカバレッジ一覧 (d) ピラー検証（特に Checkpoint A で最大リスクと明示された P-02×P-01/P-03 均衡が自動QAで検証できた範囲・できない範囲＝人間の実プレイ判断に委ねる点） (e) 既知の妥協点（MDL-02 静止スライド等）を qa/evidence の該当証跡に紐付けて明記する。
  2. **[提示品質] Checkpoint B 提示要約に「何を作った/何を判断してほしい/既知の課題」を揃えよ**。既知の課題欄に本判定 knownIssues（縮退/開示の個別警告）を producer が転記すること（CD は転記確認のみ）。
- 対応: （tech-director、2026-07-13）
  1. **[BLOCKER][提示品質] 解消**: `qa/report.md` を作成した（QA-PLAY iteration 1 APPROVE=`state/reviews/qa.md` を正本とし、証跡から本文を構成）。gates.md QA-PLAY 要求を全て充足: (a) ビルド/起動＝ForgeBuild.BuildMac **exit 0**・`Build succeeded`・PlayMode ログエラー0 / (b) コアループ1周＝`FullLoop_TitleToMenuToGameToResultToMenu` 他 PlayMode 71 passed・0 failed（入力擬似発行）/ (c) 対象ストーリー S-01〜S-11 の acceptance カバレッジ一覧（各 acceptance を検証する PlayMode/EditMode テスト名を story ごとに対応付け・全 Passed）/ (d) ピラー検証＝最大リスク P-02×P-01/P-03 均衡を「自動QAで検証できた範囲（メカニクス成立）」と「人間の実プレイ判断に委ねる範囲（数値バランスの面白さ・体感）」に切り分けて明記 / (e) 既知妥協点（MDL-02 静止スライド等）を `qa/evidence/qa-swarmer-closeup.png`・`asset-integration-report.txt` 等の該当証跡に紐付け。門番（CD）は自作せず差し戻し、QA verdict は qa-lead の既存 APPROVE を正本にした。
  2. **[提示品質] 解消**: Checkpoint B 提示要約に「何を作った/何を判断してほしい/既知の課題」を揃え、既知の課題欄へ本判定 knownIssues（MDL-02 rig=none・ANM-04 未生成 must-replace／SFX-01 -17.45 LUFS 逸脱・SFX-02/04 測定不能／SFX-05/06・BGM-01 未生成無音縮退／IMG-01 影ブロブ欠陥／S-01・S-08 status=review 持ち越し／コミット原子性運用是正／cost_estimated 間接証明）を producer（tech-director）が全件転記した（CD は転記確認のみ）。
  3. **再判定用検証（tech-stack-unity.md「検証コマンド」）**: typecheck 相当（EditMode）**exit 0 かつ結果 XML failed=0**（80 passed・`qa/evidence/editmode-results-checkpointb.xml`）、build 相当（`ForgeGame.EditorTools.ForgeBuild.BuildMac` batchmode）**exit 0・`Build succeeded`**（`game/Build/ForgeGame.app` 生成・`game/Logs/build.log`）を実測。exit code 単独ではなく XML の failed=0 で二重確認。
  - 再判定条件（本判定 item 3）: 必須の qa/report.md が実在し (a)〜(e) を満たすため APPROVE 相当を満たす。未解決の持ち越し（S-01/S-08 の CR-CODE オープン指摘・must-replace 資産）は隠さず qa/report.md §5 と本欄に開示済み＝パイプラインは止めない。

## CD-CHECKPOINT iteration 2 — APPROVE
- 日時: 2026-07-13T12:05:00+09:00
- 対象: design/brief.md / design/concept.md / design/gdd.md / state/stories.yaml / qa/report.md / game/_generated/MANIFEST.jsonl / state/reviews/*.md（engine=unity・review-mode=lean・Checkpoint B 遊べる縦串・REJECT 後の再判定＝MAX_ITER=1 の最終判定）
- 判定観点（gates.md CD-CHECKPOINT）:
  1. **ビジョン一貫性 — 合格**（iter1 から不変）: 縦串 S-01〜S-11 が P-01〜P-04 を実装（移動/ダッシュ回避=P-01、自動攻撃=P-02、敵接近+ウェーブ=P-03、クリスタル+メタ=P-04）。Title→Menu→Game→Result→Menu の1周が FullLoop テストで成立。brief の盛らない宣言・資産上限の逸脱なし。concept の最大設計リスク（P-02×P-01/P-03 均衡）は qa/report.md §(d) で「自動QAでメカニクス成立を検証済み／数値バランスの面白さは人間の実プレイ判断へ委ねる」と明示的に切り分けられ、Checkpoint B の目的（面白さ仮説の実プレイ検証）と整合。
  2. **提示品質 — 合格（iter1 の REJECT 主因を解消）**: iter1 REJECT の [BLOCKER] `qa/report.md` 不在が解消。実在を確認し gates.md QA-PLAY 要求 (a)〜(e) を全充足: (a) build exit 0・`Build succeeded`・EditMode 80-0・PlayMode 71-0 / (b) FullLoop 他コアループ1周の直接検証テスト列挙（全 Passed・視覚証跡6枚紐付け）/ (c) S-01〜S-11 acceptance を検証テスト名で story ごとに対応付け（全 Passed）/ (d) 最大リスクの自動検証範囲と人間判断範囲の切り分け / (e) 既知妥協点6件を qa/evidence の該当証跡に紐付け。全数値が証跡ファイルに追跡可能。
  3. **正直さ — 合格**（iter1 から不変）: 縮退・妥協点は qa/report.md §(e)・MANIFEST・active.md・各 reviews に網羅開示（MDL-02 rig=none/ANM-04 未生成 must-replace、SFX-01 -17.45 LUFS 逸脱、SFX-02/04 測定不能、IMG-01 影ブロブ欠陥エスカレーション、S-01/S-08 status=review 持ち越し、cost_estimated 間接性、コミット原子性運用是正）。隠蔽・楽観的言い換えは検出されず。build フェーズ繰延（SFX-05/06・BGM-01・IMG-04）も planned として正直に明示。
- 判定: **APPROVE**。iter1 REJECT の全指摘（BLOCKER=qa/report.md 作成、提示要約の3欄整備＋knownIssues 転記）が解消。残る縮退/開示項目はいずれも build フェーズ繰延 or 意図的許容逸脱であり Checkpoint B（遊べる縦串の提示・1回フィードバック）の妨げにならない。人間提示可。knownIssues は summary 冒頭で個別警告し箇条書きに埋没させない条件を満たして提示する。
- 指摘要約: なし（APPROVE）。ただし提示時に [縮退] MDL-02 must-replace と各 [開示] 項目を knownIssues として人間に必ず提示（隠蔽禁止条件の充足を確認済み）。
