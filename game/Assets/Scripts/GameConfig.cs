// GameConfig — all tunable game parameters + asset keys (single source of truth).
// Rule (unity-code): no magic numbers in Systems/Components/Ui — reference these constants.
// Initial values are copied verbatim from design/gdd.md "数値表" (Lv0 baseline). Adjustment
// ranges live in the GDD; only the initial value is materialized here.
using ForgeGame.Systems.Meta;
using UnityEngine;

namespace ForgeGame
{
    public static class GameConfig
    {
        // --- Player (gdd 数値表: プレイヤー) ---
        public static class Player
        {
            public const float ArenaRadius = 12f;              // m (ARENA_RADIUS)
            public const float MoveSpeed = 6.0f;               // m/s (PLAYER_MOVE_SPEED, Lv0)
            public const float CollisionRadius = 0.4f;         // m (PLAYER_COLLISION_RADIUS)
            public const int MaxHpBase = 100;                  // (PLAYER_MAX_HP_BASE, Lv0)
            public const float DashSpeed = 20f;                // m/s (DASH_SPEED)
            public const float DashDuration = 0.2f;            // s (DASH_DURATION)
            public const float DashCooldown = 1.2f;            // s (DASH_COOLDOWN)
            public const float DashInvulnDuration = 0.25f;     // s (DASH_INVULN_DURATION)
            public const float AutoAttackRange = 6f;           // m (AUTO_ATTACK_RANGE)
            public const float AutoAttackInterval = 0.6f;      // s (AUTO_ATTACK_INTERVAL)
            public const int AutoAttackDamageBase = 20;        // (AUTO_ATTACK_DAMAGE_BASE, Lv0)

            // --- Hero visual child (S-16: 死亡演出/被弾フラッシュが hero の Renderer/Animator を探す際の
            // 対象子オブジェクト名。Editor/AssetIntegration.PatchPlayerVisual が実データ統合時に "HeroVisual"
            // という名で Player の子として取込む — 元は AssetIntegration.cs 内のプライベート定数だったが、
            // ランタイム側(Components/HeroFxController)から同じ名前を参照する必要があるため GameConfig へ
            // 集約し、AssetIntegration.cs 側もこの定数を参照するよう変更した（マジックナンバー禁止の一貫）。
            // 統合前(プレースホルダcapsule)はこの子が存在せず、HeroFxController は Player ルート自身の
            // Renderer にフォールバックする（GameConfig.Enemy.VisualChildName の無い場合のフォールバックと
            // 同じ設計）。) ---
            public const string HeroVisualChildName = "HeroVisual";
        }

        // --- Enemy (gdd 数値表: 敵) ---
        public static class Enemy
        {
            public const float MoveSpeedBase = 2.5f;           // m/s (ENEMY_MOVE_SPEED_BASE)
            public const int HpBase = 40;                      // (ENEMY_HP_BASE)
            public const int ContactDamage = 10;               // (ENEMY_CONTACT_DAMAGE)
            public const float ContactCooldown = 0.5f;         // s (ENEMY_CONTACT_COOLDOWN)
            public const float CollisionRadius = 0.5f;         // m (ENEMY_COLLISION_RADIUS)
            public const float SpawnRadius = 13.5f;            // m (ENEMY_SPAWN_RADIUS)
            public const float HeavyHpMult = 2.5f;             // (HEAVY_ENEMY_HP_MULT)
            public const float HeavySpeedMult = 0.6f;          // (HEAVY_ENEMY_SPEED_MULT)
            public const float HeavyContactDamageMult = 1.5f;  // (HEAVY_ENEMY_CONTACT_DAMAGE_MULT)
            public const int HeavyUnlockWave = 3;              // (HEAVY_ENEMY_UNLOCK_WAVE)
            public const float HeavySpawnChance = 0.15f;       // (HEAVY_ENEMY_SPAWN_CHANCE)

            // --- Swarmer visual coded motion (S-21: MDL-02 はリグ未完了/must-replace のため
            // ANM-04(approach_loop)・Avatar・AnimatorController に依存せず、Editor/AssetIntegration が
            // 生成する "Visual" 子オブジェクト（EnemyApproachSystem が動かすルートとは別 transform —
            // Renderer/motion 分離。Tripo クレジット補充時のリグ付き差し替え口を兼ねる）へ
            // Systems/EnemyVisualMotionSystem のコードモーション（前傾チルト+上下バウンス）を適用する。
            // gdd 数値表には無い技術パラメータのため、この節で名前付き定数として集約する（マジックナンバー禁止）。
            public const string VisualChildName = "Visual";
            public const float VisualBounceAmplitude = 0.08f;   // m, 上下バウンスの振幅
            public const float VisualBounceFrequencyHz = 3.0f;  // Hz, 上下バウンスの周波数
            public const float VisualApproachTiltDeg = 12f;     // deg, 進行方向への前傾チルト角
        }

        // --- Wave / difficulty curve (gdd 数値表: ウェーブ・難度カーブ) ---
        public static class Wave
        {
            public const float WaveDuration = 30f;             // s (WAVE_DURATION)
            public const float SpawnIntervalBase = 1.5f;       // s (SPAWN_INTERVAL_BASE)
            public const float SpawnIntervalDecayPerWave = -0.08f; // s (SPAWN_INTERVAL_DECAY_PER_WAVE)
            public const float SpawnIntervalMin = 0.3f;        // s (SPAWN_INTERVAL_MIN)
            public const int SpawnCountPerTickBase = 1;        // (SPAWN_COUNT_PER_TICK_BASE)
            public const int SpawnCountGrowthInterval = 3;     // waves (SPAWN_COUNT_GROWTH_INTERVAL)
            public const int MaxConcurrentEnemies = 40;        // (MAX_CONCURRENT_ENEMIES)
            public const float EnemySpeedGrowthPerWave = 0.04f;// (ENEMY_SPEED_GROWTH_PER_WAVE)
            public const float EnemyHpGrowthPerWave = 0.06f;   // (ENEMY_HP_GROWTH_PER_WAVE, wave4+)
            public const int EnemyHpGrowthStartWave = 4;       // (gdd: HP成長はWave4から)
        }

        // --- Score / crystal (gdd 数値表: スコア・クリスタル) ---
        public static class Score
        {
            public const int PerSecondSurvived = 10;           // (SCORE_PER_SECOND_SURVIVED)
            public const int PerKillNormal = 25;               // (SCORE_PER_KILL_NORMAL)
            public const int PerKillHeavy = 60;                // (SCORE_PER_KILL_HEAVY)
            public const int PerCrystal = 5;                   // (SCORE_PER_CRYSTAL)
        }

        public static class Crystal
        {
            public const int DropPerKillNormal = 1;            // (CRYSTAL_DROP_PER_KILL_NORMAL)
            public const int DropPerKillHeavy = 3;             // (CRYSTAL_DROP_PER_KILL_HEAVY)
            public const float PickupRadius = 1.5f;            // m (CRYSTAL_PICKUP_RADIUS)
            public const float Lifetime = 8f;                  // s (CRYSTAL_LIFETIME)

            // --- Placeholder visual + coded motion (gdd/art-bible「クリスタル・アリーナ環境の視覚表現
            // 方針」: 生成MDLを使わずUnity標準プリミティブ+エミッシブマテリアル+コード側tweenで表現。S-09) ---
            public const float VisualScale = 0.35f;            // m, placeholder cube edge length
            public const float RotationSpeedDegPerSec = 60f;   // deg/s, Y軸自転（コードモーション）
            public const float BobAmplitude = 0.15f;           // m, 上下浮遊の振幅
            public const float BobFrequency = 1.2f;             // Hz, 上下浮遊の周波数
            public const float PlaceholderTiltDegX = 45f;       // deg, プリミティブ立方体の初期チルト角(X軸)
            public const float PlaceholderTiltDegZ = 45f;       // deg, プリミティブ立方体の初期チルト角(Z軸)

            // --- Emissive glow intensity (S-20: gdd/art-bible「クリスタル・アリーナ環境の視覚表現方針」
            // 「敵とクリスタル・マゼンタの識別根拠」— クリスタルは常時エミッシブ発光(val≈0.90)で敵の非発光
            // マット面(val≈0.65)と輝度差で即座に区別できる設計。_EmissionColor をbase色そのまま(HDR強度1.0
            // 相当)に留めず倍率を掛けて、輝度差による識別を実際に成立させる) ---
            public const float EmissionIntensity = 2.5f;        // _EmissionColor = baseColor * この倍率(HDR)

