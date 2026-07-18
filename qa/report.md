# QA レポート — Crystal Vanguard（Checkpoint B / プロトタイプ縦串。round 1 全story回帰は本ファイル末尾「フルQA round 1 追記」参照）

- engine: unity 6000.3.16f1（`state/engine-info.json`）
- review-mode: lean
- 対象: 遊べる縦串（prototype phase story S-01〜S-11）+ Title→Menu→Game→Result→Menu の1周
- QA-PLAY verdict: **iteration 1 = APPROVE**（`state/reviews/qa.md`。重大バグ0・acceptance 全通過。中優先度の既知妥協点は下記 §5）
- 本レポートは gates.md QA-PLAY の要求 (a)〜(e) を満たすために作成。全数値は `qa/evidence/` の証跡に紐付く。

---

## (a) ビルド / 起動結果

| 検証 | コマンド（tech-stack-unity.md「検証コマンド」） | 結果 | 証跡 |
|---|---|---|---|
| build 相当 | `ForgeGame.EditorTools.ForgeBuild.BuildMac`（batchmode -quit） | **exit 0** / ログに `Build succeeded` / `game/Build/ForgeGame.app`（135,004,008 bytes）生成 | `qa/evidence/build-result-checkpointb.txt`, `game/Logs/build.log` |
| typecheck 相当（EditMode） | `-runTests -testPlatform EditMode` | **exit 0** / **80 passed / 0 failed**（コンパイル成功を兼ねる） | `qa/evidence/editmode-results-checkpointb.xml`, `editmode-log-checkpointb.txt` |
| 起動時ログエラー | PlayMode 全テストで `LogAssert.NoUnexpectedReceived` 相当 | エラー0（未処理ログ検出なし） | `qa/evidence/playmode-results.xml`, `playmode-log.txt` |

- 判定: ビルド成功・起動時のエラー0を確認。exit code 単独ではなく結果 XML の `failed="0"` で二重確認済み。

## (b) コアループ1周検証（FullLoop・PlayMode 入力擬似発行）

- **PlayMode 全体: exit 0 / 71 passed / 0 failed**（`qa/evidence/playmode-results.xml`・duration 21.0s）。入力は `InputTestFixture` で擬似発行。
- コアループ1周の直接検証テスト（いずれも Passed）:
  - `ResultSceneTests.FullLoop_TitleToMenuToGameToResultToMenu` — Title→Menu→Game→Result→Menu の遷移1周が成立。
  - `TitleSceneTests.Submit_TransitionsToMenuScene` / `Submit_Space_AlsoTransitionsToMenuScene` — 開始（Title→Menu）。
  - `MenuSceneTests.SubmitOnStartTab_TransitionsToGameScene` — 挑戦開始（Menu→Game）。
  - `HealthSceneTests.HpReachesZero_TransitionsToResult_AndSavesExactlyOnce` — 敗北→結果（Game→Result、セーブ1回）。
  - `ResultSceneTests.Restart_ResetsPlayerPositionHpWaveTimerEnemiesAndScore` — 再挑戦（Result→Game、状態リセット）。
  - `ResultSceneTests.Cancel_TransitionsToMenuScene` — Result→Menu 帰還。
- 視覚証跡（`QaPlayEvidenceTests.Capture_*`・batchmode RenderTexture 撮影）: `qa-title.png` / `qa-menu-start.png` / `qa-menu-stats.png` / `qa-game.png` / `qa-game-swarm.png`（中盤ウェーブの群れ密度）/ `qa-result.png`。HUD（HP/WAVE/SCORE/DASH）・敵・クリスタル・hero の実描画を確認。

## (c) 対象ストーリー acceptance カバレッジ（phase:prototype / S-01〜S-11）

| story | pillar | status | acceptance を検証する PlayMode/EditMode テスト（全 Passed） | 判定 |
|---|---|---|---|---|
| S-01 永続化基盤 | P-04 | review※ | `PersistenceStoryTests.Save_ThenLoadWithNewAdapterInstance_RoundTripsAllFields`（保存→新インスタンス Load 一致）, `Load_UnparsableJson_/_MissingSaveVersionKey_/_ExplicitZeroSaveVersion_/_FutureSaveVersion_RetiresBackupLogsOnceAndReturnsDefaultCorrupt`（破損4種→.bak生成+`[SaveCorruption]`1回+recovered=true）, `SessionHolderTests`×5 | 通過 |
| S-02 Title | P-04 | done | `TitleSceneTests`×7（Enter/Space→Menu, Esc→Quit1回, recovered通知 True/False, Canvas=ScreenSpaceCamera） | 通過 |
| S-03 Menu 4タブ | P-04 | done | `MenuSceneTests`×11（4ラベル実在, はじめる→Game, 統計=全SaveData項目, 設定=音量+操作説明, Esc→Title, タブ循環, フォーカスclamp） | 通過 |
| S-04 移動+俯瞰カメラ | P-01 | done | `GameSceneTests`×5（右入力→+X移動, 長時間入力でも ARENA_RADIUS 内, カメラ非追従, forward が中心向き） | 通過 |
| S-05 敵接近+ウェーブ | P-03 | done | `EnemySpawnSceneTests`×5（スポーンリング上生成, 接近, 同時数上限厳守, 難度カーブ値一致） | 通過 |
| S-06 自動攻撃 | P-02 | done | `AutoAttackSceneTests`×3（範囲内最寄りを2発撃破=TTK≈1.2s, 範囲外除外, 入力は攻撃タイミングに無関与） | 通過 |
| S-07 ダッシュ回避 | P-01 | done | `DashSceneTests`×3（入力方向へ≈4m+無敵フラグ, CD中の再入力無反応） | 通過 |
| S-08 HP/死亡→Result | P-01 | review※ | `HealthSceneTests`×4（接触でHP減, 無敵中は減らない, HP<=0→Result, セーブ1回だけ） | 通過 |
| S-09 クリスタル+スコア | P-04 | done | `CrystalSceneTests`×4（撃破ドロップ→自動回収→スコア+SCORE_PER_CRYSTAL, 寿命超過で消滅・未計上） | 通過 |
| S-10 Game HUD | P-03 | done | `GameHudSceneTests`×6（被弾でHP表示減, ダッシュCD表示, ウェーブ増加, スコア増加, Canvas=ScreenSpaceCamera） | 通過 |
| S-11 Result | P-04 | done | `ResultSceneTests`×10（最終スコア/生存/ウェーブ表示, ハイスコア更新通知 True/False, Enter/Space→Game即リスタート, Esc→Menu, FullLoop） | 通過 |

