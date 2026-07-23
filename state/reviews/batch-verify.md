# バッチ検証記録（Build レーン合流後・直列区間）

## phase: prototype — 2026-07-21T21:51:46Z

- 対象: phase:prototype の全コード story（S-01〜S-09）のレーン中コミット済みコード
- 検証コマンド（tech-stack-unity.md「検証コマンド」節）:
  1. typecheck 相当: `"$UNITY" -batchmode -projectPath game -runTests -testPlatform EditMode -testResults qa/evidence/editmode-results.xml -logFile -`
  2. build 相当: `"$UNITY" -batchmode -projectPath game -executeMethod ForgeGame.EditorTools.ForgeBuild.BuildMac -quit -logFile game/Logs/build.log`

### 検出した問題と修正

1. **原因 story: S-02**（`git log --oneline -- game/Assets/Tests/PlayMode/TitleScreenPlayModeTests.cs` → `c0c595c`）
   - 症状: `TitleCanvas_IsScreenSpaceCamera()` が `IEnumerator` を返す `[UnityTest]` なのに `yield` 文を1つも含まず、コンパイラがイテレータメソッドと認識できず `CS0161 not all code paths return a value` でコンパイル失敗。
   - 修正: メソッド末尾に `yield break;` を追加（アサーションのみで完結するテストなのでイテレータとして即終了させる）。挙動・検証内容の変更なし。

2. **原因 story: S-03**（`git log --oneline -- game/Assets/Tests/PlayMode/MenuScreenPlayModeTests.cs` → `591c350`）
   - 症状: `MenuCanvas_IsScreenSpaceCamera()` が同様に `yield` 文を持たず `CS0161` でコンパイル失敗。
   - 修正: メソッド末尾に `yield break;` を追加。挙動・検証内容の変更なし。

3. **波及修正（S-02/S-03/S-09 の PlayMode テスト・機能に影響しない警告解消の副作用）**
   - `TitleScreenPlayModeTests.TearDown()` / `MenuScreenPlayModeTests.TearDown()` / `ResultScreenPlayModeTests.TearDown()` が基底 `InputTestFixture.TearDown()`（`void` 返却）を意図せず隠蔽していた（CS0114 warning）。一度 `override` を試みたところ戻り値型不一致（`IEnumerator` vs `void`）で `CS0508` コンパイルエラーになったため撤回し、`new` 修飾子を明示して意図的な隠蔽であることを明記する形に修正（挙動は変更なし。`[UnityTearDown]` 属性により NUnit がリフレクションで正しく呼び出すため実行時の差異なし）。

4. **原因 story: S-04**（`git log --oneline -- game/Assets/Tests/EditMode/WaveSpawnSystemTests.cs` → `7ae19e1`）
   - 症状: `Tick_EnemyReachingGoal_FiresGoalEvent_AndBecomesInactive` が EditMode テストで失敗（`Expected: 1 But was: 4`）。
   - 原因分析: テストが「WavePrepSec + SpawnIntervalBase + 経路踏破時間 + 2秒バッファ」まで一括で時間を進めていたが、WAVE1 は `SpawnIntervalBase`（1.0s）ごとに敵が出続ける仕様のため、2秒のバッファ内に後続 3 体（+1s, +2s, +3s 出現の敵で、ぎりぎり+3体目も間に合う計算）も次々ゴール到達し、イベントが 4 件発生していた。これは本番ロジック（`WaveSpawnSystem`）の仕様通りの挙動であり、テスト側の時間計算がテスト意図（「1体目がゴール到達した瞬間」だけを検証する）と噛み合っていなかったテストバグ。
   - 修正: 固定の総時間を一括で進める方式をやめ、`StepSeconds`（0.1s）刻みで Tick しながら最初のゴール到達イベントが発生した時点で停止するループに変更。以降のアサーションは変更なし。本番コード（`Systems/WaveSpawnSystem.cs` 等）は無修正。