            // --- S-29: enhanced glow halo + ambient/collect particle juice (gdd/art-bible「クリスタル・
            // アリーナ環境の視覚表現方針」の発光強化、P-04 報酬感演出)。S-27 の Bloom(GameConfig.PostProcess.
            // BloomThreshold=1.3)と連動させ、上記 EmissionIntensity(識別用の輝度差の根拠、S-20)へさらに
            // 乗算してより明瞭なブルームハローを作る。gdd 数値表に無い純粋な演出パラメータのため、この節で
            // 名前付き定数として集約する（マジックナンバー禁止）。実際の適用は Components/CrystalPickup +
            // Components/CrystalVfxLibrary + Editor/AssetIntegration.BuildCrystalGlowVfxPrefab/
            // BuildCrystalCollectVfxPrefab が行う。パーティクルテクスチャは IMG-04（既存 AssetKeys.
            // HitVfxSprite・S-17ヒットVFXと共用）を再利用し新規画像生成はしない（下記 AssetKeys.
            // CrystalGlowVfxPrefab/CrystalCollectVfxPrefab は生成される ParticleSystem prefab のパス）。 ---
            public const float GlowHaloEmissionBoost = 1.4f;      // EmissionIntensity に重ねて掛ける追加倍率(合計 EmissionIntensity*GlowHaloEmissionBoost)

            public const float GlowParticleEmissionRate = 2f;      // 個/秒。緩やかに漂うスパークルのため低頻度
            public const float GlowParticleLifetime = 1.6f;        // s, 1粒子あたりの寿命
            public const float GlowParticleStartSize = 0.22f;      // m
            public const float GlowParticleDriftSpeed = 0.12f;     // m/s, ふわっと漂う初速
            public const float GlowParticleSpawnRadius = 0.3f;     // m, クリスタル中心からの発生半径(球状シェイプ)
            public const int GlowParticleMaxParticles = 6;         // 常時存在する粒子数上限(緩やかな演出のため少数)

            public const float CollectVfxDuration = 0.2f;          // s, 回収フラッシュ全体の継続時間(ParticleSystem.main.duration)
            public const float CollectVfxParticleLifetime = 0.25f; // s, 各粒子の寿命
            public const float CollectVfxStartSize = 0.9f;         // m
            public const float CollectVfxStartSpeed = 1.5f;        // m/s, 回収の瞬間に外側へ弾ける初速
            public const int CollectVfxBurstCount = 8;             // 回収1回あたりの発生パーティクル数(HitVfxBurstCount=1より多い報酬感のあるフラッシュ)
        }

        // --- Meta progression / upgrades (gdd 数値表: メタ進行・アップグレード) ---
        public static class Upgrade
        {
            public const int CostBase = 50;                    // crystal (UPGRADE_COST_BASE)
            public const float CostGrowthPerLevel = 1.5f;      // (UPGRADE_COST_GROWTH_PER_LEVEL)
            public const int AttackLevelMax = 5;               // (UPG_ATTACK_LEVEL_MAX)
            public const float AttackBonusPerLevel = 0.10f;    // (UPG_ATTACK_BONUS_PER_LEVEL)
            public const int MoveSpeedLevelMax = 5;            // (UPG_MOVE_SPEED_LEVEL_MAX)
            public const float MoveSpeedBonusPerLevel = 0.04f; // (UPG_MOVE_SPEED_BONUS_PER_LEVEL)
            public const int MaxHpLevelMax = 5;                // (UPG_MAX_HP_LEVEL_MAX)
            public const int MaxHpBonusPerLevel = 10;          // HP (UPG_MAX_HP_BONUS_PER_LEVEL)
        }

        // --- Camera (gdd 数値表: カメラ。S-22: Phase 3 Polish revise — 旧値(PitchDeg=65/Height=14/Fov=50)
        // では ArenaCameraMath の南側可視限界が z≈-6.5m しかなく、S-20 の CR-CODE で「四方の敵を同時視認
        // できる可読性」acceptance が南側で未充足と判定された（state/reviews/s-20.md finding #1）。
        // game-designer が design/gdd.md「カメラ」節を、既存の調整レンジ（60–70°/12–18m/45–55°）の中で
        // 南側可視限界が最も深くなる境界値の組み合わせへ更新したのを受け、S-22 でその値をここへ反映する
        // （conventions.md §2: 初期値は必ず gdd 記載値を写す）。南側可視限界は z≈-9.6m まで拡大するが、
        // ARENA_RADIUS(12m)南端の完全カバーはレンジ内では未達のまま残る既知の制約（gdd「南側可視性の
        // 再点検」節参照）。 ---
        public static class Camera
        {
            public const float PitchDeg = 60f;                 // (CAMERA_PITCH_DEG)
            public const float Height = 18f;                   // m (CAMERA_HEIGHT)
            public const float Fov = 55f;                      // (CAMERA_FOV)

            // ArenaCameraMath.ComputeFixedPose divides by Mathf.Sin(pitchRad); this is the
            // meaningful tolerance ArenaCameraRig uses to detect a near-degenerate divisor
            // (pitch at/near 0 or 180 degrees). Mathf.Epsilon (denormal-min, ~1.4e-45) is NOT
            // a usable tolerance here: it only fires when sin is exactly 0, so near-0/near-180
            // pitches (e.g. 0.5deg or 180deg, where sin~=-8.7e-8) silently pass and produce a
            // degenerate (~1e8-magnitude) camera pose (CR-CODE S-04 iteration 2 finding).
            public const float MinAbsSinPitch = 1e-3f;
        }

        // --- Arena environment visuals (S-20: gdd/art-bible「クリスタル・アリーナ環境の視覚表現方針」
        // 決定 — アリーナ地面/境界/スポーンリングは生成MDLを使わずUnityプリミティブ+マテリアルのみで構成。
        // Renderer/mesh 生成は Components/ArenaEnvironment・幾何は Systems/ArenaEnvironmentSystem。
        // gdd 数値表に無い純粋な技術/表現パラメータのため、この節で名前付き定数として集約する
        // （マジックナンバー禁止）。色は art-bible.json の locked palette からのみ選ぶ) ---
        public static class Environment
        {
            public const float FloorHeight = 0.1f;              // m, 床プリミティブ(円柱)の厚み
            public const float FloorSurfaceMetallic = 0f;       // マット仕上げ(art-bible「フラット寄りの塗り」)
            public const float FloorSurfaceSmoothness = 0.15f;  // マット仕上げ(光沢を抑える)
            public const string FloorColor = Ui.ColorFocusHighlight; // #7FE850 背景基調(床・明) — art-bible palette[6]

            public const string BoundaryColor = "#1E4A26";      // 背景基調(床・暗/外周) — art-bible palette[7]
            public const float BoundaryRingWidth = 0.4f;        // m, 境界リングの幅
            public const float BoundaryRingHeight = 0.02f;      // m, 床(y=0)よりわずかに高く配置(z-fighting回避)

            // CR-CODE S-20 iteration 1 minor指摘#5対応: 旧値 #8B12A5 は敵主色(swarmerロール色)そのものだった
            // ため、敵がスポーンリングを横切る瞬間(P-01 四方可読性の要所)に同色相で輪郭コントラストが
            // 低下していた。
            // CR-CODE S-20 iteration 2 major指摘#3対応: 上記の是正で一旦 Ui.ColorTextSecondary(#3488D1)へ
            // 差し替えたが、その値は design/art-bible.md パレット表で「プレイヤー主色 — hero 装甲(ヘルメッ
            // ト・胸当て)のベース色」と明記された値そのもので、SceneWiring.cs のプレイヤープレースホルダ
            // 体色にも同じ定数が使われており、iter1で是正したはずの「敵ロール色の流用」と同種の欠陥(今度は
            // プレイヤーロール色の流用)を再発させていた。art-bible.json の palette 13色は役割1対1割当て
            // 済みでキャラクターロール(hero/enemy/crystal)と衝突しない色は「UI/テキスト」役の #F5F7FA
            // (HSV実測: sat≈0.02 の近白色。床#7FE850 sat≈0.66・敵#8B12A5 sat≈0.89・hero#3488D1 sat≈0.75
            // のいずれとも彩度で明確に区別できる)のみのため、これを採用する。Ui.ColorTextPrimary と同値だ
            // が、UI文字色とワールド内マーカー色という無関係な用途をエイリアスで結合すると片方の変更が
            // 意図せず他方に波及する(iter2 minor指摘#5)ため、独立したリテラルとして定義する。恒久確定は
            // art-reviewer/game-designer 確認待ち(state/active.md 記録・Checkpoint C 提示)。
            public const string SpawnRingColor = "#F5F7FA"; // 暫定: art-bible palette[11]「UI/テキスト」役(近白・低彩度でhero/enemy/floorいずれのロール色とも衝突しない) — 恒久確定は art-reviewer 確認待ち
            public const float SpawnRingWidth = 0.3f;           // m, スポーンリングの幅
            public const float SpawnRingHeight = 0.03f;         // m, 境界リングよりさらにわずかに高く配置