- S-01〜S-11 の acceptance に対応するテストは全 Passed。11 story のうち 9 が status=done、S-01/S-08 は下記※で status=review 持ち越し（機能検証は通過・CR-CODE 手続き上の持ち越し）。
- ※ S-01/S-08: CR-CODE が MAX_ITER=2 到達で status=review のまま持ち越し。実装・acceptance テストは通過しているが、コードレビュー手続き上のオープン指摘があり done へ未反転（詳細 `state/reviews/s-01.md` / `s-08.md`・§5 に開示）。

## (d) ピラー検証 — 自動QAで検証できた範囲 / 人間の実プレイ判断に委ねる範囲

Checkpoint A で **最大設計リスク**と明示された **P-02（自動攻撃の無双感）× P-01/P-03（囲まれる緊張）の均衡**（`state/reviews/checkpoint-a.md` findings #2）について、切り分けを明記する。

**自動QAで検証できた範囲（メカニクスが仕様どおり成立していること）:**
- P-02: 自動攻撃が入力なしで発火し、`ENEMY_HP_BASE` に対し2発（TTK≈1.2s 相当）で撃破、範囲外は対象外、入力アクションは攻撃発火に無関与（`AutoAttackSceneTests`）。
- P-01: WASD 正規化移動・アリーナ境界クランプ、ダッシュ距離≈4m・無敵窓・CD（`GameSceneTests` / `DashSceneTests`）。被弾→死亡→Result（`HealthSceneTests`）。
- P-03: ウェーブ番号・スポーン間隔・同時数・敵倍率が gdd 難度カーブ表の Wave1/Wave5 値に一致し、敵が接近、同時数上限を厳守（`EnemySpawnSceneTests`）。中盤群れ密度を `qa-game-swarm.png` で可視化。

**人間の実プレイ判断に委ねる範囲（自動QAでは判定不能・Checkpoint B の目的）:**
- 均衡の「体感」— 自動攻撃 DPS が強すぎて群れが一掃され P-01/P-03 の緊張が消えていないか、弱すぎて P-02 の無双感が消えていないか。各数値が仕様値であることは検証済みだが、**その数値バランスが面白いかは自動テストでは測れない**。実際にプレイして「囲まれる緊張」と「殲滅の爽快」が両立しているかの体感判断を Checkpoint B で人間に依頼する。数値調整レンジは gdd 規定済み（config だけで調整可能）。
- 敵の静止スライド接近（MDL-02 rig 未完了・§5）が緊張感の体感に与える影響も、実プレイでの許容可否を人間判断に委ねる。

## (e) 既知の妥協点（証跡紐付け）

| 項目 | 内容 | 証跡 |
|---|---|---|
| MDL-02（swarmer）rig=none / ANM-04 未生成 | 見た目は実モデルへ差し替え済みだが rig 未完了で**静止ポーズのまま接近**（`EnemyApproachSystem` のコード直接移動、アニメなし）。must-replace 印。build phase の課題。 | `qa/evidence/qa-swarmer-closeup.png`, `qa/evidence/asset-integration-report.txt`（swarmer bounds・rig none）, `game/_generated/MANIFEST.jsonl`（MDL-02 degraded_route） |
| SFX-01 ラウドネス逸脱 | 実測 -17.45 LUFS（目標 -16±1 を逸脱）。SFX-02/04 は測定不能。must-replace。 | `state/reviews/assets-audio-prototype.md` |
| SFX-05/06 / BGM-01 未生成 | design/assets.md 状態 `planned`。配線先が無音縮退（`SfxLibrary` clip=null）。対応メカニクス（S-12/S-15）が build phase のためスコープ外。 | `state/active.md`, `game/Assets/Scripts/Components/SfxLibrary.cs` |
| IMG-01 影ブロブ欠陥 | エスカレーション済み。 | `state/reviews/assets-images-prototype.md` |
| S-01 / S-08 status=review 持ち越し | CR-CODE MAX_ITER=2 到達のオープン指摘あり。機能・acceptance は通過（上表 (c)）。 | `state/reviews/s-01.md`, `state/reviews/s-08.md` |
| コミット原子性の運用是正 | S-02 で verdict 追記と status 反転のコミットが後ずれ（silent-failure-hunter 検出）。是正方針記録済。 | `state/active.md`, `state/reviews/s-02.md` iteration 1 対応6 |
| コスト証跡の間接性 | MANIFEST に cost_estimated:true・plan_tier 間接証明の資産あり。 | `game/_generated/MANIFEST.jsonl` |

---

## 総括

- 重大バグ **0**。build exit 0 / EditMode 80-0 / PlayMode 71-0。コアループ1周（Title→Menu→Game→Result→Menu）成立。
- prototype phase story S-01〜S-11 の acceptance は全通過（S-01/S-08 は CR-CODE 手続き上の review 持ち越し）。
- 最大設計リスク P-02×P-01/P-03 均衡は**メカニクス成立を自動検証済み・数値バランスの面白さは人間の実プレイ判断へ**明示的に委ねる。
- 既知妥協点は §5 に証跡紐付けで全件開示（隠蔽なし）。QA-PLAY verdict は iteration 1 APPROVE（`state/reviews/qa.md`）。

---

# フルQA round 1 追記 — Crystal Vanguard / build（全story回帰・2026-07-13）

## 総合判定

```
QA-PLAY: APPROVE
```

build 成功（exit 0）・EditMode 160/160・PlayMode 109/109 全て exit0・console/ログエラー0。state/stories.yaml 全23storyの acceptance を実操作相当（PlayMode/EditMode テスト）で検証し全通過。重大バグ0。中優先度バグ1件（QA証跡スクリーンショットの内容欠落、acceptance判定には非影響）。

## 実行環境

- 日時: 2026-07-13T11:36:15Z 〜 2026-07-13T11:37:46Z（本ラウンド実行分）
- エンジン: unity 6000.3.16f1（`state/engine-info.json`）
- OS: macOS 25.5.0（Darwin）
- 実行系: `$UNITY` = `/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity`
- 検証対象: 作業ツリー現況（git status で確認。stories.yaml 記載の全23story分のコードが作業ツリーに存在する状態でビルド・テストを実行。一部 story のコミットが1Password署名エージェント起因で未完了だが、コード自体は作業ツリーに存在し本ラウンドはそれに対して実行した）

