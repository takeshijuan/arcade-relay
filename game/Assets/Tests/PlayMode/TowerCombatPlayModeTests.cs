// TowerCombatPlayModeTests.cs — S-05 acceptance:
// 「空きビルドスポットへタワー種別(Bastion Cannon/Arc Emitter)を設置し、資金が実効コスト以上なら設置され
//  EconomySystem から実効コストが引かれる（不足時は設置不可）。設置タワーは射程内の敵へ規定間隔で
//  ダメージ適用イベント（発生源タワー種別込み）を発行し、EnemyHealthSystem が HP<=0 で撃破・撃破報酬を
//  資金へ加算する。『Bastion Cannon を経路脇に設置→Marauder が射程内で撃破される→資金が撃破報酬分増える』」
// を検証する。WaveSpawnController/BuildSpotController の enabled=false で MonoBehaviour.Update() の
// 自動駆動を止め、StepForTest（規約9のテスト用シーム）だけで決定論的に進める。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Systems;

namespace ForgeGame.Tests.PlayMode
{
    public class TowerCombatPlayModeTests
    {
        private GameObject coreGo;
        private GameObject waveGo;
        private GameObject buildGo;
        private CoreView coreView;
        private WaveSpawnController waveController;
        private BuildSpotController buildController;

        [SetUp]
        public void SetUp()
        {
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
        }

        [TearDown]
        public void TearDown()
        {
            if (buildGo != null) Object.Destroy(buildGo);
            if (waveGo != null) Object.Destroy(waveGo);
            if (coreGo != null) Object.Destroy(coreGo);
        }

        [UnityTest]
        public IEnumerator PlacingBastionCannon_KillsMarauderInRange_AndGrantsGoldReward()
        {
            int startingGold = GameConfig.Economy.StartingGold;
            Assert.AreEqual(startingGold, buildController.Economy.Gold);

            PlacementResult placement = buildController.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success, $"設置に失敗: {placement.FailureReason}");
            Assert.AreEqual(GameConfig.BastionCannon.Cost, placement.EffectiveCost);
            Assert.AreEqual(startingGold - GameConfig.BastionCannon.Cost, buildController.Economy.Gold);

            int goldAfterPlacement = buildController.Economy.Gold;

            const float stepSeconds = 0.1f;
            const float maxSimulatedSeconds = 60f; // WAVE_PREP_SEC(15) + 数発ぶんの余裕
            float simulated = 0f;

            while (buildController.EnemyHealth.KillCount == 0 && simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(stepSeconds);
                buildController.StepForTest(stepSeconds);
                simulated += stepSeconds;
                yield return null;
            }

            Assert.Less(simulated, maxSimulatedSeconds, "規定時間内に Marauder が撃破されなかった");
            Assert.AreEqual(1, buildController.EnemyHealth.KillCount);
            Assert.AreEqual(0, buildController.EnemyHealth.AoeKillCount, "Bastion Cannon 帰属の撃破は AoE撃破数に計上されない");
            Assert.AreEqual(goldAfterPlacement + GameConfig.Marauder.GoldReward, buildController.Economy.Gold);
            Assert.IsFalse(coreView.IsDefeated, "コア被弾なしで撃破できる射程内配置であること");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// S-25 acceptance: 撃破時（Components/WaveSpawnController.TryStartEnemyDefeatMotion 経由）は
        /// EnemyView の GameObject を同フレームで即 Destroy せず、gdd「モーション方式」節『対象メッシュの
        /// 非表示化（またはディゾルブ）』に対応するスケールダウン演出（GameConfig.Presentation.
        /// EnemyDefeatShrinkDurationSec）を挟んでから破棄することを検証する。
        /// </summary>
        [UnityTest]
        public IEnumerator DefeatedEnemyView_PlaysScaleDownMotion_BeforeBeingDestroyed()
        {
            PlacementResult placement = buildController.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success, $"設置に失敗: {placement.FailureReason}");

            const float stepSeconds = 0.1f;
            const float maxSimulatedSeconds = 60f; // WAVE_PREP_SEC(15) + 数発ぶんの余裕（既存テストと同一方針）
            float simulated = 0f;
            EnemyView defeatedView = null;