            public const int RingSegments = 64;                 // リングメッシュの分割数(円の滑らかさ)
        }

        // --- Post-process volume (S-27: art-bible style_block を key image の高発色トゥーンへ寄せる
        // ための URP グローバル Volume 設定。Bloom はクリスタル等の高輝度エミッシブ(GameConfig.Crystal.
        // EmissionIntensity=2.5倍のHDR面)を強調し、Color Adjustments は彩度/コントラストを底上げし、
        // Tonemapping は白飛び・黒潰れを抑えつつ発色を保つモードを選ぶ。gdd 数値表に無い純粋な表現/
        // 技術パラメータのため、この節で名前付き定数として集約する（マジックナンバー禁止）。実際の
        // VolumeProfile 構築・適用は Components/PostProcessRig が行う。) ---
        public static class PostProcess
        {
            // Bloom（URP VolumeComponent の標準フィールドのみ使用: threshold/intensity/scatter）。
            // Threshold は Crystal.EmissionIntensity(2.5倍)適用面だけがブルームし、非エミッシブ面
            // (hero/敵の通常マテリアル。おおむね輝度1.0未満)がブルームで白飛びしないよう、通常
            // マテリアルの想定輝度より高い値に設定する。
            public const float BloomThreshold = 1.3f;   // HDR輝度しきい値
            public const float BloomIntensity = 0.2f;   // ブルーム強度
            public const float BloomScatter = 0.35f;    // ハロー拡散半径(0-1)

            // Color Adjustments。key image の高発色トゥーンへ寄せる彩度/コントラスト強化だが、
            // シルエット可読性(P-01/P-03)を損なわないよう控えめな+方向のレンジに留める
            // (URP ColorAdjustments の有効レンジは postExposure=EV / contrast・saturation=-100..100)。
            public const float ColorPostExposure = 0f;  // EV。露出自体は変えない(白飛び回避)
            public const float ColorContrast = 12f;
            public const float ColorSaturation = 25f;

            // Tonemapping。GameConfig はレンダーパイプライン固有の型(UnityEngine.Rendering.Universal.
            // TonemappingMode)を持ち込まず bool で表現し、Components/PostProcessRig が URP の enum へ
            // マッピングする(GameConfig を汎用データのみに保つ方針)。Neutral(true)は ACES(false)より
            // 階調ロールオフが穏やかで、パレットの高彩度色(hero青/敵紫/クリスタル シアン&マゼンタ)を
            // 白飛び・黒潰れさせにくい(gdd/art-bible可読性要件)。
            public const bool TonemappingUseNeutral = true;
        }

        // --- Key light (S-27: art-bible.json style_block「Single soft key light from upper-front-left
        // producing only soft-edged ambient-occlusion darkening ... no hard cast shadows」を Game シーン
        // の Directional Light に反映する。gdd 数値表に無い純粋な表現/技術パラメータのため、この節で
        // 名前付き定数として集約する（マジックナンバー禁止）。実際の適用は Components/KeyLightRig が
        // 行う。) ---
        public static class Lighting
        {
            // 固定俯瞰カメラ(Systems/ArenaCameraMath.ComputeFixedPose: 南から北(+Z)を見る、ヨー無し)
            // 基準の "upper-front-left"。Y(yaw)を負にすると光線の進行方向が-X(西=カメラ視点で左)へ
            // 振れ、光源自体はカメラと同じ南側寄り("front")に位置しながら左上から差す形になる
            // (Components/KeyLightRig 参照)。
            public const float KeyLightPitchDeg = 50f;   // deg, X回転(仰角)
            public const float KeyLightYawDeg = -30f;    // deg, Y回転(左右)

            public const float KeyLightIntensity = 1.1f;

            // 独立したリテラル定数(GameConfig.Environment.SpawnRingColor の前例と同じ理由で Ui.* の
            // エイリアスにしない — 無関係な用途を結合すると片方の変更が意図せず他方に波及するため)。
            // art-bible.json palette の「UI/テキスト」役(近白・低彩度)を汎用ニュートラル光色として採用。
            public const string KeyLightColor = "#F5F7FA";

            // ソフトシャドウのみ(ハードシャドウ無し。実際の LightShadows.Soft 設定は Components/
            // KeyLightRig)で、かつ強度を抑えて黒潰れを避ける(style_block: ソフトエッジのAO的な陰のみ)。
            public const float KeyLightShadowStrength = 0.55f;
        }

        // --- Silhouette outline (S-28: art-bible.json style_block "clean bold 2-3px dark-navy outlines
        // on every silhouette edge" applied in-engine via a URP inverse-hull outline material (Shader
        // "ForgeGame/Outline" — game/Assets/Shaders/Outline.shader) appended as an extra material slot on
        // every hero/swarmer Renderer (Editor/AssetIntegration.BuildOutlineMaterial +
        // ApplyOutlineToRenderers, baked once at prefab-build time into Hero.prefab/Swarmer.prefab — no
        // new 3D model/texture generation per acceptance). gdd 数値表に無い純粋な表現/技術パラメータのため、
        // この節で名前付き定数として集約する（マジックナンバー禁止）。Color must stay within the locked
        // art-bible.json palette (this is palette[1], the dedicated "dark-navy outline" entry named in the
        // style_block itself, not an entity-role color — so no "role color reused for a different purpose"
        // risk like GameConfig.Environment.SpawnRingColor's precedent). ---
        public static class Outline
        {
            public const string Color = "#164583"; // dark-navy (art-bible.json palette[1] — style_block's own outline color)

            // World-space vertex-normal extrusion distance (meters) used by Outline.shader's vertex
            // stage — extrusion happens in WORLD space (not object space) so a single WidthMeters value
            // reads consistently across hero/swarmer despite their different FBX-root baked scales (CR-
            // CODE-motivated fix during S-28: an earlier object-space version made the same value read as
            // a crisp rim on one model and a silhouette-swallowing blob on the other — see Outline.shader
            // Vert's own comment for the full rationale; this comment previously said "object-space",
            // which was stale after that fix). Hero/swarmer stand ~1.6-2.0m tall (art-bible.json
            // scale.hero_height_range_m / swarmer_shoulder_height_m) — 0.02m reads as a crisp thin outline
            // at that scale (roughly the in-engine equivalent of the style_block's "2-3px" 2D-concept-art
            // outline weight) without ballooning the silhouette or swallowing small features like armor
            // edges.
            public const float WidthMeters = 0.02f;

            // Shader name backing Outline.shader (game/Assets/Shaders/Outline.shader), looked up via
            // Shader.Find in Editor/AssetIntegration.BuildOutlineMaterial and by shader-name identity in
            // Components/HeroFxController (to detect/strip the appended outline material slot during the
            // death fade — CR-CODE S-28 iter1 major finding fix). Centralized here instead of duplicated
            // as a private const in both files (資産参照はキー集約).
            public const string ShaderName = "ForgeGame/Outline";
        }

        // --- Material finish (S-28: art-bible.json style_block "thin glossy white highlight stripe on
        // armor edges" + "clear single-hue color blocking per role" — applied as in-engine URP/Lit
        // material property tweaks on the existing FBX-embedded PBR textures (Editor/AssetIntegration.
        // ApplyRoleColorBlockingAndRimHighlight, runs right after FixMaterialsForUrp), not a new texture/
        // shader per acceptance ("新規 3D モデル/テクスチャ再生成はしない"). Documented simplification:
        // URP/Lit has no bespoke rim-light term, so the "glossy highlight" is approximated by raising
        // _Smoothness (a sharper/brighter specular response under GameConfig.Lighting's upper-front-left
        // key light reads as a thin highlight along convex silhouette contours — the exact placement
        // style_block calls "armor edges"), and "white trim" stays whatever the FBX texture already
        // authored (this story intensifies contrast, it does not invent new trim geometry/paint). gdd 数値
        // 表に無い純粋な表現/技術パラメータのため、この節で名前付き定数として集約する（マジックナンバー禁止）。
        // Colors are the role-primary palette entries the style_block itself names ("hero reads as ...
        // blues", "swarmer enemy reads as ... purples"). ---
        public static class MaterialFinish
        {
            public const float HeroSmoothness = 0.65f;    // 0-1, higher = sharper/brighter specular ("glossy")
            public const float SwarmerSmoothness = 0.45f; // lower than hero — style_block: creature hide, not polished armor

            public const string HeroTintColor = "#3488D1";  // hero primary blue (art-bible.json palette[0])
            public const float HeroTintStrength = 0.18f;    // 0=texture's own _BaseColor unchanged, 1=solid tint

            public const string EnemyTintColor = "#8B12A5"; // swarmer primary purple (art-bible.json palette[3])
            public const float EnemyTintStrength = 0.18f;
        }