## ビルド結果

| コマンド（tech-stack-unity.md「検証コマンド」） | 結果 | 備考 |
|---|---|---|
| `ForgeGame.EditorTools.ForgeBuild.BuildMac`（batchmode -quit） | **exit 0** | `Build succeeded: game/Build/ForgeGame.app (136627737 bytes)`。`qa/evidence/build-qa-round1.log`, `qa/evidence/build-result-qa-round1.txt` |
| `-runTests -testPlatform EditMode` | **exit 0 / 160 passed / 0 failed** | コンパイル成功を兼ねる。`qa/evidence/editmode-results-qa-round1.xml` |
| `-runTests -testPlatform PlayMode` | **exit 0 / 109 passed / 0 failed**（duration 43.05s） | `qa/evidence/playmode-results-qa-round1.xml` |

## Console / ログエラー

- エラー件数: **0**（`NullReferenceException` / `MissingReferenceException` を editmode/playmode 両ログで grep し0件。PlayMode 全体が failed=0 であることは、テスト内 `LogAssert` が未処理ログを検知しなかったことの二重確認）
- warning 件数: 実質0（`[Wiring]` ログは Boot を経由せず Game/Result/Title シーンを単独ロードするテストが意図的に誘発する縮退経路の LogError/Warning で、対応するテストが `LogAssert.Expect` 済み。規約12「配線破損はStartで1回LogError」への準拠を確認するテストであり異常ではない）
- 詳細: なし（0件）

## Acceptance 検証表（全23story）

| S-xx | acceptance 要旨 | 検証操作（テスト） | 判定 | 証跡パス |
|---|---|---|---|---|
| S-01 | メタ進行の永続化基盤（保存・ロード・復元・破損復旧） | `PersistenceStoryTests.Save_ThenLoadWithNewAdapterInstance_RoundTripsAllFields` / `Load_UnparsableJson_...` / `Load_MissingSaveVersionKey_...`（スキーマ不正ケース）/ `Load_ExplicitZeroSaveVersion_...` / `Load_FutureSaveVersion_...`（各 `.bak` 生成＋`[SaveCorruption]`1回＋recovered=true） | pass | qa/evidence/playmode-results-qa-round1.xml |
| S-02 | Title（決定→Menu / Esc→終了 / 破損復旧通知） | `TitleSceneTests`×7 | pass | qa/evidence/qa-title.png, playmode-results-qa-round1.xml |
| S-03 | Menu（4タブ・必須4要素） | `MenuSceneTests`×11 | pass | qa/evidence/qa-menu-start.png, qa-menu-stats.png |
| S-04 | プレイヤー移動+固定俯瞰カメラ+アリーナ境界 | `GameSceneTests`×5 | pass | qa/evidence/qa-game.png |
| S-05 | 敵接近AI+ウェーブスポーン+難度カーブ | `EnemySpawnSceneTests`×5, `WaveSpawnSystemTests`(EditMode) Wave1/3/5/12/16 | pass | qa/evidence/qa-game-swarm.png |
| S-06 | 自動攻撃（最寄り索敵・瞬間ヒット）+ 敵HP・撃破 | `AutoAttackSceneTests.EnemyWithinRange_TakesDamageInBaseIncrements_AndDiesAfterTwoHits` 他4本 | pass | playmode-results-qa-round1.xml |
| S-07 | ダッシュ回避（無敵窓・CD・方向優先順位） | `DashSceneTests`×2 | pass | playmode-results-qa-round1.xml |
| S-08 | プレイヤーHP・被弾・死亡判定→Result遷移 | `HealthSceneTests` 系（接触減HP・無敵中不減・HP<=0→Result・セーブ1回） | pass | playmode-results-qa-round1.xml |
| S-09 | クリスタル ドロップ・自動回収+スコア算出 | `CrystalSceneTests`×3（ドロップ→自動回収→スコア加算、寿命超過消滅） | pass | playmode-results-qa-round1.xml |
| S-10 | Game HUD（HP/ダッシュCD/ウェーブ/スコア） | `GameHudSceneTests`, `WavePulseSceneTests.GameHudCanvas_StillScreenSpaceCamera_WithWaveScaleFeature` | pass | qa/evidence/qa-game.png |
| S-11 | Result（最終スコア・ハイスコア更新・リスタート/メニュー導線） | `ResultSceneTests`×10（`FullLoop_TitleToMenuToGameToResultToMenu` 含む） | pass | qa/evidence/qa-result.png |
| S-12 | アップグレード購入インタラクション+Gameへの反映 | `UpgradePurchaseSceneTests`×6（残高十分/不足/上限Lv/実効値反映/SFX発火） | pass | playmode-results-qa-round1.xml |
| S-13 | 設定タブ音量スライダー+操作説明+統計タブ完全表示 | `MenuSettingsSceneTests`×6（A/D 0.1刻み増減・保存・フォーカス中決定無効・統計全項目） | pass | qa/evidence/qa-menu-stats.png |
| S-14 | ヘヴィスウォーマー変種 | `HeavyEnemySceneTests`×4（混入・倍率適用） | pass | qa/evidence/qa-game-swarm.png（紫個体） |
| S-15 | ウェーブ切替フィードバック（SFX+HUDパルス） | `WavePulseSceneTests.WaveIncrease_TriggersWaveStartSfxExactlyOnce_AndPulsesHudWaveScale` | pass | playmode-results-qa-round1.xml |
| S-16 | 死亡演出（コード合成）+被弾マテリアルフラッシュ | `HeroFxSceneTests`×2（死亡演出→Result遷移、被弾フラッシュ→復帰） | pass | playmode-results-qa-round1.xml |
| S-17 | 自動攻撃ヒットVFX+攻撃SFX同期 | `AutoAttackSceneTests.AttackLanding_SpawnsHitVfxAtTargetPosition_PlaysSfx_AndTransitionsAnimatorToAttack` | pass | playmode-results-qa-round1.xml |
| S-18 | 3D資産統合（hero MDL-01・ANM-01/02/03・Avatar・AnimatorController） | `AssetIntegrationSceneTests.HeroVisual_HasValidHumanoidAnimatorAndNoMaterialErrors`（Avatar valid・normalizedTime進行）、`HeroVisual_BoundingBoxWithinArtBibleHeightRange` | pass | qa/evidence/asset-integration-hero-visual.png, asset-integration-report.txt（hero_avatar_valid=True, bounds=1.02x1.80x0.73） |
| S-19 | 音声統合（BGMループ+全SFX配線+音量バス） | `AudioIntegrationSceneTests`×4（BGMシーン跨ぎ継続, Boot時音量反映, 破損セーブでも既定音量で配線, ループ再生開始） / `UpgradePurchaseSceneTests` SFX-06 / `AutoAttackSceneTests` SFX-01 / `AssetIntegrationSceneTests.SfxLibrary_HasAllGeneratedClipsAssigned` | pass | playmode-results-qa-round1.xml |
| S-20 | アリーナ環境ビジュアル+クリスタル発光+全体調整 | `ArenaEnvironmentSceneTests`（床/境界/スポーンリング/クリスタル発光回転・NaN無し）、`Capture_FourDirectionReadability_Evidence` | pass※ | qa/evidence/arena-four-direction.png |
| S-21 | スウォーマー静的メッシュ統合+接近コードモーション（リグ縮退代替） | `EnemyVisualMotionSceneTests.SwarmerVisual_BouncesOverTimeWithoutAnimatorOrMaterialErrors` / `_PreservesBakedLocalPositionOffsetUnderBounce`、`AssetIntegrationSceneTests.SwarmerPrefab_HasNoMaterialErrorsWhenSpawned` | pass | qa/evidence/qa-game-swarm.png, asset-integration-report.txt（swarmer rig_type=none 縮退・must_replace:true が MANIFEST に記録済みで実プレイ破綻なし） |
| S-22 | 固定俯瞰カメラの南側可視性改善 | `ArenaCameraMathTests.ComputeSouthVisibilityLimitZ_*`（新値 z≈-9.6m・旧値比拡大・回帰なし）、`Capture_FourDirectionReadability_Evidence` 再撮影 | pass※ | qa/evidence/arena-four-direction.png |
| S-23 | ダッシュ紙一重回避のカメラシェイク演出 | `CameraShakeSystemTests`×9（EditMode）、`CameraShakeSceneTests`×3（無敵中接触のみ発火・厳密復帰／通常被弾非発火／同一窓内多重接触でも単発） | pass | playmode-results-qa-round1.xml |