            while (simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(stepSeconds);
                buildController.StepForTest(stepSeconds);
                simulated += stepSeconds;

                if (buildController.EnemyHealth.KillCount > 0)
                {
                    // 撃破確定直後: IsPlayingDefeatMotion フラグの立った EnemyView を捕捉する。
                    // final-hit 直後に同フレームで Destroy されていればここで見つからず、後段の
                    // Assert.IsNotNull で不合格になる（acceptance「同フレームで消滅しない」の第一検証点）。
                    foreach (EnemyView candidate in waveGo.GetComponentsInChildren<EnemyView>())
                    {
                        if (candidate.IsPlayingDefeatMotion)
                        {
                            defeatedView = candidate;
                            break;
                        }
                    }
                    break;
                }

                yield return null;
            }

            Assert.Less(simulated, maxSimulatedSeconds, "規定時間内に Marauder が撃破されなかった");
            Assert.IsNotNull(defeatedView,
                "撃破確定フレームで撃破演出中の EnemyView が見つからなかった（同フレーム破棄されている疑い）");

            Vector3 baseScale = defeatedView.transform.localScale;

            // CR-CODE S-25 iter1 minor #3: EnemyView.Update() は同一フレーム内で
            // 「localScale を書き換え → t>=1 なら Destroy」の順に実行するため、実フレームの
            // Time.deltaTime が EnemyDefeatShrinkDurationSec 以上のヒッチが最初の yield 直後に
            // 起きると、スケール変化の中間状態を観測する前に破棄まで進むことがある。破棄済み＝
            // スケール変化は必ず発生済みとみなし、誤誘導的な false-fail を避ける。
            bool scaleChanged = false;
            bool destroyedBeforeScaleObserved = false;
            const int maxWaitFramesForScaleChange = 30;
            for (int i = 0; i < maxWaitFramesForScaleChange; i++)
            {
                yield return null; // EnemyView.Update() が実フレームの Time.deltaTime でスケールダウンを進める
                if (defeatedView == null)
                {
                    destroyedBeforeScaleObserved = true;
                    break;
                }
                if (defeatedView.transform.localScale != baseScale)
                {
                    scaleChanged = true;
                    break;
                }
            }
            Assert.IsTrue(scaleChanged || destroyedBeforeScaleObserved,
                "撃破後、破棄されるまでにスケールなど視覚的変化が観測されなかった");