### 結果

- EditMode: exit 0 / 48 total, 48 passed, 0 failed, 0 skipped（`qa/evidence/editmode-results.xml`）
- build 相当（ForgeBuild.BuildMac）: exit 0 / ログに `Build succeeded: ... -> Build/ForgeGame.app` / `game/Build/ForgeGame.app` 生成確認済み

## phase: prototype — 2026-07-22T03:16:04Z

- 対象: phase:prototype の全コード story（S-01〜S-09）のレーン中コミット済みコード（前回バッチ検証以降の再検証）
- 検証コマンド: 前回と同一（tech-stack-unity.md「検証コマンド」節の typecheck 相当 EditMode + build 相当 ForgeBuild.BuildMac）。手順1で両方とも exit 0（EditMode 48/48 passed、build succeeded）で一発合格したため、手順に定義された修正対象は無し。
- 追加確認: 手順に明記された2コマンドに加え、QA用テストコマンド（PlayMode: `-testPlatform PlayMode`）も実行し、レーン中「並走レーン規律により Unity 未起動・静的確認のみで review 化」と記録されていた story群（S-04/S-05/S-06/S-07/S-08/S-09）の実機検証を行った。

### 検出した問題と修正（PlayMode 実行で判明。手順で明記された2コマンドの対象外だが、バッチ検証の趣旨〔レーン中に Unity 未起動で静的確認のみだった story の一括検証〕に沿って追加調査・修正した）

1. **原因 story: S-08**（`git log --oneline -- game/Assets/Tests/PlayMode/GameHudPlayModeTests.cs` → `d0439af` "S-08: fix CR-CODE iter 1"。担当は ui-engineer だが直列区間の最小修正例外により gameplay-engineer が対応）
   - 症状: PlayMode 全体実行（50件）で `GameHudPlayModeTests` のクリック系5件
     （`ClickEmptyBuildSpot_OpensTowerSelectPanel` / `SelectAffordableTower_PlacesTower_ClosesPanel_AndGoldDisplayDecreases` /
     `InsufficientFundsSpot_BothButtonsDisabled_ClickDoesNotPlace` / `RightClick_ClosesPanel` / `OutsideClick_ClosesPanel`）が
     全滅（`hud.TowerSelect.IsOpen` が常に `False`）。
   - 原因分析: `GameHudPlayModeTests` は共有 `[UnitySetUp] SetUp()` で `SceneManager.LoadSceneAsync(Game)` を行い、各
     `[UnityTest]` 本体で `InputSystem.AddDevice<Mouse>()` → `Press`/`Release` する構成だった。これは
     `MenuScreenPlayModeTests.cs` / `TitleScreenPlayModeTests.cs` の先行コメントに実測記録済みの batchmode 既知不具合
     （`[UnitySetUp]`（コルーチン）と `[UnityTest]`（別コルーチン）の境界を跨ぐと、テスト側で追加した Mouse デバイスの
     状態が `InputActionMap.WasPressedThisFrame()` に反映されない）と完全に同一の原因。デバッグログを一時挿入し
     `HandleInput()` 内で `inputReader.ClickPressedThisFrame` が全フレームで `false` のままであることを実測で確認して
     特定した（デバッグログは原因特定後に削除・`HudPanel.cs` は無修正のまま復元）。
   - 修正: `GameHudPlayModeTests.cs` を Menu/Title と同じ「シーンロードとデバイス追加/入力擬似発行を同一コルーチン内に
     収める」パターンへ書き換え。共有 `[UnitySetUp]` を削除し、`private static IEnumerator LoadGame()` ヘルパーを追加、
     全8テストの冒頭で `yield return LoadGame();` を呼ぶ形にインライン化した。プロダクションコード（`HudPanel.cs` /
     `TowerSelectPanel.cs` / `BuildSpotController.cs` 等）は無修正。テストの検証内容・アサーションは変更なし
     （テストの実行順序上のバグ修正のみ）。
   - 対応区分: 機能の削除・無効化ではなく、既知パターンへの構造修正によりテストが本来検証すべき挙動を正しく検証できる
     ようにした（挙動変更ではない）。