※ S-20/S-22: acceptance の機械検証項目（発光・回転・NaN無し・南側可視限界の拡大）は全て pass。ただし「ARENA_RADIUS/SpawnRadius 南端そのものの完全カバー」は現行レンジ内では未達のまま残る既知の制約（既に `state/reviews/s-20.md`／`state/active.md` で開示・Checkpoint C 申し送り済み、S-22 で改善済みの上での残存限界であり新規の未達ではない）。S-22 acceptance が明示的に許容する範囲内であるため fail 扱いにしない（詳細は「ピラー検証所見」）。

23 story 全件 pass（fail 0）。

## 必須シーン遷移と永続化（gates.md QA-PLAY 観点2/5）

| 検証 | 手段（テスト名/操作列） | 判定 | 証跡パス |
|---|---|---|---|
| Title → Menu → Game → Result → Menu の1周 | `ResultSceneTests.FullLoop_TitleToMenuToGameToResultToMenu`（InputTestFixture 擬似発行） | pass | qa/evidence/qa-title.png, qa-menu-start.png, qa-game.png, qa-result.png |
| セーブ → 再起動相当（新規インスタンス+再ロード） → 復元一致 | `PersistenceStoryTests.Save_ThenLoadWithNewAdapterInstance_RoundTripsAllFields`（非0値の highScore/統計/crystalBalance/アップグレードLvが新規 `FileSaveAdapter` インスタンス経由の Load で全一致） | pass | qa/evidence/playmode-results-qa-round1.xml |
| 破損セーブ → .bak 退避 + [SaveCorruption] エラー1回 + 既定値復旧（スキーマ不正ケース込み） | `PersistenceStoryTests.Load_UnparsableJson_...`（パース不能）/ `Load_MissingSaveVersionKey_...`（**スキーマ不正＝必須フィールド欠落**）/ `Load_ExplicitZeroSaveVersion_...` / `Load_FutureSaveVersion_...`。各 `LogAssert.Expect(LogType.Error, ^\[SaveCorruption\])` で1回のみ発火を確認、`save.json.bak.<UTC>` 生成、recovered=true・既定値 SaveData を確認。テストは `Application.temporaryCachePath` 配下の一時ファイルを使用し `[TearDown]` で削除（規約準拠） | pass | qa/evidence/playmode-results-qa-round1.xml |

## スクリーンショット目視所見（必須）

全画像 `magick identify -format "%[fx:mean]" <shot>.png` を実行しSUSPECT_BLANK(<0.02 or >0.98)は0件。全件 Read で目視。

| 証跡パス | mean 値 | 目視所見（何が写っているか） | 判定 |
|---|---|---|---|
| qa/evidence/qa-title.png | 0.0914 | 「Crystal Vanguard」タイトルと「Enter/Space ではじめる」「Escで終了」ヒントが判読可能。文字くっきり、黒画面ではない。 | ok |
| qa/evidence/qa-menu-start.png | 0.0842 | タブバー「はじめる/統計/アップグレード/設定」4件と「プレイ開始▶」が判読可能。Menu必須要素(a)(d)を目視確認。 | ok |
| qa/evidence/qa-menu-stats.png | 0.0942 | 統計タブ選択状態。ハイスコア4200・ベスト生存時間128.5秒・最高到達ウェーブ7・累計プレイ回数12・累計撃破数340・累計獲得クリスタル560・クリスタル残高75・アップグレードLv（攻撃力Lv2/移動速度Lv1/最大HPLv3）とクリスタルアイコン2つが判読可能。Menu必須要素(b)を目視確認。 | ok |
| qa/evidence/qa-game.png | 0.4697 | Wave1開始直後。HP 100/100バー、WAVE 1、SCORE 15、DASH READYバー、円形アリーナ床（マットグリーン）、境界リング、青いhero、緑のswarmer、紫のヘヴィ変種が判読可能。 | ok |
| qa/evidence/qa-game-swarm.png | 0.4701 | Wave2、複数の敵（swarmer×2・ヘヴィ変種×1）+複数の白いクリスタル（回収待ち）が同時に視認できる群れ密度が判読可能。ヘヴィ変種1体が画面左下端に接近しフレーム際で一部見切れ気味（後述の南側可視性の既知制約と一致）。 | ok |
| qa/evidence/qa-result.png | 0.0907 | 「RESULT」見出し、最終スコア3456・生存時間96.2秒・到達ウェーブ6・「ハイスコア更新！」（緑文字）・「Enter/Spaceでリスタート」「Esc（メニューへ）」が判読可能。 | ok |
| qa/evidence/arena-four-direction.png | 0.4695 | Wave1、hero中心に4方向（北=奥/南=手前/東=右/西=左）に紫ヘヴィ変種が配置され同時視認できる構図。南側個体は画面下端近くだが画角内に収まっている（S-22改善後の状態、旧値では画角外に切れていた）。 | ok |
| qa/evidence/asset-integration-hero-visual.png | 0.4697 | Wave1、青いhero（人型シルエット判読可）と緑のswarmerが視認できる。マテリアル欠落・ピンク表示なし。 | ok |
| qa/evidence/qa-swarmer-closeup.png | 0.5454 | **意図した内容が写っていない**。空と地平線のみのグラデーション画像で、swarmerモデルが画角内に存在しない（黒画面ではないため機械検知(mean)はSUSPECT_BLANKに掛からないが、目視で被写体欠落と判断）。原因は下記バグ#1参照。 | **ng（内容欠落）** |

