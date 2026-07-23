// BuildSpotController.cs — Game シーンのビルドスポット/タワー戦闘/経済オーケストレータ（S-05）。
// Systems/BuildSpotSystem・TowerCombatSystem・EnemyHealthSystem・EconomySystem（いずれも純粋 C#）を
// Update() の Time.deltaTime で駆動し、BuildSpotView/TowerView の生成・撃破時の敵View削除・資金反映を
// 配線する。判定・戦闘解決・経済計算のロジックは全て Systems/ に委譲する（規約3: Components は薄く）。
// 左クリック→タワー種別選択メニュー（Ui/TowerSelectPanel・S-08）からの確定操作は TryPlaceTower を呼ぶ想定
// （メニューの表示・開閉自体は S-08 の責務）。
using System;
using System.Collections.Generic;
using UnityEngine;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;

namespace ForgeGame.Components
{
    /// <summary>Game シーンに1つだけ置く。WaveSpawnController への参照は SerializeField で配線する。</summary>
    public sealed class BuildSpotController : MonoBehaviour
    {
        [SerializeField] private WaveSpawnController waveSpawnController;

        private BuildSpotSystem buildSpotSystem;
        private EconomySystem economySystem;
        private float towerDiscountRate;
        private readonly EnemyHealthSystem enemyHealthSystem = new EnemyHealthSystem();
        private readonly List<DamageEvent> damageEventsBuffer = new List<DamageEvent>();
        private readonly Dictionary<int, TowerView> towerViews = new Dictionary<int, TowerView>();
        private bool spotViewsSpawned;

        public BuildSpotSystem BuildSpots => buildSpotSystem;
        public EconomySystem Economy => economySystem;
        public EnemyHealthSystem EnemyHealth => enemyHealthSystem;

        /// <summary>UPG-02 適用後のタワー設置/強化コスト割引率（S-14。TryPlaceTower/TryUpgradeTower が使う実効値のテスト観測用）。</summary>
        public float TowerDiscountRate => towerDiscountRate;

        /// <summary>
        /// Awake() 時点で GameFlow.CurrentSaveData が未設定（Boot 非経由）だったため
        /// SaveData.CreateDefault() へフォールバックしたかどうか（S-14。CR-CODE iter2 major対応 —
        /// Debug.Log による可視化は LogAssert.NoUnexpectedReceived() を汚し既存 PlayMode テスト
        /// （TowerCombatPlayModeTests/PausePanelPlayModeTests/GameHudPlayModeTests 等、いずれも
        /// GameFlow.CurrentSaveData 未設定のまま BuildSpotController を生成する）を破壊するため撤回し、
        /// ログを出さないテスト/呼び出し元観測用フラグに置き換えた。QA-PLAY 観点の可視化はこのフラグを
        /// 参照する形（例: 将来の Ui 層での「Boot 非経由」表示）に委ねる。
        /// </summary>
        public bool UsedDefaultSaveFallback { get; private set; }

        /// <summary>テスト/将来ストーリーからの WaveSpawnController 注入。Awake/Start 実行前（非アクティブ状態）に呼ぶこと（規約9）。</summary>
        public void SetWaveSpawnControllerForTest(WaveSpawnController controller) => waveSpawnController = controller;