        // --- Presentation timings (gdd 決定) ---
        public static class Fx
        {
            public const float DeathSequenceDuration = 0.5f;   // s (gdd 勝敗条件: 死亡演出)
            public const float WavePulseDuration = 0.3f;       // s (gdd 難易度曲線: HUDパルス)
            public const float WavePulseScale = 1.3f;          // (1.0→1.3→1.0)

            // --- Death sequence + hit flash (S-16: gdd 勝敗条件「死亡演出の実現手段」/「被弾時の表現」—
            // 専用アニメクリップを追加せず、コード合成(マテリアルフェード+回転チルト+画面ディゾルブ)と
            // マテリアルフラッシュのみで表現する決定に対応。DeathSequenceDuration 以外はgdd数値表に無い
            // 純粋な演出パラメータのため、この節で名前付き定数として集約する（マジックナンバー禁止）。) ---
            public const float DeathTiltMaxDeg = 80f;    // deg, 死亡演出終了時点の転倒チルト角(hero local右軸まわり)
            public const float HitFlashDuration = 0.15f; // s, 被弾フラッシュ1回の継続時間(ENEMY_CONTACT_COOLDOWN=0.5sより
                                                          // 十分短く、連続被弾でも毎回クリーンにベース色へ復帰できる)

            // --- Auto-attack hit VFX (S-17: gdd「自動攻撃の当たり表現方式」— ヒット箇所に短命VFXを発生させ
            // 単体ヒットでも「効いている」当たり手応えを補う。IMG-04テクスチャを使ったUnity Particle System
            // (Editor/AssetIntegration.BuildHitVfxPrefab が生成、Components/AutoAttackDriver が
            // ヒット位置へInstantiateするだけの薄い配線。ParticleSystem.main.stopAction=Destroyで自己消滅する
            // ため寿命管理コードは不要). gdd 数値表に無い純粋な表現パラメータのため、この節で名前付き定数
            // として集約する（マジックナンバー禁止）。) ---
            public const float HitVfxDuration = 0.1f;          // s, ParticleSystem.main.duration(単発バーストのため最小限)
            public const float HitVfxParticleLifetime = 0.18f; // s, 各パーティクルの寿命(「短命VFX」— 瞬間ヒットに同期して消える)
            public const float HitVfxStartSize = 1.4f;         // m, パーティクル1個の初期サイズ(Billboardレンダリング)
            public const int HitVfxBurstCount = 1;             // 1ヒットあたりの発生パーティクル数(単発フラッシュ)

            // --- Dash near-miss camera shake (S-23: gdd P-01「紙一重回避」— ダッシュ無敵窓中に敵接触が
            // 無敵で無効化された瞬間、ArenaCameraRig の固定注視方向は変えずposition一時オフセットのみを
            // 発行して「際どく躱せた」実感を強化する。gdd 数値表に無い純粋な演出パラメータのため、この節
            // で名前付き定数として集約する（マジックナンバー禁止）。調整レンジは本story acceptanceの記載
            // 値（Duration 0.10–0.25s / Magnitude 0.10–0.30m）。) ---
            public const float DashNearMissShakeDuration = 0.15f;  // s (DASH_NEARMISS_SHAKE_DURATION)
            public const float DashNearMissShakeMagnitude = 0.15f; // m (DASH_NEARMISS_SHAKE_MAGNITUDE)

            // --- Dash afterimage trail (S-31: gdd P-01「紙一重回避」juice ブラッシュアップ — ダッシュ発動中
            // hero の見た目(HeroVisual)の半透明コピー(ゴースト)を一定間隔で生成しフェードアウトさせる、
            // 表示レイヤのみの演出。Systems/DashSystem の判定ロジックには関与しない。gdd 数値表に無い純粋な
            // 演出パラメータのため、この節で名前付き定数として集約する（マジックナンバー禁止）。調整レンジは
            // 本 story acceptance の記載値。) ---
            public const float DashTrailSpawnIntervalS = 0.03f; // s (DASH_DURATION=0.2sの間に約5〜7体生成)
            public const float DashTrailGhostLifetimeS = 0.25f; // s (ダッシュ終了後もわずかに軌跡が残る)
            public const float DashTrailGhostAlpha = 0.35f;     // 0-1 初期不透明度（背景/敵の可読性を阻害しない値）
            public const string DashTrailGhostTintColor = "#3488D1"; // art-bible.json palette[0] hero主色（新規色を発明しない）

            // --- Enemy kill impact: pop + camera nudge (S-32: gdd P-02「照準ゼロの自動攻撃」/P-03「群れ密度の
            // 圧力」juice ブラッシュアップ — 撃破確定の瞬間（非致死ヒットとは区別）にのみ、消滅前の一瞬の
            // スケールアップ「ポップ」と、ArenaCameraRig の固定注視方向は変えない小型カメラpositionノッジを
            // 発生させ「群れを確実に仕留めている」手応えを強調する。カメラノッジは S-23 DashNearMissShakeと
            // 同じ Systems/CameraShakeSystem 基盤（TickTimer/ComputeOffset）を再利用し、値だけ小さい別定数を
            // 使う（ニアミス回避の合図と撃破の合図を振幅で混同させないため — EnemyKillCameraNudgeMagnitudeM は
            // DashNearMissShakeMagnitude=0.15mより明確に小さい）。スコア加点・クリスタルドロップ
            // (Components/AutoAttackDriver.RegisterKillAndDropCrystals) はこの演出タイマーとは独立に撃破確定と
            // 同時に即座に実行され続ける（見た目の消滅のみ Components/EnemyAgent がポップ分だけ遅らせる）。
            // gdd 数値表に無い純粋な演出パラメータのため、この節で名前付き定数として集約する
            // （マジックナンバー禁止）。調整レンジは本 story acceptance の記載値。) ---
            public const float EnemyKillPopScaleMultiplier = 1.3f;      // (EnemyKillPopScaleMultiplier, 調整レンジ1.15-1.5)
            public const float EnemyKillPopDurationS = 0.12f;           // s (EnemyKillPopDurationS, 調整レンジ0.08-0.18s)
            public const float EnemyKillCameraNudgeMagnitudeM = 0.05f;  // m (EnemyKillCameraNudgeMagnitudeM, 調整レンジ0.03-0.08m)
            public const float EnemyKillCameraNudgeDurationS = 0.08f;   // s (EnemyKillCameraNudgeDurationS, 調整レンジ0.05-0.12s)

            // --- Result score count-up + high-score notice pulse (S-33: gdd P-04「積み上がる再挑戦」juice
            // ブラッシュアップ — Result 表示直後、最終スコアを0から最終値までイーズアウトでカウントアップし
            // 「積み上がっていく実感」を提示する。ハイスコア更新時は ResultHighScoreUpdatedText に
            // Systems/WavePulseSystem.ComputeScale（S-15と同一の三角波パルス関数）をこの節の値で再適用し
            // 強調する（新規パルス関数は作らず既存を再利用）。表示レイヤのみの追加で、Systems/ScoreSystem・
            // Components/ResultController の Submit/Cancel 遷移ロジックには関与しない（gdd 数値表に無い
            // 純粋な演出パラメータのため、この節で名前付き定数として集約する — マジックナンバー禁止）。
            // 調整レンジは本 story acceptance の記載値。) ---
            public const float ResultScoreCountUpDurationS = 0.8f;      // s (ResultScoreCountUpDurationS, 調整レンジ0.5-1.2s)
            public const float ResultCountUpEaseExponent = 2.0f;        // (ResultCountUpEaseExponent, 調整レンジ1.5-3.0. 大きいほど終盤の減速が強い)
            public const float ResultHighScoreNoticeScalePulse = 1.3f;  // (ResultHighScoreNoticeScalePulse, 調整レンジ1.15-1.5. S-15 WavePulseScale=1.3と揃えた基準値)
            public const float ResultHighScoreNoticePulseDurationS = 0.4f; // s (ResultHighScoreNoticePulseDurationS, 調整レンジ0.3-0.6s)
        }

        // --- Perf (ship-review: 高チャーン Instantiate/Destroy 面の free-list プール化 —
        // Components/GameObjectPool。敵(WaveSpawner/EnemyAgent)・クリスタル(CrystalPickup)・ヒット/回収
        // VFX(AutoAttackDriver/CrystalVfxLibrary)が対象。gdd 数値表に無い純粋な技術パラメータのため、
        // この節で名前付き定数として集約する（マジックナンバー禁止）。) ---
        public static class Perf
        {
            // プール1本あたりの保持上限。MAX_CONCURRENT_ENEMIES=40(GameConfig.Wave)を上回る余裕を持たせ、
            // 上限超過分は従来通り Destroy される（プール導入前とライフタイム同一 — メモリ保持量の上限
            // ガードであって挙動の分岐点ではない）。
            public const int PoolMaxRetained = 64;
        }