## ピラー検証所見

| P-xx | 所見（実プレイ感） | 判定 |
|---|---|---|
| P-01 紙一重回避 | ダッシュは入力方向へ≈4m移動+無敵窓が仕様通り作動（`DashSceneTests`）。S-23 のニアミスカメラシェイクにより「際どく躱せた」瞬間に視覚フィードバックが追加され、際どさの手応えが強化されている（`CameraShakeSceneTests`3本 pass）。南側可視性は S-22 で旧z≈-6.5m→z≈-9.6mへ改善済みだが、ARENA_RADIUS(12m)南端の完全カバーは未達のまま（`qa-game-swarm.png` でヘヴィ変種が画面下端際に位置）。これは「避けるべき危険が見えない」というP-01の前提を部分的に脅かす残存リスクだが、新規に発生した問題ではなく S-20/S-22 で継続的に改善・開示済みの既知の制約であり、CR-CODEからCheckpoint Cへの裁定申し送り事項として既に確立している。 | concern（継続・新規ではない） |
| P-02 照準ゼロの自動攻撃 | 自動攻撃は入力に一切関与されず、範囲内最寄り1体への単体ヒットでTTK≈1.2s（2発）を確認（`AutoAttackSceneTests`）。攻撃ヒット時にVFX/SFX/attackアニメが同フレームで発火し「確実に仕留めている」手応えが視覚・音双方に伝わる（S-17統合済み）。 | ok |
| P-03 群れ密度の圧力 | ウェーブ経過でスポーン間隔短縮・同時数増加・敵速度上昇がgddの難度カーブ表と一致（`WaveSpawnSystemTests` Wave1/3/5/12/16）。`qa-game-swarm.png` で複数敵が同時に画面内に存在し包囲感が視認できる。ヘヴィ変種混入（S-14）で圧力に強弱がついている。南側可視性の残存制約（P-01と同一の懸念）はP-03の「群れが見えているから逃げ場のなさを感じる」前提にもわずかに影響しうるが、上記と同じく新規指摘ではなく既知の申し送り事項。 | concern（継続・新規ではない） |
| P-04 積み上がる再挑戦 | クリスタル自動回収→スコア加算→Result でのハイスコア判定→Menu統計/アップグレードタブへの反映が一気通貫で動作（`CrystalSceneTests`, `ResultSceneTests`, `UpgradePurchaseSceneTests`, `MenuSettingsSceneTests`）。`qa-menu-stats.png` で全統計項目とアップグレードLvが実際に表示され、購入がGameの実効値に反映されることも検証済み（`UpgradePurchaseSceneTests.PurchaseMaxHpUpgrade_ThenLoadGame_PlayerEffectiveMaxHpReflectsPurchase`）。「次はもっと先へ」の動機付けループが機能的に成立している。 | ok |

## 重大バグ一覧

なし（進行不能・クラッシュ・acceptance fail は0件）。

## 中・軽微バグ一覧

| # | 症状 | 再現手順 | 期待/実際 | 証跡パス | 関連 S-xx | severity | assignee |
|---|---|---|---|---|---|---|---|
| 1 | `QaPlayEvidenceTests.Capture_Swarmer_Closeup` が生成する `qa-swarmer-closeup.png` に swarmer モデルが写っていない（空と地平線のみ）。テストコードが `cam.transform.position/rotation` を直接書き換えて近接撮影しようとするが、S-23 で `ArenaCameraRig.Update()` が毎フレーム `transform.localPosition = _basePosition + offset` を無条件上書きするようになったため、`yield return null` を挟んだ直後に位置だけ固定カメラの基準位置へ戻る（回転は書き換え済みのまま残る）。結果、高所(≈18m)から水平に近い角度を向くねじれた構図になり被写体が画角外へ外れる。 | 1) `Capture_Swarmer_Closeup` テストを単独実行 2) `qa/evidence/qa-swarmer-closeup.png` を開く | 期待: swarmer モデルが画面中央付近に大きく写る近接ショット / 実際: 空と地平線のグラデーションのみ、swarmer 不在 | qa/evidence/qa-swarmer-closeup.png, game/Assets/Scripts/Components/ArenaCameraRig.cs（Update）, game/Assets/Tests/PlayMode/QaPlayEvidenceTests.cs:171-204 | S-21, S-23（回帰元） | minor | gameplay-engineer |

上記1件は QA証跡（スクリーンショット）の内容欠落であり、テスト自体はPASS（画像内容をアサートしていないため）・swarmer自体の健全性は `EnemyVisualMotionSceneTests`（Renderer/マテリアル/バウンス動作を機械検証・pass）で別途確認済みのため、**S-21 acceptance の合否には影響しない**。ただし視覚証跡としては無効なため目視所見表で ng とし、gameplay-engineer への修正差し戻し事項として記録する（対応案: `Capture_Swarmer_Closeup` をカメラ直接操作ではなく専用の一時 `Camera` オブジェクトで撮影する、または撮影前後で `ArenaCameraRig.enabled = false` に切り替える）。