        private void Awake()
        {
            // S-14: ラン開始時に UPG-01（初期資金）/UPG-02（設置・強化コスト割引率）を反映する
            // （docs/architecture.md データフロー節「EconomySystem 初期資金 = STARTING_GOLD + UPG-01 効果」）。
            // GameFlow.CurrentSaveData 未設定（Boot 非経由の単体テスト等）は黙示初期化ではなく
            // SaveData.CreateDefault()（Lv0=無効果）へフォールバックする既存契約（GameFlow.cs コメント参照）。
            // CR-CODE S-14 iter1 minor指摘: 出荷ビルドや Editor 直接再生で Boot 非経由のまま Game に入った
            // 場合、購入済みアップグレードが黙って無効なランが開始されても可視化されない。
            // CR-CODE S-14 iter2 major指摘: iter1 対応の Debug.Log(LogType.Log) は
            // LogAssert.NoUnexpectedReceived() を LogType 不問で汚す（Unity Test Framework の
            // LogScope.AddLog 実装が全 LogType を記録するため）。TowerCombatPlayModeTests/
            // PausePanelPlayModeTests/GameHudPlayModeTests 等、GameFlow.CurrentSaveData 未設定のまま
            // BuildSpotController を生成する既存フィクスチャを false-fail させるため、ログ出力自体を撤回し
            // UsedDefaultSaveFallback（下記プロパティ）という非ログの観測用フラグへ置き換える。
            UsedDefaultSaveFallback = GameFlow.CurrentSaveData == null;
            SaveData save = GameFlow.CurrentSaveData ?? SaveData.CreateDefault();
            buildSpotSystem = new BuildSpotSystem();
            economySystem = new EconomySystem(MetaProgression.ComputeStartingGold(save.upgStartingGoldLv));
            towerDiscountRate = MetaProgression.ComputeTowerDiscountRate(save.upgTowerDiscountLv);
        }

        private void Start()
        {
            if (waveSpawnController == null)
            {
                // 規約12: 配線破損は Start で1回 LogError。未注入ではタワーが敵をターゲティングできない。
                Debug.LogError("[BuildSpotController] WaveSpawnController is not wired; tower combat cannot target enemies.");
            }

            SpawnBuildSpotViews();
        }

        private void SpawnBuildSpotViews()
        {
            if (spotViewsSpawned) return;
            spotViewsSpawned = true;

            for (int i = 0; i < buildSpotSystem.SpotCount; i++)
            {
                var spotGo = new GameObject($"BuildSpot_{i}");
                spotGo.transform.SetParent(transform, false);
                BuildSpotView view = spotGo.AddComponent<BuildSpotView>();
                view.Initialize(i, GameConfig.Build.SpotPositions[i]);
            }
        }

        private void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        /// <summary>PlayMode テスト用の直接駆動口（WaveSpawnController.StepForTest と同様の規約9シーム）。</summary>
        public void StepForTest(float deltaTime) => StepSimulation(deltaTime);