### 結果（今回）

- EditMode: exit 0 / 48 total, 48 passed, 0 failed, 0 skipped（`qa/evidence/editmode-results.xml`）
- build 相当（ForgeBuild.BuildMac）: exit 0 / ログに `Build succeeded: ... -> Build/ForgeGame.app`
- PlayMode（QA用・追加確認）: exit 0 / 50 total, 50 passed, 0 failed, 0 skipped（`qa/evidence/playmode-results.xml`）

## phase: build — 2026-07-22T14:32:24Z

- 対象: phase:build の全コード story（S-10〜S-21）のレーン中コミット済みコード。うち S-14 の
  CR-CODE iteration 2 対応（BuildSpotController.cs 等）は本バッチ検証の直前に staged のまま未コミットで
  残存していたため、内容確認の上まず別コミット「S-14: fix CR-CODE iteration 2」として確定させてから
  本バッチ検証を実施した（batch-verify とは独立した既存 story fix のため commit を分離）。
- 検証コマンド（tech-stack-unity.md「検証コマンド」節）:
  1. typecheck 相当: `"$UNITY" -batchmode -projectPath game -runTests -testPlatform EditMode -testResults qa/evidence/editmode-results.xml -logFile -`
  2. build 相当: `"$UNITY" -batchmode -projectPath game -executeMethod ForgeGame.EditorTools.ForgeBuild.BuildMac -quit -logFile game/Logs/build.log`
- 手順1（1回目実行）: EditMode 92/92 passed（exit 0、コンパイルエラー0）・build 相当 exit 0（`Build succeeded`）。
  手順で定義された2コマンドはいずれも一発合格したため、手順上の修正対象は無し。
- 追加確認（前回 phase:prototype 分と同様、S-04/S-05等で「並走レーン規律により Unity 未起動・静的確認のみ」
  と記録されていた phase:build 全story の実機検証のため）: PlayMode（`-testPlatform PlayMode`）を実行したところ
  107件中9件が失敗（exit 2）。手順に明記された2コマンドの対象外だが、バッチ検証の趣旨に沿って追加調査・修正した。

### 検出した問題と修正（PlayMode 実行で判明）

1. **原因 story: S-21**（`git log --oneline -- game/Assets/Scripts/Components/EnvironmentView.cs` →
   `a812f8c`〜。CR-CODE iter2 が既に「解消はレーン合流後のバッチ検証実施者が (a) IMG-01/02 Integrate を
   バッチ実行前に完了させる、または (b) LogAssert.Expect 追加、のいずれかで行うこと」と明記していた既知
   リスク — state/reviews/s-21.md iter2）
   - 症状: `EnvironmentView.CreateTileMaterial` が IMG-01/02（tile-grass/tile-dirt-path）未取込時に出す
     `Debug.LogWarning` が `LogAssert.NoUnexpectedReceived()` を汚し、Game シーンをロードする
     `GameHudPlayModeTests`（2件）・`HoverPreviewPlayModeTests`（3件）・`PausePanelPlayModeTests`
     （`EscTwice_...`・1件）を false-fail させていた。加えて `EnvironmentPlayModeTests.
     Game_TileTextures_LoadOrFallbackAreConsistent` もフォールバック分岐の `Color` 厳密一致比較
     （URP マテリアルの `material.color` 読み出し時の内部色空間変換で生じる浮動小数差）で失敗していた。
   - 対応: **(a) を採用**。IMG-01（tile-grass.png）・IMG-02（tile-dirt-path.png）はいずれも AR-ASSET
     APPROVE 済み（`state/reviews/assets-images.md` iteration2・sha256実測一致確認済み）で `game/_generated/
     textures/` に存在していたため、`Assets/Resources/Generated/textures/` へ取込んだ（`tile-grass.png`
     ・`tile-dirt-path.png` を配置し、`textureType: Default`（0）・`sRGBTexture: 1`・`wrapU/V: Repeat`
     の新規 `.meta` を作成 — EnvironmentView.CreateTileMaterial が `Resources.Load<Texture2D>` 後に
     `wrapMode = Repeat` を実行時設定するため設定は実質的にランタイム側が上書きするが import 設定も一致
     させた）。`Editor/ForgeAssetIntegration.cs` の `CheckKnownGaps`（PLANNED 扱い）から IMG-01/02 を除去し
     `CheckTextures`（PASS 判定対象）へ移動。取込の結果、`CreateTileMaterial` は tex!=null の分岐を通るため
     LogWarning 自体が発生しなくなり、上記6件（フォールバック分岐比較テストの1件含む）は解消。
   - 対応区分: 機能の削除・無効化ではなく、既存 AR-ASSET 承認済み資産の Integrate を前倒しすることで
     EnvironmentView が本来の「テクスチャ表示」経路を通るようにした（意図された最終状態への到達）。
     `qa/evidence/s21-environment-composition.png` を再撮影し、地面(grass)・経路(dirt path)のタイル
     テクスチャが実際に表示されていることを目視確認済み。