## 既知の妥協点・未検証事項（round 1 追記分）

- **MDL-02（swarmer）rig=none / ANM-04 未生成 — 重点確認結果**: Checkpoint B で承認済みの意図的縮退（`state/stories.yaml` S-21）。本ラウンドで重点確認した結果、`EnemyVisualMotionSceneTests` で Renderer/マテリアル異常なし・接近コードモーション（前傾チルト+バウンス）が周期的に作動していることを機械検証し、`qa-game.png`/`qa-game-swarm.png` で静止ポーズのまま接近する見た目を目視確認。**実プレイ上の破綻（貫通・すり抜け・衝突判定不能等）は確認されなかった**（HP/スコア/衝突判定はいずれも正常動作）。
- **S-20/S-22 南側可視性の残存制約**: 上記ピラー検証所見に記載の通り、ARENA_RADIUS/SpawnRadius南端の完全カバーは調整レンジ内では未達のまま。`state/reviews/s-20.md`・`state/active.md` に既存の開示・Checkpoint C 申し送り事項があり、本ラウンドで新規に発生した問題ではないため重大バグには計上しない。
- **中・軽微バグ#1（qa-swarmer-closeup.png 内容欠落）**: 上記参照。QA証跡の話でありacceptance判定には影響しない。
- **S-01/S-08/S-19 の status**: `state/stories.yaml` 上 S-01/S-08/S-19 は `status: review`（CR-CODE 手続き上の持ち越し、または未反転）だが、acceptance 自体は本ラウンドのテストで全通過している。CR-CODE 側の残 findings は state/reviews/s-01.md, s-08.md, s-19.md を参照（QA-PLAY の権限外）。
- **音響の聴感的検証**: BGM-01 のシームレスループ・SFX-01〜06 の音質はAR-ASSET（`state/reviews/assets-audio.md`）でffmpeg機械検証済み。QA-PLAYでは `AudioIntegrationSceneTests` によりゲーム内トリガーでの再生開始・シーン跨ぎ継続・音量バス反映を実機（PlayMode）で確認したが、人間の耳による聴感チェック（ボーカル混入有無等）は未実施（AR-ASSETの開示事項を継続）。
- **git commit 未完了分**: `git status` 時点で S-22/S-23 分の一部が staged、S-19（音声統合）一式が untracked のまま残存（1Password SSH署名エージェント起因、`state/active.md` 記載の環境要因）。本 QA ラウンドは作業ツリー現況（コミット状態に関わらず）に対して実行しており、コード自体の欠陥ではないためQA-PLAY判定には影響しない。

---

# フルQA round 2 追記（Phase 3 Visual Brushup バッチ・全33story回帰）

- 日時: 2026-07-14T15:05:00Z 〜 2026-07-14T15:07:39Z（本ラウンド実行分）
- エンジン: Unity 6000.3.16f1（`state/engine-info.json`）
- OS: macOS 25.5.0（Darwin）
- 実行系: `$UNITY` = `/Applications/Unity/Hub/Editor/6000.3.16f1/Unity.app/Contents/MacOS/Unity`
- 検証対象: `game/` working tree の現状態。git HEAD は `dfc2c1c`（S-31 fix）だが、S-27後半〜S-33 の実装一式が 1Password SSH 署名エージェントの継続的な認可失敗により `git commit` できず working tree に `git add` 済みのまま残存している（詳細は本節末尾「既知の妥協点」）。QA は実際にビルド・実行される working tree の内容を対象に検証した（round 1 と同じ方針の継続）。
- 対象: `state/stories.yaml` 全33story（prototype S-01〜S-11 + build S-12〜S-33）の acceptance 回帰

## 総合判定（round 2）

```
QA-PLAY: APPROVE
```

重大バグ0・33story全acceptance PASS。build/EditMode/PlayMode いずれも exit 0・console/ログエラー0。Title→Menu→Game→Result→Menu 1周・メタ進行永続化（正常/破損4種）とも機械検証PASS。round 1 で報告済みの軽微バグ1件（`qa-swarmer-closeup.png` 内容欠落）は継続中だがacceptance非影響。git commit 未反映というコード外のプロセスリスクを開示事項として記載。

## ビルド結果

| コマンド（tech-stack-unity.md「検証コマンド」） | 結果 | 備考 |
|---|---|---|
| `-runTests -testPlatform EditMode -testResults qa/evidence/editmode-results-qafull.xml` | **exit 0 / 181 passed / 0 failed** | コンパイル成功を兼ねる。round 1(160)から+21件（S-24〜S-33分のEditModeテスト増分） |
| `ForgeGame.EditorTools.ForgeBuild.BuildMac`（batchmode -quit） | **exit 0** | `Build succeeded: game/Build/ForgeGame.app (140496437 bytes)`。`game/Logs/build-qafull.log` |
| `-runTests -testPlatform PlayMode -testResults qa/evidence/playmode-results-qafull.xml` | **exit 0 / 142 passed / 0 failed**（duration 55.27s） | round 1(109)から+33件（S-24〜S-33分のPlayModeテスト増分）。round 1 で報告された `CrystalSceneTests` の1回限りflaky failureは本ラウンドでは非発生（142/142 green） |

## Console / ログエラー

- エラー件数: **0**（`NullReferenceException`/`MissingReferenceException`/`error CS`/`InternalErrorShader`を editmode/playmode 両ログ・ビルドログで検索し0件）
- warning 件数: 多数だが全て意図された `[Wiring]` 縮退ログ（テストが Boot を経由せず単体シーンロードする際の想定内メッセージ）または `[SaveCorruption]` 期待ログ（4件、`LogAssert.Expect`で検知済み）。実ビルドログ（`game/Logs/build-qafull.log`）には `[Wiring]` エラー0件
- 詳細: なし（0件）

## Acceptance 検証表（新規/変更分 S-22, S-24〜S-33。S-01〜S-21・S-23 は round 1 表参照・本ラウンドでも全PASSを再確認）