            // CR-CODE S-25 iter1 minor #1: 60フレーム予算は EnemyDefeatShrinkDurationSec(0.2s) の
            // フレーム換算に余裕が無く、batchmode の実フレーム Time.deltaTime 次第で意図せず超過し得る。
            // batch-verify 修正メモ（phase:build Polish バッチ検証区間で発覚。tech-stack-unity.md「既知の
            // 落とし穴」参照）: 300フレームでも足りなかった。-batchmode の PlayMode 実フレームは vsync 無しで
            // 極めて高速に回り（本環境実測平均 Time.deltaTime ≈ 0.00013秒/フレーム＝約7500fps）、300フレーム
            // では実時間換算 約0.04秒しか経過せず EnemyDefeatShrinkDurationSec(0.2s) に届かない。
            // frame-timing 依存を避けるため十分大きいフレーム予算へ引き上げる。
            bool destroyed = defeatedView == null;
            const int maxWaitFramesForDestroy = 20000;
            for (int i = 0; i < maxWaitFramesForDestroy && !destroyed; i++)
            {
                yield return null;
                destroyed = defeatedView == null;
            }
            Assert.IsTrue(destroyed, "撃破演出の持続時間経過後に EnemyView（GameObject）が破棄されなかった");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TryPlaceTower_InsufficientGold_DoesNotPlaceOrDeductGold()
        {
            int placedCount = 0;
            while (buildController.Economy.Gold >= GameConfig.BastionCannon.Cost && placedCount < GameConfig.Build.NumBuildSpots)
            {
                PlacementResult r = buildController.TryPlaceTower(placedCount, TowerType.BastionCannon);
                Assert.IsTrue(r.Success);
                placedCount++;
            }

            int goldBefore = buildController.Economy.Gold;
            Assert.Less(goldBefore, GameConfig.BastionCannon.Cost, "資金が枯渇するまで設置してテスト前提を成立させる");

            PlacementResult failed = buildController.TryPlaceTower(placedCount, TowerType.BastionCannon);

            Assert.IsFalse(failed.Success);
            Assert.AreEqual(PlacementFailureReason.InsufficientGold, failed.FailureReason);
            Assert.AreEqual(goldBefore, buildController.Economy.Gold, "資金不足時は残高が変化しない");

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TryPlaceTower_OccupiedSpot_Fails_AndDoesNotDeductGoldAgain()
        {
            PlacementResult first = buildController.TryPlaceTower(0, TowerType.ArcEmitter);
            Assert.IsTrue(first.Success);
            int goldAfterFirst = buildController.Economy.Gold;

            PlacementResult second = buildController.TryPlaceTower(0, TowerType.BastionCannon);

            Assert.IsFalse(second.Success);
            Assert.AreEqual(PlacementFailureReason.SpotOccupied, second.FailureReason);
            Assert.AreEqual(goldAfterFirst, buildController.Economy.Gold);

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // S-10 acceptance: 「設置済みタワー左クリックでアップグレード/売却パネルを開き、資金が次Lv実効コスト
        // （基礎コスト×(1-UPG-02割引率)）以上なら Lv を上げダメージのみ増加する（役割・射程・間隔は不変、
        //  Lv3 で打止め）。Bastion Cannon を Lv1→Lv2→Lv3 へ強化するとダメージ/発が GameConfig の各Lv値に
        //  一致し、資金が実効コスト分減ることを検証できる」。パネル UI 自体は本 story のスコープ外
        // （BuildSpotController.TryUpgradeTower の設計コメント参照）のため、確定操作の受け口を直接呼ぶ。
        [UnityTest]
        public IEnumerator TryUpgradeTower_Lv1ToLv2ToLv3_IncreasesDamagePerShot_AndDeductsEffectiveCost()
        {
            PlacementResult placement = buildController.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success);
            int towerId = placement.Tower.Id;

            AssertFiredDamageAtCurrentLevel(GameConfig.BastionCannon.DamageLv1);

            int goldBeforeLv2 = buildController.Economy.Gold;
            TowerUpgradeResult toLv2 = buildController.TryUpgradeTower(towerId);
            Assert.IsTrue(toLv2.Success, $"Lv1→2 のアップグレードに失敗: {toLv2.FailureReason}");
            Assert.AreEqual(GameConfig.BastionCannon.UpgradeLv2Cost, toLv2.EffectiveCost);
            Assert.AreEqual(2, toLv2.Tower.Level);
            Assert.AreEqual(goldBeforeLv2 - GameConfig.BastionCannon.UpgradeLv2Cost, buildController.Economy.Gold);
            AssertFiredDamageAtCurrentLevel(GameConfig.BastionCannon.DamageLv2);

            // CR-CODE S-10 iter1 major #1: StartingGold(100) - Cost(50) - UpgradeLv2Cost(40) = 10 では
            // UpgradeLv3Cost(70) に届かず必ず InsufficientGold になる。撃破報酬経路（Wave 進行）に依存せず
            // 決定論的にテスト前提の資金を用意するため、Lv3強化に必要な分だけ直接加算する。
            buildController.Economy.Add(GameConfig.BastionCannon.UpgradeLv3Cost);
            int goldBeforeLv3 = buildController.Economy.Gold;
            TowerUpgradeResult toLv3 = buildController.TryUpgradeTower(towerId);
            Assert.IsTrue(toLv3.Success, $"Lv2→3 のアップグレードに失敗: {toLv3.FailureReason}");
            Assert.AreEqual(GameConfig.BastionCannon.UpgradeLv3Cost, toLv3.EffectiveCost);
            Assert.AreEqual(3, toLv3.Tower.Level);
            Assert.AreEqual(goldBeforeLv3 - GameConfig.BastionCannon.UpgradeLv3Cost, buildController.Economy.Gold);
            AssertFiredDamageAtCurrentLevel(GameConfig.BastionCannon.DamageLv3);

            TowerUpgradeResult beyondLv3 = buildController.TryUpgradeTower(towerId);
            Assert.IsFalse(beyondLv3.Success, "Lv3 は打止めのためアップグレードは失敗すること");
            Assert.AreEqual(TowerUpgradeFailureReason.AlreadyMaxLevel, beyondLv3.FailureReason);

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // S-11 acceptance: 「売却操作で設置+強化投入額×TOWER_SELL_REFUND_RATE を資金へ返還しスポットを空きに
        // 戻す（移設ではなく撤去）。投入額 N のタワーを売却すると資金が round(N×0.5) 増え、同スポットが
        // 再び設置可能になる」。パネル UI 自体は本 story のスコープ外（TrySellTower 設計コメント参照。
        // S-10 と同型のエスカレーション事象）のため、確定操作の受け口を直接呼ぶ。
        [UnityTest]
        public IEnumerator TrySellTower_RefundsInvestedGoldAtConfiguredRate_AndFreesSpotForReplacement()
        {
            PlacementResult placement = buildController.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success);
            int towerId = placement.Tower.Id;
            int investedGold = placement.EffectiveCost;
            int goldBeforeSell = buildController.Economy.Gold;

            TowerSellResult sell = buildController.TrySellTower(towerId);

            Assert.IsTrue(sell.Success, $"売却に失敗: {sell.FailureReason}");
            int expectedRefund = Mathf.RoundToInt(investedGold * GameConfig.Build.SellRefundRate);
            Assert.AreEqual(expectedRefund, sell.RefundAmount);
            Assert.AreEqual(goldBeforeSell + expectedRefund, buildController.Economy.Gold);
            Assert.IsFalse(buildController.BuildSpots.IsOccupied(0), "売却後はスポットが空きに戻ること");

            // 同スポットが再び設置可能になることを確認する（移設ではなく撤去+再設置前提 — gdd「売却」節）。
            PlacementResult replacement = buildController.TryPlaceTower(0, TowerType.ArcEmitter);
            Assert.IsTrue(replacement.Success, $"売却後の再設置に失敗: {replacement.FailureReason}");
            Assert.AreEqual(TowerType.ArcEmitter, buildController.BuildSpots.Towers[0].Type);

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator TrySellTower_AfterUpgrade_RefundsTotalInvestedGold()
        {
            PlacementResult placement = buildController.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success);
            int towerId = placement.Tower.Id;

            // 撃破報酬経路に依存せず決定論的にアップグレード資金を用意する（S-10 PlayMode テストと同じ方針）。
            buildController.Economy.Add(GameConfig.BastionCannon.UpgradeLv2Cost);
            TowerUpgradeResult upgrade = buildController.TryUpgradeTower(towerId);
            Assert.IsTrue(upgrade.Success);

            int investedGold = placement.EffectiveCost + GameConfig.BastionCannon.UpgradeLv2Cost;
            int goldBeforeSell = buildController.Economy.Gold;

            TowerSellResult sell = buildController.TrySellTower(towerId);

            Assert.IsTrue(sell.Success);
            int expectedRefund = Mathf.RoundToInt(investedGold * GameConfig.Build.SellRefundRate);
            Assert.AreEqual(expectedRefund, sell.RefundAmount);
            Assert.AreEqual(goldBeforeSell + expectedRefund, buildController.Economy.Gold);

            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        // S-24 acceptance: 「タワー発射イベント発生の前後でタワーの見た目のTransform（回転またはスケール）が
        // 変化し、一定時間後に既定状態へ戻ることを検証できる」。Bastion Cannon を経路脇に設置し、自然な
        // ウェーブ進行（PlacingBastionCannon_...テストと同一の駆動方式）で実際に発射させ、TowerView.VisualTransform
        // の回転/スケールが変化すること、その後リコイル持続時間経過後に既定状態へ戻ることを検証する。
        [UnityTest]
        public IEnumerator TowerFires_RotatesTowardTargetAndPlaysRecoilScalePunch_ThenReturnsToDefault()
        {
            PlacementResult placement = buildController.TryPlaceTower(0, TowerType.BastionCannon);
            Assert.IsTrue(placement.Success, $"設置に失敗: {placement.FailureReason}");

            TowerView towerView = buildGo.GetComponentInChildren<TowerView>();
            Assert.IsNotNull(towerView, "設置直後に TowerView が生成されていること");
            Transform visual = towerView.VisualTransform;
            Assert.IsNotNull(visual, "TowerView の見た目(visual)が生成されていること");

            Vector3 baseScale = visual.localScale;
            Quaternion baseRotation = visual.rotation;

            const float stepSeconds = 0.1f;
            const float maxSimulatedSeconds = 60f; // WAVE_PREP_SEC(15) + 数発ぶんの余裕（S-05 既存テストと同一方針）
            float simulated = 0f;
            bool motionObserved = false;

            while (simulated < maxSimulatedSeconds)
            {
                waveController.StepForTest(stepSeconds);
                buildController.StepForTest(stepSeconds);
                simulated += stepSeconds;
                yield return null; // TowerView.Update() が実フレームの Time.deltaTime でリコイルスケールを進める

                if (visual.localScale != baseScale)
                {
                    motionObserved = true;
                    break;
                }
            }

            Assert.IsTrue(motionObserved, "規定時間内にタワー発射のリコイル（スケール変化）が観測されなかった");
            Assert.Greater(Quaternion.Angle(baseRotation, visual.rotation), 1f,
                "発射時に visual が対象敵方向へ回転していること（1度未満の差は誤差とみなさない）");
            Assert.Greater(visual.localScale.x, baseScale.x, "発射直後はリコイルでスケールが既定値より拡大していること");

            // 一定時間後（TowerFireRecoilDurationSec 経過後）に既定サイズへ戻ることを検証する。
            // 以降 StepForTest は呼ばない（更なる発射でリコイルが再トリガーされ復帰を観測できなくなることを避ける）。
            // batch-verify 修正メモ（phase:build Polish バッチ検証区間で発覚。tech-stack-unity.md「既知の
            // 落とし穴」参照）: 300フレームでは -batchmode の実フレーム速度（本環境実測平均 Time.deltaTime
            // ≈ 0.00013秒/フレーム＝約7500fps）に対し実時間換算 約0.04秒しか経過せず
            // TowerFireRecoilDurationSec(0.18s) に届かない。frame-timing 依存を避けるため十分大きいフレーム
            // 予算へ引き上げる。
            bool returnedToDefault = false;
            const int maxWaitFrames = 20000;
            for (int i = 0; i < maxWaitFrames; i++)
            {
                yield return null;
                if (visual.localScale == baseScale)
                {
                    returnedToDefault = true;
                    break;
                }
            }
            Assert.IsTrue(returnedToDefault, "リコイル持続時間経過後に既定サイズへ戻らなかった");

            LogAssert.NoUnexpectedReceived();
        }

        /// <summary>
        /// タワー射程内(スポット0)へ撃破されない十分なHPの敵を直接注入し、TowerCombatSystem.Tick を
        /// 発射間隔ぶん直接進めて1発の発射ダメージを観測する（BuildSpotController.BuildSpots は公開プロパティ
        /// のためテストから直接 Tick を駆動できる。役割・射程・間隔は不変のため FireInterval も定数のまま使う）。
        /// </summary>
        private void AssertFiredDamageAtCurrentLevel(int expectedDamage)
        {
            var enemies = new System.Collections.Generic.List<EnemyInstance>
            {
                new EnemyInstance
                {
                    Id = 999,
                    Type = EnemyType.Warbeast,
                    DistanceTraveledM = GameConfig.Build.SpotPositions[0].x - GameConfig.Path.StartPoint.x,
                    Active = true,
                    Hp = GameConfig.Warbeast.Hp * 10,
                },
            };
            var events = new System.Collections.Generic.List<DamageEvent>();

            TowerCombatSystem.Tick(GameConfig.BastionCannon.FireInterval, buildController.BuildSpots, enemies, events);

            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(expectedDamage, events[0].Damage);
        }
    }
}