2. **原因 story: S-19**（`git log --oneline -- game/Assets/Tests/PlayMode/AssetIntegrationPlayModeTests.cs`
   → `a31db59` "S-19: 資産統合"）
   - 症状: `AssetIntegrationPlayModeTests.WaveStart_PlaysAnnouncementSfx` が
     `'[Log] There are 2 audio listeners in the scene...'` の unhandled log で失敗。
   - 原因分析: 直前に実行される `Victory_PlaysVictoryJingleSfx` が勝利確定後に
     `GameFlow.GoToResult()`（`SceneManager.LoadScene`、既定 Single）で Result シーンへ実遷移するまで
     テストを進める設計のため、Result.unity 自身の Main Camera + AudioListener がランナーに残存する
     （Single ロードで前シーンは破棄されるが、テストコード側の `cameraGo`（自前カメラ）は既に fake-null
     になっているため TearDown の `if (cameraGo != null)` チェックで素通りし実害はない一方、Result.unity
     側の Listener は残り続ける）。次テストの `[SetUp]` が無条件で新しい AudioListener を追加していたため
     一時的に2つ存在する状態になっていた。
   - 修正: `AssetIntegrationPlayModeTests.SetUp()` を「既存 AudioListener が無い場合のみ自前で追加する」
     方式に変更（`cameraOwnsAudioListener` フィールドで所有権を記録）。TearDown の `DestroyImmediate(cameraGo)`
     は cameraGo 自身のみを破棄するため、既存（他 GameObject 所有）の Listener を巻き添えにしない。
   - **1回目の修正で新たな回帰を検出**: 当初は「SetUp 開始時点で残存する AudioListener を全て破棄してから
     自前のものを追加する」という、より単純な実装を先に試した。EditMode/PlayMode を再実行したところ
     `CoreDefensePlayModeTests.UnopposedWaves_...`（自前の Camera/AudioListener を持たない設計 — S-04）が
     `'[Log] There are no audio listeners in the scene...'` で新規に失敗することを検出した。原因は
     「クラス全体の実行後に残存 Listener が0になる」ことで、CoreDefensePlayModeTests のような『本番シーンは
     常に Listener が1つ存在する』という暗黙の前提に依存するテストが、他クラスの PlayClipAtPoint 由来の
     使い捨て AudioSource（自己破棄までの遅延タイマーがまだ切れていないもの）に対して Unity ネイティブの
     警告を誘発したため。単純な「全破棄」ではなく「既存が無い時だけ追加・自分が追加した分だけ破棄」という
     所有権ベースの実装に変更し、この回帰を解消した（再実行で確認）。
   - 対応区分: テストコードのみの修正（プロダクションコード無変更）。既存 Listener を破棄せず尊重する
     ことで両方の警告クラスを同時に回避する。