        // --- Audio defaults (gdd セーブデータ方針) ---
        public static class Audio
        {
            public const float DefaultBgmVolume = 0.8f;        // (bgmVolume 初期値)
            public const float DefaultSfxVolume = 0.8f;        // (sfxVolume 初期値)
            public const float VolumeStep = 0.1f;              // Menu 設定タブ A/D 1段階

            // --- AudioMixer bus (S-13: 設定タブ音量スライダー→AudioMixer即時反映。gdd「操作仕様」/
            // 「設定（音量・操作表示）」). Unity の AudioMixer は Editor 上でしか作成できない資産のため
            // Editor/AudioMixerSetup.cs が一度だけ生成し Assets/Generated/Audio/ に置く（他の Editor 生成
            // 資産＝Hero.controller と同じ置き場方針）。全 AudioSource をこのバスへ実際にルーティングする
            // のは S-19（音声統合）の範囲 — S-13 はミキサー資産の生成と exposed parameter の即時反映のみ。
            public const string MixerAssetPath = "Assets/Generated/Audio/Mixer.mixer";
            public const string MixerBgmGroupName = "Bgm";
            public const string MixerSfxGroupName = "Sfx";
            public const string MixerBgmVolumeParam = "BgmVolume"; // AudioMixer.SetFloat/GetFloat の exposed parameter 名
            public const string MixerSfxVolumeParam = "SfxVolume";
            public const float MixerMinDb = -80f;                  // 無音フロア（linear 0.0 相当）
            public const float MixerSilenceLinearFloor = 0.0001f;  // log10(0) = -∞ を避けるための下限
        }

        // --- 3D モデル取込後スケール検証基準 (design/art-bible.json "scale" が正本。Blender authoring-time
        // 計測と Unity import-time 計測はツール間の FBX 単位解釈差でズレ得るため、実際にレンダリングする
        // Unity 側の実測値をショップ時の正とし、範囲外なら ModelImporter.globalScale で補正する —
        // assets-config.md「生成後パイプライン」3D節 / tech-stack-unity.md「資産の取り扱い」) ---
        public static class ModelScale
        {
            public const float HeroTargetHeightM = 1.8f;   // art-bible.json scale.hero_height_m
            public const float HeroHeightRangeMinM = 1.6f; // art-bible.json scale.hero_height_range_m[0]
            public const float HeroHeightRangeMaxM = 2.0f; // art-bible.json scale.hero_height_range_m[1]
        }

        // --- Persistence (tech-stack-unity セーブ/永続化) ---
        public static class Save
        {
            public const int CurrentSchemaVersion = 1;         // save_version
            public const string FileName = "save.json";
        }

        // --- UI (shared uGUI parameters — rule 14: Canvas は RenderMode.ScreenSpaceCamera 固定。
        // フォントサイズ・色・座標は場当たり指定せずここに集約。色は design/art-bible.json の
        // palette からのみ選ぶ（独自色の発明禁止）) ---
        public static class Ui
        {
            public const float ReferenceWidth = 1920f;         // px, CanvasScaler reference resolution
            public const float ReferenceHeight = 1080f;        // px
            public const float CanvasPlaneDistance = 1f;        // m, ScreenSpaceCamera Canvas planeDistance
            public const string BuiltinFontName = "LegacyRuntime.ttf"; // Resources.GetBuiltinResource<Font>

            // Palette (design/art-bible.json "palette" より抜粋。hex文字列で保持し ColorUtility でパース)
            public const string ColorTextPrimary = "#F5F7FA";  // 白系（タイトル/本文）
            public const string ColorTextSecondary = "#3488D1"; // ヒーロー青（補助テキスト/ヒント）
            public const string ColorAlert = "#FF3B30";         // 警告/セーブ破損通知
            public const string ColorBackground = "#12081F";    // 近黒（void。メニュー系背景）
            public const string ColorFocusHighlight = "#7FE850"; // 緑（フォーカス中タブ/項目のハイライト）
            public const string ColorCrystalCyan = "#4FE8E2";    // クリスタル発光色1種目（通常ドロップ・S-09）
            public const string ColorCrystalMagenta = "#E62284"; // クリスタル発光色2種目（ヘヴィ変種ドロップ用に予約。未使用 — gdd ヘヴィ変種はドロップ数のみ差分、ドロップ色は現状据え置き）
            public const string ColorHeavyEnemyTint = "#6B1030"; // ヘヴィスウォーマーのマテリアル差し色（S-14。gdd 敵・障害物「見た目/ステータス差分のみ」・art-bible.json palette 収録色）

            // S-16 (死亡演出+被弾フラッシュ): ColorAlert/ColorBackground をそのまま再利用する。両者とも
            // 「警告」「近黒void」という汎用ロール色であり、hero青(#3488D1)/敵紫(#8B12A5)のような特定
            // エンティティのシルエット識別色ではないため、Environment.SpawnRingColor のコメントで指摘された
            // ような「エンティティロール色の流用による識別コントラスト低下」は生じない。
            public const string ColorHitFlash = ColorAlert;          // #FF3B30 被弾マテリアルフラッシュ色
            public const string ColorDeathDissolve = ColorBackground; // #12081F 死亡演出の画面ディゾルブ色

            // --- Title screen (S-02) ---
            public const int TitleFontSize = 96;
            public const int TitleHintFontSize = 32;
            public const int TitleNoticeFontSize = 26;
            public const string GameTitleText = "Crystal Vanguard";
            public const string TitleHintText = "Enter / Space ではじめる　　Esc で終了";
            public const string TitleRecoveryNoticeText = "セーブデータの一部が破損していたため初期状態から復旧しました";

            // Title screen layout: anchor Y (normalized, 0=bottom/1=top) and RectTransform
            // sizeDelta (px, at ReferenceWidth/Height) per text element. Anchor X is always
            // centered (0.5f, structural — not a tuning value) inside TitleScreen.CreateText.
            public const float TitleTextAnchorY = 0.62f;
            public const float TitleHintAnchorY = 0.42f;
            public const float TitleNoticeAnchorY = 0.16f;
            public static readonly Vector2 TitleTextSize = new Vector2(1400f, 160f);
            public static readonly Vector2 TitleHintSize = new Vector2(1200f, 80f);
            public static readonly Vector2 TitleNoticeSize = new Vector2(1500f, 90f);

            // --- Menu screen (S-03: 4タブ・必須4要素の枠) ---
            // Tab bar order/copy (gdd「Menu 画面構成」: はじめる/統計/アップグレード/設定、左から右).
            public const string MenuTabStartLabel = "はじめる";
            public const string MenuTabStatsLabel = "統計";
            public const string MenuTabUpgradeLabel = "アップグレード";
            public const string MenuTabSettingsLabel = "設定";
            public static readonly string[] MenuTabLabels =
                { MenuTabStartLabel, MenuTabStatsLabel, MenuTabUpgradeLabel, MenuTabSettingsLabel };

            // Tab indices / item counts (gdd: 統計はフォーカス項目なし = 表示専用).
            public static class MenuTabIndex
            {
                public const int Start = 0;
                public const int Stats = 1;
                public const int Upgrade = 2;
                public const int Settings = 3;
                public const int Count = 4;
            }
            public static class MenuItemCount
            {
                public const int Start = 1;
                public const int Stats = 0;
                public const int Upgrade = 3;
                public const int Settings = 2;
            }

            public const string MenuStartItemLabel = "プレイ開始 ▶";

            // 統計タブ項目ラベル（値は SaveData から実行時に合成。gdd「アウトゲーム表示」必須項目と一致）。
            public const string MenuStatHighScoreLabel = "ハイスコア";
            public const string MenuStatBestSurvivalLabel = "ベスト生存時間";
            public const string MenuStatBestWaveLabel = "最高到達ウェーブ";
            public const string MenuStatTotalRunsLabel = "累計プレイ回数";
            public const string MenuStatTotalKillsLabel = "累計撃破数";
            public const string MenuStatTotalCrystalsLabel = "累計獲得クリスタル";
            public const string MenuStatCrystalBalanceLabel = "クリスタル残高";
            public const string MenuStatUpgradeLevelsLabel = "アップグレードLv";

            // アップグレードタブ項目名（購入操作の配線は Components/MenuController — S-12）。
            public const string MenuUpgradeAttackLabel = "攻撃力";
            public const string MenuUpgradeMoveSpeedLabel = "移動速度";
            public const string MenuUpgradeMaxHpLabel = "最大HP";
            public static readonly string[] MenuUpgradeLabels =
                { MenuUpgradeAttackLabel, MenuUpgradeMoveSpeedLabel, MenuUpgradeMaxHpLabel };
            public const string MenuUpgradeMaxLevelText = "MAX";

