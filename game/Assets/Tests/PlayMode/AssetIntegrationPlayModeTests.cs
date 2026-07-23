// AssetIntegrationPlayModeTests.cs — S-19 acceptance:
// 「MDL-01〜05（GLB）を...タワー(Bastion Cannon/Arc Emitter)・敵(Marauder/Warbeast)・コアの各 View に
//  割当てる。SFX-01〜06・BGM-01を...取込み、BGM が Game 中ループ再生され、FeedbackCueSystem のキューに
//  対応して発射/撃破/コア被弾/ウェーブ開始(SFX-05)/勝利の各イベントで対応 SFX が鳴る。PlayMode テストで、
//  タワー/敵/コアの主要 Renderer に null/InternalErrorShader(ピンク) が無いこと・各イベント発火時に対応
//  AudioSource が再生されること・スクリーンショット証跡に配置モデルが写ることを検証できる」を検証する。
// TowerCombatPlayModeTests/RunOutcomePlayModeTests と同じ規約9パターン（非アクティブ生成→注入→アクティブ化・
// StepForTest だけで決定論的に進める）を踏襲するが、Components/AudioCuePlayer.PlayOneShot は
// AudioListener の実在を要求する（ファイル冒頭コメント参照。無いと再生自体をスキップする）ため、
// 本テストは専用の Camera+AudioListener を明示的に用意する（GameConfig.Camera の構図＝S-21を流用し、
// スクリーンショット証跡に配置モデルが写ることの検証も兼ねる）。
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Persistence;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Tests.PlayMode
{
    public class AssetIntegrationPlayModeTests
    {
        private const int CaptureWidth = 1280;
        private const int CaptureHeight = 720;

        // gates.md 撮影方式の機械判定基準（magick identify -format "%[fx:mean]" と同型）。平均輝度が
        // この範囲外なら黒潰れ/白飛び(SUSPECT_BLANK)の疑い（CR-CODE S-19 iter1 minor #2/#5 対応）。
        private const double BlankMeanMinThreshold = 0.02;
        private const double BlankMeanMaxThreshold = 0.98;

        private GameObject cameraGo;
        // batch-verify(Build) 2026-07-22: cameraGo が自前で AudioListener を追加したか（=破棄してよい所有物か）
        // を記録する。SetUp コメント参照。
        private bool cameraOwnsAudioListener;
        private GameObject lightGo;
        private GameObject coreGo;
        private GameObject waveGo;
        private GameObject buildGo;
        private GameObject outcomeGo;

        // CR-CODE S-19 iter2 minor #1: RenderSettings.ambientMode/ambientLight はシーン共有のグローバル状態。
        // SetUp で Flat 環境光へ上書きするだけで TearDown が元に戻さないと、後続 PlayMode テスト（同一ランナー
        // シーンを共有する）へ Flat 上書きが漏れ、テスト分離が壊れる。SetUp 開始時点の値を退避し TearDown で復元する。
        private AmbientMode prevAmbientMode;
        private Color prevAmbientLight;

        private CoreView coreView;
        private WaveSpawnController waveController;
        private BuildSpotController buildController;
        private RunOutcomeController outcome;

        [SetUp]
        public void SetUp()
        {
            cameraGo = new GameObject("TestCamera");
            cameraGo.tag = "MainCamera";
            Camera cam = cameraGo.AddComponent<Camera>();
            // batch-verify(Build) 2026-07-22 対応: Victory_PlaysVictoryJingleSfx は勝利確定後に
            // GameFlow.GoToResult()（SceneManager.LoadScene、既定 Single）で実シーン遷移まで進める
            // 「テスト後の状態を綺麗にする」設計のため、ランナーへ Result.unity（自前の Main Camera+
            // AudioListener を持つ）がロードされたまま残ることがある。無条件に自前 AudioListener を
            // 追加すると「There are 2 audio listeners in the scene」警告で LogAssert.NoUnexpectedReceived
            // を false-fail させる（実測: WaveStart_PlaysAnnouncementSfx）ため、既存の AudioListener が
            // 無いときだけ自前で追加する（cameraOwnsAudioListener で所有権を記録し、TearDown の
            // DestroyImmediate(cameraGo) が「他 GameObject が持つ既存 Listener」を巻き添えにしないように
            // する）。逆に全ての残存 Listener を無条件破棄する設計は、他テストクラス（例:
            // CoreDefensePlayModeTests。自前の Listener を持たない設計）が「シーンに常に最低1つ Listener が
            // 存在する」という本番と同じ前提に暗黙に依存しているため、"There are no audio listeners" 警告を
            // 誘発し別テストを無関係に落とすことが判明した（実測）。既存 Listener を尊重し破棄しないことで
            // 両方の警告を同時に回避する。
            cameraOwnsAudioListener = Object.FindAnyObjectByType<AudioListener>() == null;
            if (cameraOwnsAudioListener)
            {
                cameraGo.AddComponent<AudioListener>();
            }
            cameraGo.transform.position = GameConfig.Camera.Position;
            cameraGo.transform.eulerAngles = GameConfig.Camera.EulerAngles;
            cam.fieldOfView = GameConfig.Camera.FieldOfViewDeg;

            // CR-CODE S-19 iter1 minor #2/#5: EnvironmentView(S-21)と同じ GameConfig.Lighting 定数で
            // Directional Light + Flat 環境光を明示的に用意する。Game シーンをロードしない孤立フィクスチャの
            // ため、Light 無しでは配置モデルが暗転しスクリーンショット証跡が SUSPECT_BLANK になりかねない。
            lightGo = new GameObject("TestSunLight");
            Light sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = GameConfig.Lighting.DirectionalIntensityLux;
            sun.color = GameConfig.Lighting.DirectionalColor;
            lightGo.transform.eulerAngles = GameConfig.Lighting.DirectionalEulerAngles;
            prevAmbientMode = RenderSettings.ambientMode;
            prevAmbientLight = RenderSettings.ambientLight;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = GameConfig.Lighting.AmbientColor;

            coreGo = new GameObject("TestCore");
            coreGo.SetActive(false);
            coreView = coreGo.AddComponent<CoreView>();
            coreGo.SetActive(true);

            waveGo = new GameObject("TestWaveSpawnController");
            waveGo.SetActive(false);
            waveController = waveGo.AddComponent<WaveSpawnController>();
            waveController.SetCoreViewForTest(coreView);
            waveGo.SetActive(true);
            waveController.enabled = false;

            buildGo = new GameObject("TestBuildSpotController");
            buildGo.SetActive(false);
            buildController = buildGo.AddComponent<BuildSpotController>();
            buildController.SetWaveSpawnControllerForTest(waveController);
            buildGo.SetActive(true);
            buildController.enabled = false;

            outcomeGo = new GameObject("TestRunOutcomeController");
            outcomeGo.SetActive(false);
            outcome = outcomeGo.AddComponent<RunOutcomeController>();
            outcome.SetWaveSpawnControllerForTest(waveController);
            outcome.SetCoreViewForTest(coreView);
            outcome.SetBuildSpotControllerForTest(buildController);
            // 実 I/O を避けるだけのテストダブル注入（S-19 独自ダブルの重複定義を避け、既存の
            // Persistence.InMemorySaveStore を再利用する — CR-CODE S-19 iter1 minor #6）。
            outcome.SetSaveStoreForTest(new InMemorySaveStore());
            outcomeGo.SetActive(true);
            outcome.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (outcomeGo != null) Object.Destroy(outcomeGo);
            if (buildGo != null) Object.Destroy(buildGo);
            if (waveGo != null) Object.Destroy(waveGo);
            if (coreGo != null) Object.Destroy(coreGo);
            // CR-CODE S-19 iter1 minor #7: cameraGo が自前で AudioListener を持つ場合（cameraOwnsAudioListener）、
            // [Test]（非コルーチン）同期テストは同一フレーム内で連続実行され得るため、遅延破棄（Destroy）だと
            // 前テストの AudioListener 破棄が完了する前に次 SetUp が新しい AudioListener を生成し、一時的に
            // 2つ存在する状態（"There are 2 audio listeners" Warning）が起こり得る。この Warning が後続
            // [UnityTest] の LogAssert.NoUnexpectedReceived() 検証区間に落ちると無関係な原因で失敗するため、
            // cameraGo は常に即時破棄（DestroyImmediate）にする。cameraOwnsAudioListener が false の場合、
            // 破棄されるのは cameraGo 自身（と付与した Camera）のみで、他 GameObject が持つ既存
            // AudioListener（Victory_PlaysVictoryJingleSfx 等の実シーン遷移の残存物）は巻き添えにしない
            // （SetUp コメント参照 — 破棄すると後続の無関係なテストで "no audio listeners" Warning を誘発する）。
            if (cameraGo != null) Object.DestroyImmediate(cameraGo);
            if (lightGo != null) Object.Destroy(lightGo);
            // CR-CODE S-19 iter2 minor #1: SetUp で上書きした Flat 環境光を後続テストへ漏らさないよう復元する。
            RenderSettings.ambientMode = prevAmbientMode;
            RenderSettings.ambientLight = prevAmbientLight;
            GameFlow.ClearRunResult();
            GameFlow.SetCurrentSaveData(null);
            GameFlow.SetSaveFailed(false);
        }

        private static string EvidenceDir
        {
            get
            {
                string gameDir = Directory.GetParent(Application.dataPath).FullName;
                string repoRoot = Directory.GetParent(gameDir).FullName;
                string dir = Path.Combine(repoRoot, "qa", "evidence");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static void CaptureSceneScreenshot(Camera cam, string fileName)
        {
            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;
            Texture2D tex = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                tex = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                tex.Apply();

                // CR-CODE S-19 iter2 minor #2: SUSPECT_BLANK で assert が落ちても診断用の証跡が qa/evidence/
                // に一切残らない（かつ正準ファイル名のまま古い成功時 PNG が残存して QA を誤認させかねない）問題の
                // 対応。先に正準ファイル名で書き出してから輝度 assert する（assert 失敗時も直近描画の実写証跡が
                // 残る。stale な旧 PNG を確実に最新の描画結果で上書きする副次効果もある）。
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(Path.Combine(EvidenceDir, fileName), png);

                AssertNotBlank(tex, fileName);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                rt.Release();
                Object.Destroy(rt);
                if (tex != null) Object.Destroy(tex);
            }
        }

        /// <summary>gates.md「視覚証跡の目視義務」の機械検知先行チェック（magick identify -format
        /// "%[fx:mean]" 相当）をテスト内で行う。CR-CODE S-19 iter1 minor #2/#5 対応: 証跡が黒潰れ/白飛び
        /// のまま green になる取りこぼしを塞ぐ（目視自体は QA-PLAY の責務のまま — ここでは機械検知のみ）。</summary>
        private static void AssertNotBlank(Texture2D tex, string context)
        {
            Color32[] pixels = tex.GetPixels32();
            double sum = 0;
            foreach (Color32 p in pixels)
            {
                sum += (p.r + p.g + p.b) / 3.0 / 255.0;
            }
            double mean = pixels.Length > 0 ? sum / pixels.Length : 0.0;
            Assert.IsTrue(
                mean >= BlankMeanMinThreshold && mean <= BlankMeanMaxThreshold,
                $"{context}: スクリーンショットの平均輝度が {mean:F4}（許容範囲 {BlankMeanMinThreshold:F2}〜{BlankMeanMaxThreshold:F2} 外）— 黒潰れ/白飛び(SUSPECT_BLANK)の疑い");
        }

        /// <summary>CR-CODE S-19 iter1 major #1 対応: GeneratedModelFactory が実モデルを解決できず
        /// PlaceholderFactory へフォールバックした場合でも、フォールバック後の Renderer は
        /// AssertRendererSane と同じ健全性判定（null/ピンク無し）を通過してしまう（プレースホルダ盲目）。
        /// 「実モデルが実際に使われたこと」を (1) Resources 解決可否 (2) GeneratedModelFactory 命名規約
        /// （"{type}Model"／"CoreModel"。PlaceholderFactory の "{type}PlaceholderVisual" ではない）の
        /// 両方で明示的に検証する。</summary>
        private static void AssertUsesRealModel(GameObject root, string expectedModelChildName, string assetKey, string context)
        {
            Assert.IsNotNull(
                Resources.Load<GameObject>(assetKey),
                $"{context}: モデル資産 '{assetKey}' が Resources から解決できない（未取込/未生成）");

            Transform modelChild = root.transform.Find(expectedModelChildName);
            Assert.IsNotNull(
                modelChild,
                $"{context}: 実モデル子 '{expectedModelChildName}' が見つからない" +
                "（GeneratedModelFactory が null を返し PlaceholderFactory へフォールバックした可能性）");
        }

        private static void AssertRendererSane(GameObject root, string context)
        {
            Assert.IsNotNull(root, $"{context}: GameObject が見つからない");
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Assert.Greater(renderers.Length, 0, $"{context}: Renderer が1つも無い");
            foreach (Renderer r in renderers)
            {
                Material mat = r.sharedMaterial;
                Assert.IsNotNull(mat, $"{context}: '{r.name}' の material が null");
                Assert.IsNotNull(mat.shader, $"{context}: '{r.name}' の material に shader が無い");
                Assert.AreNotEqual("Hidden/InternalErrorShader", mat.shader.name,
                    $"{context}: '{r.name}' の material がピンク(InternalErrorShader)＝マテリアル欠落");
            }
        }

        /// <summary>resourceKey の AudioClip を再生中の AudioSource がシーン内に存在することを検証する。
        /// AudioCuePlayer.PlayOneShot はワンショット GameObject を実時間ベースの遅延破棄（Destroy(obj, delay)）
        /// で消すため、トリガー直後（yield を挟まず）に呼ぶこと。</summary>
        private static void AssertOneShotPlaying(string assetKey, string context)
        {
            AudioClip expectedClip = Resources.Load<AudioClip>(assetKey);
            Assert.IsNotNull(expectedClip, $"{context}: AudioClip '{assetKey}' が Resources から解決できない（未取込）");

            var sources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            bool found = false;
            foreach (AudioSource s in sources)
            {
                if (s.clip == expectedClip && s.isPlaying) { found = true; break; }
            }
            Assert.IsTrue(found, $"{context}: '{assetKey}' を再生中の AudioSource(isPlaying==true) が見つからない");
        }

        [UnityTest]
        public IEnumerator PlacedTowersAndSpawnedEnemy_HaveSaneRenderers_AndScreenshotShowsPlacedModels()
        {
            PlacementResult bastion = buildController.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(bastion.Success, $"Bastion Cannon 設置に失敗: {bastion.FailureReason}");
            PlacementResult arc = buildController.TryPlaceTower(1, TowerType.ArcEmitter);
            Assert.IsTrue(arc.Success, $"Arc Emitter 設置に失敗: {arc.FailureReason}");

            const float stepSeconds = 0.5f;
            const float maxSimulatedSeconds = 60f;
            float simulated = 0f;
            while (waveController.WaveSystem.ActiveEnemyCount == 0 && simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(stepSeconds);
                simulated += stepSeconds;
                yield return null;
            }
            Assert.Less(simulated, maxSimulatedSeconds, "規定時間内に敵がスポーンしなかった");

            GameObject bastionGo = GameObject.Find($"Tower_{bastion.Tower.Id}_{TowerType.BastionCannon}");
            GameObject arcGo = GameObject.Find($"Tower_{arc.Tower.Id}_{TowerType.ArcEmitter}");
            AssertRendererSane(bastionGo, "Bastion Cannon View");
            AssertRendererSane(arcGo, "Arc Emitter View");
            AssertRendererSane(coreGo, "Core View");

            // major #1 対応: プレースホルダ盲目を塞ぐ（実モデル子の実在を明示検証）。
            AssertUsesRealModel(bastionGo, $"{TowerType.BastionCannon}Model", GameConfig.AssetKeys.ModelBastionCannon, "Bastion Cannon View");
            AssertUsesRealModel(arcGo, $"{TowerType.ArcEmitter}Model", GameConfig.AssetKeys.ModelArcEmitter, "Arc Emitter View");
            AssertUsesRealModel(coreGo, "CoreModel", GameConfig.AssetKeys.ModelCoreCrystal, "Core View");

            bool anyEnemyRendererSane = false;
            foreach (Transform child in waveGo.transform)
            {
                if (!child.name.StartsWith("Enemy_")) continue;
                AssertRendererSane(child.gameObject, $"Enemy View ({child.name})");

                EnemyView enemyView = child.GetComponent<EnemyView>();
                Assert.IsNotNull(enemyView, $"Enemy View ({child.name}): EnemyView コンポーネントが無い");
                string modelKey = enemyView.Type == EnemyType.Marauder
                    ? GameConfig.AssetKeys.ModelMarauder
                    : GameConfig.AssetKeys.ModelWarbeast;
                AssertUsesRealModel(child.gameObject, $"{enemyView.Type}Model", modelKey, $"Enemy View ({child.name})");

                anyEnemyRendererSane = true;
            }
            Assert.IsTrue(anyEnemyRendererSane, "スポーン済みの Enemy View が見つからない");

            yield return null;
            CaptureSceneScreenshot(cameraGo.GetComponent<Camera>(), "s19-asset-integration-placed-models.png");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EnemyView_Warbeast_Initialize_ProducesSaneRenderer()
        {
            // Warbeast(MDL-04) は WAVE 3 以降でのみ湧く（S-12）ため、通常のウェーブ進行では検証に時間が
            // かかりすぎる。EnemyView.Initialize を直接呼び、Renderer の健全性のみを対象に検証する。
            var go = new GameObject("TestWarbeastView");
            go.SetActive(false);
            EnemyView view = go.AddComponent<EnemyView>();
            go.SetActive(true);

            view.Initialize(1, EnemyType.Warbeast);

            AssertRendererSane(go, "Warbeast EnemyView");
            // major #1 対応: MDL-04 は stories.yaml が明記する「GLTFast インポート確定タイミング」の
            // 既知リスクにより最もプレースホルダへ縮退しやすい資産。ここで実モデル使用を明示検証する。
            AssertUsesRealModel(go, $"{EnemyType.Warbeast}Model", GameConfig.AssetKeys.ModelWarbeast, "Warbeast EnemyView");

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator WaveStart_PlaysAnnouncementSfx()
        {
            waveController.StepForTest(0.01f);
            AssertOneShotPlaying(GameConfig.AssetKeys.SfxWaveStart, "ウェーブ開始(SFX-05)");

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CoreHit_PlaysCoreHitSfx()
        {
            coreView.ApplyDamage(EnemyType.Marauder);
            AssertOneShotPlaying(GameConfig.AssetKeys.SfxCoreHit, "コア被弾(SFX-04)");
        }

        [UnityTest]
        public IEnumerator PlaceTower_TowerFire_EnemyDefeat_PlayCorrespondingSfx()
        {
            PlacementResult placement = buildController.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success, $"設置に失敗: {placement.FailureReason}");

            // SFX-01（設置音）は TryPlaceTower 呼び出し内で同期的に再生される（BuildSpotController.TryPlaceTower）。
            AssertOneShotPlaying(GameConfig.AssetKeys.SfxTowerPlace, "タワー設置(SFX-01)");

            const float stepSeconds = 0.1f;
            const float maxSimulatedSeconds = 60f;
            float simulated = 0f;
            while (buildController.EnemyHealth.KillCount == 0 && simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(stepSeconds);
                buildController.StepForTest(stepSeconds);
                simulated += stepSeconds;
                if (buildController.EnemyHealth.KillCount > 0) break;
                yield return null;
            }
            Assert.Less(simulated, maxSimulatedSeconds, "規定時間内に撃破が発生しなかった");
            Assert.AreEqual(1, buildController.EnemyHealth.KillCount);

            // 撃破に至った StepForTest 呼び出し内で SFX-02（発射）→SFX-03（撃破）の順に同期再生される
            // （BuildSpotController.StepSimulation）。yield を挟まず直後に確認する。
            AssertOneShotPlaying(GameConfig.AssetKeys.SfxTowerFire, "タワー発射(SFX-02)");
            AssertOneShotPlaying(GameConfig.AssetKeys.SfxEnemyDefeat, "敵撃破(SFX-03)");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Victory_PlaysVictoryJingleSfx()
        {
            Assert.IsTrue(buildController.TryPlaceTower(0, TowerType.BastionCannon).Success);
            Assert.IsTrue(buildController.TryPlaceTower(1, TowerType.ArcEmitter).Success);

            const float step = 0.5f;
            const float maxSimulatedSeconds = 400f;
            float simulated = 0f;

            // 迎撃シミュレーション: 各ティック後に生存中の敵を即座に撃破し、ゴール到達を防ぎながら
            // 全ウェーブを消化する（RunOutcomePlayModeTests と同じ方針。ここでは勝利確定時の SFX-06
            // 再生配線のみを対象にする）。
            while (!outcome.IsFinalized && simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(step);

                foreach (EnemyInstance enemy in new List<EnemyInstance>(waveController.WaveSystem.Enemies))
                {
                    if (!enemy.Active) continue;
                    EnemyDamageResult dr = waveController.WaveSystem.ApplyDamage(enemy.Id, 9999);
                    if (dr.Defeated)
                    {
                        TowerType attributedTo = enemy.Type == EnemyType.Marauder ? TowerType.BastionCannon : TowerType.ArcEmitter;
                        buildController.EnemyHealth.RecordKill(attributedTo);
                    }
                }

                outcome.StepForTest(step);
                simulated += step;
                if (outcome.IsFinalized) break;
                yield return null;
            }

            Assert.Less(simulated, maxSimulatedSeconds, "規定時間内に勝利判定が確定しなかった");
            Assert.IsTrue(outcome.IsFinalized);
            Assert.IsFalse(coreView.IsDefeated, "想定外の敗北（迎撃シミュレーションが壊れている）");

            // FinalizeRun(isWin:true) 内で SFX-06（勝利ジングル）が同期再生される。yield を挟まず直後に確認する。
            AssertOneShotPlaying(GameConfig.AssetKeys.SfxVictoryJingle, "勝利(SFX-06)");

            // Result 遷移まで進めてテスト後の状態を綺麗にする（RunOutcomePlayModeTests と同じ後始末方針）。
            const float delayStep = 0.5f;
            float waited = 0f;
            float ceiling = GameConfig.Presentation.WinResultDelaySec + 5f;
            while (!outcome.HasTransitioned && waited < ceiling)
            {
                outcome.StepForTest(delayStep);
                waited += delayStep;
                yield return null;
            }
            Assert.Less(waited, ceiling, "演出待機後も Result へ遷移しなかった");
        }

        [UnityTest]
        public IEnumerator BgmController_Start_PlaysLoopingBgmClip()
        {
            var bgmGo = new GameObject("TestBgmController");
            bgmGo.SetActive(false);
            BgmController bgm = bgmGo.AddComponent<BgmController>();
            bgmGo.SetActive(true);

            yield return null; // Start() 実行を待つ

            AudioClip expectedClip = Resources.Load<AudioClip>(GameConfig.AssetKeys.BgmMainTheme);
            Assert.IsNotNull(expectedClip, "BGM-01 の AudioClip が Resources から解決できない（未取込）");

            AudioSource source = bgm.AudioSourceForTest;
            Assert.IsNotNull(source, "BgmController が AudioSource を生成していない");
            Assert.AreEqual(expectedClip, source.clip, "BgmController の AudioSource.clip が BGM-01 と一致しない");
            Assert.IsTrue(source.loop, "BGM は Game シーン中ループ再生される契約（loop=true）");
            Assert.IsTrue(source.isPlaying, "BgmController が BGM を再生していない");

            Object.Destroy(bgmGo);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