3. **原因 story: S-17**（`git log --oneline -- game/Assets/Tests/PlayMode/PausePanelPlayModeTests.cs` →
   `7c8c37f`〜）
   - 症状: `PausePanelPlayModeTests.Esc_OpensOverlay_SetsTimeScaleZero_AndStopsEnemyMovement` が
     「一時停止中に敵の位置が変化してしまっている」で失敗（期待値・実測値とも表示上は同じに見えるが
     実際は0.01m規模で不一致）。
   - 原因分析: テストが基準位置 `posBeforePause` を Esc の Press **より前**（一時停止が実際に反映される前）
     に採取していた。Press〜Release の2フレームは Esc 入力を処理して `Time.timeScale` を0へ切替える途中で、
     その間は通常速度で敵が移動するフレームが存在するため、「一時停止確定前」の基準位置と「一時停止確定後
     5フレーム経過後」の位置を比較すると、一時停止確定までの間に生じた僅かな移動量が不一致として現れていた。
   - 修正: `posBeforePause` の採取位置を、`Assert.IsTrue(pausePanel.IsOpen, ...)` で一時停止確定を確認した
     **直後**に移動。「一時停止中は動かない」という acceptance の検証意図（一時停止が有効な間の位置比較）
     により正確に一致させた。プロダクションコード（PausePanel/WaveSpawnController 等）は無変更 — 元の
     `Time.deltaTime` 駆動の自動停止ロジック自体は正しく機能していた（テスト側の基準タイミングの誤りだった）。

4. **既知の落とし穴への追記**: 上記2件（EnvironmentView LogWarning×LogAssert 衝突／AudioListener の
   SetUp/TearDown 所有権）はエンジン挙動・テストランナー起因の一般則のため
   `.claude/docs/tech-stack-unity.md`「既知の落とし穴」節へ追記する。

### 結果（phase:build・全修正後の再実行）

- EditMode: exit 0 / 92 total, 92 passed, 0 failed, 0 skipped（`qa/evidence/editmode-results.xml`）
- build 相当（ForgeBuild.BuildMac）: exit 0 / ログに `Build succeeded: ... -> Build/ForgeGame.app`
- PlayMode（QA用・追加確認・全修正後）: exit 0 / **107 total, 107 passed, 0 failed, 0 skipped**
  （`qa/evidence/playmode-results.xml`）
- `ForgeAssetIntegration.RunIntegrationCheck`: exit 0。MDL-01〜05 全PASS、IMG-01/02/03/04/08 全PASS、
  SFX-01〜06・BGM-01 全PASS。残る PLANNED は IMG-05/06/07（UI アイコン。各 UI story の担当領域・

## phase: build (Polish) — 2026-07-22T20:30:44Z

Polish レーン（gameplay-engineer 4件: S-22/S-24/S-25/S-26 / ui-engineer 1件: S-23）並走合流後、
未コミットのまま作業ツリーに残存していた（1Password SSH signing agent 障害により本セッション全体で
`git commit` が成立しなかった — 各 story のレビュー履歴 `state/reviews/s-22.md`〜`s-26.md` に記録済みの
継続ブロッカー）Polish story 分のコードを一括検証・修正した。着手時点の初回実行で EditMode 1件のコンパイル
エラー・PlayMode 10件の失敗を検出し、全て解消した。

### 検出・修正した問題（原因 story ごと）