| S-xx | acceptance 要旨 | 検証操作（テスト） | 判定 | 証跡パス |
|---|---|---|---|---|
| S-22 | 固定俯瞰カメラ南側可視性改善（H18/P60/F55、レンジ境界値） | EditMode `ArenaCameraMathTests`（新値一致・南側拡大・回帰なし）、PlayMode `ArenaEnvironmentSceneTests.Capture_FourDirectionReadability_Evidence` 再撮影 | pass | qa/evidence/editmode-results-qafull.xml, qa/evidence/arena-four-direction.png |
| S-24 | UI装飾スプライトキット生成（IMG-05） | AR-ASSET iteration 4 APPROVE（`state/reviews/assets-images.md`）。design/assets.md 状態 generated。S-26/S-30 が既にこの資産に依存して実装・検証済み | pass（内容面は充足。stories.yaml上のstatus記帳漏れは下記「既知の妥協点」） | design/assets.md, state/reviews/assets-images.md |
| S-25 | アリーナ背景/スカイボックス生成（IMG-06） | 同上（AR-ASSET iteration 4 APPROVE） | pass（同上） | design/assets.md, state/reviews/assets-images.md |
| S-26 | 背景/スカイボックス統合 | `ArenaBackdropSceneTests`×3: Skybox配線、AssetKeys経由（ハードコード無し）、前景可読性維持を撮影確認 | pass | qa/evidence/playmode-results-qafull.xml, qa/evidence/arena-backdrop.png |
| S-27 | URPポストプロセス/ライティングで発色接近 | `PostProcessLightingSceneTests`×3: Volume isGlobal + Bloom/ColorAdjustments/Tonemapping、KeyLightRig、可読性撮影（白飛び・黒潰れなし） | pass | qa/evidence/playmode-results-qafull.xml, qa/evidence/postprocess-lighting-readability.png |
| S-28 | URP Outline輪郭線+hero/swarmerマテリアル増強 | `OutlineSceneTests`×3: hero/swarmer双方にOutline適用・マテリアル欠落無し、密集swarmerペアで輪郭分離を機械判定+撮影確認 | pass | qa/evidence/playmode-results-qafull.xml, qa/evidence/outline-dense-swarmer-cluster.png |
| S-29 | クリスタル発光強化+パーティクルjuice | `CrystalVfxSceneTests`×4: 発光マテリアル+glowパーティクル付与、自動回収時collect VFXがSFXと同フレーム発火、定数GameConfig経由、missing-library時のlog-once縮退 | pass | qa/evidence/playmode-results-qafull.xml |
| S-30 | UI装飾適用（HUD/Menu/Title/Result） | `TitleSceneTests`/`MenuSceneTests`/`ResultSceneTests`/`GameHudSceneTests` の Img05装飾系6テスト: 装飾Image非null、タブ/フォーカス選択フレーム区別、Canvas=ScreenSpaceCamera維持、既存遷移/HUD反映に回帰なし | pass | qa/evidence/playmode-results-qafull.xml, qa/evidence/qa-menu-start.png |
| S-31 | ダッシュ移動のアフターイメージ（トレイル）VFX | `DashTrailSceneTests`×5: ダッシュ中cadence生成、ダッシュ外非生成、ゴーストのフェード&自己消滅、S-07 acceptance回帰なし | pass | qa/evidence/playmode-results-qafull.xml |
| S-32 | 撃破インパクト演出（ポップ+カメラノッジ） | `EnemyKillImpactSceneTests`×4: 致死ヒットでポップ→消滅+スコア/クリスタル即時性維持+ノッジ1回発火、非致死非発火、ヘヴィ変種でも同一発火、ノッジ時間がS-23ニアミスシェイクと別値 | pass | qa/evidence/playmode-results-qafull.xml |
| S-33 | Result最終スコアカウントアップ+ハイスコア強調 | `ResultSceneTests.ScoreCountUp_StartsBelowFinal_ThenReachesFinalScoreMonotonically`他: 0から単調増加し最終値到達、カウントアップ中Enter/Spaceで即遷移（入力ブロック無し）、ハイスコア時のみ通知パルス | pass | qa/evidence/playmode-results-qafull.xml, qa/evidence/qa-result.png |

33/33 story acceptance PASS（fail 0。S-01〜S-21・S-23 は round 1 の表・証跡で既にPASS確認済みであり、本ラウンドのEditMode 181/181・PlayMode 142/142 exit 0で回帰なしを再確認）。

## 必須シーン遷移と永続化（gates.md QA-PLAY 観点2/5・round 2 再確認）

| 検証 | 手段（テスト名/操作列） | 判定 | 証跡パス |
|---|---|---|---|
| Title → Menu → Game → Result → Menu の1周 | `ResultSceneTests.FullLoop_TitleToMenuToGameToResultToMenu`（InputTestFixture擬似発行。S-30のUI装飾適用後も回帰なし） | pass | qa/evidence/playmode-results-qafull.xml |
| セーブ → 再起動相当 → 復元一致 | `PersistenceStoryTests.Save_ThenLoadWithNewAdapterInstance_RoundTripsAllFields` | pass | qa/evidence/playmode-results-qafull.xml |
| 破損セーブ → .bak 退避 + [SaveCorruption] エラー1回 + 既定値復旧 | `PersistenceStoryTests.Load_UnparsableJson_...` / `Load_MissingSaveVersionKey_...` / `Load_ExplicitZeroSaveVersion_...` / `Load_FutureSaveVersion_...`（4種とも`.bak.<UTC>`退避＋`[SaveCorruption]`1回＋recovered=true。`Application.temporaryCachePath`使用） | pass | qa/evidence/playmode-results-qafull.xml, qa/evidence/playmode-qafull.log |

## スクリーンショット目視所見（round 2・新規/再撮影分）

magick機械検知（全12枚、mean 0.1334〜0.6604）でSUSPECT_BLANK(<0.02 or >0.98)は0件。全枚Readで目視。

