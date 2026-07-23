# QA Report — Crystal Bastion（仮）/ 対象フェーズ: build（フルQA・全story回帰）

## 総合判定

```
QA-PLAY: APPROVE
```

build/EditMode/PlayMode すべて exit 0・全件 pass（EditMode 93/93・PlayMode 117/117、qa-lead が独立に一次実行）。
phase:prototype（S-01〜S-09）+ phase:build（S-10〜S-26）全26ストーリーの acceptance が PlayMode/EditMode テストで
pass、必須シーン遷移1周・永続化（復元一致/破損復旧2種）も pass。console/ログエラー0（ホワイトリスト済み想定内ログのみ）。

（本セクション以下は本ラウンド〔build phase・全story回帰、2026-07-22T20:39:04Z〜20:43:42Z〕の記録。
旧 prototype 単独QAの記録はファイル末尾「附録」に退避した。）

## 実行環境

- 日時: 2026-07-22T20:39:04Z 〜 2026-07-22T20:43:42Z（本ラウンド・qa-lead による独立フル再実行）
- エンジン: unity 6000.3.16f1
- OS: macOS 25.5 (Darwin 25.5.0)
- 実行系: `/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity`（state/engine-info.json）
- 検証対象: git HEAD `290839d`（"S-19: status done"）+ working tree 上の未コミット変更（S-22〜S-26・S-23・
  batch-verify 記録・qa-lead 追加分含む）。**[既知の妥協点] 参照**: 1Password SSH signing agent 障害により
  本セッション中も一部コミットが成立していない（HEAD以降の作業ツリー差分として存在）。本QA はコミット状態ではなく
  working tree の実際のコード（＝実際にビルド・テストされる内容）を対象に検証した。

## ビルド結果

| コマンド | 結果 | 備考 |
|---|---|---|
| EditMode テスト（typecheck相当） | exit 0 / 93 total, 93 passed, 0 failed | `qa/evidence/qa-round1-editmode-results.xml` |
| `ForgeBuild.BuildMac`（build相当） | exit 0 | `game/Logs/qa-round1-build.log` に `Build succeeded: 523282667 bytes -> Build/ForgeGame.app`。`game/Build/ForgeGame.app` 生成確認済み |
| PlayMode テスト（QA用・全story回帰） | exit 0 / 117 total, 117 passed, 0 failed | `qa/evidence/qa-round1-playmode-results.xml`（qa-lead が Game 画面撮影テストへタワー設置を追加した後の最終実行結果） |

補足: `state/reviews/batch-verify.md`「phase: build (Polish)」節（gameplay-engineer による直列バッチ検証、
2026-07-22T20:30:44Z）で同一 working tree に対し EditMode 93/93・PlayMode 117/117 が報告済みだったが、本QAでは
その報告を転記せず、qa-lead が独立に `$UNITY -batchmode -runTests` を再実行し同一の合格結果を一次証跡として
再現・確認した。

## Console / ログエラー

- エラー件数: 0（`qa/evidence/qa-round1-playmode.log` を grep — ゲームコード起因の未ホワイトリストエラー無し。
  117/117 passed の内訳自体が `LogAssert.NoUnexpectedReceived()` 相当のホワイトリスト機構を通過したことを示す）
- warning 件数: 多数（Unity標準の Package/Shader/ライセンスモジュール由来。ゲームコード起因の異常なし）
- 詳細（意図的シミュレーション・ホワイトリスト済みのもののみ観測）:
  - `[SaveCorruption] reason=ParseFailed|VersionMissing|SchemaInvalid backup=...`（4件、`FileSaveStorePlayModeTests`
    内 `LogAssert.Expect` でホワイトリスト済みの想定内破損復旧テスト）
  - `[SaveFailure] MenuScreen failed to persist SaveData after UPG purchase: ...`／
    `[SaveFailure] RunOutcomeController failed to persist SaveData after run finalize...`（各1件、意図的な
    永続化失敗シミュレーションテストで `LogAssert.Expect` によりホワイトリスト済み）
  - `[AudioSettingsLoad] failed to parse .../audio-settings.json (ArgumentException); using defaults.`（
    `FileAudioSettingsStorePlayModeTests` の不正JSON入力テストでホワイトリスト済み）
  - それ以外のエラー/例外なし