        private void StepSimulation(float deltaTime)
        {
            if (waveSpawnController == null) return;

            damageEventsBuffer.Clear();
            TowerCombatSystem.Tick(deltaTime, buildSpotSystem, waveSpawnController.WaveSystem.Enemies, damageEventsBuffer);

            for (int i = 0; i < damageEventsBuffer.Count; i++)
            {
                DamageEvent evt = damageEventsBuffer[i];

                // Integrate: SFX-02（タワー発射音）— design/assets.md トリガー「TowerCombatSystem の発射
                // エフェクトトリガー」に対応。ダメージ適用イベント発生＝命中/発射の瞬間として、命中先の
                // 敵位置で再生する（View 取得不能時のみ再生スキップ）。同じ hitPosition は、後段で
                // final-hit 撃破が成立した場合の SFX-03 再生位置（View 破棄前に確定した最後の既知位置）
                // としても使い回す。演出キューの選択は FeedbackCueSystem に委譲する（S-13。タワー種別ごとの
                // ピッチ差別化 — SFX-02 は Bastion Cannon/Arc Emitter 共通音のため）。
                bool hasHitPosition = waveSpawnController.TryGetEnemyPosition(evt.EnemyId, out Vector3 hitPosition);
                if (hasHitPosition)
                {
                    FeedbackCue fireCue = FeedbackCueSystem.SelectTowerFiredCue(evt.SourceTowerType);
                    AudioCuePlayer.PlayOneShot(fireCue.AssetKey, hitPosition, pitch: fireCue.PitchMultiplier);

                    // S-24: 発射モーション（照準追従+リコイル）— 発生源タワーの見た目へトリガーする。
                    // CR-CODE iter1 minor指摘対応: TryFireBastionCannon/TryFireArcEmitter は必ず tower.Id を
                    // 渡し、View は設置時（TryPlaceTower）に towerViews へ同期生成されるため現状は到達不能だが、
                    // 将来 DamageEvent 生成箇所が増えて SourceTowerId（既定-1）を渡し忘れた場合、無言スキップだと
                    // モーションが消えるだけでコンパイルエラーにも QA のエラー0検査にも映らない。本ファイルの
                    // 確立済み規範（TrySellTower / TryRemoveEnemyView）に揃え、desync を1回明示ログで表面化する。
                    if (towerViews.TryGetValue(evt.SourceTowerId, out TowerView firingTowerView))
                    {
                        firingTowerView.PlayFireMotion(hitPosition);
                    }
                    else
                    {
                        Debug.LogError($"[BuildSpotController] firing tower {evt.SourceTowerId} had no view; view/state desync");
                    }
                }

                EnemyDamageResult result = waveSpawnController.WaveSystem.ApplyDamage(evt.EnemyId, evt.Damage);
                if (!result.Found || !result.Defeated) continue;

                // final-hit の発生源（evt.SourceTowerType）で総撃破数/AoE撃破数を分離集計し、撃破報酬を資金へ加算する
                // （conventions.md 撃破帰属ルール。同フレーム内の2発目以降は WaveSpawnSystem.ApplyDamage が
                // Active ガードで自然に無効化するため、この for ループは final-hit のみを処理する）。
                enemyHealthSystem.RecordKill(evt.SourceTowerType);
                economySystem.Add(EnemyHealthSystem.GoldReward(result.Type));

                // Integrate: SFX-03（敵撃破音）— View を破棄する前に確定した位置で再生する
                // （hasHitPosition が false のケースは View/状態 desync 相当で直下のログにも表面化するため、
                // ここでは再生自体をスキップして無音を無言の代替値で誤魔化さない）。演出キューの選択は
                // FeedbackCueSystem に委譲する（S-13。final-hit の帰属先タワー種別に関わらず同一SFX）。
                if (hasHitPosition)
                {
                    FeedbackCue defeatCue = FeedbackCueSystem.SelectEnemyDefeatedCue();
                    AudioCuePlayer.PlayOneShot(defeatCue.AssetKey, hitPosition, pitch: defeatCue.PitchMultiplier);
                }

                // S-25: 撃破時は即 Destroy ではなく演出（スケールダウン等の非表示化）を開始する
                // （gdd「モーション方式」節『対象メッシュの非表示化（またはディゾルブ）』）。GameObject の実破棄は
                // 演出完了後に EnemyView 自身が行う。CR-CODE S-05 iter1 L指摘: 撃破経路では View はスポーン時に
                // SyncEnemyViews が生成済みでActive ガードにより撃破解決は敵1体1回のため、false は View/状態
                // desync の兆候。無言破棄せず表面化する（S-25 でも同じ desync 検知契約を維持）。
                if (!waveSpawnController.TryStartEnemyDefeatMotion(evt.EnemyId))
                {
                    Debug.LogError($"[BuildSpotController] defeated enemy {evt.EnemyId} had no view; view/state desync");
                }
            }
        }

        /// <summary>
        /// 空きスポットへタワーを設置する（左クリック→種別選択メニュー確定操作の受け口。
        /// メニューUI自体は Ui/TowerSelectPanel — S-08 — の責務。ここは設置判定+View生成のみ）。
        /// </summary>
        public PlacementResult TryPlaceTower(int spotIndex, TowerType type)
        {
            if (buildSpotSystem == null) throw new InvalidOperationException(
                "[BuildSpotController] TryPlaceTower called before Awake(); BuildSpotSystem is not initialized yet.");

            PlacementResult result = buildSpotSystem.TryPlace(spotIndex, type, economySystem, towerDiscountRate);
            if (result.Success)
            {
                var towerGo = new GameObject($"Tower_{result.Tower.Id}_{type}");
                towerGo.transform.SetParent(transform, false);
                TowerView view = towerGo.AddComponent<TowerView>();
                view.Initialize(result.Tower.Id, type, result.Tower.Position);
                towerViews[result.Tower.Id] = view;

                // Integrate: SFX-01（タワー設置音）— design/assets.md トリガー「左クリックでのタワー設置確定」。
                AudioCuePlayer.PlayOneShot(GameConfig.AssetKeys.SfxTowerPlace, result.Tower.Position);
            }
            return result;
        }