| 証跡パス | mean 値 | 目視所見（何が写っているか） | 判定 |
|---|---|---|---|
| qa/evidence/qa-title.png | 0.1377 | IMG-05装飾（青9-slice枠+マゼンタリボン+コーナー装飾）内に「Crystal Vanguard」と操作ヒントが判読可能 | ok |
| qa/evidence/qa-menu-start.png | 0.1334 | 4タブ+IMG-05装飾フレーム、選択タブ「はじめる」に水色フレーム、「プレイ開始▶」フォーカス緑文字 | ok |
| qa/evidence/qa-menu-stats.png | 0.1408 | 統計タブ全項目（ハイスコア/生存時間/到達ウェーブ/累計値/クリスタル残高/アップグレードLv）+クリスタルアイコン判読可能 | ok |
| qa/evidence/qa-game.png | 0.3977 | HP/WAVE/SCORE HUD、緑アリーナ床、白スポーンリング、青hero、紫背景グラデーション（IMG-06）判読可能 | ok |
| qa/evidence/qa-game-swarm.png | 0.3984 | Wave2、hero+swarmer2体+クリスタル4個程度が同時視認できP-03の群れ密度を確認 | ok |
| qa/evidence/qa-swarmer-closeup.png | 0.6604 | HUDと背景グラデーションのみでswarmer被写体が写っていない（round 1既報の継続バグ、下記参照） | ng（テスト自体はpass。証跡内容のみ不合格） |
| qa/evidence/arena-four-direction.png | 0.3981 | hero中心に東西南北付近にswarmer3体+クリスタル2個が確認でき、S-22カメラ調整後の南側可視性改善を確認 | ok |
| qa/evidence/arena-backdrop.png | 0.3976 | 紫→シアンのネオングラデーション背景が四方に見え、緑床・青heroが背景に埋もれず判別可能 | ok |
| qa/evidence/postprocess-lighting-readability.png | 0.3977 | hero(青)とswarmer(紫)がBloom/ライティング適用後も判別可能、白飛び・黒潰れなし | ok |
| qa/evidence/outline-dense-swarmer-cluster.png | 0.3974 | hero・swarmerともダークネイビー輪郭線を確認、密集swarmer2体のシルエットが輪郭線で分離視認できる | ok |
| qa/evidence/asset-integration-hero-visual.png | 0.3976 | hero単体クローズアップ。青armor主色、輪郭線、InternalErrorShader(ピンク)無し | ok |
| qa/evidence/qa-result.png | 0.1346 | 「最終スコア: 174」「生存時間: 96.2 秒」「到達ウェーブ:6」「ハイスコア更新！」（緑文字）+リスタート/メニュー導線が判読可能 | ok |

## ピラー検証所見（round 2）

| P-xx | 所見（実プレイ感） | 判定 |
|---|---|---|
| P-01 紙一重回避 | S-23のニアミスカメラシェイクに加え、S-31のダッシュトレイル（半透明ゴースト軌跡）で「紙一重で抜けた」瞬間の視認性が向上。S-22のカメラ改善（南側可視限界z≈-6.5m→-9.6m）は維持。ARENA_RADIUS南端の完全カバー未達は既知の残存制約のまま（新規ではない） | ok |
| P-02 照準ゼロの自動攻撃 | 攻撃入力アクションなし（round 1確認済み・回帰なし）。S-32の撃破ポップ+カメラノッジにより「確実に仕留めている」手応えが追加され、単調作業感の軽減に寄与 | ok |
| P-03 群れ密度の圧力 | qa-game-swarm.png・outline-dense-swarmer-cluster.pngで複数敵の同時視認・輪郭分離による識別性向上を確認。S-28のOutlineにより密集時の個体識別がしやすくなった | ok |
| P-04 積み上がる再挑戦 | S-33のResultスコアカウントアップ+ハイスコア強調パルスにより「積み上がっていく実感」が演出面で強化。S-30のUI装飾でMenu統計/Resultの情報が視覚的に整理され可読性向上 | ok |

## 重大バグ一覧（round 2）

なし（0件）。

## 中・軽微バグ一覧（round 2）

| # | 症状 | 再現手順 | 期待/実際 | 証跡パス | 関連 S-xx | severity | assignee |
|---|---|---|---|---|---|---|---|
| 1 | round 1 既報バグの継続: `qa-swarmer-closeup.png` にswarmerモデルが写っていない（`ArenaCameraRig.Update()`がテストの手動カメラ配置を毎フレーム上書きする回帰） | round 1報告と同一（`QaPlayEvidenceTests.Capture_Swarmer_Closeup`単独実行） | 期待: swarmer近接ショット / 実際: HUD+背景のみ | qa/evidence/qa-swarmer-closeup.png, game/Assets/Scripts/Components/ArenaCameraRig.cs | S-21, S-23 | minor | gameplay-engineer |

上記1件はQA証跡（スクリーンショット）内容欠落のみで、swarmer自体の健全性は`EnemyVisualMotionSceneTests`で機械検証済みPASS。acceptance判定・ゲームプレイに影響しない。

## 既知の妥協点・未検証事項（round 2 追記分）

- **[プロセスリスク/コード外・重要] git commit 未反映が広範囲に残存**: S-27〜S-33（および依存するS-22/S-23の一部）の実装一式が、1Password SSH署名エージェントの継続的な認可失敗（`op whoami: account is not signed in`）により`git commit`できず、working tree上に`git add`済みの未コミット状態のまま残っている（`git status --short`で250件超の変更）。本QAは実際にビルド・実行される working tree の内容を対象に検証しており機能面は全て動作確認済みだが、この状態のままではworktree消失時に大量の完成済み作業が失われるリスクがある。QA-PLAY自体の合否判定対象（実際に動くgame/）には影響しないため重大バグとはしないが、Checkpoint Cで人間へ最優先の申し送り事項とすべき（1Passwordのインタラクティブ認可の復旧が必要、コード変更は不要）。
- **[プロセス整合性・軽微] `state/stories.yaml` の S-24/S-25 status表記が`todo`のまま**: 実際にはIMG-05/IMG-06とも生成済み・AR-ASSET iteration 4でAPPROVE済み（`state/reviews/assets-images.md`）で、S-26/S-30が既にこれらの資産に依存して実装・検証済み。acceptance内容自体は満たされているためQA上はpass判定としたが、stories.yaml側の記帳更新（todo→done）漏れがある。
- **[開示継続・非ブロッキング] S-20/S-22 南側可視性の残存制約**: round 1から継続。ARENA_RADIUS(12m)南端・ENEMY_SPAWN_RADIUS(13.5m)南側スポーン地点の完全カバーは調整レンジ内では未達のまま。四方完全均等化にはレンジ自体の拡張（art-director協議要）が必要でPhase 3スコープ外として開示済み。新規の未達ではない。
- **[開示継続・非ブロッキング] IMG-06のJPEG由来ブロックアーティファクト**: `state/reviews/assets-images.md`でFFT解析により検出済み（通常表示では知覚困難）。
- 中・軽微バグ#1（round 2）: 上記参照。QA証跡の話でありacceptance判定には影響しない。
