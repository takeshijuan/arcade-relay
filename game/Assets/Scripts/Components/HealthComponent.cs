// HealthComponent — Game シーンのプレイヤーHP・被弾・死亡配線 (gameplay-engineer, S-08). Every frame,
// evaluates contact against Components/EnemyAgent.ActiveEnemies via the pure Systems/HealthSystem
// (XZ radius overlap + per-enemy ENEMY_CONTACT_COOLDOWN) and applies ENEMY_CONTACT_DAMAGE through the
// shared EntityState.ApplyDamage reducer (Types.cs — the same reducer Components/EnemyAgent already
// uses for enemy HP). Skips damage while Components/PlayerController.IsDashInvulnerable is true
// (ダッシュ無敵窓 — gdd 決定). On HP<=0 (effectiveMaxHp 基準), locks player input (PlayerController.LockInput),
// runs the death-sequence timer (GameConfig.Fx.DeathSequenceDuration — the coded fade/tilt/dissolve
// VISUALS themselves are S-16's scope; this story only owns the timer + transition gate), then folds
// the run into SaveData via MetaProgression.ApplyRunResult and Persistence/FileSaveAdapter.Save EXACTLY
// ONCE (gdd 勝敗条件 / conventions.md §6) before loading Result. Thin by design (rule: Components は
// ライフサイクルと配線のみ) — all HP/cooldown arithmetic lives in Systems/HealthSystem and Types.cs; this
// component only owns per-enemy cooldown bookkeeping (keyed by EnemyAgent, since gdd's cooldown is
// per-enemy — 「同一敵からの連続被弾間隔」— not global) and the death-sequence/save/transition wiring.
using System.Collections.Generic;
using ForgeGame.Persistence;
using ForgeGame.Systems;
using ForgeGame.Systems.Meta;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ForgeGame.Components
{
    public sealed class HealthComponent : MonoBehaviour
    {
        /// <summary>Test seam (conventions.md §9: 永続化テストは Application.temporaryCachePath を使い
        /// persistentDataPath 直使用禁止・実セーブを汚さない). Null (default — SceneWiring/production never
        /// sets it) resolves to FileSaveAdapter's default Application.persistentDataPath ctor.</summary>
        public static string SaveDirectoryOverrideForTests;

        /// <summary>Test-observability counter: counts FileSaveAdapter.Save invocations made by
        /// CompleteRun. Save() itself has no return value to assert on, and the S-08 acceptance
        /// requires verifying "セーブ1回のみ" — PlayMode tests reset this to 0 in SetUp/TearDown.</summary>
        public static int SaveInvocationCountForTests;

        /// <summary>Test seam: long fast-forward tests (e.g. the EnemySpawnSceneTests wave-cap test,
        /// ~300 simulated seconds with an idle player) need the run to outlive the window, but an
        /// idle player legitimately dies well before that. True skips ONLY the damage application —
        /// contact detection, cooldown bookkeeping, and near-miss evaluation still run, so the
        /// window exercises the real spawn/contact pipeline. Production never sets it; tests reset
        /// it in TearDown (same lifecycle contract as SaveInvocationCountForTests above).</summary>
        public static bool ContactDamageDisabledForTests;

        public int CurrentHp => _health.Hp;
        public int EffectiveMaxHp => _health.MaxHp;
        public bool IsDead => _health.IsDead;
        public bool IsDeathSequenceActive => _deathSequenceActive;

        /// <summary>Elapsed survival time this run, in seconds. Read-only exposure of the internal
        /// accumulator for Ui/GameHud (S-10 HUD 現在スコア表示 — gdd「HUD表示用の現在スコア」は生存時間+
        /// 撃破数+回収クリスタル数の同一集計式を live に適用する). Mirrors the CurrentHp/EffectiveMaxHp
        /// accessor pattern above; no logic here, just surfacing already-computed state.</summary>
        public float SurvivalTimeSec => _survivalTimeSec;

        private EntityState _health;
        private readonly Dictionary<EnemyAgent, float> _contactCooldowns = new Dictionary<EnemyAgent, float>();
        private readonly HashSet<EnemyAgent> _activeScratch = new HashSet<EnemyAgent>();
        private readonly List<EnemyAgent> _staleKeysScratch = new List<EnemyAgent>();

        private float _survivalTimeSec;
        private bool _deathSequenceActive;
        private float _deathSequenceRemaining;
        private bool _runCompleted;

        /// <summary>S-23 (gdd P-01「紙一重回避」ダッシュ紙一重回避のカメラシェイク演出) latch: true once a
        /// near-miss shake has already fired for the current dash invuln window, so repeated/multi-enemy
        /// contact within the same window doesn't re-trigger it (acceptance: 単発シェイクに丸める). Reset
        /// to false the instant invulnerable observes false (Systems/CameraShakeSystem.
        /// ShouldTriggerNearMissShake owns the decision; this field is the latch storage it requires).</summary>
        private bool _nearMissShakeTriggeredThisWindow;

        private void Start()
        {
            int maxHp = ResolveEffectiveMaxHp();
            _health = new EntityState(maxHp, maxHp);

            // S-23 / tech-stack-unity.md 規約12: Editor/SceneWiring.WireGame は ArenaCameraRig を
            // Main Camera へ無条件注入する（配線破損クラス。ヘッダコメントで文書化された「正当な縮退」では
            // ないため LogWarning ではなく Start で1回 LogError — CR-CODE s-23 finding 対応）。実行時の
            // null ガード自体は ProcessContacts に残す（このログの後もシェイクをスキップして継続する）。
            if (ArenaCameraRig.Instance == null)
            {
                Debug.LogError("[Wiring] HealthComponent: ArenaCameraRig.Instance missing at Start; near-miss camera shake will not play for this run");
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;

            if (_deathSequenceActive)
            {
                TickDeathSequence(deltaTime);
                return;
            }

            _survivalTimeSec += deltaTime;
            ProcessContacts(deltaTime);

            if (_health.IsDead)
            {
                BeginDeathSequence();
            }
        }

        private void ProcessContacts(float deltaTime)
        {
            List<EnemyAgent> enemies = EnemyAgent.ActiveEnemies;
            bool invulnerable = PlayerController.Instance != null && PlayerController.Instance.IsDashInvulnerable;
            float radiusSum = GameConfig.Player.CollisionRadius + GameConfig.Enemy.CollisionRadius;

            // S-23: the invuln window just ended (or was never active) — clear the per-window latch so
            // the NEXT dash's window can trigger its own single shake (Systems/CameraShakeSystem.
            // ShouldTriggerNearMissShake's "alreadyTriggeredThisWindow" input).
            if (!invulnerable)
            {
                _nearMissShakeTriggeredThisWindow = false;
            }

            _activeScratch.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyAgent enemy = enemies[i];
                _activeScratch.Add(enemy);
                _contactCooldowns.TryGetValue(enemy, out float cooldownRemaining);

                bool isContacting = HealthSystem.IsContacting(transform.position, enemy.transform.position, radiusSum);

                // S-23 (gdd P-01「紙一重回避」ダッシュ紙一重回避のカメラシェイク演出): a contact while
                // dash-invulnerable is a "near miss" regardless of this enemy's own contact-damage
                // cooldown state (invincibility is what blocked it) — deliberately evaluated before/
                // independent of HealthSystem.EvaluateContact below, which only decides damage.
                if (CameraShakeSystem.ShouldTriggerNearMissShake(isContacting, invulnerable, _nearMissShakeTriggeredThisWindow))
                {
                    _nearMissShakeTriggeredThisWindow = true;
                    // Wiring break already reported once in Start() (規約12) — this null check is only
                    // the runtime degrade-and-continue guard, no repeated per-trigger logging.
                    if (ArenaCameraRig.Instance != null)
                    {
                        ArenaCameraRig.Instance.TriggerNearMissShake();
                    }
                }

                HealthSystem.ContactEvaluation evaluation =
                    HealthSystem.EvaluateContact(isContacting, cooldownRemaining, deltaTime, invulnerable);
                _contactCooldowns[enemy] = evaluation.CooldownRemaining;

                if (evaluation.ShouldApplyDamage && !ContactDamageDisabledForTests)
                {
                    // S-14 (ヘヴィスウォーマー変種): 接触ダメージにHEAVY_ENEMY_CONTACT_DAMAGE_MULTを適用
                    // (gdd 敵・障害物). No new AI/contact logic — same EvaluateContact/cooldown path as
                    // the normal variant, only the damage magnitude differs.
                    int contactDamage = enemy.Kind == EnemyKind.HeavySwarmer
                        ? HeavyEnemySystem.AdjustedContactDamage(GameConfig.Enemy.ContactDamage)
                        : GameConfig.Enemy.ContactDamage;
                    _health = _health.ApplyDamage(contactDamage);
                    // SFX-03 (プレイヤー被弾) — every applied hit, including the killing one (gdd 音要件:
                    // 被弾のたびに鳴らす、死亡固有のSFXは無い — S-16の死亡演出はコード合成のみ).
                    if (SfxLibrary.Instance != null)
                    {
                        SfxLibrary.Instance.Play(SfxLibrary.Instance.PlayerHit);
                    }
                    if (_health.IsDead)
                    {
                        break; // gdd: 判定した瞬間にラン終了 — no need to keep evaluating remaining contacts.
                    }
                }
            }

            PruneStaleCooldowns();
        }

        /// <summary>Drops cooldown entries for enemies no longer in ActiveEnemies (destroyed/despawned)
        /// so the dictionary doesn't grow unbounded across a long run.</summary>
        private void PruneStaleCooldowns()
        {
            _staleKeysScratch.Clear();
            foreach (KeyValuePair<EnemyAgent, float> entry in _contactCooldowns)
            {
                if (!_activeScratch.Contains(entry.Key))
                {
                    _staleKeysScratch.Add(entry.Key);
                }
            }
            for (int i = 0; i < _staleKeysScratch.Count; i++)
            {
                _contactCooldowns.Remove(_staleKeysScratch[i]);
            }
        }

        private void BeginDeathSequence()
        {
            _deathSequenceActive = true;
            _deathSequenceRemaining = GameConfig.Fx.DeathSequenceDuration;

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.LockInput();
            }
            else
            {
                Debug.LogError("[Wiring] HealthComponent: PlayerController.Instance is null at death — cannot lock input");
            }
        }

        private void TickDeathSequence(float deltaTime)
        {
            _deathSequenceRemaining -= deltaTime;
            if (_deathSequenceRemaining > 0f)
            {
                return;
            }
            CompleteRun();
        }

        /// <summary>Folds the finished run into SaveData and saves EXACTLY ONCE (gdd 勝敗条件 /
        /// conventions.md §6: 「セーブ書込はResult到達時に1回」), then loads Result. Guarded by
        /// _runCompleted so a stray extra frame (e.g. before SceneManager.LoadScene takes effect
        /// mid-frame) can never double-save.</summary>
        private void CompleteRun()
        {
            if (_runCompleted)
            {
                return;
            }
            _runCompleted = true;

            int waveReached = ResolveWaveReached();

            // S-09 (クリスタル ドロップ・自動回収 + スコア算出): kill/crystal tallies come from
            // Components/RunStatsTracker, which AutoAttackDriver (kills) and CrystalPickup (pickups)
            // report to over the course of the run.
            (int normalKillCount, int heavyKillCount, int crystalsCollected) = ResolveRunTallies();

            var run = new RunResult
            {
                SurvivalTimeSec = _survivalTimeSec,
                WaveReached = waveReached,
                NormalKillCount = normalKillCount,
                HeavyKillCount = heavyKillCount,
                CrystalsCollected = crystalsCollected,
            };
            run.FinalScore = ScoreSystem.ComputeFinalScore(
                run.SurvivalTimeSec, run.NormalKillCount, run.HeavyKillCount, run.CrystalsCollected);

            SaveData currentSave = ResolveCurrentSave();
            bool highScoreUpdated = MetaProgression.IsNewHighScore(currentSave, run);
            SaveData nextSave = MetaProgression.ApplyRunResult(currentSave, run);

            FileSaveAdapter adapter = SaveDirectoryOverrideForTests != null
                ? new FileSaveAdapter(SaveDirectoryOverrideForTests)
                : new FileSaveAdapter();
            try
            {
                adapter.Save(nextSave);
                SaveInvocationCountForTests++;
            }
            catch (System.Exception ex)
            {
                // I/O failure (disk full / permissions / locked file — Directory.CreateDirectory,
                // File.WriteAllText, File.Move are all unguarded in FileSaveAdapter). Persistence is
                // lost for this run, but the run must still complete (Result transition + input
                // unlock) rather than soft-lock the player in Game with _runCompleted latched true.
                Debug.LogError("[SaveCorruption] save write failed: " + ex);
            }

            if (SessionHolder.Instance != null)
            {
                SessionHolder.Instance.UpdateSave(nextSave);
                // S-11: Result 画面が最終スコア/生存時間/到達ウェーブ/ハイスコア更新の有無を表示するための
                // 直近 RunResult 受け渡し（architecture.md §2）。UpdateSave 済みの nextSave.highScore ではなく
                // pre-run の currentSave 基準で判定した highScoreUpdated をそのまま渡す（二重計算しない）。
                SessionHolder.Instance.SetLastRunResult(run, highScoreUpdated);
            }
            else
            {
                Debug.LogWarning("[Wiring] SessionHolder missing at Result transition; Result screen will show a zeroed run summary");
            }

            SceneManager.LoadScene(GameConfig.Scenes.Result);
        }

        private static int ResolveWaveReached()
        {
            if (WaveSpawner.Instance != null)
            {
                return WaveSpawner.Instance.CurrentWave;
            }
            Debug.LogWarning("[Wiring] HealthComponent: WaveSpawner.Instance missing at death; recording waveReached=1");
            return 1;
        }

        /// <summary>Reads the run's accumulated kill/crystal tallies from Components/RunStatsTracker
        /// (S-09). Falls back to all-zero if the tracker is missing (SceneWiring.WireGame normally
        /// injects it unconditionally, so absence means a wiring break — LogError, matching
        /// AutoAttackDriver/CrystalPickup's treatment of the same missing-tracker condition per
        /// tech-stack-unity.md 規約12, not ResolveWaveReached's degrade-and-warn policy which covers a
        /// genuinely optional fallback).</summary>
        private static (int normalKillCount, int heavyKillCount, int crystalsCollected) ResolveRunTallies()
        {
            if (RunStatsTracker.Instance != null)
            {
                return (RunStatsTracker.Instance.NormalKillCount, RunStatsTracker.Instance.HeavyKillCount,
                    RunStatsTracker.Instance.CrystalsCollected);
            }
            Debug.LogError("[Wiring] HealthComponent: RunStatsTracker.Instance missing at Result transition; recording kill/crystal counts as 0");
            return (0, 0, 0);
        }

        private static SaveData ResolveCurrentSave()
        {
            if (SessionHolder.Instance != null && SessionHolder.Instance.Save != null)
            {
                return SessionHolder.Instance.Save;
            }
            Debug.LogWarning("[Wiring] SessionHolder missing at Result transition (not loaded via Boot, or session state lost); folding run result into a fresh default SaveData");
            return SaveData.CreateDefault();
        }

        /// <summary>Game 初期化時のアップグレード反映 (conventions.md §5): effectiveMaxHp は
        /// MetaProgression.EffectiveMaxHp で算出する（式の再実装禁止）。フォールバック方針は
        /// Components/PlayerController.ResolveMoveSpeed と同じ（SessionHolder/Save が無い場合は Lv0 基準）。</summary>
        private static int ResolveEffectiveMaxHp()
        {
            if (SessionHolder.Instance != null && SessionHolder.Instance.Save != null)
            {
                return MetaProgression.EffectiveMaxHp(SessionHolder.Instance.Save.upgradeMaxHpLevel);
            }
            Debug.LogWarning("[Wiring] SessionHolder missing at Game (not loaded via Boot, or session state lost); using base PLAYER_MAX_HP_BASE (Lv0)");
            return GameConfig.Player.MaxHpBase;
        }
    }
}