            // Single source of truth for "Upgrade tab row index -> MetaProgression.UpgradeKind"
            // (CR-CODE S-12 iteration 1, finding M-2): row order must match MenuUpgradeLabels above
            // 1:1. Components/MenuController.UpgradeKindForFocusIndex derives the purchased kind from
            // this array instead of a hand-written switch, so there is exactly one place that encodes
            // the row ordering for the purchase path. Ui/MenuScreen.RenderUpgradeRow's own 0/1/2 call
            // sites remain a second (display-only) point of truth for now — MenuScreen.cs is
            // ui-engineer-owned (S-03) and out of scope for this gameplay-engineer story; see
            // state/reviews/s-12.md for the follow-up note.
            public static readonly MetaProgression.UpgradeKind[] MenuUpgradeRowKinds =
            {
                MetaProgression.UpgradeKind.Attack,
                MetaProgression.UpgradeKind.MoveSpeed,
                MetaProgression.UpgradeKind.MaxHp,
            };

            // 設定タブ（音量スライダーの A/D 調整実装自体は S-13。本 story は項目表示のみ）。
            public const string MenuSettingsBgmLabel = "BGM音量";
            public const string MenuSettingsSfxLabel = "SFX音量";
            public static readonly string[] MenuSettingsLabels = { MenuSettingsBgmLabel, MenuSettingsSfxLabel };
            public const string MenuInstructionsText = "WASD移動 / Spaceダッシュ / 攻撃は自動";

            // Settings row index (S-13: 0=BGM/1=SFX — MenuSettingsLabels の並びと一致・GameConfig.Audio の
            // どちらを更新するか Components/MenuController が参照する).
            public static class MenuSettingsIndex
            {
                public const int Bgm = 0;
                public const int Sfx = 1;
            }

            // Settings スライダーの可視化バー（S-13: HudHpBar/HudDashBar と同じ「背景+Filled塗り」方式を
            // 流用。各行の直下に薄いバーを重ねる）。
            public static readonly Vector2 MenuSettingsBarSize = new Vector2(360f, 12f);
            public const float MenuSettingsBarOffsetY = -30f; // px, 行テキストの直下（行スロット内のオフセット）

            // 統計タブ: 累計獲得クリスタル/クリスタル残高の行に添えるIMG-03アイコン（conventions.md §8）。
            // 行indexはBuildStatsPanelのlabels配列の並び（0 highScore..7 upgradeLevels）と一致させる。
            public static readonly Vector2 MenuStatCrystalIconSize = new Vector2(34f, 34f);
            public const float MenuStatCrystalIconOffsetX = -580f; // px, 行中心から左オフセット

            // Layout: tab bar row + stacked content rows (anchor Y + per-row px offset, mirrors
            // the Title screen's anchor+anchoredPos+size approach above).
            public const int MenuTabFontSize = 40;
            public const int MenuItemFontSize = 32;
            public const int MenuStatsFontSize = 28;
            public const int MenuInstructionsFontSize = 26;

            public const float MenuTabBarAnchorY = 0.90f;
            public static readonly float[] MenuTabAnchorXs = { 0.125f, 0.375f, 0.625f, 0.875f };
            public static readonly Vector2 MenuTabLabelSize = new Vector2(400f, 70f);

            public const float MenuContentAnchorY = 0.72f;
            public const float MenuRowSpacingPx = 56f;
            public static readonly Vector2 MenuRowSize = new Vector2(1100f, 52f);

            public const float MenuInstructionsAnchorY = 0.14f;
            public static readonly Vector2 MenuInstructionsSize = new Vector2(1300f, 60f);

            // UI ナビゲーション（W/S フォーカス移動）の入力しきい値。Navigate は Move と同じ
            // 2DVector(WASD) 合成なので、押下エッジ検出をこのしきい値で行う（GameInput 変更不要）。
            public const float MenuNavigateAxisThreshold = 0.5f;

            // --- Game HUD (S-10: HP/ダッシュCD/ウェーブ/スコア。P-01/P-03「危機感と回避余力を常時把握」) ---
            // No full-screen background panel here (unlike Title/Menu) — the HUD must never cover the
            // play area (role: 「プレイ領域を覆う演出は禁止」). Only small anchored elements per corner.
            public const int HudHpFontSize = 28;
            public const int HudDashFontSize = 22;
            public const int HudWaveFontSize = 46;
            public const int HudScoreFontSize = 34;

            public const string HudHpLabel = "HP";
            public const string HudDashLabel = "DASH";
            public const string HudDashReadyText = "DASH READY";
            public const string HudWaveLabelPrefix = "WAVE ";
            public const string HudScoreLabelPrefix = "SCORE ";

            // HP (top-left): value text above a filled bar.
            public static readonly Vector2 HudHpTextAnchor = new Vector2(0f, 1f);
            public static readonly Vector2 HudHpTextAnchoredPos = new Vector2(190f, -30f);
            public static readonly Vector2 HudHpTextSize = new Vector2(340f, 34f);
            public static readonly Vector2 HudHpBarAnchor = new Vector2(0f, 1f);
            public static readonly Vector2 HudHpBarAnchoredPos = new Vector2(190f, -60f);
            public static readonly Vector2 HudHpBarSize = new Vector2(340f, 20f);

            // Dash cooldown (bottom-left): value text above a filled bar (1.0 = ready, 0.0 = just used).
            public static readonly Vector2 HudDashTextAnchor = new Vector2(0f, 0f);
            public static readonly Vector2 HudDashTextAnchoredPos = new Vector2(150f, 76f);
            public static readonly Vector2 HudDashTextSize = new Vector2(260f, 30f);
            public static readonly Vector2 HudDashBarAnchor = new Vector2(0f, 0f);
            public static readonly Vector2 HudDashBarAnchoredPos = new Vector2(150f, 46f);
            public static readonly Vector2 HudDashBarSize = new Vector2(260f, 18f);

            // Wave (top-center, large — S-15 will pulse-scale this Text's RectTransform on wave-up).
            public static readonly Vector2 HudWaveAnchor = new Vector2(0.5f, 1f);
            public static readonly Vector2 HudWaveAnchoredPos = new Vector2(0f, -40f);
            public static readonly Vector2 HudWaveSize = new Vector2(420f, 70f);

            // Score (top-right).
            public static readonly Vector2 HudScoreAnchor = new Vector2(1f, 1f);
            public static readonly Vector2 HudScoreAnchoredPos = new Vector2(-190f, -40f);
            public static readonly Vector2 HudScoreSize = new Vector2(340f, 60f);

            // Colors reused from the locked palette (design/art-bible.json) — HP fill = green
            // (healthy/danger read at a glance), Dash fill = hero blue (charge indicator), bar
            // backgrounds reuse the near-black void background color at reduced coverage.
            public const string HudHpBarFillColor = ColorFocusHighlight;  // #7FE850
            public const string HudDashBarFillColor = ColorTextSecondary; // #3488D1 (hero blue)
            public const string HudBarBackgroundColor = ColorBackground;  // #12081F

            // --- Result screen (S-11: 最終スコア・ハイスコア更新・リスタート/メニュー導線) ---
            public const int ResultTitleFontSize = 72;
            public const int ResultStatFontSize = 36;
            public const int ResultHighScoreNoticeFontSize = 34;
            public const int ResultHintFontSize = 28;

            public const string ResultTitleText = "RESULT";
            public const string ResultFinalScoreLabel = "最終スコア";
            public const string ResultSurvivalTimeLabel = "生存時間";
            public const string ResultWaveReachedLabel = "到達ウェーブ";
            public const string ResultHighScoreUpdatedText = "ハイスコア更新！";
            public const string ResultRestartHintText = "Enter / Space でリスタート";
            public const string ResultMenuHintText = "Esc（メニューへ）";

            public const float ResultTitleAnchorY = 0.84f;
            public const float ResultFinalScoreAnchorY = 0.66f;
            public const float ResultSurvivalTimeAnchorY = 0.58f;
            public const float ResultWaveReachedAnchorY = 0.50f;
            public const float ResultHighScoreNoticeAnchorY = 0.40f;
            public const float ResultRestartHintAnchorY = 0.24f;
            public const float ResultMenuHintAnchorY = 0.16f;

            public static readonly Vector2 ResultTitleSize = new Vector2(900f, 110f);
            public static readonly Vector2 ResultStatSize = new Vector2(900f, 60f);
            public static readonly Vector2 ResultHighScoreNoticeSize = new Vector2(900f, 60f);
            public static readonly Vector2 ResultHintSize = new Vector2(900f, 50f);

            // --- S-30: IMG-05 decoration layout (panel background / heading ribbon / corner ornament /
            // tab-and-focus selection frames), applied on top of the existing Title/Menu/Result/HUD layout
            // above without changing any existing anchor/size constant (display-only addition — S-30
            // acceptance: 「既存の入力/遷移/表示ロジックは不変で、装飾は表示レイヤのみに閉じる」).