## Acceptance 検証表

| S-xx | acceptance（要約） | 検証操作（テスト） | 判定 | 証跡パス |
|---|---|---|---|---|
| S-01 | Boot→Title自動遷移・GameFlow共通API・RunResultキャリー | `GameFlowPlayModeTests` 3件 | pass | qa/evidence/qa-round1-playmode-results.xml |
| S-02 | Title: ScreenSpaceCamera・クリック/任意キーでMenuへ遷移・recovered通知 | `TitleScreenPlayModeTests` 5件 | pass | 同上 |
| S-03 | Menu必須4要素の実在・「プレイ開始」→Game・「タイトルへ」→Title | `MenuScreenPlayModeTests`（RequiredFourElements_Exist 他4件） | pass | 同上 |
| S-04 | 経路/ウェーブ/コア防衛（NaN無し・敗北成立） | `CoreDefensePlayModeTests` 1件 + `WaveSpawnSystemTests`（EditMode、経路線形性・移動） | pass | 同上、qa-round1-editmode-results.xml |
| S-05 | ビルドスポット設置・タワー自動攻撃・撃破報酬 | `TowerCombatPlayModeTests`（PlacingBastionCannon_KillsMarauderInRange_AndGrantsGoldReward 他2件） | pass | qa-round1-playmode-results.xml |
| S-06 | 勝敗判定・RunResult確定・二重保存なし・Result遷移 | `RunOutcomePlayModeTests` 3件 | pass | 同上 |
| S-07 | メタ進行永続化（保存/復元一致・破損復旧プロトコル） | `FileSaveStorePlayModeTests` 7件 | pass | 同上 |
| S-08 | Game HUD表示・タワー選択メニュー開閉・資金/コアHP反映 | `GameHudPlayModeTests` 8件 | pass | 同上 |
| S-09 | Result: 勝敗/スコア/実績表示、「もう一度」→Game、「メニューへ」→Menu | `ResultScreenPlayModeTests`（コア表示系5件+FullLoop） | pass | 同上 |
| S-10 | タワーアップグレードLv2/Lv3・役割不変・UPG-02割引 | `TowerUpgradeSystemTests`（EditMode 7件）+ `TowerCombatPlayModeTests.TryUpgradeTower_Lv1ToLv2ToLv3_...`+ `TowerActionPanelPlayModeTests`（UI到達経路。S-23で解消） | pass | 同上・qa-round1-editmode-results.xml |
| S-11 | タワー売却（返還率・スポット解放） | `BuildSpotCombatSystemTests`（EditMode、TrySell系6件）+ `TowerCombatPlayModeTests.TrySellTower_*`2件 + `TowerActionPanelPlayModeTests.ClickSellButton_...` | pass | 同上 |
| S-12 | 全8波難易度曲線（Warbeast混成・スポーン間隔・準備フェーズ） | `WaveSpawnSystemTests`（WaveComposition_MatchesGddDifficultyCurveTable_ForAllEightWaves 他2件） | pass | qa-round1-editmode-results.xml |
| S-13 | 撃破帰属AoE分離集計・FeedbackCueSystem演出キュー | `FeedbackCueSystemTests`（EditMode 17件）+ `AssetIntegrationPlayModeTests`（SFXイベント再生系） | pass | 同上 |
| S-14 | UPG-01/02/03効果反映・essence購入 | `ScaffoldSmokeTests`（ComputeStartingGold/ComputeTowerDiscountRate/TryPurchaseUpgrade 3件）+ `BuildSpotControllerUpgradePlayModeTests` 4件 | pass | 同上 |
| S-15 | Menuアウトゲームパネル（実績/統計/essence・UPG購入UI） | `MenuScreenPlayModeTests`（OutgamePanel_ShowsAchievementProgressBarsForAch03AndAch04・ClickUpg01BuyButton_*2件） | pass | qa-round1-playmode-results.xml |
| S-16 | 設定パネル音量スライダー実効化・永続化 | `MenuScreenPlayModeTests`（ChangeBgm/SfxVolumeSlider・ClickNearRightEdge系3件）+ `FileAudioSettingsStorePlayModeTests` 11件 | pass | 同上 |
| S-17 | 一時停止オーバーレイ（Esc・timeScale=0・再開/設定/タイトル） | `PausePanelPlayModeTests` 9件 | pass | 同上 |
| S-18 | ホバーハイライト・射程プレビュー円 | `HoverPreviewPlayModeTests` 3件 | pass | 同上 |
| S-19 | MDL View割当・SFX/BGM再生配線 | `AssetIntegrationPlayModeTests` 8件（Renderer健全性・SFX/BGM再生・スクリーンショット証跡） | pass | 同上、qa/evidence/s19-asset-integration-placed-models.png |
| S-20 | 破損復旧トースト・未記録「--」表示 | `MenuScreenPlayModeTests`（RecoveredFlag/SaveFailedFlag系4件）+ `ResultScreenPlayModeTests`（Unrecorded/Recorded/SaveFailed系4件） | pass | 同上 |
| S-21 | 環境ビジュアル本仕上げ（地形タイル・URPライト・固定俯瞰カメラ） | `EnvironmentConfigTests`（EditMode 4件）+ `EnvironmentPlayModeTests` 3件 | pass | qa-round1-editmode-results.xml, qa/evidence/s21-environment-composition.png |
| S-22 | Arc Emitter AoE半径バグ修正（RadiusM 3f→4f） | `BuildSpotCombatSystemTests.Tick_ArcEmitter_MarauderTraversingSearchRange_DamagesAtLeastOnce_RegressionForZeroWidthCoverage` | pass | qa-round1-editmode-results.xml |
| S-23 | タワー強化/売却パネルUI実装（S-10/11到達不能ブロッカー解消） | `TowerActionPanelPlayModeTests` 7件 | pass | qa-round1-playmode-results.xml |
| S-24 | タワー発射モーション（照準追従+リコイル演出） | `TowerCombatPlayModeTests.TowerFires_RotatesTowardTargetAndPlaysRecoilScalePunch_ThenReturnsToDefault` | pass | 同上 |
| S-25 | 敵撃破演出（スケールダウン+撃破ビート） | `TowerCombatPlayModeTests.DefeatedEnemyView_PlaysScaleDownMotion_BeforeBeingDestroyed` | pass | 同上 |
| S-26 | コア被弾ヒットフラッシュ+スケールパルス | `CoreHitFlashPlayModeTests.ApplyDamage_TriggersHitFlashAndScalePulse_ThenReturnsToDefault_AfterCoreHitFlashSec` | pass | 同上 |