1. **原因 story: S-23**（コミット未成立。working tree 上の新規ファイル
   `game/Assets/Tests/PlayMode/TowerActionPanelPlayModeTests.cs`）
   - 症状: EditMode 実行が **コンパイルエラーで起動不能**（`CS0120: An object reference is required for
     the non-static field, method, or property 'InputTestFixture.Move/Press/Release'`）。
   - 原因分析: クリック操作をまとめた補助コルーチン `ClickAt(Mouse mouse, Vector2 point)` が
     `private static IEnumerator` として宣言されていたが、内部で呼ぶ `Move`/`Press`/`Release` は
     `InputTestFixture`（テストクラスの継承元）のインスタンスメソッドのため、static コンテキストから
     `this` 無しで呼べない。
   - 修正: `ClickAt` から `static` 修飾子を除去（インスタンスメソッド化）。呼び出し側（同クラス内の
     `[UnityTest]` 群、いずれもインスタンスメソッド）は無変更で解決する。

2. **原因 story: S-23**（`Ui/HudPanel.cs` の `HandleInput`/`FindClickedEmptySpot`/`FindClickedTowerId`。
   コミット未成立のため story 帰属はレビュー履歴 `state/reviews/s-23.md` の対象ファイル一覧で特定）
   - 症状: 上記コンパイル修正後、PlayMode で `TowerActionPanelPlayModeTests` の6テスト全てが
     「設置済みタワー左クリックでアップグレード/売却パネルが開いていない」等で失敗。
   - 原因分析（診断用 `Debug.Log` を一時追加して実測・診断後に除去）: `GameConfig.Build.SpotPositions` は
     同一列（同じX）に経路を挟んで2スポット（Z=+3m/-3m）を配置するが、S-21 の固定俯瞰カメラ構図では
     同一列2スポットの画面距離が実測 約53〜58px しかなく、`GameConfig.Ui.BuildSpotClickPickRadiusPx`（70px）
     と重なる。旧 `HandleInput` は「空きスポットを全走査（`FindClickedEmptySpot`）→ 見つからなければ
     設置済みタワーを全走査（`FindClickedTowerId`）」という2段構えで、それぞれが独立に70px半径のヒット
     テストを行っていたため、スポット0（占有済み・クリック対象）をクリックしたはずが、隣接する
     スポット1（空き・同列反対側）を誤検出し `TowerSelectPanel` が開いて `TowerActionPanel` が開かない
     という実バグだった（既存の GameHudPlayModeTests は常に隣に占有スポットが無い単独クリックのみを
     検証しており、この隣接誤検出パターンを一度も踏んでいなかったため今回のバッチ検証で初めて顕在化）。
   - 修正: `FindClickedEmptySpot`/`FindClickedTowerId` の2段階走査を、占有有無を問わず画面上最近傍の
     1スポットのみを求める `FindClickedSpot` に一本化し、`HandleInput` 側でそのスポットの占有状態
     （`BuildSpotController.BuildSpots.IsOccupied`）に応じて `TowerSelectPanel`/`TowerActionPanel` の
     どちらを開くか分岐する設計に変更（`TryFindTowerBySpotIndex` を新設し `TowerInstance.SpotIndex` で
     引く）。`GameConfig.Ui.BuildSpotClickPickRadiusPx` の値自体は変更していない（ピック半径を単に縮める
     対症療法は他の列ペアにも同型の重なりがあるため根治にならず、UX の許容誤差を不必要に狭める副作用も
     あるため、ヒットテストの走査方式を直す構造的な修正を選んだ）。コメントのみで実装は変更していなかった
     `Assets/Tests/PlayMode/HoverPreviewPlayModeTests.cs`・`Assets/Scripts/Components/HoverPreviewController.cs`・
     `Assets/Scripts/GameConfig.cs` 内の「Ui/HudPanel.FindClickedEmptySpot」参照コメントも新関数名
     `FindClickedSpot` に追従して更新した（挙動変更なし）。
   - 対応区分: プロダクションコード修正（`Ui/HudPanel.cs`）。ui-engineer 領域だが、直列バッチ検証区間の
     例外規定（プロンプト指定）により最小修正として実施。機能の削除・無効化ではなく、クリックヒット
     テストの走査方式を構造的に正した。

