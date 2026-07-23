# state/reviews/qa.md — QA-PLAY レビュー履歴

## QA-PLAY iteration 1 — CONCERNS
- 日時: 2026-07-21T13:50:00Z
- 指摘要約（優先度順）:
  1. [重大・優先1] Title/Menu/GameHud のクリック起点の遷移・パネル開閉が PlayMode テストで9/45件 fail（`TitleScreenPlayModeTests.LeftClick_TransitionsToMenu`/`AnyKey_TransitionsToMenu`、`MenuScreenPlayModeTests.ClickPlayButton_TransitionsToGame`/`ClickBackToTitleButton_TransitionsToTitle`、`GameHudPlayModeTests` 5件）。S-02/S-03/S-08 の acceptance が直接この失敗テストを指定しているため acceptance FAIL 扱い。3回の独立実行全てで同一内容が再現（pre-existing・Integrate起因ではないことをstash比較で確認済み）。同一クリック機構を使う `ResultScreenPlayModeTests` とフルループテストは pass しており、実行文脈依存の再現条件が疑われる（ui-engineer要調査）。
  2. [参考] 実機ビルド（`game/Build/ForgeGame.app`）でのマウス実操作確認は本サンドボックス環境（画面収録権限なし）では実施不能だったため、PlayModeテストの`InputTestFixture`擬似発行のみで判定した旨を明記。
  3. [軽微] 視覚証跡PlayModeテストが未整備だったため qa-lead が `game/Assets/Tests/PlayMode/QaVisualEvidencePlayModeTests.cs` を新規追加（Title/Menu/Game/Result の RenderTexture スクリーンショット＋マテリアル欠落/NaN/カメラ向き検査）。4件全pass、証跡はqa/evidence/qa-visual-*.pngで目視確認済み。
- 対応: 未対応（次ラウンドでui-engineerが指摘1を修正すること。指摘3はqa-lead側で追加実装済みのためengineer対応不要）。

## QA-PLAY iteration 2 — APPROVE
- 日時: 2026-07-22T03:26:16Z
- 指摘要約: なし。iteration 1 の重大バグ#1（Title/Menu/GameHud のクリック起点遷移9件failure）は解消を確認した。
  qa-lead が独立に `game/Build` 削除→再ビルド（exit 0）→EditMode（48/48 passed, exit 0）→PlayMode（50/50 passed,
  exit 0・安定性確認のため2回連続実行）を実行し、iteration 1 で fail していた
  `TitleScreenPlayModeTests.LeftClick_TransitionsToMenu`/`AnyKey_TransitionsToMenu`、
  `MenuScreenPlayModeTests.ClickPlayButton_TransitionsToGame`/`ClickBackToTitleButton_TransitionsToTitle`、
  `GameHudPlayModeTests` 5件すべてが pass することを一次証跡（`qa/evidence/playmode-results-qa2.xml`・
  `qa/evidence/playmode-results-qa2-rerun.xml`）で確認した。prototype phase 全9ストーリー（S-01〜S-09）の
  acceptance 全通過、必須シーン遷移1周（Title→Menu→Game→Result→Menu）pass、永続化（復元一致・破損復旧3種）pass、
  視覚証跡4枚を機械検知（mean値）＋Read目視で確認（黒画面・文字欠落・ピンクマテリアルなし）。
  P-03（溶ける実感の演出）はS-13/S-19未実装のためconcern注記のまま持ち越すが、prototype範囲では許容（build phaseで再検証）。
- 対応: 対応不要（APPROVE）。gameplay-engineer/ui-engineer による b3ad6e8 修正が有効であったことを本QAで確認した。

## QA-PLAY iteration 3（フルQA round 1・phase:build 全story回帰） — APPROVE
- 日時: 2026-07-22T20:53:08Z
- 対象: state/stories.yaml 全story（phase:prototype S-01〜S-09 + phase:build S-10〜S-26、計26ストーリー）の
  acceptance を1件ずつ回帰検証。バッチ検証（`state/reviews/batch-verify.md`「phase: build (Polish)」節）が
  既に EditMode 93/93・PlayMode 117/117 を報告していたが、qa-lead が独立に `$UNITY -batchmode -runTests`
  （EditMode/PlayMode）と `ForgeBuild.BuildMac` を一次実行し、同一の合格結果（EditMode 93/93 passed・build
  exit 0・PlayMode 117/117 passed）を再現・確認した。console/ログエラー0（ホワイトリスト済み想定内ログのみ）。
  必須シーン遷移 Title→Menu→Game→Result→Menu の1周、永続化（保存/復元一致・破損セーブ2種の復旧プロトコル）も
  全てpass。全26ストーリーでacceptance FAIL 0件。ピラー検証（P-01〜P-04）は全てok（P-03は前回prototype QAで
  concern指摘した撃破演出未実装がbuild phaseのS-13/19/24/25/26で解消されたことを確認）。
  qa-lead は視覚証跡の質向上のため `game/Assets/Tests/PlayMode/QaVisualEvidencePlayModeTests.cs` の
  Game画面撮影テストへ `BuildSpotController.TryPlaceTower`（実ゲーム入口APIと同一）によるタワー2種設置を追加し、
  「開始直後の空盤面」ではなくタワー設置後の盤面を撮影するよう修正した（変更後もPlayMode 117/117 pass再確認済み）。
  全6スクリーンショットを機械検知（mean値0.02〜0.98範囲内）＋Read目視で確認（黒画面・文字欠落・ピンクマテリアルなし）。
  詳細は `qa/report.md`（本ラウンドの正式版に更新）参照。
- 既知の妥協点（重大バグではないがCheckpointで開示すべき事項）:
  1. [INFRA] 1Password SSH signing agent 障害により、working tree上のS-22〜S-26・S-23等の変更がgit未コミットの
     まま（HEAD `290839d`以降）。本QAはworking treeの実コードを対象に検証したためAPPROVE判定に影響しないが、
     人間のサインイン復旧後にコミット確定が必要。
  2. [ASSET] IMG-05/06/07（実績/UPG/敵インジケータのUIアイコン画像）がAR-ASSET承認済み・MANIFEST記録済みだが
     `Assets/Resources/Generated/textures/`へ未Integrate（`ForgeAssetIntegration.cs`が引き続きPLANNED計上）。
     該当UIはテキスト/プレースホルダ表示のみでacceptance自体は満たすが、意図されたアイコングラフィックは未反映。
- 対応: 対応不要（APPROVE）。上記既知の妥協点はCD-CHECKPOINTでの開示対象として引き継ぐ。