            // Title: one panel behind Title+Hint, a ribbon banner above it, two mirrored corner flourishes.
            public static readonly Vector2 TitlePanelAnchor = new Vector2(0.5f, 0.52f);
            public static readonly Vector2 TitlePanelAnchoredPos = Vector2.zero;
            public static readonly Vector2 TitlePanelSize = new Vector2(1500f, 620f);
            public static readonly Vector2 TitleRibbonAnchoredPos = new Vector2(0f, 340f); // relative to TitlePanelAnchor
            public static readonly Vector2 TitleRibbonSize = new Vector2(200f, 168f);
            public static readonly Vector2 TitleCornerAnchoredPos = new Vector2(-720f, 280f); // relative to TitlePanelAnchor; mirrored for the opposite corner
            public static readonly Vector2 TitleCornerSize = new Vector2(90f, 104f);

            // Result: mirrors the Title panel/ribbon/corner treatment (same visual language for the two
            // "headline" screens — HUD stays panel-only, no ribbon/corner, per the class-level "must never
            // cover play area" rule already documented on Ui/GameHud).
            // S-30 self-verification (qa/evidence/temp-s30-result.png during implementation): the initial
            // anchor(0.5,0.5)+900px-tall panel left only 90px of headroom above its top edge at 1080
            // reference height, clipping the ribbon's top off-screen. Anchor lowered to 0.47 and panel
            // height trimmed to 860 to reproduce the same ~54px ribbon/panel overlap and ~30px corner
            // inset Ui.TitlePanelAnchor/TitleRibbonAnchoredPos/TitleCornerAnchoredPos already use, without
            // clipping (content — Title/stats/hints, anchored 0.16–0.84 — stays comfortably inside either way).
            public static readonly Vector2 ResultPanelAnchor = new Vector2(0.5f, 0.47f);
            public static readonly Vector2 ResultPanelAnchoredPos = Vector2.zero;
            public static readonly Vector2 ResultPanelSize = new Vector2(1100f, 860f);
            public static readonly Vector2 ResultRibbonAnchoredPos = new Vector2(0f, 460f); // relative to ResultPanelAnchor
            public static readonly Vector2 ResultRibbonSize = new Vector2(200f, 168f);
            public static readonly Vector2 ResultCornerAnchoredPos = new Vector2(-500f, 380f); // relative to ResultPanelAnchor; mirrored for the opposite corner
            public static readonly Vector2 ResultCornerSize = new Vector2(90f, 104f);

            // Menu: one content-area panel (shared across all 4 tabs, sits behind whichever TabPanel is
            // active), a small ribbon accent between the tab bar and panel top edge, mirrored corner
            // flourishes, plus per-tab and per-item selection frames (Image swapped between
            // UiTabSelectedSprite/UiTabUnselectedSprite by SetActiveTab/SetFocusIndex, alongside — not
            // instead of — the existing text-color highlight).
            public static readonly Vector2 MenuPanelAnchor = new Vector2(0.5f, 0.46f);
            public static readonly Vector2 MenuPanelAnchoredPos = Vector2.zero;
            public static readonly Vector2 MenuPanelSize = new Vector2(1300f, 760f);
            // S-30 self-verification (qa/evidence/temp-s30-menu.png during implementation): the ribbon must
            // sit low enough to clear the tab bar row (MenuTabBarAnchorY=0.90) — the long "アップグレード"
            // label's rendered glyph width exceeds MenuTabFrameSize at MenuTabFontSize, so a ribbon placed
            // near tab-bar height visually collided with it. Kept small and hugging the panel's top edge
            // instead of hanging above it.
            public static readonly Vector2 MenuRibbonAnchoredPos = new Vector2(0f, 300f); // relative to MenuPanelAnchor
            public static readonly Vector2 MenuRibbonSize = new Vector2(150f, 124f);
            public static readonly Vector2 MenuCornerAnchoredPos = new Vector2(-620f, 340f); // relative to MenuPanelAnchor; mirrored for the opposite corner
            public static readonly Vector2 MenuCornerSize = new Vector2(70f, 80f);

            public static readonly Vector2 MenuTabFrameSize = new Vector2(200f, 66f); // slightly larger than MenuTabLabelSize, centered on the same anchor/pos
            public static readonly Vector2 MenuItemFrameSize = new Vector2(1150f, 50f); // slightly larger than MenuRowSize, centered on the same anchor/pos

            // HUD: small panels behind each corner stat group only (no ribbon/corner — functional overlay,
            // not a "headline" screen; class-level rule on Ui/GameHud already forbids covering the play
            // area, so these stay tightly sized around the existing HP/Dash/Wave/Score text+bar groups).
            public static readonly Vector2 HudHpPanelAnchor = HudHpTextAnchor;
            public static readonly Vector2 HudHpPanelAnchoredPos = new Vector2(190f, -45f);
            public static readonly Vector2 HudHpPanelSize = new Vector2(380f, 90f);

            public static readonly Vector2 HudDashPanelAnchor = HudDashTextAnchor;
            public static readonly Vector2 HudDashPanelAnchoredPos = new Vector2(150f, 61f);
            public static readonly Vector2 HudDashPanelSize = new Vector2(300f, 90f);

            public static readonly Vector2 HudWavePanelAnchor = HudWaveAnchor;
            public static readonly Vector2 HudWavePanelAnchoredPos = HudWaveAnchoredPos;
            public static readonly Vector2 HudWavePanelSize = new Vector2(460f, 100f);

            public static readonly Vector2 HudScorePanelAnchor = HudScoreAnchor;
            public static readonly Vector2 HudScorePanelAnchoredPos = HudScoreAnchoredPos;
            public static readonly Vector2 HudScorePanelSize = new Vector2(380f, 90f);
        }

        // --- Scene names (contract §11 必須シーン集合。Boot→Title→Menu→Game→Result) ---
        public static class Scenes
        {
            public const string Boot = "Boot";
            public const string Title = "Title";
            public const string Menu = "Menu";
            public const string Game = "Game";
            public const string Result = "Result";
        }

        // --- Asset keys (rule 5: dynamic-load paths/addresses live here only) ---
        // Populated as art/audio assets are integrated (design/assets.md IMG-/SFX-/BGM-/MDL-/ANM-).
        // Values are project-relative AssetDatabase paths (engine=unity 3D 資産統合: raw は game/_generated/
        // からコピーし game/Assets/Generated/ 配下に取込済み — contract §11). Editor/AssetIntegration.cs が
        // これらのパスだけを使って ModelImporter 設定・Prefab 生成・SerializeField 代入（WaveSpawner.enemyPrefab /
        // Components/SfxLibrary の各 AudioClip 欄）を行う。ランタイムコードはパス文字列を直接読まない
        // （SceneWiring/AssetIntegration が焼き込んだ SerializeField 参照経由でのみ資産に触れる）。
        public static class AssetKeys
        {
            // Prefabs (Editor-time AssetDatabase.LoadAssetAtPath source — baked into scene/SerializeField
            // at integrate time, not loaded at runtime via these strings directly).
            public const string HeroModel = "Assets/Generated/Models/model-hero.fbx";       // MDL-01
            public const string SwarmerModel = "Assets/Generated/Models/model-swarmer.fbx"; // MDL-02 (unrigged — must_replace, see MANIFEST)
            public const string HeroAnimIdle = "Assets/Generated/Anims/anim-hero-idle.fbx";     // ANM-02
            public const string HeroAnimRun = "Assets/Generated/Anims/anim-hero-run.fbx";       // ANM-03
            public const string HeroAnimAttack = "Assets/Generated/Anims/anim-hero-attack.fbx"; // ANM-01
            public const string HeroController = "Assets/Generated/Hero.controller";
            public const string HeroPrefab = "Assets/Generated/Prefabs/Hero.prefab";       // MDL-01 (built by AssetIntegration)
            public const string SwarmerPrefab = "Assets/Generated/Prefabs/Swarmer.prefab"; // MDL-02 (built by AssetIntegration; unrigged/static — approach_loop ANM-04 not generated, see degraded_route. S-21: root carries EnemyAgent, FBX nested under Enemy.VisualChildName child)
            public const string CrystalPrefab = "";    // クリスタル取込プレハブ枠（予約・未使用 — gdd 視覚表現方針によりクリスタルはプリミティブ生成のため取込プレハブ無し。Components/CrystalPickup.SpawnDrop 参照）
            public const string HitVfxSprite = "Assets/Generated/Images/img-04-hit-vfx.png"; // IMG-04 (S-17 が Editor/AssetIntegration.BuildHitVfxPrefab で Particle Material のテクスチャとして使用)
            public const string HitVfxPrefab = "Assets/Generated/Prefabs/HitVfx.prefab"; // IMG-04 (S-17 が Editor/AssetIntegration.BuildHitVfxPrefab で生成)
            public const string CrystalGlowVfxPrefab = "Assets/Generated/Prefabs/CrystalGlowVfx.prefab"; // S-29 (IMG-04相当のテクスチャ/マテリアルを再利用 — Editor/AssetIntegration.BuildCrystalGlowVfxPrefab で生成)
            public const string CrystalCollectVfxPrefab = "Assets/Generated/Prefabs/CrystalCollectVfx.prefab"; // S-29 (Editor/AssetIntegration.BuildCrystalCollectVfxPrefab で生成)
            public const string CrystalIconSprite = "Assets/Generated/Images/img-03-crystal-icon.png"; // IMG-03 (S-13 が Ui 層で使用)
            public const string ArenaBackdropTexture = "Assets/Generated/Images/img-06-arena-backdrop.png"; // IMG-06 (S-26: Editor/AssetIntegration.BuildArenaBackdropSkyboxMaterial が Skybox/6 Sided の全6面テクスチャとして焼き込み、生成した .mat 資産を Components/ArenaBackdrop._skyboxMaterial に配線する — CR-CODE s-26 iter1 major指摘#1/#4 fix: ランタイム Shader.Find 廃止)
            public const string ArenaBackdropSkyboxMaterial = "Assets/Generated/Materials/ArenaBackdropSkybox.mat"; // S-26 (CR-CODE s-26 iter2 minor指摘#3 fix: acceptance が「マテリアルキーは GameConfig の AssetKeys 経由」と明記するため、Editor/AssetIntegration.cs にあった private const をここへ移設。Editor/AssetIntegration.BuildArenaBackdropSkyboxMaterial が ArenaBackdropTexture から焼き込む)