全26ストーリー（S-01〜S-26）で acceptance FAIL 0件。

## 必須シーン遷移と永続化（gates.md QA-PLAY 観点2/5）

| 検証 | 手段（テスト名/操作列） | 判定 | 証跡パス |
|---|---|---|---|
| Title → Menu → Game → Result → Menu の1周 | `ResultScreenPlayModeTests.FullLoop_TitleMenuGameResultMenu_CompletesRequiredSceneCycle`（実クリック擬似発行で全遷移） | pass | qa-round1-playmode-results.xml |
| セーブ → 再起動相当 → 復元一致 | `FileSaveStorePlayModeTests.SaveThenLoad_WithNewStoreInstance_RestoresAllFields`（`Application.temporaryCachePath` の一意ディレクトリ使用・実ユーザーセーブ非汚染） | pass | 同上 |
| 破損セーブ → .bak 退避 + [SaveCorruption] エラー1回 + 既定値復旧 | `FileSaveStorePlayModeTests.Load_UnparsableJson_...`（パース不能）／`Load_SchemaInvalid_MissingSaveVersion_...`（save_version欠落）／`Load_SchemaInvalid_NegativeField_...`（スキーマ不正=型/値不正）の3種。いずれも`.bak.<UTC>`生成・`[SaveCorruption]`ログ1回（`LogAssert.Expect`機械検証）・`recovered=true`を確認。実ログでも`reason=ParseFailed/VersionMissing/SchemaInvalid`を実測 | pass | 同上, qa/evidence/qa-round1-playmode.log |
| 音量設定の保存/復元（付帯） | `FileAudioSettingsStorePlayModeTests` 11件 + `MenuScreenPlayModeTests`（ChangeBgm/SfxVolumeSlider...PersistsAcrossReload） | pass | qa-round1-playmode-results.xml |