3. **原因 story: S-26**（`Assets/Tests/PlayMode/CoreHitFlashPlayModeTests.cs`）/
   **S-24**（`Assets/Tests/PlayMode/TowerCombatPlayModeTests.cs` の
   `TowerFires_RotatesTowardTargetAndPlaysRecoilScalePunch_ThenReturnsToDefault`）/
   **S-25**（同ファイルの `DefeatedEnemyView_PlaysScaleDownMotion_BeforeBeingDestroyed`）
   - 症状: 3テストとも「持続時間経過後に既定状態へ戻らなかった」で失敗（CoreHitFlashSec=0.15s /
     TowerFireRecoilDurationSec=0.18s / EnemyDefeatShrinkDurationSec=0.2s のいずれも未達）。
   - 原因分析: 診断用テスト（`Time.deltaTime` を300フレーム分累積して破棄する一時ファイル。診断後に削除）
     で実測したところ、本環境の `-batchmode -runTests -testPlatform PlayMode` は平均
     `Time.deltaTime ≈ 0.00013秒/フレーム`（約7500fps相当）と極めて高速に回っており、
     `const int maxWaitFrames = 300;`（または `maxWaitFramesForDestroy = 300`）という「戻り待ち」ループの
     フレーム数上限では実時間換算 約0.04秒しか経過せず、いずれの演出時間（0.15〜0.2s）にも届かない。
     3件とも同一パターンの環境起因バグで、プロダクションコード（CoreView/TowerView/EnemyView の
     Update() 演出ロジック自体）に欠陥は無い。
   - 修正: 3ファイルとも `maxWaitFrames`/`maxWaitFramesForDestroy` を `300` → `20000`
     （対象 duration に対して2桁以上のマージンを確保できる値）に引き上げ、根拠コメントを追記した。
     テストコードのみの修正（プロダクションコード無変更）。

4. **既知の落とし穴への追記**: 上記2/3件（batchmode PlayMode の実フレーム速度／ビルドスポットの
   クリック判定を占有有無で走査を分けてはいけない）はエンジン挙動・設計原則としての一般則のため
   `.claude/docs/tech-stack-unity.md`「既知の落とし穴」節へ項目4・5として追記した。

### 結果（phase:build (Polish)・全修正後の再実行）

- EditMode: exit 0 / **93 total, 93 passed, 0 failed, 0 skipped**（`qa/evidence/editmode-results.xml`）
- build 相当（ForgeBuild.BuildMac）: exit 0 / ログに `Build succeeded: ... -> Build/ForgeGame.app`
- PlayMode（QA用・追加確認・全修正後）: exit 0 / **117 total, 117 passed, 0 failed, 0 skipped**
  （`qa/evidence/playmode-results.xml`。修正前は1件コンパイルエラーで起動不能 → 修正後は起動し10件失敗 →
  全修正後117件全passed）

### 未解決事項（Checkpoint へ carry-forward）

- **[BLOCKER] git commit 不能（1Password SSH signing agent 障害・環境起因・継続）**: 本バッチ検証時点でも
  `git commit --allow-empty` によるテスト実行で `error: 1Password: failed to fill whole buffer` /
  `fatal: failed to write commit object` を確認（`op whoami` は `account is not signed in`）。
  Polish 5story（S-22〜S-26）はいずれもコード内容としては CR-CODE MAX_ITER 到達・findings解消済みだが、
  git 上のコミットが一件も成立していない（working tree 上の変更のみで存在）。本バッチ検証はこの状態から
  出発し、コードの検証・修正は完了させたが、**コミットの成立自体は本バッチ検証実施者の権限では解決できない
  環境インフラ課題**であり、人間の 1Password サインイン復旧を要する。復旧後、本バッチ検証で確定した
  working tree 内容（S-22〜S-26 の実装 + 上記の修正）をコミットすること。
  未取込のまま）のみ。
