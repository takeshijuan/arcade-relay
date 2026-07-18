// SessionHolder — the single DontDestroyOnLoad state holder that carries data across scene
// loads (docs/architecture.md §2 "セッションホルダ" / conventions.md §5). It ONLY holds values
// and delegates persistence to Persistence/FileSaveAdapter; it contains no game logic.
using ForgeGame.Systems.Meta;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class SessionHolder : MonoBehaviour
    {
        public static SessionHolder Instance { get; private set; }

        /// <summary>Currently loaded/active save (Boot load result, later updated after purchases/runs).</summary>
        public SaveData Save { get; private set; }

        /// <summary>True when Boot's FileSaveAdapter.Load() had to recover from a corrupted save.
        /// UI (Title/Menu) reads this to surface a recovery notice (conventions.md §5).</summary>
        public bool Recovered { get; private set; }

        /// <summary>The RunResult folded into Save by the most recent Components/HealthComponent.CompleteRun
        /// (architecture.md §2: セッションホルダが保持する「直近 RunResult」). Null until the first run
        /// completes this session — Ui/ResultScreen degrades to a zeroed display in that case (mirrors
        /// TitleController/MenuController's documented SessionHolder-missing degradation policy).</summary>
        public RunResult? LastRunResult { get; private set; }

        /// <summary>True if LastRunResult.FinalScore exceeded the pre-run high score (gdd Result 画面:
        /// ハイスコア更新の有無表示). Meaningless while LastRunResult is null.</summary>
        public bool LastRunHighScoreUpdated { get; private set; }

        /// <summary>
        /// Creates the singleton holder on first call (marking it DontDestroyOnLoad), or updates the
        /// existing one in place on subsequent calls. Boot calls this once with the FileSaveAdapter.Load()
        /// result; later scenes (Menu purchases, Result run application) call UpdateSave to keep the
        /// in-memory SaveData authoritative between saves (conventions.md §6: reuse in-memory SaveData).
        /// </summary>
        public static SessionHolder EnsureCreated(SaveData save, bool recovered)
        {
            if (save == null)
            {
                if (Instance != null && Instance.Save != null)
                {
                    // A valid in-memory value already exists (this is at least the 2nd call).
                    // Overwriting it with CreateDefault() here would silently discard real
                    // player progress the next time FileSaveAdapter.Save() persists it, with
                    // only a console line as evidence. Keep the existing value instead, matching
                    // UpdateSave's null-handling policy (ignore, keep prior state).
                    Debug.LogError("[Wiring] SessionHolder.EnsureCreated called with null SaveData; keeping existing in-memory value");
                    return Instance;
                }

                // No prior value to preserve (first call, or Instance not yet created): this is
                // effectively a corrupted/missing load, so fall back to defaults and force
                // recovered=true so the Title/Menu recovery notice UI fires (rules/unity-code.md
                // 3-point corruption protocol: backup + log + default+recovered).
                Debug.LogError("[Wiring] SessionHolder.EnsureCreated called with null SaveData; no existing value to preserve, falling back to defaults");
                save = SaveData.CreateDefault();
                recovered = true;
            }
            if (Instance == null)
            {
                var go = new GameObject(nameof(SessionHolder));
                Instance = go.AddComponent<SessionHolder>();
                DontDestroyOnLoad(go);
            }
            Instance.Save = save;
            Instance.Recovered = recovered;
            return Instance;
        }

        /// <summary>Replace the held SaveData after a mutation (purchase/run result) is persisted.</summary>
        public void UpdateSave(SaveData save)
        {
            if (save == null)
            {
                Debug.LogError("[Wiring] SessionHolder.UpdateSave called with null SaveData; ignoring");
                return;
            }
            Save = save;
        }

        /// <summary>Records the just-finished run's RunResult and whether it set a new high score
        /// (S-11: Result 画面の表示元). Called by Components/HealthComponent.CompleteRun right before
        /// loading Result, after UpdateSave — display-only data, no logic beyond storing what the
        /// caller already computed.</summary>
        public void SetLastRunResult(RunResult run, bool highScoreUpdated)
        {
            LastRunResult = run;
            LastRunHighScoreUpdated = highScoreUpdated;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Wiring] duplicate SessionHolder destroyed (scene={gameObject.scene.name})");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            if (transform.parent != null)
            {
                // DontDestroyOnLoad only works on root GameObjects; Unity silently emits a
                // Warning (not caught by LogAssert.NoUnexpectedReceived()) and otherwise does
                // nothing, leaving a DDOL-less fake singleton that gets destroyed on the next
                // scene load. Reparent to root before calling it so DDOL actually takes effect.
                Debug.LogError("[Wiring] SessionHolder must be a root GameObject");
                transform.SetParent(null);
            }
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