        /// <summary>
        /// 設置済みタワーの Lv 強化確定操作の受け口（S-10）。左クリック→アップグレード/売却パネルからの
        /// 確定操作は本メソッドを呼ぶ想定だが、パネル UI（表示・開閉）を実装する ui-engineer story は
        /// CR-CODE S-10 iter1 major #2 時点で state/stories.yaml に存在しない（TryPlaceTower の
        /// 「メニューの表示・開閉自体は S-08 の責務」と同型の分担を意図したが、対になる story が未発行
        /// のまま本 story のコメントで存在を主張していた誤り。state/reviews/s-10.md 参照）。
        /// そのためプレイヤー入力からの到達経路が現状無く、本メソッドはテスト/将来 story からの直接呼び出し
        /// のみで到達可能（未解決事項として Checkpoint へ蓄積・tech-director へ story 新設 or 既存 story への
        /// スコープ追加を委ねる）。売却操作自体は S-11 が TrySellTower として別途追加した（下記）。
        /// discountRate は UPG-02 割引率（S-14。Awake 時に GameFlow.CurrentSaveData から算出し
        /// towerDiscountRate へ保持したものを TryPlaceTower と同じ方針で使う。呼び出し側が個別に
        /// 割引率を渡す経路は無い＝ラン中は一定）。
        /// </summary>
        public TowerUpgradeResult TryUpgradeTower(int towerId)
        {
            if (buildSpotSystem == null) throw new InvalidOperationException(
                "[BuildSpotController] TryUpgradeTower called before Awake(); BuildSpotSystem is not initialized yet.");

            return TowerUpgradeSystem.TryUpgrade(buildSpotSystem, towerId, economySystem, towerDiscountRate);
        }

        /// <summary>
        /// 設置済みタワーの売却確定操作の受け口（S-11）。TryUpgradeTower と対になる操作で、gdd「売却」節
        /// （設置+強化投入額×TOWER_SELL_REFUND_RATE を資金へ返還しスポットを空きに戻す・移設ではなく撤去）
        /// を実行する。パネル UI（表示・開閉・アップグレード/売却の選択）は CR-CODE S-10 iter1〜2 で
        /// エスカレーション済みの未解決事項（対になる ui-engineer story が state/stories.yaml に存在しない
        /// — state/reviews/s-10.md 参照）と同型のため、本メソッドもプレイヤー入力からの到達経路は現状無く
        /// テスト/将来 story からの直接呼び出しのみで到達可能（Checkpoint への蓄積は s-10 と同一事象のため
        /// 二重計上しない）。成功時は BuildSpotSystem 側の状態更新に合わせて対応する TowerView を破棄する
        /// （撤去=View消滅。移設先を作らない）。
        /// </summary>
        public TowerSellResult TrySellTower(int towerId)
        {
            if (buildSpotSystem == null) throw new InvalidOperationException(
                "[BuildSpotController] TrySellTower called before Awake(); BuildSpotSystem is not initialized yet.");

            TowerSellResult result = buildSpotSystem.TrySell(towerId, economySystem);
            if (result.Success)
            {
                // CR-CODE S-11 iter1 major指摘: 撃破経路（StepSimulation内 TryRemoveEnemyView 失敗ログ）と
                // 同型で、System 側は撤去済みなのに View が見つからない/破棄済みのケースを無言スキップせず
                // 表面化する（現状 TryPlaceTower が設置時に必ず View を生成するため到達不能だが、将来
                // BuildSpotSystem への直挿入経路が増えた場合の孤児 View/desync を QA のエラー0検査で検出可能にする）。
                if (towerViews.TryGetValue(towerId, out TowerView view))
                {
                    towerViews.Remove(towerId);
                    if (view != null)
                    {
                        Destroy(view.gameObject);
                    }
                    else
                    {
                        Debug.LogError($"[BuildSpotController] sold tower {towerId} had a destroyed view; view/state desync");
                    }
                }
                else
                {
                    Debug.LogError($"[BuildSpotController] sold tower {towerId} had no view; view/state desync");
                }
            }
            return result;
        }
    }
}