## スクリーンショット目視所見（必須）

全て `QaVisualEvidencePlayModeTests`（RenderTexture方式）で本ラウンド内に再撮影。qa-lead が Game 画面撮影テストに
`BuildSpotController.TryPlaceTower`（実際のゲーム入口API と同一）でタワー2種設置を追加し、「開始直後の空盤面」
にならないよう修正した（変更後も PlayMode 117/117 pass を再確認済み）。機械検知（`magick identify -format
"%[fx:mean]"`）は全て0.02〜0.98の範囲内でSUSPECT_BLANK無し。全画像をReadで目視確認済み。

| 証跡パス | mean 値 | 目視所見（何が写っているか） | 判定 |
|---|---|---|---|
| qa/evidence/qa-visual-title.png | 0.1406 | 濃紺背景に「CRYSTAL BASTION」の白文字タイトル中央、下部に「クリック、または任意のキーで開始」の水色文字が判読可能 | ok |
| qa/evidence/qa-visual-menu.png | 0.1656 | 「MENU」見出し、「プレイ開始」ボタン、「アウトゲーム表示」見出し下に総ラン数/総勝利数/総撃破数/ハイスコア/ベストクリアタイム/essence/UPG-01〜03 Lv、UPG購入ボタン3個（不足(30)表示）、実績5件（ACH-01〜05 [未獲得]表記+ACH-03/04進捗バー）、「設定」見出し下にBGM/SFX音量スライダーと操作説明文、「タイトルへ戻る」ボタンが全て判読可能 | ok |
| qa/evidence/qa-visual-game.png | 0.2696 | 俯瞰カメラで緑の地面と茶色の一本道、右端にコアクリスタル（水色発光）を確認。HUD左上に「資金: 10G」「コアHP: 100/100」「ウェーブ: 1/8」の文字が判読可能。経路脇にBastion Cannon（塔状）とArc Emitter（ドーム状）の2タワーモデルが実際に設置された状態で写っている（qa-lead が実配置操作を追加。資金が100→10Gに減少していることも確認でき配置が実際にEconomySystemへ反映されていることの視覚的裏付け） | ok |
| qa/evidence/qa-visual-result.png | 0.1441 | 「勝利」の緑文字見出し、「スコア: 989」「ハイスコア: 0」「ベストクリアタイム: −」「新規実績: なし」、「もう一度」「メニューへ」ボタンが判読可能 | ok |
| qa/evidence/s19-asset-integration-placed-models.png | 0.3838 | 近接カメラでBastion Cannon・Arc Emitterの2タワー、Marauder（小型二足）敵、コアクリスタルの4モデルが緑がかった中間色背景に鮮明に表示され、ピンク（マテリアル欠落）や黒抜けは無い | ok |
| qa/evidence/s21-environment-composition.png | 0.2696 | qa-visual-game.png と同一構図（S-21固定俯瞰カメラ構図の検証用）。地面・経路タイルテクスチャとコアクリスタルが判読可能 | ok |

## ピラー検証所見

