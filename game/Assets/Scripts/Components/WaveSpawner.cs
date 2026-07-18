// WaveSpawner — drives Systems/WaveSpawnSystem every frame and Instantiates enemies on the spawn
// ring (gdd ウェーブスポーン＆難度カーブ; P-03; S-05). Thin by design (rule: Components はライフサイクル
// と配線のみ) — all interval/count/speed/HP derivation lives in WaveSpawnSystem; this component only
// owns the timer accumulation, the random spawn angle (RNG stays out of the pure Systems layer — see
// WaveSpawnSystem.SpawnPointOnRadius), the Instantiate call, and the MAX_CONCURRENT_ENEMIES cap (via
// EnemyAgent.ActiveEnemies).
using ForgeGame.Systems;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class WaveSpawner : MonoBehaviour
    {
        /// <summary>Convenience lookup (mirrors Components/PlayerController.Instance) — S-08's
        /// Components/HealthComponent reads CurrentWave from here to fill RunResult.WaveReached at
        /// Result transition without a scene-wide FindObjectOfType lookup.</summary>
        public static WaveSpawner Instance { get; private set; }

        [Tooltip("Optional prefab for spawned enemies (assigned once MDL-02/S-18 is integrated via " +
                 "GameConfig.AssetKeys.SwarmerPrefab). Left unassigned for now: falls back to a " +
                 "primitive-cube placeholder with a placeholder material, matching the pattern " +
                 "SceneWiring.WireGame already uses for the player capsule.")]
        [SerializeField] private GameObject enemyPrefab;

        /// <summary>Test-observability accessor (mirrors HealthComponent.SaveInvocationCountForTests) —
        /// PlayMode tests assert the Swarmer prefab was actually assigned by Editor/AssetIntegration
        /// rather than reaching into the private SerializeField via reflection/UnityEditor APIs (which
        /// wouldn't compile against a Player-platform test assembly).</summary>
        public GameObject EnemyPrefabForTests => enemyPrefab;

        /// <summary>Test-observability accessor (mirrors EnemyPrefabForTests): total number of SpawnOne
        /// calls (= number of angle/heavy-chance RNG roll pairs consumed) since this instance was
        /// created. CR-CODE S-14 iter2 major finding: HeavyEnemySceneTests' post-unlock mixing test used
        /// to sample a fixed real-time window and assert on whatever landed in it, which made "how many
        /// heavy-chance rolls actually happened" an invisible, real-time-frame-count-dependent quantity —
        /// this counter lets that test instead loop until it has observed a guaranteed number of rolls
        /// (bounded by roll count, not wall-clock time), which is what actually determines the residual
        /// P(zero heavies) flake probability.</summary>
        public int SpawnCallCountForTests { get; private set; }

        private float _elapsedSec;
        private float _spawnTimer;
        private bool _prefabMissingAgentLogged;

        // Ship-review perf fix: free-list pool for spawned enemies (Components/GameObjectPool).
        // Instance field, NOT static (contract in GameObjectPool's header) — the pool and every
        // GameObject it retains die with this scene-resident spawner on scene unload, so a Result
        // transition/restart can never resurrect a previous run's enemies. EnemyAgent returns itself
        // here at kill-pop end (SetPool below) instead of Destroy; pooled-inactive enemies are not in
        // EnemyAgent.ActiveEnemies (OnDisable removed them), so the MAX_CONCURRENT_ENEMIES room check
        // in Update() is untouched by pooling.
        private readonly GameObjectPool _enemyPool = new GameObjectPool();

        // Ship-review AUTO-FIX: ApplyPlaceholderColor used to build a brand-new `new Material(shader)`
        // per spawned placeholder enemy — every placeholder is visually identical by construction
        // (same GameConfig.Ui.ColorAlert), so the per-instance materials only accumulated
        // (renderer.sharedMaterial assignment does not tie the material's lifetime to the enemy
        // GameObject). Build once per session and share the cached instance. Static +
        // SubsystemRegistration reset mirrors Components/CrystalPickup._placeholderMaterial.
        private static Material _placeholderMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForDomainReloadDisabled()
        {
            _placeholderMaterial = null;
        }

        /// <summary>Current wave number (gdd currentWave), exposed for HUD/tests (S-10 will read this).</summary>
        public int CurrentWave { get; private set; } = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError($"[Wiring] duplicate WaveSpawner destroyed (scene={gameObject.scene.name})");
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

        private void Update()
        {
            _elapsedSec += Time.deltaTime;
            WaveSpawnSystem.WaveParameters parameters = WaveSpawnSystem.Compute(_elapsedSec);
            CurrentWave = parameters.Wave;

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer < parameters.SpawnInterval)
            {
                return;
            }
            _spawnTimer -= parameters.SpawnInterval;

            int room = GameConfig.Wave.MaxConcurrentEnemies - EnemyAgent.ActiveEnemies.Count;
            int toSpawn = Mathf.Max(0, Mathf.Min(parameters.SpawnCount, room));
            for (int i = 0; i < toSpawn; i++)
            {
                SpawnOne(parameters);
            }
        }

        private void SpawnOne(WaveSpawnSystem.WaveParameters parameters)
        {
            SpawnCallCountForTests++;
            float angleRad = Random.Range(0f, Mathf.PI * 2f);
            Vector3 position = WaveSpawnSystem.SpawnPointOnRadius(angleRad, GameConfig.Enemy.SpawnRadius);

            // Ship-review perf fix: reuse a pooled enemy before falling back to the original
            // Instantiate/CreatePrimitive factory path. Reposition BEFORE SetActive(true) so
            // OnEnable's ActiveEnemies registration never observes the previous life's position
            // (GameObjectPool's Rent contract); Initialize below then reseeds HP/speed/kind exactly
            // as it always has for a freshly-Instantiated enemy.
            GameObject instance = _enemyPool.Rent();
            if (instance != null)
            {
                instance.transform.SetPositionAndRotation(position, Quaternion.identity);
                instance.SetActive(true);
            }
            else
            {
                instance = enemyPrefab != null
                    ? Instantiate(enemyPrefab, position, Quaternion.identity)
                    : CreatePlaceholder(position);
            }

            EnemyAgent agent = instance.GetComponent<EnemyAgent>();
            if (agent == null)
            {
                // Wiring guard (rule 12): the placeholder-cube path always lacks EnemyAgent by design
                // (no prefab configured yet), so AddComponent there is expected and silent. Once
                // enemyPrefab is assigned (S-18), a missing EnemyAgent means the prefab itself is
                // misconfigured — auto-adding it at runtime would mask that forever. This is not a
                // recoverable/expected-degradation case (rule 12's LogWarning allowance doesn't apply
                // here) — it's a broken wiring case, so it must be LogError to fail QA-PLAY's
                // LogAssert.NoUnexpectedReceived() rather than silently pass as a Warning. Surface it once.
                if (enemyPrefab != null && !_prefabMissingAgentLogged)
                {
                    Debug.LogError("[Wiring] enemyPrefab lacks EnemyAgent; auto-adding at runtime — fix the prefab.");
                    _prefabMissingAgentLogged = true;
                }
                agent = instance.AddComponent<EnemyAgent>();
            }

            // Ship-review perf fix: hand the agent its way home — at kill-pop end EnemyAgent returns
            // itself to this pool instead of Destroy(gameObject) (manually-created test enemies never
            // get a pool and keep the original Destroy path — see EnemyAgent.TickKillPop).
            agent.SetPool(_enemyPool);

            // S-14 (ヘヴィスウォーマー変種・任意): RNG stays out of Systems (rules/unity-code.md #3) — this
            // is the same pattern as the spawn-angle roll above, just handed to
            // HeavyEnemySystem.ShouldSpawnHeavy instead of WaveSpawnSystem.SpawnPointOnRadius. No new
            // prefab/geometry: the same enemyPrefab (MDL-02/placeholder) is reused for both variants
            // (conventions.md §7「MDL-02 プレハブ複製＋マテリアル差し替えのみ」) — EnemyAgent.Initialize's
            // 3-arg overload applies the material swap when kind is HeavySwarmer.
            bool spawnHeavy = HeavyEnemySystem.ShouldSpawnHeavy(parameters.Wave, Random.value);
            if (spawnHeavy)
            {
                agent.Initialize(
                    HeavyEnemySystem.AdjustedSpeed(parameters.EnemySpeed),
                    HeavyEnemySystem.AdjustedHp(parameters.EnemyHp),
                    EnemyKind.HeavySwarmer);
            }
            else
            {
                agent.Initialize(parameters.EnemySpeed, parameters.EnemyHp, EnemyKind.Swarmer);
            }
        }

        // Placeholder-only visual (contract §11: 3D 資産統合は S-18/S-20。今は単色マテリアルで代用し、
        // MDL-02 swarmer FBX 差し替え時に enemyPrefab へ置き換える). Mirrors
        // SceneWiring.ApplyPlaceholderColor's URP-safe shader lookup.
        private static GameObject CreatePlaceholder(Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            // Ship-review AUTO-FIX: CreatePrimitive attaches a BoxCollider, but this game has zero
            // physics — contact checks are pure XZ-distance math in Systems/ (mirrors
            // Components/CrystalPickup.SpawnDrop / Editor/AssetIntegration.PatchPlayerVisual's
            // CapsuleCollider strip).
            Object.Destroy(go.GetComponent<BoxCollider>());
            go.name = "Swarmer";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * (GameConfig.Enemy.CollisionRadius * 2f);
            ApplyPlaceholderColor(go);
            return go;
        }

        private static void ApplyPlaceholderColor(GameObject go)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning("[Wiring] enemy placeholder color skipped: '" + go.name + "' has no Renderer.");
                return;
            }
            if (_placeholderMaterial == null)
            {
                // Ship-review AUTO-FIX: built once per session and shared across all placeholders — see
                // _placeholderMaterial field comment. On build failure the warnings re-log per spawn,
                // matching the pre-cache behavior (the cache stays null, so each spawn retries).
                _placeholderMaterial = BuildPlaceholderMaterial();
                if (_placeholderMaterial == null)
                {
                    return;
                }
            }
            renderer.sharedMaterial = _placeholderMaterial;
        }

        private static Material BuildPlaceholderMaterial()
        {
            if (!ColorUtility.TryParseHtmlString(GameConfig.Ui.ColorAlert, out Color color))
            {
                Debug.LogWarning("[Wiring] enemy placeholder color skipped: GameConfig hex parse failed.");
                return null;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogWarning("[Wiring] URP/Lit shader not found; falling back to Standard (pink InternalErrorShader risk under URP).");
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                Debug.LogWarning("[Wiring] enemy placeholder color skipped: neither URP/Lit nor Standard shader found.");
                return null;
            }
            return new Material(shader) { color = color };
        }
    }
}
