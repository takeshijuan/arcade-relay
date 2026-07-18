// RunStatsTracker — Game シーンの撃破/クリスタル回収カウンタ集約 (gameplay-engineer, S-09). Singleton
// (mirrors Components/WaveSpawner.Instance / PlayerController.Instance) so AutoAttackDriver (kills) and
// CrystalPickup (pickups) can report events without a scene-wide FindObjectOfType lookup each time, and
// so Components/HealthComponent.CompleteRun can read the final tallies into RunResult at the Result
// transition (gdd スコア算出: 撃破数+回収クリスタル数を集計式に適用). Thin by design (rule: Components は
// ライフサイクルと配線のみ) — the count/score bookkeeping here is intentionally trivial (plain
// increments driven by GameConfig.Score constants); the survival-time term of the gdd final-score
// formula lives in Systems/ScoreSystem.ComputeFinalScore, invoked once at Result by
// HealthComponent.CompleteRun (not duplicated here — a second independently-ticking survival timer would
// risk drifting from HealthComponent's and isn't needed by this story's acceptance). CurrentScore
// therefore only exposes the discrete kill+crystal portion live during a run; a later HUD story can
// combine it with elapsed survival time via ScoreSystem if it wants the exact live total.
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class RunStatsTracker : MonoBehaviour
    {
        public static RunStatsTracker Instance { get; private set; }

        public int NormalKillCount { get; private set; }
        public int HeavyKillCount { get; private set; }
        public int CrystalsCollected { get; private set; }

        /// <summary>Discrete kill+crystal score accrued so far this run (SCORE_PER_KILL_NORMAL/HEAVY +
        /// SCORE_PER_CRYSTAL per event). Excludes the survival-time term of the gdd final-score formula —
        /// see class header.</summary>
        public int CurrentScore { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Wiring] duplicate RunStatsTracker destroyed (scene={gameObject.scene.name})");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Records one enemy kill (gdd 行為→点数: 通常敵はSCORE_PER_KILL_NORMAL、ヘヴィ変種は
        /// SCORE_PER_KILL_HEAVY). Called by Components/AutoAttackDriver right after
        /// EnemyAgent.ApplyAutoAttackDamage reports a kill.</summary>
        public void RegisterKill(bool isHeavy)
        {
            if (isHeavy)
            {
                HeavyKillCount++;
                CurrentScore += GameConfig.Score.PerKillHeavy;
            }
            else
            {
                NormalKillCount++;
                CurrentScore += GameConfig.Score.PerKillNormal;
            }
        }

        /// <summary>Records one crystal auto-pickup (gdd: SCORE_PER_CRYSTAL 加点). Called by
        /// Components/CrystalPickup when the player enters CRYSTAL_PICKUP_RADIUS.</summary>
        public void RegisterCrystalPickup()
        {
            CrystalsCollected++;
            CurrentScore += GameConfig.Score.PerCrystal;
        }
    }
}