            // S-30: IMG-05 UI 装飾スプライトキット。raw の1枚シート（Editor/AssetIntegration.
            // ConfigureUiFrameKitSprites が UiFrameKit.*Rect の座標で5要素を切り出し、下記5パスへ個別
            // Sprite として書き出す — Assets/Generated/Images/ 配下の派生ファイルであり、既存の
            // ArenaBackdropSkyboxMaterial（既に生成済みのテクスチャから Editor ツールが焼く派生アセット）
            // と同じ扱いのため新規 MANIFEST 行は追加しない（元の IMG-05 raw 生成の provenance が正）。
            public const string UiFrameKitTexture = "Assets/Generated/Images/img-05-ui-frame-kit.png"; // IMG-05 raw sheet (5要素合成シート)
            public const string UiPanelSprite = "Assets/Generated/Images/ui-frame-panel.png";                   // (a) 9-slice パネル枠
            public const string UiTabSelectedSprite = "Assets/Generated/Images/ui-frame-tab-selected.png";      // (b) 選択タブ/フォーカス項目フレーム
            public const string UiTabUnselectedSprite = "Assets/Generated/Images/ui-frame-tab-unselected.png";  // (b) 非選択タブ/項目フレーム
            public const string UiRibbonSprite = "Assets/Generated/Images/ui-frame-ribbon.png";                 // (c) 見出しリボン
            public const string UiCornerSprite = "Assets/Generated/Images/ui-frame-corner.png";                 // (c) コーナー装飾
            // Audio clips (design/assets.md SFX-/BGM-).
            public const string SfxAttackHit = "Assets/Generated/Audio/sfx-01-attack-hit.ogg";     // SFX-01
            public const string SfxDash = "Assets/Generated/Audio/sfx-02-dash.ogg";                // SFX-02
            public const string SfxPlayerHit = "Assets/Generated/Audio/sfx-03-player-hit.ogg";     // SFX-03
            public const string SfxCrystalPickup = "Assets/Generated/Audio/sfx-04-crystal-pickup.ogg"; // SFX-04
            public const string SfxWaveStart = "Assets/Generated/Audio/sfx-05-wave-start.ogg"; // SFX-05 (S-15)
            public const string SfxUpgradePurchase = "Assets/Generated/Audio/sfx-06-upgrade-purchase.ogg"; // SFX-06 (S-19)
            public const string BgmMainLoop = "Assets/Generated/Audio/bgm-01-main-loop.ogg";      // BGM-01 (S-19)
        }

        // --- IMG-05 UI frame kit slicing (S-30) ---
        // The raw sheet (AssetKeys.UiFrameKitTexture, 1024x1024) composites 5 decoration elements into a
        // 2-column x 3-row grid (design/assets.md IMG-05 MANIFEST post_process: "1024x1024の2x3グリッド
        // （マージン20px・ギャップ20px）"). Editor/AssetIntegration.ConfigureUiFrameKitSprites crops each
        // element out of the checked-in sheet using the rects below and writes 5 standalone Sprite PNGs
        // (AssetKeys.UiPanelSprite 等). Rects are pixel-space (x, yFromBottom, width, height — yFromBottom
        // matches Texture2D.GetPixels' bottom-left origin) measured once via an alpha-bbox scan over the
        // authored sheet (threshold=10, matching art-director's own MANIFEST alpha_verification_note
        // tolerance) and hardcoded here rather than re-scanned at Editor-integration time, so a future
        // regeneration of IMG-05 with a different composite layout fails loudly (garbled/misaligned crop —
        // caught by AR-ASSET-style visual sanity, not a silently drifting runtime scan) instead of silently
        // drifting like a purely dynamic scan would.
        public static class UiFrameKit
        {
            public const int SourceTextureSize = 1024; // px, both dimensions (square sheet)

            // (a) 9-slice panel border (top row, left column — dark-navy flat-fill interior).
            public static readonly RectInt PanelRect = new RectInt(110, 710, 299, 275);
            // (b) selected tab/focus-item frame (top row, right column — cyan glow border).
            public static readonly RectInt TabSelectedRect = new RectInt(607, 708, 311, 278);
            // (b) unselected tab/item frame (middle row, left column — dim navy border).
            public static readonly RectInt TabUnselectedRect = new RectInt(104, 374, 313, 278);
            // (c) heading ribbon/banner (middle row, right column — magenta fill).
            public static readonly RectInt RibbonRect = new RectInt(597, 374, 331, 278);
            // (c) corner flourish ornament (bottom row, left column — blue).
            public static readonly RectInt CornerRect = new RectInt(140, 39, 242, 280);

            // 9-slice border (left, bottom, right, top) in source-sheet pixels — panel sprite only.
            // Measured as the inset at which element (a)'s flat-fill interior (color dist<10 from
            // Ui.ColorBackground #12081F, per MANIFEST revision:2 spec_verification) begins on each side.
            // Uses the exact (non-uniform) per-side insets rather than a rounded-down uniform value: a
            // uniform border smaller than the true ring thickness on any side leaves a sliver of ring
            // pixels (light-blue highlight/shading) inside the supposedly-flat stretched interior column —
            // observed as a visible vertical streak in a self-verification screenshot (qa/evidence/
            // temp-s30-title.png during S-30 implementation) before this fix.
            public static readonly Vector4 PanelBorderPx = new Vector4(29f, 28f, 35f, 29f);

            public const int SpritePixelsPerUnit = 100; // uGUI default reference PPU
        }

        // --- Animator parameters (rule 13: アニメ切替は AnimatorController 資産側で行い、コードは
        // SetFloat/SetTrigger でパラメータを流すだけ。パラメータ名の文字列もマジックナンバー禁止の趣旨で
        // ここに集約する — GameConfig.Scenes の文字列定数と同じ扱い). Editor/AssetIntegration.cs が
        // 同名のパラメータを Hero.controller に生成する（Idle<->Run は Speed の閾値遷移、Attack は
        // AnyState からのトリガー遷移）。
        public static class Animation
        {
            public const string SpeedParam = "Speed";     // float, 0=idle / >0=run
            public const string AttackTrigger = "Attack"; // trigger, 自動攻撃ヒットの都度発火
            public const float RunSpeedThreshold = 0.1f;   // Hero.controller の Idle<->Run 遷移条件と同値

            // CR-CODE s-18 iter1 指摘(minor): Hero.controller の遷移クロスフェード/exitTime も
            // ゲームフィール(P-02)に直結するため、RunSpeedThreshold と同様に GameConfig へ集約する
            // (Editor/AssetIntegration.BuildHeroController がここを参照して AnimatorController を生成).
            public const float IdleRunBlendDurationS = 0.1f;   // Idle<->Run 遷移のクロスフェード時間
            public const float AttackExitTime = 1f;            // Attack ステートの正規化時間(1周)で Idle へ遷移
            public const float AttackToIdleBlendDurationS = 0.1f; // Attack->Idle 遷移のクロスフェード時間
        }
    }
}