| P-xx | 所見（実プレイ感） | 判定 |
|---|---|---|
| P-01（一手必中の配置） | 設置後の無償移設は未実装のまま（設計方針どおり）。売却（S-11・TOWER_SELL_REFUND_RATE）のみ許容され、`TowerActionPanelPlayModeTests`で強化/売却パネルの実クリック操作が機械検証済み。ビルドスポットのクリック判定は「占有有無で走査を分けない」構造修正（S-23で発見・修正）により固定俯瞰カメラの近接2スポットでも誤爆しないことを確認 | ok |
| P-02（二種の役割分担） | `TowerType.BastionCannon`（単体高火力）/`ArcEmitter`（範囲低火力）の2種のみ。アップグレード（S-10）はダメージのみ増加し役割・射程・間隔は不変（`AfterUpgrade_BastionCannon_FiresLv2ThenLv3Damage_RoleUnchanged`で機械検証）。S-22でArc EmitterのAoE半径バグ（実質無効化）を是正し「範囲低火力」の役割が実際に機能するようになった | ok |
| P-03（溶ける実感） | prototype QA時点でconcern指摘した撃破演出未実装は、S-13(FeedbackCueSystem)・S-19(SFX/BGM配線)・S-24(発射リコイル)・S-25(撃破スケールダウン)・S-26(コア被弾フラッシュ)で全て実装済みとなり機械検証（`TowerCombatPlayModeTests`/`CoreHitFlashPlayModeTests`）でpass。qa-visual-game.pngでタワー設置後の見た目も確認できた。既知の落とし穴4（batchmode高速フレーム）はテスト側で対処済みで演出自体の実装に欠陥は無い | ok |
| P-04（負けても伸びる防衛網） | `RunOutcomePlayModeTests`（勝敗いずれもメタ進行へ確定保存・二重保存なし）、UPG-01/02/03効果反映（S-14）、essence購入・実績進捗表示（S-15）、破損復旧・未記録表示（S-20）が全てpassし、「負けても実績/進捗が積み上がる」という核が機械検証で裏付けられた | ok |

## 重大バグ一覧

なし。

## 既知の妥協点・未検証事項

- **[INFRA] git commit 不能（1Password SSH signing agent 障害・環境起因）**: `state/reviews/batch-verify.md`
  「phase: build (Polish)」節・`state/active.md`に記録済みの継続ブロッカー。S-22〜S-26・S-23（およびそれらの
  CR-CODE fixコミット）はコード内容としては本QAで実機検証済み（working tree上に存在し実際にビルド・テストされた）
  だが、git上のコミットが一件も成立していない（HEAD `290839d`以降）。本QAはコミット状態ではなくworking treeの
  実コードを検証対象としたためAPPROVE判定に影響しないが、人間の1Passwordサインイン復旧後にコミットを確定する
  必要がある（コードそのものの欠陥ではない）。
- **[ASSET] IMG-05/06/07（実績/UPG/敵インジケータのUIアイコン画像）が生成済みだが未Integrate**:
  `game/_generated/textures/icon-achievements.png`・`icon-upgrades.png`・`icon-enemy-indicator.png`は
  AR-ASSET承認済みでMANIFESTにも記録されているが、`Assets/Resources/Generated/textures/`への取込
  （`Editor/ForgeAssetIntegration.cs`の`CheckKnownGaps`が引き続き`PLANNED`として計上）が完了していない。
  該当UI（Menu実績パネル・UPGボタン・Game中の敵インジケータ）は現状テキスト/プレースホルダ図形のみで表示され
  ピンクマテリアル等の破綻は無いが、意図されたアイコングラフィックは未反映。acceptance文言自体はテキスト表示で
  満たされているため重大バグではないが、Checkpointでの開示対象（ui-engineer領域のフォローアップ推奨）。
- 実機（ビルド済み`game/Build/ForgeGame.app`）でのマウス実操作によるプレイ確認は本サンドボックス環境
  （画面収録権限なし・GUI自動操作不可）では未実施。PlayModeの`InputTestFixture`擬似発行による検証のみで判定した。
- CR-CODE側で継続エスカレーション中の未解決事項（S-06/S-07/S-10/S-19/S-22等のstories.yaml note参照）は
  CR-CODEゲートの管轄でありQA-PLAYの合否判定には影響しないが、Checkpointでの正直な開示対象として引き続き
  carry-forwardする。

## 附録: phase:prototype 単独QA（過去ラウンド・参考）

以下は prototype phase（S-01〜S-09）のみを対象にした過去のQAラウンド（2026-07-22T03:21:00Z〜、iteration 2
APPROVE）の記録。上記の本ラウンド（build phase・全26story回帰）が最新の正式判定であり、本ラウンドの総合判定
（1行目のQA-PLAY判定）が有効な最終結果である。

- 総合判定: APPROVE（iteration 2）
- 検証対象: git HEAD `b5ee27a`
- 詳細は `state/reviews/qa.md` の iteration 1/2 記録を参照。
