// AssetIntegration — batchmode entry that takes the raw generated 3D/audio/image assets already
// copied into Assets/Generated/ (from game/_generated/ — contract §11) and finishes the Unity-side
// integration: ModelImporter Humanoid/Avatar setup for the rigged hero, a generated AnimatorController
// (rule 13: idle/run/attack via SetFloat/SetTrigger only — no code-driven clip switching; S-17 also
// bakes the Attack state's speed scaling — Systems/AutoAttackSystem.ComputeAttackAnimSpeedScale — so the
// clip always finishes within one AUTO_ATTACK_INTERVAL), URP-safe materials (raw FBX materials import
// against the legacy Standard shader, which renders pink InternalErrorShader under this project's URP
// pipeline), Hero/Swarmer/HitVfx (S-17: IMG-04 Particle System) prefabs, and the Game scene wiring that
// swaps WaveSpawner.enemyPrefab / SfxLibrary's AudioClip slots / AutoAttackDriver's hit-VFX prefab slot
// from their placeholder state (contract §11 3D 資産統合; tech-stack-unity.md「資産の取り扱い」).
// S-19 音声統合: also opens Boot.unity (Components/BgmPlayer — BGM-01 全シーン共通ループ) and Menu.unity
// (Components/MenuController — SFX-06 アップグレード購入確定) to finish wiring the AudioMixer bus/clip
// references those scenes need (PatchBootScene/PatchMenuScene, alongside the existing PatchGameScene).
// Invoke: -executeMethod ForgeGame.EditorTools.AssetIntegration.IntegrateAll
// Rule 11: any unrecoverable failure escalates to a non-zero exit (never LogError+silently continue on
// a broken save). Safe to re-run (idempotent — mirrors SceneWiring's pattern).
// S-21 note: BuildSwarmerPrefab wraps the MDL-02 FBX under a "Visual" child of a plain root (instead of
// putting EnemyAgent directly on the FBX root) so Systems/EnemyVisualMotionSystem's coded forward-lean
// tilt + up/down bounce (Components/EnemyAgent) never fights Systems/EnemyApproachSystem for control of
// the same transform — see BuildSwarmerPrefab's own comment for the full rationale.
using System;
using System.IO;
using System.Linq;
using ForgeGame;
using ForgeGame.Components;
using ForgeGame.Systems;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

namespace ForgeGame.EditorTools
{
    public static class AssetIntegration
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        // S-19 音声統合: Boot/Menu scene patches (BgmPlayer / MenuController's purchase-SFX AudioSource)
        // mirror PatchGameScene's "SceneWiring ensures baseline -> open scene -> patch -> save" shape.
        private const string BootScenePath = "Assets/Scenes/Boot.unity";
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string BgmPlayerRootName = "BgmPlayer";
        private const string MenuRootName = "MenuController"; // mirrors Editor/SceneWiring.MenuRootName
        private const string PlayerRootName = "Player";
        // S-16: this name is also read at runtime by Components/HeroFxController (death fade/tilt + hit
        // flash need to find the same child under Player), so the literal now lives in GameConfig (the
        // single source of truth for cross-layer string constants — マジックナンバー禁止/rule 5と同じ趣旨)
        // instead of being duplicated as a private Editor-only const.
        private const string HeroVisualName = GameConfig.Player.HeroVisualChildName;
        private const string WaveSpawnerRootName = "WaveSpawner";
        private const string SfxLibraryRootName = "SfxLibrary";
        private const string ArenaBackdropRootName = "ArenaBackdrop"; // mirrors Editor/SceneWiring.ArenaBackdropRootName (S-26)
        private const string CrystalVfxLibraryRootName = "CrystalVfxLibrary"; // S-29, mirrors SfxLibraryRootName

        // CR-CODE s-18 iter2 minor指摘#4: extracted from ConfigureHeroModel's local literal so the
        // degradation-note wiring (path + sha256 + AppendManifestDegradationNote call, not just the two
        // leaf helpers) has a single source-of-truth path and can be exercised from
        // RecordHeroAvatarDegradation in an EditMode test.
        private const string HeroRawModelRelative = "models/model-hero.fbx"; // MDL-01 raw file under game/_generated/

        private static readonly string ReportPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "qa", "evidence", "asset-integration-report.txt"));

        // CR-CODE s-18 iter1 major指摘#2: MANIFEST 正本パス（contract §6: engine=unity は
        // game/_generated/MANIFEST.jsonl — Application.dataPath は game/Assets なので ".." 一つで game/）.
        private static readonly string ManifestPath =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_generated", "MANIFEST.jsonl"));

        private static readonly System.Text.StringBuilder Report = new System.Text.StringBuilder();

        // CR-CODE s-19 iteration 2 minor finding: PatchBgmPlayer/PatchMenuPurchaseSfx/PatchSfxLibrary's
        // AssignClip outcomes were only ever visible as free-text "# ..." comment lines in the report — a
        // key=value-parsing caller (gates.md Integrate 実施者の構造化返却 responsibility) could not tell
        // Assigned/SkippedPlanned/Failed apart without scraping comment text. These fields are populated by
        // the three Patch* methods (all run before WriteReport in IntegrateAll) and emitted as dedicated
        // key=value lines in WriteReport below.
        private static AssignClipResult _bgmClipResult = AssignClipResult.Failed;
        private static AssignClipResult _sfxAttackHitResult = AssignClipResult.Failed;
        private static AssignClipResult _sfxDashResult = AssignClipResult.Failed;
        private static AssignClipResult _sfxPlayerHitResult = AssignClipResult.Failed;
        private static AssignClipResult _sfxCrystalPickupResult = AssignClipResult.Failed;
        private static AssignClipResult _sfxWaveStartResult = AssignClipResult.Failed;
        private static AssignClipResult _sfxUpgradePurchaseResult = AssignClipResult.Failed;
        // S-26: reuses the same Assigned/SkippedPlanned/Failed vocabulary as the SFX/BGM clip results
        // above (AssignClipResult is not audio-specific despite the enum name — see PatchArenaBackdrop /
        // BuildArenaBackdropSkyboxMaterial below, which bake a Skybox material asset from a Texture2D
        // field instead of assigning an AudioClip field directly).
        private static AssignClipResult _arenaBackdropTextureResult = AssignClipResult.Failed;
        // S-28: whether BuildOutlineMaterial succeeded this run (shader resolved + GameConfig.Outline.Color
        // parsed) — surfaced as a structured WriteReport line, mirroring the AssignClipResult fields above.
        private static bool _outlineMaterialBuilt;
        // CR-CODE S-28 iter1 minor finding: ApplyRoleColorBlockingAndRimHighlight's [DEGRADED] outcomes
        // (tint parse failure, or a model whose materials carry none of _BaseColor/_Smoothness/_SpecColor)
        // were previously only visible as free-text "# ..." Log() comment lines — same key=value-parsing
        // gap S-19 iteration 2 already fixed for the audio AssignClip* fields above. Reuses the same
        // Assigned/Failed vocabulary (SkippedPlanned is not applicable to this presentation-only pass).
        private static AssignClipResult _materialFinishHeroResult = AssignClipResult.Failed;
        private static AssignClipResult _materialFinishSwarmerResult = AssignClipResult.Failed;
        // S-29: whether BuildCrystalGlowVfxPrefab/BuildCrystalCollectVfxPrefab succeeded this run —
        // structured WriteReport lines, mirroring _outlineMaterialBuilt above.
        private static bool _crystalGlowVfxBuilt;
        private static bool _crystalCollectVfxBuilt;
        // CR-CODE S-30 iter1 minor finding: whether all 5 ConfigureUiFrameKitSprites crops (panel/tab-
        // selected/tab-unselected/ribbon/corner) sliced successfully this run — structured WriteReport
        // line, mirroring _outlineMaterialBuilt/_crystalGlowVfxBuilt above (single aggregate flag rather
        // than one line per element, matching the established one-bool-per-builder precedent those two
        // fields set; a caller that needs to know *which* element degraded still has the free-text
        // per-rect "[DEGRADED] ... skipping '<path>'" Log() lines from SliceUiFrameKitElement).
        private static bool _uiFrameKitSpritesSliced;

        public static void IntegrateAll()
        {
            try
            {
                Log("=== AssetIntegration.IntegrateAll start ===");

                Avatar heroAvatar = ConfigureHeroModel();
                bool heroAvatarValid = heroAvatar != null && heroAvatar.isValid && heroAvatar.isHuman;
                Log($"Hero Avatar: valid={heroAvatarValid} (isValid={heroAvatar?.isValid}, isHuman={heroAvatar?.isHuman})");

                AnimationClip idleClip = ConfigureHeroAnimation(GameConfig.AssetKeys.HeroAnimIdle, heroAvatarValid);
                AnimationClip runClip = ConfigureHeroAnimation(GameConfig.AssetKeys.HeroAnimRun, heroAvatarValid);
                AnimationClip attackClip = ConfigureHeroAnimation(GameConfig.AssetKeys.HeroAnimAttack, heroAvatarValid);
                Log($"Hero clips: idle={idleClip != null} run={runClip != null} attack={attackClip != null}");

                ConfigureSwarmerModel();
                ConfigureCrystalIconSprite();
                ConfigureHitVfxTexture();
                ConfigureArenaBackdropTexture();
                ConfigureUiFrameKitSprites();

                FixMaterialsForUrp(GameConfig.AssetKeys.HeroModel);
                FixMaterialsForUrp(GameConfig.AssetKeys.SwarmerModel);

                // S-28: material-only "見た目密度" boost on top of the already-URP-fixed materials above —
                // no new texture/model (acceptance). Must run after FixMaterialsForUrp (needs _BaseColor/
                // _Smoothness to already exist on the URP/Lit shader).
                _materialFinishHeroResult = ApplyRoleColorBlockingAndRimHighlight(GameConfig.AssetKeys.HeroModel,
                    GameConfig.MaterialFinish.HeroTintColor, GameConfig.MaterialFinish.HeroTintStrength, GameConfig.MaterialFinish.HeroSmoothness);
                _materialFinishSwarmerResult = ApplyRoleColorBlockingAndRimHighlight(GameConfig.AssetKeys.SwarmerModel,
                    GameConfig.MaterialFinish.EnemyTintColor, GameConfig.MaterialFinish.EnemyTintStrength, GameConfig.MaterialFinish.SwarmerSmoothness);

                RuntimeAnimatorController controller = null;
                if (idleClip != null && runClip != null && attackClip != null)
                {
                    controller = BuildHeroController(idleClip, runClip, attackClip);
                }
                else
                {
                    Log("[DEGRADED] Hero AnimatorController skipped: one or more clips missing.");
                }

                // S-28: built once, applied as an extra material slot on every hero/swarmer Renderer by
                // BuildHeroPrefab/BuildSwarmerPrefab below (ApplyOutlineToRenderers) — must exist before
                // both prefab builds run.
                Material outlineMaterial = BuildOutlineMaterial();
                _outlineMaterialBuilt = outlineMaterial != null;
                Log($"Outline material built: {_outlineMaterialBuilt}");

                Vector3 heroBounds = BuildHeroPrefab(controller, heroAvatarValid, outlineMaterial);
                Log($"Hero prefab bounds (world size at identity transform): {heroBounds}");

                Vector3 swarmerBounds = BuildSwarmerPrefab(outlineMaterial);
                Log($"Swarmer prefab bounds (world size at identity transform): {swarmerBounds}");

                GameObject hitVfxPrefab = BuildHitVfxPrefab();
                Log($"Hit VFX prefab built: {hitVfxPrefab != null}");

                // S-29: crystal glow-halo + collect-flash particle prefabs — reuse the shared IMG-04
                // material BuildHitVfxPrefab just built above (see BuildCrystalGlowVfxPrefab's comment).
                // CR-CODE S-29 iter1 minor #1: gate both builders on hitVfxPrefab != null (this run's own
                // "IMG-04 resolved" signal) rather than letting them trust a stale HitVfxMaterial.mat left
                // on disk from a prior successful run.
                bool hitVfxHealthyThisRun = hitVfxPrefab != null;
                GameObject crystalGlowVfxPrefab = BuildCrystalGlowVfxPrefab(hitVfxHealthyThisRun);
                _crystalGlowVfxBuilt = crystalGlowVfxPrefab != null;
                Log($"Crystal glow VFX prefab built: {_crystalGlowVfxBuilt}");

                GameObject crystalCollectVfxPrefab = BuildCrystalCollectVfxPrefab(hitVfxHealthyThisRun);
                _crystalCollectVfxBuilt = crystalCollectVfxPrefab != null;
                Log($"Crystal collect VFX prefab built: {_crystalCollectVfxBuilt}");

                PatchGameScene();
                PatchBootScene();
                PatchMenuScene();

                WriteReport(heroAvatarValid, heroBounds, swarmerBounds, idleClip != null && runClip != null && attackClip != null,
                    hitVfxPrefab != null, _crystalGlowVfxBuilt, _crystalCollectVfxBuilt);
                Log("=== AssetIntegration.IntegrateAll done ===");
            }
            catch (Exception e)
            {
                Fail("Exception during AssetIntegration.IntegrateAll: " + e);
            }
        }

        // ------------------------------------------------------------------
        // Model import configuration
        // ------------------------------------------------------------------

        private static Avatar ConfigureHeroModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(GameConfig.AssetKeys.HeroModel);
            if (importer == null)
            {
                Fail("Hero model not found at " + GameConfig.AssetKeys.HeroModel);
                return null;
            }
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();

            // Engine take-in scale verification (gates.md AR-ASSET ※節 / tech-stack-unity.md「資産の取り
            // 扱い」: 取込後バウンディングボックスが art-bible.json のヒト型想定サイズから外れていたら
            // ModelImporter.scaleFactor(=globalScale) で補正する). Blender authoring-time 計測
            // (MANIFEST bbox_authoring_m, height_m≈1.8) と Unity import-time 実測がツール間の FBX 単位
            // 解釈差で乖離するケースを実際に観測したため（revision3 のスケール補正済み FBX でも Unity 側
            // 実測は約2.07m — swarmer 側は authoring 値と一致したため hero 固有の乖離）、Unity 実測を
            // 正として一様スケールで是正する。
            float measuredHeight = MeasureHeight(GameConfig.AssetKeys.HeroModel);
            Log($"  hero import (pre-correction) measured height={measuredHeight:F4}m (target range [{GameConfig.ModelScale.HeroHeightRangeMinM},{GameConfig.ModelScale.HeroHeightRangeMaxM}]m)");
            if (measuredHeight <= 0f)
            {
                // CR-CODE s-06 iter1 指摘#4: 0f was previously indistinguishable from "in range, no
                // correction needed" — a failed FBX load or a renderer-less instance (ComputeRendererBoundsSize's
                // own 0-renderer sentinel) silently skipped both the scale correction AND any error,
                // masquerading as success in this tool's own log even though the height is plainly
                // out-of-range. (The PlayMode height-range test still catches the resulting prefab, but
                // this tool should not itself claim success.)
                Log("[DEGRADED] hero height measurement failed (0m — FBX failed to load or has no Renderer) — scale correction skipped.");
            }
            else if (measuredHeight < GameConfig.ModelScale.HeroHeightRangeMinM || measuredHeight > GameConfig.ModelScale.HeroHeightRangeMaxM)
            {
                float correction = GameConfig.ModelScale.HeroTargetHeightM / measuredHeight;
                importer.globalScale = correction;
                importer.SaveAndReimport();
                float correctedHeight = MeasureHeight(GameConfig.AssetKeys.HeroModel);
                Log($"  hero import corrected: globalScale={correction:F6} -> measured height={correctedHeight:F4}m");
                if (correctedHeight < GameConfig.ModelScale.HeroHeightRangeMinM || correctedHeight > GameConfig.ModelScale.HeroHeightRangeMaxM)
                {
                    Log("[DEGRADED] Hero height still outside target range after ModelImporter.globalScale correction.");
                }
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(GameConfig.AssetKeys.HeroModel);
            Avatar avatar = assets.OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                // Generic 縮退 (tech-stack-unity.md「資産の取り扱い」: 失敗時 Generic へ縮退し MANIFEST に注記).
                // CR-CODE s-18 iter1 major指摘#2: この分岐は縮退をログ(Report)にしか残さず MANIFEST への
                // 注記が無かった（将来 hero FBX を再生成して Avatar 生成が実際に失敗した場合、provenance
                // 正本に何も残らない握り潰しになる）。MANIFEST.jsonl へ revision 注記行を追記する
                // （既存の「エンジン取込後検証」revision エントリと同じ、ファイル本体無変更・メタデータのみ
                // 追記の慣例に合わせる — assets-config.md Provenance 節）。
                Log("[DEGRADED] Hero Humanoid Avatar generation failed or invalid — falling back to Generic animation type.");
                string rawModelFullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_generated", HeroRawModelRelative));
                RecordHeroAvatarDegradation(ManifestPath, rawModelFullPath);
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.SaveAndReimport();
                assets = AssetDatabase.LoadAllAssetsAtPath(GameConfig.AssetKeys.HeroModel);
                avatar = assets.OfType<Avatar>().FirstOrDefault();
            }
            return avatar;
        }

        private static AnimationClip ConfigureHeroAnimation(string path, bool heroAvatarValid)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Log("[DEGRADED] Animation FBX not found at " + path);
                return null;
            }
            if (heroAvatarValid)
            {
                // Deliberately NOT "Copy From Other Avatar" (sourceAvatar): these animation-only FBX
                // share the hero's exact 24-bone rig (same Meshy rig task — MANIFEST bone_count/bone
                // names match), so letting each FBX build its own Humanoid Avatar
                // (CreateFromThisModel) auto-maps successfully on that identical skeleton. Mecanim's
                // normalized human-muscle-space clip data is what actually makes the resulting
                // AnimationClip retargetable onto ANY Humanoid Avatar at runtime (including Hero's) —
                // sourceAvatar-copy isn't required for that and failed here ("No human bone found")
                // because Copy-From-Other needs the *target* FBX's own bone names to already resolve
                // against the source avatar's HumanDescription in the same import pass, which doesn't
                // reliably hold across a fresh domain/asset-database reimport in batchmode.
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.Generic;
            }
            importer.SaveAndReimport();

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            // Rule 13: never pick Unity's auto-generated "__preview__" clip.
            return assets.OfType<AnimationClip>().FirstOrDefault(c => c.name != "__preview__");
        }

        private static void ConfigureSwarmerModel()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(GameConfig.AssetKeys.SwarmerModel);
            if (importer == null)
            {
                Fail("Swarmer model not found at " + GameConfig.AssetKeys.SwarmerModel);
                return;
            }
            // MDL-02 is unrigged (bone_count=0, rigged=false — MANIFEST degraded_route). Static mesh only.
            importer.animationType = ModelImporterAnimationType.None;
            importer.SaveAndReimport();
        }

        private static void ConfigureCrystalIconSprite()
        {
            string path = GameConfig.AssetKeys.CrystalIconSprite;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Log("[DEGRADED] Crystal icon texture not found at " + path);
                return;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        /// <summary>S-17: IMG-04 is consumed as a ParticleSystemRenderer material texture (not a uGUI
        /// Sprite like IMG-03), so this stays TextureImporterType.Default — no atlas/pivot/PPU settings
        /// apply. Mirrors ConfigureCrystalIconSprite's alpha/mipmap handling otherwise.</summary>
        private static void ConfigureHitVfxTexture()
        {
            string path = GameConfig.AssetKeys.HitVfxSprite;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Log("[DEGRADED] Hit VFX texture not found at " + path);
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        /// <summary>S-26: IMG-06 is opaque (design/assets.md「アルファチャンネル不要」/ MANIFEST
        /// alpha_verified="n/a (opaque background art...)"), consumed as a Skybox/6 Sided face texture
        /// (Components/ArenaBackdrop), not a uGUI Sprite or alpha-blended particle texture — so this
        /// mirrors ConfigureHitVfxTexture's Default/no-mipmap/Clamp shape but with alphaIsTransparency=false
        /// (no alpha channel to treat as transparency) and mipmaps left disabled (a skybox face is always
        /// sampled near-orthogonally at effectively infinite distance, so mip filtering buys nothing and
        /// would only soften the already-low-detail gradient further).</summary>
        private static void ConfigureArenaBackdropTexture()
        {
            string path = GameConfig.AssetKeys.ArenaBackdropTexture;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Log("[DEGRADED] Arena backdrop texture not found at " + path);
                return;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        /// <summary>S-30: crops the 5 IMG-05 decoration elements (panel/tabSelected/tabUnselected/ribbon/
        /// corner — GameConfig.UiFrameKit.*Rect) out of the single composite sheet
        /// (GameConfig.AssetKeys.UiFrameKitTexture) and writes each as a standalone Sprite PNG under
        /// Assets/Generated/Images/ (GameConfig.AssetKeys.Ui*Sprite), mirroring ConfigureCrystalIconSprite's
        /// Sprite/Single/alphaIsTransparency import shape for each output. These are derived engine-side
        /// assets built from an already-provenance-recorded raw generation (same precedent as
        /// BuildArenaBackdropSkyboxMaterial baking a .mat from ArenaBackdropTexture) — no new MANIFEST rows.
        /// Missing/unreadable source degrades to [DEGRADED] + return (non-fatal — cosmetic UI decoration,
        /// same tolerance as ConfigureCrystalIconSprite/ConfigureHitVfxTexture for a not-yet-generated
        /// source texture).</summary>
        private static void ConfigureUiFrameKitSprites()
        {
            string sourcePath = GameConfig.AssetKeys.UiFrameKitTexture;
            var sourceImporter = (TextureImporter)AssetImporter.GetAtPath(sourcePath);
            if (sourceImporter == null)
            {
                Log("[DEGRADED] UI frame kit sheet not found at " + sourcePath);
                return;
            }
            // Read the source pixels via a temporary isReadable import (never a Sprite itself — only its
            // 5 crops are consumed by the UI layer). CR-CODE S-30 iter1 minor finding fix: reverted to
            // isReadable=false in the finally block below on every exit path (including the size-
            // validation early return, which previously left isReadable=true stuck permanently) so this
            // method is idempotent and doesn't leave a standing CPU-readable-copy side effect on the
            // source sheet's importer once slicing is done.
            sourceImporter.textureType = TextureImporterType.Default;
            sourceImporter.isReadable = true;
            sourceImporter.mipmapEnabled = false;
            sourceImporter.SaveAndReimport();

            try
            {
                var sourceTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(sourcePath);
                if (sourceTexture == null || sourceTexture.width != GameConfig.UiFrameKit.SourceTextureSize
                    || sourceTexture.height != GameConfig.UiFrameKit.SourceTextureSize)
                {
                    Log($"[DEGRADED] UI frame kit sheet failed to load or has unexpected size at {sourcePath} " +
                        $"(expected {GameConfig.UiFrameKit.SourceTextureSize}x{GameConfig.UiFrameKit.SourceTextureSize}) — skipping slice.");
                    return;
                }

                bool allOk = true;
                allOk &= SliceUiFrameKitElement(sourceTexture, GameConfig.UiFrameKit.PanelRect, GameConfig.AssetKeys.UiPanelSprite, GameConfig.UiFrameKit.PanelBorderPx);
                allOk &= SliceUiFrameKitElement(sourceTexture, GameConfig.UiFrameKit.TabSelectedRect, GameConfig.AssetKeys.UiTabSelectedSprite, Vector4.zero);
                allOk &= SliceUiFrameKitElement(sourceTexture, GameConfig.UiFrameKit.TabUnselectedRect, GameConfig.AssetKeys.UiTabUnselectedSprite, Vector4.zero);
                allOk &= SliceUiFrameKitElement(sourceTexture, GameConfig.UiFrameKit.RibbonRect, GameConfig.AssetKeys.UiRibbonSprite, Vector4.zero);
                allOk &= SliceUiFrameKitElement(sourceTexture, GameConfig.UiFrameKit.CornerRect, GameConfig.AssetKeys.UiCornerSprite, Vector4.zero);
                _uiFrameKitSpritesSliced = allOk;
                Log($"UI frame kit sprites sliced: allOk={allOk}");
            }
            finally
            {
                sourceImporter.isReadable = false;
                sourceImporter.SaveAndReimport();
            }
        }

        /// <summary>Crops one <paramref name="rect"/> (x, yFromBottom, width, height — bottom-left origin,
        /// matching Texture2D.GetPixels) out of <paramref name="sourceTexture"/>, writes it as a standalone
        /// PNG at <paramref name="outputPath"/>, and imports it as a Sprite (Single mode). A non-zero
        /// <paramref name="border"/> sets the sprite's 9-slice border (panel sprite only — every other
        /// caller passes Vector4.zero, which Image.Type.Sliced renders as an equivalent plain stretch).</summary>
        private static bool SliceUiFrameKitElement(Texture2D sourceTexture, RectInt rect, string outputPath, Vector4 border)
        {
            if (rect.x < 0 || rect.y < 0 || rect.x + rect.width > sourceTexture.width || rect.y + rect.height > sourceTexture.height)
            {
                Log($"[DEGRADED] UI frame kit element rect {rect} out of bounds for {sourceTexture.width}x{sourceTexture.height} sheet — skipping '{outputPath}'.");
                return false;
            }

            Color[] pixels = sourceTexture.GetPixels(rect.x, rect.y, rect.width, rect.height);
            var cropped = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
            cropped.SetPixels(pixels);
            cropped.Apply();

            byte[] png = cropped.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(cropped);
            if (png == null || png.Length == 0)
            {
                Log($"[DEGRADED] UI frame kit element EncodeToPNG failed for '{outputPath}'.");
                return false;
            }

            // Application.dataPath already IS the project's "Assets" folder — outputPath (e.g.
            // "Assets/Generated/Images/ui-frame-panel.png") must be combined with its portion AFTER
            // "Assets/" directly onto dataPath, not with an extra ".." (that would land one level up,
            // outside Assets/ entirely and thus never get imported as a project asset).
            string fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, outputPath.Substring("Assets/".Length)));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllBytes(fullPath, png);
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(outputPath);
            if (importer == null)
            {
                Log($"[DEGRADED] UI frame kit element wrote '{outputPath}' but AssetImporter could not be resolved after import.");
                return false;
            }
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = GameConfig.UiFrameKit.SpritePixelsPerUnit;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            if (border != Vector4.zero)
            {
                importer.spriteBorder = border;
            }
            importer.SaveAndReimport();
            return true;
        }

        /// <summary>FBX materials import against the legacy Standard shader (Unity's default FBX material
        /// mapping), which renders as pink InternalErrorShader under this project's URP pipeline
        /// (tech-stack-unity.md 規約 visual sanity check). Re-point every material sub-asset at
        /// Universal Render Pipeline/Lit, carrying over the base color texture/tint and normal map that
        /// Unity's default Standard-shader mapping already resolved from the FBX's embedded PBR textures
        /// (MANIFEST texture_resolution=2048/pbr=true) — metallic/roughness packing is intentionally not
        /// remapped (URP/Lit's single-channel MetallicGlossMap needs an R=metallic/A=smoothness combine
        /// that these separate grayscale maps aren't packed for); this is a documented simplification,
        /// not a silent drop (see the return-value degradation note surfaced by IntegrateAll's caller).</summary>
        private static void FixMaterialsForUrp(string modelPath)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Log("[DEGRADED] URP/Lit shader not found — materials left as imported (pink InternalErrorShader risk).");
                return;
            }

            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);

            // FBX-embedded PBR textures (MANIFEST texture_resolution=2048/pbr=true, byte-scan confirmed
            // 4 embedded PNGs) aren't surfaced as loadable sub-assets by default — ModelImporter.
            // ExtractTextures pulls them out into real Texture2D assets next to the model so they can be
            // assigned to the URP/Lit material below (assets-config.md 3D 節: PBR テクスチャは base_color/
            // metallic/roughness/normal の4枚).
            string textureFolder = Path.Combine(Path.GetDirectoryName(modelPath)!.Replace('\\', '/'),
                Path.GetFileNameWithoutExtension(modelPath) + "_Textures");
            bool texturesExtracted = importer.ExtractTextures(textureFolder);
            AssetDatabase.Refresh();

            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { textureFolder });
            if (!texturesExtracted || textureGuids.Length == 0)
            {
                // CR-CODE s-06 iter1 指摘#2: ExtractTextures' bool return was previously ignored, and the
                // 0-texture path fell through to the generic "baseMap=none" summary log at the bottom of
                // this method (which reads identically whether 0 or N textures were found) — making a
                // fully-untextured (flat-gray) material indistinguishable from a normal run in the
                // report. Surface it explicitly as a degradation.
                Log($"[DEGRADED] no textures extracted from {modelPath} (ExtractTextures returned {texturesExtracted}, found {textureGuids.Length} Texture2D asset(s) under {textureFolder}) — material(s) will have no BaseMap/BumpMap.");
            }
            Texture2D baseMap = null;
            Texture2D normalMap = null;
            foreach (string guid in textureGuids)
            {
                string texPath = AssetDatabase.GUIDToAssetPath(guid);
                string lower = texPath.ToLowerInvariant();
                Log($"  extracted texture: {texPath}");
                if (lower.Contains("normal"))
                {
                    normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                }
                else if (lower.Contains("base_color") || lower.Contains("basecolor") || lower.Contains("albedo")
                         || lower.Contains("diffuse") || lower.Contains("_0.") || lower.Contains("_base"))
                {
                    baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                }
            }
            // Fallback heuristic when names carry no semantic hint (e.g. generic "texture_0..3"):
            // MANIFEST/Blender pipeline convention for these models is texture_0=base_color (see
            // MDL-02 revision2 MANIFEST note: "再ダウンロードしたbase_colorテクスチャ（texture_0.png...)").
            if (baseMap == null && textureGuids.Length > 0)
            {
                string firstPath = AssetDatabase.GUIDToAssetPath(textureGuids[0]);
                baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(firstPath);
                Log($"  [DEGRADED] no name-matched base color texture — falling back to first extracted texture: {firstPath}");
            }
            if (baseMap != null)
            {
                var baseImporter = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(baseMap));
                if (baseImporter != null && baseImporter.sRGBTexture == false)
                {
                    baseImporter.sRGBTexture = true; // base color must be sRGB (metallic/roughness/normal must not — left alone)
                    baseImporter.SaveAndReimport();
                }
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            int fixedCount = 0;
            foreach (Material mat in assets.OfType<Material>())
            {
                Color mainColor = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;

                mat.shader = urpLit;
                mat.SetColor("_BaseColor", mainColor);
                if (baseMap != null)
                {
                    mat.SetTexture("_BaseMap", baseMap);
                }
                if (normalMap != null)
                {
                    mat.SetTexture("_BumpMap", normalMap);
                    mat.EnableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(mat);
                fixedCount++;
            }
            AssetDatabase.SaveAssets();
            Log($"FixMaterialsForUrp({modelPath}): {fixedCount} material(s) re-pointed at URP/Lit (baseMap={(baseMap != null ? baseMap.name : "none")}, normalMap={(normalMap != null ? normalMap.name : "none")}).");
        }

        /// <summary>S-28: material-only "見た目密度" boost (art-bible.json style_block "thin glossy white
        /// highlight stripe on armor edges" + "clear single-hue color blocking per role") on top of the
        /// URP/Lit materials FixMaterialsForUrp already re-pointed — no new texture/model (acceptance:
        /// "新規 3D モデル/テクスチャ再生成はしない"). Lerps each material's existing _BaseColor toward
        /// <paramref name="tintColorHex"/> by <paramref name="tintStrength"/> (strengthens the role's
        /// locked palette hue on top of the FBX-embedded PBR texture, does not replace it) and raises
        /// _Smoothness to <paramref name="smoothness"/> (sharper/brighter specular response under
        /// GameConfig.Lighting's key light — see GameConfig.MaterialFinish's header comment for the
        /// documented "no bespoke rim shader" simplification this approximates). [DEGRADED] (not Fail) on
        /// an unparseable tint color, or when no material at <paramref name="modelPath"/> exposes any of
        /// _BaseColor/_Smoothness/_SpecColor (nothing was actually changed) — presentation-only, must not
        /// block the rest of IntegrateAll. Returns Assigned/Failed (CR-CODE S-28 iter1 minor finding: the
        /// caller surfaces this as a structured material_finish_hero=/material_finish_swarmer= WriteReport
        /// line instead of leaving [DEGRADED] only reachable by scraping "# ..." comment lines — same gap
        /// S-19 iteration 2 already fixed for the audio AssignClip* fields).</summary>
        private static AssignClipResult ApplyRoleColorBlockingAndRimHighlight(string modelPath, string tintColorHex, float tintStrength, float smoothness)
        {
            if (!ColorUtility.TryParseHtmlString(tintColorHex, out Color tint))
            {
                Log("[DEGRADED] ApplyRoleColorBlockingAndRimHighlight(" + modelPath + "): tint color '" + tintColorHex + "' failed to parse — material finish skipped.");
                return AssignClipResult.Failed;
            }

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(modelPath);
            int patched = 0;
            foreach (Material mat in assets.OfType<Material>())
            {
                // CR-CODE S-28 iter1 minor finding: previously `patched++` ran unconditionally for every
                // Material asset found, even ones with none of these three properties (e.g. a stray
                // sub-asset material the FBX importer created) — so a report claiming "-> N material(s)"
                // could be true even when zero actual property writes happened. Only count a material once
                // at least one property on it was actually set.
                bool changed = false;
                if (mat.HasProperty("_BaseColor"))
                {
                    Color baseColor = mat.GetColor("_BaseColor");
                    mat.SetColor("_BaseColor", Color.Lerp(baseColor, tint, tintStrength));
                    changed = true;
                }
                if (mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", smoothness);
                    changed = true;
                }
                if (mat.HasProperty("_SpecColor"))
                {
                    // style_block: "thin glossy white highlight stripe" — keep the specular reflection
                    // itself neutral white regardless of role tint, so the highlight reads as a bright
                    // white rim rather than picking up the hero-blue/enemy-purple _BaseColor tint above.
                    mat.SetColor("_SpecColor", Color.white);
                    changed = true;
                }
                if (changed)
                {
                    EditorUtility.SetDirty(mat);
                    patched++;
                }
            }
            AssetDatabase.SaveAssets();
            Log($"ApplyRoleColorBlockingAndRimHighlight({modelPath}): tint={tintColorHex}@{tintStrength:F2} smoothness={smoothness:F2} -> {patched} material(s).");
            if (patched == 0)
            {
                Log("[DEGRADED] ApplyRoleColorBlockingAndRimHighlight(" + modelPath + "): no material at this path exposed _BaseColor/_Smoothness/_SpecColor — material finish had no effect.");
                return AssignClipResult.Failed;
            }
            return AssignClipResult.Assigned;
        }

        // ------------------------------------------------------------------
        // AnimatorController (rule 13)
        // ------------------------------------------------------------------

        private static RuntimeAnimatorController BuildHeroController(AnimationClip idle, AnimationClip run, AnimationClip attack)
        {
            string path = GameConfig.AssetKeys.HeroController;
            // CR-CODE s-06 iter1 指摘#5: the previous File.Exists(Path.Combine(dataPath, "..", path.Substring(...)))
            // stripped "Assets/" twice (Application.dataPath already ends in .../Assets), pointing at
            // <project>/Generated/Hero.controller instead of <project>/Assets/Generated/Hero.controller —
            // so DeleteAsset never ran and re-integration silently relied on
            // CreateAnimatorControllerAtPath's own overwrite behavior instead. Use the AssetDatabase
            // directly (project-relative path, no manual filesystem path math needed).
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter(GameConfig.Animation.SpeedParam, AnimatorControllerParameterType.Float);
            controller.AddParameter(GameConfig.Animation.AttackTrigger, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine rootSm = controller.layers[0].stateMachine;
            AnimatorState idleState = rootSm.AddState("Idle");
            idleState.motion = idle;
            AnimatorState runState = rootSm.AddState("Run");
            runState.motion = run;
            AnimatorState attackState = rootSm.AddState("Attack");
            attackState.motion = attack;
            // S-17: gdd 決定「アニメ長が AUTO_ATTACK_INTERVAL を超える場合は再生速度でスケールする」— baked
            // once here (rule 13: アニメ切替はコードでなく AnimatorController 資産) rather than at runtime;
            // AnimatorStateInfo.speed reflects this authored value at playback time.
            attackState.speed = AutoAttackSystem.ComputeAttackAnimSpeedScale(attack.length, GameConfig.Player.AutoAttackInterval);
            rootSm.defaultState = idleState;

            AnimatorStateTransition idleToRun = idleState.AddTransition(runState);
            idleToRun.hasExitTime = false;
            idleToRun.duration = GameConfig.Animation.IdleRunBlendDurationS;
            idleToRun.AddCondition(AnimatorConditionMode.Greater, GameConfig.Animation.RunSpeedThreshold, GameConfig.Animation.SpeedParam);

            AnimatorStateTransition runToIdle = runState.AddTransition(idleState);
            runToIdle.hasExitTime = false;
            runToIdle.duration = GameConfig.Animation.IdleRunBlendDurationS;
            runToIdle.AddCondition(AnimatorConditionMode.Less, GameConfig.Animation.RunSpeedThreshold, GameConfig.Animation.SpeedParam);

            AnimatorStateTransition anyToAttack = rootSm.AddAnyStateTransition(attackState);
            anyToAttack.hasExitTime = false;
            anyToAttack.duration = 0f;
            anyToAttack.canTransitionToSelf = false;
            anyToAttack.AddCondition(AnimatorConditionMode.If, 0, GameConfig.Animation.AttackTrigger);

            AnimatorStateTransition attackToIdle = attackState.AddTransition(idleState);
            attackToIdle.hasExitTime = true;
            attackToIdle.exitTime = GameConfig.Animation.AttackExitTime;
            attackToIdle.duration = GameConfig.Animation.AttackToIdleBlendDurationS;

            AssetDatabase.SaveAssets();
            return controller;
        }

        // ------------------------------------------------------------------
        // Outline material (S-28: URP Outline 輪郭線 + hero/swarmer マテリアル増強)
        // ------------------------------------------------------------------

        private const string OutlineMaterialPath = "Assets/Generated/Materials/OutlineMaterial.mat";
        // CR-CODE S-28 iter1 major finding fix: shader name moved to GameConfig.Outline.ShaderName so
        // Components/HeroFxController can detect the appended outline slot by the same identity without
        // duplicating this string as a second private const (資産参照はキー集約).
        private const string OutlineColorProperty = "_OutlineColor";
        private const string OutlineWidthProperty = "_OutlineWidth";

        /// <summary>Builds (or updates in-place, mirroring BuildArenaBackdropSkyboxMaterial's/
        /// HitVfxMaterialPath's idempotent load-or-create pattern) the single shared inverse-hull outline
        /// material used by both hero and swarmer (Outline.shader, values from GameConfig.Outline).
        /// Baking it as a project asset — instead of constructing it purely at runtime with Shader.Find —
        /// is what makes Unity's default build-time shader stripping include "ForgeGame/Outline" in the
        /// player build (a non-builtin shader ships only when referenced by an asset included in the
        /// build; mirrors BuildArenaBackdropSkyboxMaterial's identical reasoning for "Skybox/6 Sided").
        /// [DEGRADED] (not Fail) on a missing shader or an invalid GameConfig.Outline.Color — this is a
        /// presentation-only enhancement (mirrors BuildHitVfxPrefab's own "must not block the rest of
        /// IntegrateAll" reasoning), so a failure here must not stop the rest of asset integration.</summary>
        private static Material BuildOutlineMaterial()
        {
            Shader outlineShader = Shader.Find(GameConfig.Outline.ShaderName);
            if (outlineShader == null)
            {
                Log("[DEGRADED] BuildOutlineMaterial: shader '" + GameConfig.Outline.ShaderName + "' not found — outline material not built (hero/swarmer will render without silhouette outlines).");
                return null;
            }
            if (!ColorUtility.TryParseHtmlString(GameConfig.Outline.Color, out Color outlineColor))
            {
                Log("[DEGRADED] BuildOutlineMaterial: GameConfig.Outline.Color ('" + GameConfig.Outline.Color + "') failed to parse as a hex color — outline material not built.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            bool materialIsNew = material == null;
            if (materialIsNew)
            {
                material = new Material(outlineShader) { name = "OutlineMaterial" };
            }
            else if (material.shader != outlineShader)
            {
                material.shader = outlineShader;
            }
            material.SetColor(OutlineColorProperty, outlineColor);
            material.SetFloat(OutlineWidthProperty, GameConfig.Outline.WidthMeters);

            Directory.CreateDirectory(Path.GetDirectoryName(OutlineMaterialPath)!);
            if (materialIsNew)
            {
                AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Log($"BuildOutlineMaterial: color={GameConfig.Outline.Color} width={GameConfig.Outline.WidthMeters}m -> {OutlineMaterialPath}");
            return material;
        }

        /// <summary>Appends <paramref name="outlineMaterial"/> as an extra sharedMaterials slot on every
        /// Renderer under <paramref name="root"/> (SkinnedMeshRenderer for hero, MeshRenderer for
        /// swarmer). When a Renderer's material count exceeds its mesh's submesh count, Unity draws the
        /// extra material(s) against submesh 0 again — this is the standard "extra material slot" inverse-
        /// hull outline technique, and it needs no duplicate GameObject/hierarchy (Cull Front in
        /// Outline.shader ensures only the outward-facing rim of that second pass is visible). No-op
        /// ([DEGRADED], not Fail — same presentation-only reasoning as BuildOutlineMaterial) when
        /// <paramref name="outlineMaterial"/> is null (shader/color resolution already failed and logged
        /// its own [DEGRADED] line) or <paramref name="root"/> carries no Renderer at all.</summary>
        private static void ApplyOutlineToRenderers(GameObject root, Material outlineMaterial)
        {
            if (outlineMaterial == null)
            {
                Log("[DEGRADED] ApplyOutlineToRenderers('" + root.name + "'): outline material unavailable — silhouette outline skipped.");
                return;
            }
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Log("[DEGRADED] ApplyOutlineToRenderers('" + root.name + "'): no Renderer found — silhouette outline skipped.");
                return;
            }
            int patched = 0;
            foreach (Renderer renderer in renderers)
            {
                Material[] existing = renderer.sharedMaterials;
                renderer.sharedMaterials = existing.Concat(new[] { outlineMaterial }).ToArray();
                patched++;
            }
            Log($"ApplyOutlineToRenderers('{root.name}'): outline material appended to {patched} renderer(s).");
        }

        // ------------------------------------------------------------------
        // Prefabs
        // ------------------------------------------------------------------

        private static Vector3 BuildHeroPrefab(RuntimeAnimatorController controller, bool heroAvatarValid, Material outlineMaterial)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(GameConfig.AssetKeys.HeroModel);
            if (fbx == null)
            {
                Fail("Hero FBX asset failed to load for prefab build: " + GameConfig.AssetKeys.HeroModel);
                return Vector3.zero;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);

            Animator animator = instance.GetComponent<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }
            if (controller != null)
            {
                animator.runtimeAnimatorController = controller;
            }
            animator.applyRootMotion = false;

            // S-28: append the outline material as an extra sharedMaterials slot on every hero Renderer
            // (SkinnedMeshRenderer(s) under this FBX instance) — baked once into Hero.prefab so every
            // instantiation (Components/PlayerController's HeroVisual child — Editor/AssetIntegration.
            // PatchPlayerVisual) carries it automatically.
            ApplyOutlineToRenderers(instance, outlineMaterial);

            Vector3 bounds = ComputeRendererBoundsSize(instance);

            Directory.CreateDirectory(Path.GetDirectoryName(GameConfig.AssetKeys.HeroPrefab)!);
            PrefabUtility.SaveAsPrefabAsset(instance, GameConfig.AssetKeys.HeroPrefab);
            UnityEngine.Object.DestroyImmediate(instance);
            return bounds;
        }

        // S-21: root object name for the swarmer prefab wrapper (renderer/motion separation — see below).
        private const string SwarmerRootName = "Swarmer";

        private static Vector3 BuildSwarmerPrefab(Material outlineMaterial)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(GameConfig.AssetKeys.SwarmerModel);
            if (fbx == null)
            {
                Fail("Swarmer FBX asset failed to load for prefab build: " + GameConfig.AssetKeys.SwarmerModel);
                return Vector3.zero;
            }

            // MDL-02 is unrigged (degraded_route in MANIFEST — quadruped auto-rig unavailable). No
            // Animator/AnimatorController for this prefab; approach_loop (ANM-04) remains must-replace and
            // is reported as a degradation, not silently invented as a placeholder clip. Instead (S-21),
            // the FBX visual is nested under a "Visual" child (GameConfig.Enemy.VisualChildName) of a
            // plain wrapper root that carries EnemyAgent: Systems/EnemyApproachSystem exclusively drives
            // the wrapper root's world position (Components/EnemyAgent.Update), while
            // Systems/EnemyVisualMotionSystem's coded forward-lean tilt + up/down bounce is applied only
            // to the "Visual" child's local transform — the two never fight over the same transform, and
            // a future rigged replacement (Tripo credit top-up) is a drop-in swap of the "Visual" child.
            // Deliberately NOT resetting visual.transform's local position/rotation/scale here (unlike
            // PatchPlayerVisual's HeroVisual, which does reset them): the swarmer FBX's own root node
            // carries a non-identity baked scale (Blender/Meshy export unit convention) that previously
            // produced the correct authoring-matched world bounds precisely because the un-parented
            // instance's local transform WAS its world transform. Parenting under `root` (itself created
            // at the identity transform) preserves that same local-equals-world relationship, so simply
            // leaving InstantiatePrefab's own default local values alone reproduces the prior (correct)
            // bounds exactly.
            var root = new GameObject(SwarmerRootName);
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            visual.name = GameConfig.Enemy.VisualChildName;

            if (root.GetComponent<EnemyAgent>() == null)
            {
                root.AddComponent<EnemyAgent>();
            }

            // S-28: append the outline material as an extra sharedMaterials slot on every swarmer
            // Renderer (under the "Visual" child) — baked once into Swarmer.prefab so both the normal
            // WaveSpawner.SpawnOne path and the heavy variant (Components/EnemyAgent.ApplyHeavyTint, which
            // only ever touches index 0 of the resolved Renderer's sharedMaterials via renderer.material)
            // carry it automatically without conflicting with the heavy-tint color swap.
            ApplyOutlineToRenderers(root, outlineMaterial);

            Vector3 bounds = ComputeRendererBoundsSize(root);

            Directory.CreateDirectory(Path.GetDirectoryName(GameConfig.AssetKeys.SwarmerPrefab)!);
            // CR-CODE S-21 iteration 1 minor finding: the previous call ignored SaveAsPrefabAsset's
            // success signal. A silent save failure (LogError + null return, not an exception) would let
            // IntegrateAll continue with either a stale Swarmer.prefab from a prior run or no prefab at
            // all — either way violating this file's own header rule 11 (never LogError+silently continue
            // on a broken save). Use the `out bool success` overload and Fail() (exit 1) on failure.
            PrefabUtility.SaveAsPrefabAsset(root, GameConfig.AssetKeys.SwarmerPrefab, out bool swarmerSaveSuccess);
            UnityEngine.Object.DestroyImmediate(root);
            if (!swarmerSaveSuccess)
            {
                Fail("Failed to save Swarmer prefab asset at " + GameConfig.AssetKeys.SwarmerPrefab);
                return Vector3.zero;
            }
            return bounds;
        }

        // S-17: 自動攻撃ヒットVFX (gdd 自動攻撃の当たり表現方式 — 「ヒット箇所に短命VFXを発生させ...当たり
        // 手応えを補う」). IMG-04 is a single radial-flash sprite, not a spritesheet/atlas, so this builds a
        // minimal one-shot ParticleSystem (single burst, billboard-rendered, stopAction=Destroy so the
        // spawned instance cleans itself up with no runtime lifetime-tracking code needed on the
        // Components/AutoAttackDriver caller side) rather than a Sprite Animator. [DEGRADED] (not Fail —
        // this is a presentation-only feature; a missing texture/shader must not block the rest of
        // IntegrateAll) when the texture or the URP particle shader can't be resolved.
        private const string HitVfxMaterialPath = "Assets/Generated/Materials/HitVfxMaterial.mat";

        // S-26: CR-CODE iteration 1 major指摘#1/#4 fix — mirrors HitVfxMaterialPath's idempotent
        // load-or-create-in-place pattern. Baking the Skybox material as a project asset (instead of
        // constructing it at runtime with Shader.Find, which the previous implementation did) is what
        // makes Unity's default build-time shader stripping include "Skybox/6 Sided" in the player build
        // (a builtin shader ships only when referenced by an asset included in the build).
        // CR-CODE s-26 iter2 minor指摘#3 fix: the path constant itself now lives in
        // GameConfig.AssetKeys.ArenaBackdropSkyboxMaterial (acceptance says "マテリアルキーは GameConfig の
        // AssetKeys 経由") — this file only keeps the shader name, which is not an asset-key path.
        private const string SkyboxShaderName = "Skybox/6 Sided";

        // Ship-review dedup: the load→DeleteAsset→Fail/[DEGRADED] control flow below was carried
        // verbatim by four stale-asset helpers (hit-VFX prefab/material, the S-29 generalized prefab
        // variant, the S-26 ArenaBackdrop skybox material). This generic core owns the flow once; each
        // named wrapper keeps supplying its own exact Fail/[DEGRADED] message strings (byte-identical to
        // the pre-dedup ones — they carry helper-specific consumer context reviewers signed off on).
        // CR-CODE S-17 iter2 major finding fix (applies to every wrapper): AssetDatabase.DeleteAsset
        // returns false (without throwing) on failure — file lock/permissions/VCS integration etc.
        // Blindly logging success there would let a degraded run's stale asset survive on disk for a
        // later wiring step to load anyway, reintroducing the exact "stale asset ships silently while
        // the report says [DEGRADED]" regression these helpers exist to close — just one step removed.
        // Escalate per rule 11 instead of continuing with an unresolved stale asset.
        private static void DeleteStaleAssetIfPresent<T>(string assetPath, string failMessage, string degradedMessage)
            where T : UnityEngine.Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(assetPath) == null)
            {
                return;
            }
            if (!AssetDatabase.DeleteAsset(assetPath))
            {
                Fail(failMessage);
                return;
            }
            Log(degradedMessage);
        }

        // CR-CODE S-17 iter1 minor finding fix: a degraded run (texture/shader unresolved, or the prefab
        // save itself failing) used to leave a *previous* run's HitVfx.prefab untouched on disk.
        // PatchAutoAttackDriverVfx would then happily wire AutoAttackDriver to that stale prefab and log a
        // success line ("-> HitVfx prefab.") in the very same report that also says [DEGRADED] "attacks
        // will land without a VFX" — contradictory, and the stale prefab could reference an
        // outdated/deleted texture or material (broken/pink rendering shipped silently). Delete it so a
        // degraded run is honestly VFX-less end to end, matching its own report line.
        private static void DeleteStaleHitVfxPrefabIfPresent(string reason)
        {
            DeleteStaleAssetIfPresent<GameObject>(
                GameConfig.AssetKeys.HitVfxPrefab,
                "BuildHitVfxPrefab: failed to delete stale HitVfx prefab at " + GameConfig.AssetKeys.HitVfxPrefab +
                    " (" + reason + ") — refusing to continue and risk wiring AutoAttackDriver to a stale asset.",
                "[DEGRADED] BuildHitVfxPrefab: deleted a stale HitVfx prefab left over from a previous " +
                    "run (" + reason + ") so PatchAutoAttackDriverVfx cannot wire AutoAttackDriver to a " +
                    "prefab referencing a now-missing/outdated asset.");
        }

        // CR-CODE S-29 iter2 minor #2 fix: mirrors DeleteStaleHitVfxPrefabIfPresent one asset layer down.
        // BuildHitVfxPrefab's texture/shader-missing branches return early *without* touching
        // HitVfxMaterial.mat at all, so a *previous* run's material (its _BaseMap pointing at a
        // now-missing/outdated texture) is left on disk. The three current consumers
        // (BuildHitVfxPrefab's own idempotent update path, BuildCrystalGlowVfxPrefab,
        // BuildCrystalCollectVfxPrefab) are already gated on hitVfxHealthyThisRun (CR-CODE S-29 iter1 minor
        // #1) and so never load this stale asset — but that gate lives at each *consumer*, not at the
        // source. Deleting the stale material here closes the hole at the source: any future consumer that
        // LoadAssetAtPath's HitVfxMaterialPath directly (instead of checking hitVfxHealthyThisRun) gets
        // null and must handle the missing-material case explicitly, instead of silently inheriting a
        // dangling texture reference.
        private static void DeleteStaleHitVfxMaterialIfPresent(string reason)
        {
            DeleteStaleAssetIfPresent<Material>(
                HitVfxMaterialPath,
                "BuildHitVfxPrefab: failed to delete stale HitVfxMaterial at " + HitVfxMaterialPath +
                    " (" + reason + ") — refusing to continue and risk a future consumer loading a material with a dangling texture reference.",
                "[DEGRADED] BuildHitVfxPrefab: deleted a stale HitVfxMaterial left over from a previous " +
                    "run (" + reason + ") so no consumer can load a material referencing a now-missing/outdated texture.");
        }

        private static GameObject BuildHitVfxPrefab()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GameConfig.AssetKeys.HitVfxSprite);
            if (texture == null)
            {
                Log("[DEGRADED] BuildHitVfxPrefab: hit VFX texture not found at " + GameConfig.AssetKeys.HitVfxSprite + " — auto-attack hits will land without a VFX.");
                DeleteStaleHitVfxMaterialIfPresent("hit VFX texture missing this run");
                DeleteStaleHitVfxPrefabIfPresent("hit VFX texture missing this run");
                return null;
            }
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null)
            {
                Log("[DEGRADED] BuildHitVfxPrefab: 'Universal Render Pipeline/Particles/Unlit' shader not found — auto-attack hits will land without a VFX.");
                DeleteStaleHitVfxMaterialIfPresent("URP particle shader unresolved this run");
                DeleteStaleHitVfxPrefabIfPresent("URP particle shader unresolved this run");
                return null;
            }

            // Idempotent re-run (mirrors SceneWiring's pattern): update the existing material asset in
            // place instead of Delete+CreateAsset. The old always-delete-then-recreate approach changed
            // the material's GUID on every single re-run even when nothing about it actually changed,
            // forcing prefab-reference + .meta churn on every AssetIntegration run (CR-CODE S-17 iter1
            // minor finding).
            Material material = AssetDatabase.LoadAssetAtPath<Material>(HitVfxMaterialPath);
            bool materialIsNew = material == null;
            if (materialIsNew)
            {
                material = new Material(particleShader) { name = "HitVfxMaterial" };
            }
            else
            {
                material.shader = particleShader;
            }
            material.SetTexture("_BaseMap", texture);
            // Additive-style transparent blend (mirrors FixMaterialsForUrp's own HasProperty-guarded
            // _Surface pattern above) so overlapping flashes brighten rather than muddy each other —
            // IMG-04's own soft alpha falloff (see MANIFEST alpha_verification_note) supplies the shape.
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f); // Transparent
            }
            if (material.HasProperty("_Blend"))
            {
                material.SetFloat("_Blend", 2f); // URP Particles/Unlit BlendMode enum: 2 = Additive
            }

            Directory.CreateDirectory(Path.GetDirectoryName(HitVfxMaterialPath)!);
            if (materialIsNew)
            {
                AssetDatabase.CreateAsset(material, HitVfxMaterialPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }

            var root = new GameObject("HitVfx");
            var ps = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = GameConfig.Fx.HitVfxDuration;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = GameConfig.Fx.HitVfxParticleLifetime;
            main.startSpeed = 0f; // stationary flash at the hit position, not a projectile
            main.startSize = GameConfig.Fx.HitVfxStartSize;
            main.startColor = Color.white;
            main.stopAction = ParticleSystemStopAction.Destroy; // self-cleans; no runtime timer needed

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)GameConfig.Fx.HitVfxBurstCount) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false; // point emitter at the instantiated transform (the hit position)

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;

            // Presentation-only feature (see class comment above) — save failure is [DEGRADED], not
            // Fail() (TrySavePrefab); the stale delete stays routed through DeleteStaleHitVfxPrefabIfPresent
            // so its PatchAutoAttackDriverVfx-specific messages are preserved.
            return TrySavePrefab(
                root, GameConfig.AssetKeys.HitVfxPrefab, "BuildHitVfxPrefab",
                "[DEGRADED] BuildHitVfxPrefab: failed to save HitVfx prefab asset at " + GameConfig.AssetKeys.HitVfxPrefab + " — auto-attack hits will land without a VFX.",
                DeleteStaleHitVfxPrefabIfPresent);
        }

        // S-29: generalized version of DeleteStaleHitVfxPrefabIfPresent above for the two new S-29
        // prefabs — same rationale (a degraded run must not leave a stale prefab referencing a
        // now-missing/outdated asset for PatchCrystalVfxLibrary to wire up next). Ship-review dedup:
        // control flow now lives in DeleteStaleAssetIfPresent (see its comment).
        private static void DeleteStalePrefabIfPresent(string prefabPath, string reason, string logLabel)
        {
            DeleteStaleAssetIfPresent<GameObject>(
                prefabPath,
                logLabel + ": failed to delete stale prefab at " + prefabPath + " (" + reason +
                    ") — refusing to continue and risk wiring to a stale asset.",
                "[DEGRADED] " + logLabel + ": deleted a stale prefab left over from a previous run (" +
                    reason + ") so it cannot be wired to a prefab referencing a now-missing/outdated asset.");
        }

        // Ship-review dedup: the three prefab-build methods (BuildHitVfxPrefab / BuildCrystalGlowVfxPrefab /
        // BuildCrystalCollectVfxPrefab) carried this exact save trailer verbatim: ensure the directory,
        // SaveAsPrefabAsset, always DestroyImmediate the scene-side root, and on failure log the caller's
        // own [DEGRADED] line (presentation-only features — [DEGRADED], not Fail(), per BuildHitVfxPrefab's
        // class-level rationale) then delete any stale prefab a previous run left at the same path.
        // <paramref name="deleteStaleOnFailure"/> lets BuildHitVfxPrefab keep routing that delete through
        // DeleteStaleHitVfxPrefabIfPresent (its messages name PatchAutoAttackDriverVfx specifically);
        // null uses the generalized DeleteStalePrefabIfPresent with <paramref name="label"/>.
        private static GameObject TrySavePrefab(
            GameObject root, string prefabPath, string label, string degradedMessage,
            Action<string> deleteStaleOnFailure = null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(prefabPath)!);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saveSuccess);
            UnityEngine.Object.DestroyImmediate(root);
            if (!saveSuccess)
            {
                Log(degradedMessage);
                if (deleteStaleOnFailure != null)
                {
                    deleteStaleOnFailure("prefab save failed this run");
                }
                else
                {
                    DeleteStalePrefabIfPresent(prefabPath, "prefab save failed this run", label);
                }
                return null;
            }
            return prefab;
        }

        /// <summary>S-29: ambient "glow halo" particle system parented onto each spawned crystal
        /// (Components/CrystalPickup.SpawnDrop via Components/CrystalVfxLibrary.SpawnGlow — crystals are
        /// created at runtime via GameObject.CreatePrimitive, not from a prefab in the scene, so there is
        /// no scene/prefab instance for this method to SerializedObject-patch directly; PatchCrystalVfxLibrary
        /// below wires this prefab onto a scene-resident CrystalVfxLibrary singleton instead, mirroring how
        /// Components/SfxLibrary exposes clips to the same kind of dynamically-spawned caller). Reuses the
        /// exact HitVfxMaterial asset BuildHitVfxPrefab already built above (same IMG-04 texture —
        /// acceptance: "IMG-04相当のパーティクルテクスチャ", no new image asset) so this method does not
        /// re-resolve the texture/shader itself.
        /// <paramref name="hitVfxHealthyThisRun"/> (IntegrateAll's own <c>hitVfxPrefab != null</c>, the
        /// authoritative "IMG-04 resolved successfully this run" signal) gates the material lookup below —
        /// CR-CODE S-29 iter1 minor #1: relying on HitVfxMaterial.mat's mere presence on disk was not
        /// sufficient, because BuildHitVfxPrefab's own texture/shader-missing branches return early
        /// *without* touching the material asset at all, leaving a *previous* run's material (with a
        /// _BaseMap pointing at a now-missing/outdated texture) on disk. Without this gate this method would
        /// load that stale material, report crystal_glow_vfx_built=True, and silently ship a broken-texture
        /// particle in the very same report that says hit_vfx_built=False for the identical underlying
        /// cause.</summary>
        private static GameObject BuildCrystalGlowVfxPrefab(bool hitVfxHealthyThisRun)
        {
            Material material = hitVfxHealthyThisRun ? AssetDatabase.LoadAssetAtPath<Material>(HitVfxMaterialPath) : null;
            if (material == null)
            {
                Log("[DEGRADED] BuildCrystalGlowVfxPrefab: shared HitVfxMaterial (IMG-04) not available/healthy this run — crystals will glow without the ambient particle halo.");
                DeleteStalePrefabIfPresent(GameConfig.AssetKeys.CrystalGlowVfxPrefab, "shared HitVfxMaterial missing or degraded this run", "BuildCrystalGlowVfxPrefab");
                return null;
            }

            var root = new GameObject("CrystalGlowVfx");
            var ps = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.loop = true; // クリスタルが存在する間ずっと緩やかに発光し続けるハロー(HitVfxの単発flashとは対照的)
            main.playOnAwake = true;
            main.startLifetime = GameConfig.Crystal.GlowParticleLifetime;
            main.startSpeed = GameConfig.Crystal.GlowParticleDriftSpeed;
            main.startSize = GameConfig.Crystal.GlowParticleStartSize;
            main.startColor = Color.white; // IMG-04自体が中心白〜シアン寄りの発光テクスチャ(design/assets.md) — 追加着色は不要
            main.maxParticles = GameConfig.Crystal.GlowParticleMaxParticles;
            // World simulation space: emitted particles drift freely instead of rigidly following the
            // parent crystal's coded self-rotation/bob (Components/CrystalPickup.ApplyCodedMotion) —
            // "緩やかに漂う" reads as independently-drifting particles, not particles glued to the spinning
            // placeholder cube.
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = GameConfig.Crystal.GlowParticleEmissionRate;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = GameConfig.Crystal.GlowParticleSpawnRadius;

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;

            return TrySavePrefab(
                root, GameConfig.AssetKeys.CrystalGlowVfxPrefab, "BuildCrystalGlowVfxPrefab",
                "[DEGRADED] BuildCrystalGlowVfxPrefab: failed to save CrystalGlowVfx prefab asset at " + GameConfig.AssetKeys.CrystalGlowVfxPrefab + " — crystals will glow without the ambient particle halo.");
        }

        /// <summary>S-29: short-lived "collect flash" particle burst (Components/CrystalPickup.TryPickup
        /// via Components/CrystalVfxLibrary.SpawnCollect — same call-site pattern AutoAttackDriver uses for
        /// the S-17 hit VFX: instantiated standalone at the pickup position, not parented, self-cleans via
        /// stopAction=Destroy). Reuses the same shared HitVfxMaterial asset as BuildCrystalGlowVfxPrefab
        /// above (see that method's comment for the "no new texture" rationale and for why
        /// <paramref name="hitVfxHealthyThisRun"/> gates the material lookup — CR-CODE S-29 iter1 minor
        /// #1).</summary>
        private static GameObject BuildCrystalCollectVfxPrefab(bool hitVfxHealthyThisRun)
        {
            Material material = hitVfxHealthyThisRun ? AssetDatabase.LoadAssetAtPath<Material>(HitVfxMaterialPath) : null;
            if (material == null)
            {
                Log("[DEGRADED] BuildCrystalCollectVfxPrefab: shared HitVfxMaterial (IMG-04) not available/healthy this run — crystal pickups will land without the collect flash.");
                DeleteStalePrefabIfPresent(GameConfig.AssetKeys.CrystalCollectVfxPrefab, "shared HitVfxMaterial missing or degraded this run", "BuildCrystalCollectVfxPrefab");
                return null;
            }

            var root = new GameObject("CrystalCollectVfx");
            var ps = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.duration = GameConfig.Crystal.CollectVfxDuration;
            main.loop = false;
            main.playOnAwake = true;
            main.startLifetime = GameConfig.Crystal.CollectVfxParticleLifetime;
            main.startSpeed = GameConfig.Crystal.CollectVfxStartSpeed; // 回収の瞬間に外側へ弾ける
            main.startSize = GameConfig.Crystal.CollectVfxStartSize;
            main.startColor = Color.white;
            main.stopAction = ParticleSystemStopAction.Destroy; // self-cleans; no runtime timer needed (mirrors HitVfx)

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)GameConfig.Crystal.CollectVfxBurstCount) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0f; // 構造的な値(HitVfxのshape.enabled=falseと同じ趣旨) — 点状の中心からstartSpeed方向へ放射状に弾けさせるためのゼロ半径球

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = material;

            return TrySavePrefab(
                root, GameConfig.AssetKeys.CrystalCollectVfxPrefab, "BuildCrystalCollectVfxPrefab",
                "[DEGRADED] BuildCrystalCollectVfxPrefab: failed to save CrystalCollectVfx prefab asset at " + GameConfig.AssetKeys.CrystalCollectVfxPrefab + " — crystal pickups will land without the collect flash.");
        }

        /// <summary>Instantiates <paramref name="modelPath"/> transiently to measure its combined
        /// renderer bounds height (Y), then destroys the instance. Used for the pre/post scale-correction
        /// check in ConfigureHeroModel — a dedicated lightweight measurement, not the final prefab build
        /// (BuildHeroPrefab does that once scale/animator/etc. are all finalized).</summary>
        private static float MeasureHeight(string modelPath)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (fbx == null)
            {
                return 0f;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            float height = ComputeRendererBoundsSize(instance).y;
            UnityEngine.Object.DestroyImmediate(instance);
            return height;
        }

        private static Vector3 ComputeRendererBoundsSize(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return Vector3.zero;
            }
            Bounds bounds = renderers[0].bounds;
            Log($"  renderer[0]='{renderers[0].name}' type={renderers[0].GetType().Name} bounds.size={renderers[0].bounds.size:F6}");
            for (int i = 1; i < renderers.Length; i++)
            {
                Log($"  renderer[{i}]='{renderers[i].name}' type={renderers[i].GetType().Name} bounds.size={renderers[i].bounds.size}");
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds.size;
        }

        // ------------------------------------------------------------------
        // Game scene wiring
        // ------------------------------------------------------------------

        private static void PatchGameScene()
        {
            // Ensure the baseline story wiring (Player/PlayerController/.../WaveSpawner/RunStatsTracker/
            // GameHud) exists first — this method only replaces placeholder visuals/prefab slots on top
            // of it. Idempotent (SceneWiring.WireGame re-run is a documented no-op on top of itself).
            SceneWiring.WireGame();

            Scene scene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            PatchPlayerVisual();
            PatchWaveSpawnerPrefab();
            PatchSfxLibrary();
            PatchAutoAttackDriverVfx();
            PatchCrystalVfxLibrary();
            PatchArenaBackdrop();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail("Failed to save Game scene after AssetIntegration patch.");
            }
        }

        // S-19 音声統合: BGM-01 must loop for the whole session across every scene (Boot→Title→Menu→
        // Game→Result), so its player lives in Boot.unity as a DontDestroyOnLoad singleton
        // (Components/BgmPlayer) rather than being re-created per scene like SfxLibrary. Mirrors
        // PatchGameScene's shape exactly (open scene -> patch -> save), just without a SceneWiring
        // baseline call — Boot.unity's only other object (BootLoader) is created by ForgeScaffold, not
        // SceneWiring.
        private static void PatchBootScene()
        {
            Scene scene = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            PatchBgmPlayer();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail("Failed to save Boot scene after AssetIntegration patch.");
            }
        }

        private static void PatchBgmPlayer()
        {
            GameObject go = GameObject.Find(BgmPlayerRootName);
            if (go == null)
            {
                go = new GameObject(BgmPlayerRootName);
            }
            if (go.GetComponent<AudioSource>() == null)
            {
                go.AddComponent<AudioSource>();
            }
            BgmPlayer player = go.GetComponent<BgmPlayer>();
            if (player == null)
            {
                player = go.AddComponent<BgmPlayer>();
            }

            AudioMixer mixer = AudioMixerSetup.EnsureMixer(
                GameConfig.Audio.MixerAssetPath, GameConfig.Audio.MixerBgmGroupName, GameConfig.Audio.MixerSfxGroupName,
                GameConfig.Audio.MixerBgmVolumeParam, GameConfig.Audio.MixerSfxVolumeParam);
            bool mixerWired;
            if (mixer != null)
            {
                var mixerSo = new SerializedObject(player);
                SerializedProperty mixerProp = mixerSo.FindProperty("_mixer");
                if (mixerProp == null)
                {
                    Fail("PatchBgmPlayer: BgmPlayer._mixer serialized field not found (rename?).");
                    return;
                }
                mixerProp.objectReferenceValue = mixer;
                mixerSo.ApplyModifiedPropertiesWithoutUndo();
                mixerWired = true;
            }
            else
            {
                Log("[DEGRADED] PatchBgmPlayer: AudioMixer creation/load failed (see AudioMixerSetup log above) — BGM will play at unity gain, ignoring the bgmVolume bus this session.");
                mixerWired = false;
            }

            AssignClipResult clipResult = AssignClip(player, "_bgmClip", GameConfig.AssetKeys.BgmMainLoop);
            _bgmClipResult = clipResult;

            // CR-CODE s-19 iteration 1 minor finding: this used to print an unconditional success line
            // even when the branches above already logged [DEGRADED] (mixer creation failed, or the clip
            // couldn't be loaded) — a reader skimming only the "-> wired." lines of the integration report
            // would miss that BGM is actually degraded this run. Fold both outcomes into the summary.
            // CR-CODE s-19 iteration 3 minor finding: also distinguish "clip actually assigned" from
            // "clip assignment skipped because AssetKeys.BgmMainLoop was empty by design" — collapsing
            // both into the same success line would hide a future regression of that constant back to "".
            if (mixerWired && clipResult == AssignClipResult.Assigned)
            {
                Log("PatchBgmPlayer: BgmPlayer present with BGM-01 clip + mixer wired.");
            }
            else if (mixerWired && clipResult == AssignClipResult.SkippedPlanned)
            {
                // CR-CODE s-19 iteration 2 minor finding: "planned" was an unverified self-assertion that
                // this build would eventually go stale (it did — BgmMainLoop is non-empty as of this
                // iteration). Phrase this as an observation of the current AssetKeys value instead of a
                // claim about design/assets.md's intent, so the log can't quietly become false.
                Log("PatchBgmPlayer: BgmPlayer present with mixer wired; BGM-01 clip assignment skipped — AssetKeys.BgmMainLoop is empty (verify design/assets.md status).");
            }
            else
            {
                Log($"[DEGRADED] PatchBgmPlayer: completed with degraded wiring this run (mixerWired={mixerWired}, clipResult={clipResult}) — see the [DEGRADED] line(s) above for details.");
            }
        }

        // S-19 音声統合: SFX-06 (アップグレード購入確定) fires from the Menu scene (Components/
        // MenuController.HandlePurchase — S-12), so its clip/AudioSource live on the same MenuController
        // GameObject the Menu 設定タブ's AudioMixer reference (_mixer) is already baked onto (S-13). Mirrors
        // PatchGameScene's "ensure SceneWiring baseline -> open scene -> patch -> save" shape.
        private static void PatchMenuScene()
        {
            SceneWiring.WireMenu();

            Scene scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

            PatchMenuPurchaseSfx();

            if (!EditorSceneManager.SaveScene(scene))
            {
                Fail("Failed to save Menu scene after AssetIntegration patch.");
            }
        }

        private static void PatchMenuPurchaseSfx()
        {
            GameObject root = GameObject.Find(MenuRootName);
            if (root == null)
            {
                Fail("PatchMenuPurchaseSfx: '" + MenuRootName + "' GameObject missing after SceneWiring.WireMenu.");
                return;
            }
            if (root.GetComponent<AudioSource>() == null)
            {
                root.AddComponent<AudioSource>();
            }
            MenuController controller = root.GetComponent<MenuController>();
            if (controller == null)
            {
                Fail("PatchMenuPurchaseSfx: MenuController component missing on '" + MenuRootName + "'.");
                return;
            }

            AssignClipResult clipResult = AssignClip(controller, "_upgradePurchaseSfx", GameConfig.AssetKeys.SfxUpgradePurchase);
            _sfxUpgradePurchaseResult = clipResult;

            // CR-CODE s-19 iteration 1 minor finding: mirrors PatchBgmPlayer's conditional summary — an
            // AssignClip [DEGRADED] line above must not be followed by an unconditional "assigned." line.
            // CR-CODE s-19 iteration 3 minor finding: also distinguish "assigned" from "skipped because
            // AssetKeys.SfxUpgradePurchase is empty by design" so a future regression of that constant
            // back to "" cannot masquerade as full success in this summary.
            if (clipResult == AssignClipResult.Assigned)
            {
                Log("PatchMenuPurchaseSfx: MenuController present with SFX-06 (upgrade purchase) clip + AudioSource assigned.");
            }
            else if (clipResult == AssignClipResult.SkippedPlanned)
            {
                // CR-CODE s-19 iteration 2 minor finding: same rephrase as PatchBgmPlayer above — an
                // observation of the AssetKeys value, not an assertion about design/assets.md's intent.
                Log("PatchMenuPurchaseSfx: MenuController present with AudioSource ensured; SFX-06 (upgrade purchase) clip assignment skipped — AssetKeys.SfxUpgradePurchase is empty (verify design/assets.md status).");
            }
            else
            {
                Log("[DEGRADED] PatchMenuPurchaseSfx: SFX-06 (upgrade purchase) clip was not assigned this run — see the [DEGRADED] line above for details.");
            }
        }

        private static void PatchPlayerVisual()
        {
            GameObject player = GameObject.Find(PlayerRootName);
            if (player == null)
            {
                Fail("PatchPlayerVisual: 'Player' GameObject missing after SceneWiring.WireGame.");
                return;
            }

            if (player.transform.Find(HeroVisualName) != null)
            {
                Log("PatchPlayerVisual: HeroVisual already present — skipping (idempotent re-run).");
                return;
            }

            GameObject heroPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameConfig.AssetKeys.HeroPrefab);
            if (heroPrefab == null)
            {
                Log("[DEGRADED] PatchPlayerVisual: Hero prefab not found — leaving primitive-capsule placeholder in place.");
                return;
            }

            // Remove the primitive capsule's own visual/collider components (CreatePrimitive adds
            // MeshFilter/MeshRenderer/CapsuleCollider) now that a real visual child is being attached.
            // PlayerController/AutoAttackDriver/HealthComponent stay on the root untouched — none of
            // them depend on Unity physics colliders (all contact/pickup checks are pure XZ-distance
            // math in Systems/, not Collider-based).
            SafeDestroy(player.GetComponent<CapsuleCollider>());
            SafeDestroy(player.GetComponent<MeshRenderer>());
            SafeDestroy(player.GetComponent<MeshFilter>());

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(heroPrefab, player.transform);
            visual.name = HeroVisualName;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            Log("PatchPlayerVisual: HeroVisual attached under Player.");
        }

        private static void PatchWaveSpawnerPrefab()
        {
            GameObject waveSpawnerGo = GameObject.Find(WaveSpawnerRootName);
            if (waveSpawnerGo == null)
            {
                Fail("PatchWaveSpawnerPrefab: 'WaveSpawner' GameObject missing after SceneWiring.WireGame.");
                return;
            }
            WaveSpawner waveSpawner = waveSpawnerGo.GetComponent<WaveSpawner>();
            if (waveSpawner == null)
            {
                Fail("PatchWaveSpawnerPrefab: WaveSpawner component missing on 'WaveSpawner' GameObject.");
                return;
            }

            GameObject swarmerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameConfig.AssetKeys.SwarmerPrefab);
            if (swarmerPrefab == null)
            {
                Log("[DEGRADED] PatchWaveSpawnerPrefab: Swarmer prefab not found — WaveSpawner keeps the primitive-cube placeholder fallback.");
                return;
            }

            var so = new SerializedObject(waveSpawner);
            SerializedProperty prop = so.FindProperty("enemyPrefab");
            if (prop == null)
            {
                Fail("PatchWaveSpawnerPrefab: WaveSpawner.enemyPrefab serialized field not found (rename?).");
                return;
            }
            prop.objectReferenceValue = swarmerPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            Log("PatchWaveSpawnerPrefab: WaveSpawner.enemyPrefab -> Swarmer prefab.");
        }

        private static void PatchSfxLibrary()
        {
            GameObject go = GameObject.Find(SfxLibraryRootName);
            if (go == null)
            {
                go = new GameObject(SfxLibraryRootName);
            }
            if (go.GetComponent<AudioSource>() == null)
            {
                go.AddComponent<AudioSource>();
            }
            SfxLibrary lib = go.GetComponent<SfxLibrary>();
            if (lib == null)
            {
                lib = go.AddComponent<SfxLibrary>();
            }

            AssignClipResult attackHitResult = AssignClip(lib, "_attackHit", GameConfig.AssetKeys.SfxAttackHit);
            AssignClipResult dashResult = AssignClip(lib, "_dash", GameConfig.AssetKeys.SfxDash);
            AssignClipResult playerHitResult = AssignClip(lib, "_playerHit", GameConfig.AssetKeys.SfxPlayerHit);
            AssignClipResult crystalPickupResult = AssignClip(lib, "_crystalPickup", GameConfig.AssetKeys.SfxCrystalPickup);
            AssignClipResult waveStartResult = AssignClip(lib, "_waveStart", GameConfig.AssetKeys.SfxWaveStart);
            _sfxAttackHitResult = attackHitResult;
            _sfxDashResult = dashResult;
            _sfxPlayerHitResult = playerHitResult;
            _sfxCrystalPickupResult = crystalPickupResult;
            _sfxWaveStartResult = waveStartResult;
            // CR-CODE s-19 iteration 2 minor finding: renamed from allClipsAssigned — the actual condition
            // is "none of the 5 slots outright Failed" (SkippedPlanned slots still make this true), so the
            // old name overstated what was verified. See anySkippedPlanned below for the distinct
            // "assigned vs. intentionally skipped" signal this alone can't carry.
            bool noneFailed = attackHitResult != AssignClipResult.Failed && dashResult != AssignClipResult.Failed
                && playerHitResult != AssignClipResult.Failed && crystalPickupResult != AssignClipResult.Failed
                && waveStartResult != AssignClipResult.Failed;
            // CR-CODE s-19 iteration 3 minor finding: "all clips non-degraded" alone can't tell a full
            // success apart from one where an AssetKeys constant regressed to "" and got silently
            // skipped-as-planned — surface that distinctly in the summary below.
            bool anySkippedPlanned = attackHitResult == AssignClipResult.SkippedPlanned
                || dashResult == AssignClipResult.SkippedPlanned
                || playerHitResult == AssignClipResult.SkippedPlanned
                || crystalPickupResult == AssignClipResult.SkippedPlanned
                || waveStartResult == AssignClipResult.SkippedPlanned;

            // S-19: route the shared AudioSource to the mixer's Sfx group (mirrors PatchBgmPlayer's
            // identical EnsureMixer + SerializedObject-assign pattern, targeting SfxLibrary._mixer instead
            // of BgmPlayer._mixer / MenuController._mixer — three distinct fields on the same shared asset).
            AudioMixer mixer = AudioMixerSetup.EnsureMixer(
                GameConfig.Audio.MixerAssetPath, GameConfig.Audio.MixerBgmGroupName, GameConfig.Audio.MixerSfxGroupName,
                GameConfig.Audio.MixerBgmVolumeParam, GameConfig.Audio.MixerSfxVolumeParam);
            bool mixerWired;
            if (mixer != null)
            {
                var mixerSo = new SerializedObject(lib);
                SerializedProperty mixerProp = mixerSo.FindProperty("_mixer");
                if (mixerProp == null)
                {
                    Fail("PatchSfxLibrary: SfxLibrary._mixer serialized field not found (rename?).");
                    return;
                }
                mixerProp.objectReferenceValue = mixer;
                mixerSo.ApplyModifiedPropertiesWithoutUndo();
                mixerWired = true;
            }
            else
            {
                Log("[DEGRADED] PatchSfxLibrary: AudioMixer creation/load failed (see AudioMixerSetup log above) — SFX will play at unity gain, ignoring the sfxVolume bus this session.");
                mixerWired = false;
            }

            // CR-CODE s-19 iteration 1 minor finding: mirrors PatchBgmPlayer/PatchMenuPurchaseSfx's
            // conditional summary — any AssignClip [DEGRADED] line (or the mixer-degraded branch above)
            // must not be followed by an unconditional "clip slots + mixer wired." success line.
            if (noneFailed && mixerWired && !anySkippedPlanned)
            {
                Log("PatchSfxLibrary: SfxLibrary present with SFX-01..05 clip slots + mixer wired.");
            }
            else if (noneFailed && mixerWired && anySkippedPlanned)
            {
                // CR-CODE s-19 iteration 3 minor finding: planned-skip is not a degradation, but it must
                // still be visible (not folded into the same line as a full clip-slot success).
                // CR-CODE s-19 iteration 2 minor finding: rephrased from "(planned — AssetKeys empty by
                // design)" — that wording asserted an unverified intent; state the observed AssetKeys value
                // instead and point at design/assets.md for the authoritative status.
                Log($"PatchSfxLibrary: SfxLibrary present with mixer wired; some SFX-01..05 clip slots skipped — AssetKeys empty (verify design/assets.md status): attackHit={attackHitResult}, dash={dashResult}, playerHit={playerHitResult}, crystalPickup={crystalPickupResult}, waveStart={waveStartResult}.");
            }
            else
            {
                Log($"[DEGRADED] PatchSfxLibrary: completed with degraded wiring this run (attackHit={attackHitResult}, dash={dashResult}, playerHit={playerHitResult}, crystalPickup={crystalPickupResult}, waveStart={waveStartResult}, mixerWired={mixerWired}) — see the [DEGRADED] line(s) above for details.");
            }
        }

        // S-17: mirrors PatchWaveSpawnerPrefab's SerializedObject-field-assignment pattern exactly, just
        // targeting AutoAttackDriver._hitVfxPrefab instead of WaveSpawner.enemyPrefab.
        private static void PatchAutoAttackDriverVfx()
        {
            GameObject player = GameObject.Find(PlayerRootName);
            if (player == null)
            {
                Fail("PatchAutoAttackDriverVfx: 'Player' GameObject missing after SceneWiring.WireGame.");
                return;
            }
            var driver = player.GetComponent<AutoAttackDriver>();
            if (driver == null)
            {
                Fail("PatchAutoAttackDriverVfx: AutoAttackDriver component missing on 'Player' GameObject.");
                return;
            }

            var so = new SerializedObject(driver);
            SerializedProperty prop = so.FindProperty("_hitVfxPrefab");
            if (prop == null)
            {
                Fail("PatchAutoAttackDriverVfx: AutoAttackDriver._hitVfxPrefab serialized field not found (rename?).");
                return;
            }

            GameObject hitVfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameConfig.AssetKeys.HitVfxPrefab);
            if (hitVfxPrefab == null)
            {
                // CR-CODE S-17 iter2 minor finding fix: explicitly clear (idempotent — no-op when already
                // null) the serialized reference instead of leaving whatever a *previous* successful run
                // wired here. Without this, a degraded re-run after a prior successful run would save the
                // Game scene with a dangling/missing-GUID prefab reference on AutoAttackDriver instead of an
                // honest null — contradicting this run's own [DEGRADED] "attacks will land without a VFX"
                // report line (runtime is unaffected either way — Components/AutoAttackDriver's Instantiate
                // call already guards with a Unity `== null` check — but the saved scene asset should be
                // honest about what this run actually resolved).
                if (prop.objectReferenceValue != null)
                {
                    prop.objectReferenceValue = null;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                Log("[DEGRADED] PatchAutoAttackDriverVfx: HitVfx prefab not found — attacks will land without a VFX.");
                return;
            }

            prop.objectReferenceValue = hitVfxPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();
            Log("PatchAutoAttackDriverVfx: AutoAttackDriver._hitVfxPrefab -> HitVfx prefab.");
        }

        /// <summary>S-29: wires GameConfig.AssetKeys.CrystalGlowVfxPrefab/CrystalCollectVfxPrefab onto a
        /// scene-resident CrystalVfxLibrary singleton (mirrors PatchSfxLibrary's find-or-create root /
        /// find-or-add component / SerializedObject-assign shape) so Components/CrystalPickup's
        /// dynamically spawned instances (created via GameObject.CreatePrimitive, not from a prefab
        /// AssetIntegration could SerializedObject-patch directly like PatchAutoAttackDriverVfx does for
        /// AutoAttackDriver) can reach the built prefabs via CrystalVfxLibrary.Instance at runtime.</summary>
        private static void PatchCrystalVfxLibrary()
        {
            GameObject go = GameObject.Find(CrystalVfxLibraryRootName);
            if (go == null)
            {
                go = new GameObject(CrystalVfxLibraryRootName);
            }
            CrystalVfxLibrary lib = go.GetComponent<CrystalVfxLibrary>();
            if (lib == null)
            {
                lib = go.AddComponent<CrystalVfxLibrary>();
            }

            var so = new SerializedObject(lib);
            SerializedProperty glowProp = so.FindProperty("_glowVfxPrefab");
            SerializedProperty collectProp = so.FindProperty("_collectVfxPrefab");
            if (glowProp == null || collectProp == null)
            {
                Fail("PatchCrystalVfxLibrary: CrystalVfxLibrary serialized field(s) not found (rename?).");
                return;
            }

            GameObject glowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameConfig.AssetKeys.CrystalGlowVfxPrefab);
            GameObject collectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameConfig.AssetKeys.CrystalCollectVfxPrefab);
            glowProp.objectReferenceValue = glowPrefab;
            collectProp.objectReferenceValue = collectPrefab;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (glowPrefab != null && collectPrefab != null)
            {
                Log("PatchCrystalVfxLibrary: CrystalVfxLibrary present with glow+collect VFX prefabs wired.");
            }
            else
            {
                Log($"[DEGRADED] PatchCrystalVfxLibrary: completed with degraded wiring this run (glow={glowPrefab != null}, collect={collectPrefab != null}) — see the [DEGRADED] BuildCrystal*VfxPrefab line(s) above for details.");
            }
        }

        // CR-CODE s-19 iteration 3 minor finding: a bool return could not distinguish "assetPath was
        // empty by design (planned, not yet generated)" from "clip was actually assigned" — a caller
        // summary line built only from the bool could describe an empty-by-design AssetKeys constant as
        // full success, and (more importantly) could no longer tell the two apart if a previously-filled
        // AssetKeys constant ever regressed back to "" — the exact silent-success shape the blocker finding
        // in this same iteration was about. Tri-state makes "skipped (planned)" observably distinct from
        // "assigned" in every caller's summary log.
        private enum AssignClipResult
        {
            Assigned,
            SkippedPlanned,
            Failed
        }

        // S-19: generalized from an SfxLibrary-only helper to any UnityEngine.Object target so
        // PatchBgmPlayer (Components/BgmPlayer._bgmClip) and PatchMenuPurchaseSfx (Components/
        // MenuController._upgradePurchaseSfx) can reuse the exact same SerializedObject-assign pattern as
        // PatchSfxLibrary's SFX-01..05 calls, instead of hand-rolling three near-identical copies.
        // CR-CODE s-19 iteration 1 minor finding: now returns whether the assignment left the caller in a
        // non-degraded state, so PatchBgmPlayer/PatchSfxLibrary/PatchMenuPurchaseSfx can fold this result
        // into their own summary line instead of always printing an unconditional success message even
        // when this method (or the mixer setup alongside it) already logged [DEGRADED] above it — see
        // those callers for how the return value is used.
        private static AssignClipResult AssignClip(UnityEngine.Object target, string fieldName, string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                // Not-yet-generated clip (design/assets.md 状態 planned) — leave the field null (every
                // caller's Play(null) is a documented silent no-op, not an error). This is a deliberate,
                // expected state, not a degradation — callers must not fold this into a "[DEGRADED]"
                // summary just because assetPath was empty by design.
                return AssignClipResult.SkippedPlanned;
            }
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip == null)
            {
                Log("[DEGRADED] AssignClip: AudioClip not found at " + assetPath);
                return AssignClipResult.Failed;
            }
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Fail("AssignClip: " + target.GetType().Name + " field not found: " + fieldName);
                return AssignClipResult.Failed;
            }
            prop.objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();

            // CR-CODE s-19 iteration 2 minor finding: the write above was never confirmed to have actually
            // taken — a future incompatible field-type change (e.g. AudioClip -> AudioResource) could leave
            // objectReferenceValue silently null after ApplyModifiedPropertiesWithoutUndo while this method
            // still reports Assigned, making the whole integration summary claim success over an unwired
            // scene. Re-read the property immediately after the write and degrade explicitly if it didn't
            // stick, instead of trusting the assignment call blindly.
            if (!ReferenceEquals(prop.objectReferenceValue, clip))
            {
                Log("[DEGRADED] AssignClip: SerializedProperty write did not take for " + target.GetType().Name + "." + fieldName +
                    " (expected " + clip.name + ", field type may be incompatible — rename/retype?).");
                return AssignClipResult.Failed;
            }
            return AssignClipResult.Assigned;
        }

        /// <summary>S-26: builds (or updates in place, idempotent re-run — mirrors BuildHitVfxPrefab's
        /// materialIsNew handling) the ArenaBackdropSkybox.mat asset that bakes IMG-06 onto all six
        /// Skybox/6 Sided faces, then assigns it to Components/ArenaBackdrop._skyboxMaterial on the
        /// 'ArenaBackdrop' root SceneWiring.WireGame created. CR-CODE s-26 iteration 1 major指摘#1/#4 fix:
        /// this replaces the previous runtime Shader.Find + `new Material` construction in
        /// Components/ArenaBackdrop.Start() — baking the material as a project asset here (Editor-time,
        /// where builtin shaders are always resolvable) is what guarantees "Skybox/6 Sided" survives
        /// player-build shader stripping (a builtin shader ships only when referenced by an asset included
        /// in the build; a SerializeField-referenced .mat on a scene object qualifies).</summary>
        private static void PatchArenaBackdrop()
        {
            GameObject go = GameObject.Find(ArenaBackdropRootName);
            if (go == null)
            {
                Fail("PatchArenaBackdrop: '" + ArenaBackdropRootName + "' GameObject missing after SceneWiring.WireGame.");
                return;
            }
            ArenaBackdrop backdrop = go.GetComponent<ArenaBackdrop>();
            if (backdrop == null)
            {
                Fail("PatchArenaBackdrop: ArenaBackdrop component missing on '" + ArenaBackdropRootName + "'.");
                return;
            }

            string assetPath = GameConfig.AssetKeys.ArenaBackdropTexture;
            if (string.IsNullOrEmpty(assetPath))
            {
                Log("PatchArenaBackdrop: skipped — GameConfig.AssetKeys.ArenaBackdropTexture is empty (IMG-06 not yet generated per design/assets.md).");
                _arenaBackdropTextureResult = AssignClipResult.SkippedPlanned;
                return;
            }
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                // CR-CODE s-26 iter2 minor指摘#2 fix: a prior successful run may have already baked
                // ArenaBackdropSkyboxMaterial and wired ArenaBackdrop._skyboxMaterial to it (e.g. Game.unity
                // was committed while IMG-06 was still present, then IMG-06 was rejected/must-replace'd and
                // deleted before this re-run). Leaving both the stale .mat asset and the scene's serialized
                // reference untouched would ship a texture-less/dangling-GUID skybox while this report says
                // Failed — mirrors DeleteStaleHitVfxPrefabIfPresent's fix for the same "report says degraded,
                // scene still references stale asset" pattern (S-17 iter1/iter2).
                Log("[DEGRADED] PatchArenaBackdrop: Texture2D not found at " + assetPath);
                DeleteStaleArenaBackdropSkyboxMaterialIfPresent("arena backdrop texture missing this run");
                ClearArenaBackdropSkyboxMaterialReference(backdrop);
                _arenaBackdropTextureResult = AssignClipResult.Failed;
                return;
            }

            Material skyboxMaterial = BuildArenaBackdropSkyboxMaterial(texture);
            if (skyboxMaterial == null)
            {
                // Same stale-asset/stale-reference concern as above, for the "texture resolved but the
                // material itself couldn't be built/saved" branch (BuildArenaBackdropSkyboxMaterial already
                // logs the specific [DEGRADED] reason before returning null).
                DeleteStaleArenaBackdropSkyboxMaterialIfPresent("skybox material build failed this run");
                ClearArenaBackdropSkyboxMaterialReference(backdrop);
                _arenaBackdropTextureResult = AssignClipResult.Failed;
                return;
            }

            var so = new SerializedObject(backdrop);
            SerializedProperty prop = so.FindProperty("_skyboxMaterial");
            if (prop == null)
            {
                Fail("PatchArenaBackdrop: ArenaBackdrop._skyboxMaterial serialized field not found (rename?).");
                return;
            }
            prop.objectReferenceValue = skyboxMaterial;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!ReferenceEquals(prop.objectReferenceValue, skyboxMaterial))
            {
                Log("[DEGRADED] PatchArenaBackdrop: SerializedProperty write did not take for ArenaBackdrop._skyboxMaterial (expected " + skyboxMaterial.name + ").");
                _arenaBackdropTextureResult = AssignClipResult.Failed;
                return;
            }
            _arenaBackdropTextureResult = AssignClipResult.Assigned;
            Log("PatchArenaBackdrop: IMG-06-backed ArenaBackdropSkybox material assigned to ArenaBackdrop._skyboxMaterial.");
        }

        /// <summary>CR-CODE s-26 iter2 minor指摘#2 fix: mirrors DeleteStaleHitVfxPrefabIfPresent — deletes a
        /// leftover ArenaBackdropSkyboxMaterial asset from a previous successful run when the current run
        /// degrades, so a degraded run is honestly skybox-less end to end instead of leaving a stale .mat
        /// (possibly referencing a now-deleted IMG-06 texture GUID) on disk for PatchArenaBackdrop's own
        /// scene wiring to point at.</summary>
        private static void DeleteStaleArenaBackdropSkyboxMaterialIfPresent(string reason)
        {
            // Ship-review dedup: control flow now lives in DeleteStaleAssetIfPresent (see its comment).
            DeleteStaleAssetIfPresent<Material>(
                GameConfig.AssetKeys.ArenaBackdropSkyboxMaterial,
                "PatchArenaBackdrop: failed to delete stale ArenaBackdropSkyboxMaterial at " +
                    GameConfig.AssetKeys.ArenaBackdropSkyboxMaterial + " (" + reason + ") — refusing to continue and risk leaving ArenaBackdrop wired to a stale asset.",
                "[DEGRADED] PatchArenaBackdrop: deleted a stale ArenaBackdropSkyboxMaterial left over from a previous run (" + reason + ").");
        }

        /// <summary>Clears a previously-wired ArenaBackdrop._skyboxMaterial reference on a degraded run
        /// (companion to DeleteStaleArenaBackdropSkyboxMaterialIfPresent) so the committed Game.unity does
        /// not keep pointing Components/ArenaBackdrop at a now-deleted material GUID — ArenaBackdrop.Start()
        /// already has an explicit null-reference [Wiring] error path for exactly this state.</summary>
        private static void ClearArenaBackdropSkyboxMaterialReference(ArenaBackdrop backdrop)
        {
            var so = new SerializedObject(backdrop);
            SerializedProperty prop = so.FindProperty("_skyboxMaterial");
            if (prop == null || prop.objectReferenceValue == null)
            {
                return;
            }
            prop.objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Idempotent load-or-create-in-place (mirrors BuildHitVfxPrefab's materialIsNew handling
        /// so re-runs update the same asset GUID instead of Delete+CreateAsset churn) construction of the
        /// Skybox/6 Sided material asset with <paramref name="texture"/> on all six faces.</summary>
        private static Material BuildArenaBackdropSkyboxMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find(SkyboxShaderName);
            if (shader == null)
            {
                Log("[DEGRADED] BuildArenaBackdropSkyboxMaterial: shader '" + SkyboxShaderName + "' not found — Skybox material not built.");
                return null;
            }

            string materialPath = GameConfig.AssetKeys.ArenaBackdropSkyboxMaterial;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            bool materialIsNew = material == null;
            if (materialIsNew)
            {
                material = new Material(shader) { name = "ArenaBackdropSkybox" };
            }
            else
            {
                material.shader = shader;
            }
            material.SetTexture("_FrontTex", texture);
            material.SetTexture("_BackTex", texture);
            material.SetTexture("_LeftTex", texture);
            material.SetTexture("_RightTex", texture);
            material.SetTexture("_UpTex", texture);
            material.SetTexture("_DownTex", texture);

            Directory.CreateDirectory(Path.GetDirectoryName(materialPath)!);
            if (materialIsNew)
            {
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
            }

            // CR-CODE s-26 iter2 minor指摘#1 fix: every other Failed branch in this file logs a [DEGRADED]
            // reason before returning null (rule 11/12) — this final reload was the one silent exception.
            // A null here means CreateAsset/SaveAssets did not actually persist the asset (missing/
            // unwritable Generated/Materials folder, disk/VCS write failure, or a post-save import failure)
            // and PatchArenaBackdrop's report would otherwise show arena_backdrop_texture=Failed with no
            // clue why, undebuggable in batchmode.
            Material result = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (result == null)
            {
                Log("[DEGRADED] BuildArenaBackdropSkyboxMaterial: " + materialPath +
                    " could not be saved/reloaded after CreateAsset/SaveAssets (folder uncreatable, write failure, or import failure) — Skybox material not built.");
            }
            return result;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj != null)
            {
                UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        // ------------------------------------------------------------------
        // MANIFEST degradation notes (CR-CODE s-18 iter1 major指摘#2)
        // ------------------------------------------------------------------

        /// <summary>Builds a single MANIFEST.jsonl line recording an integration-time degradation against
        /// an already-recorded asset. Mirrors the project's existing "revision" note convention already
        /// present in game/_generated/MANIFEST.jsonl for metadata-only additions (e.g. the MDL-01/ANM-0x
        /// "エンジン取込後検証" revision entries: same file content, sha256 unchanged, only a
        /// revision_reason describing what was observed) rather than inventing a new schema. Public +
        /// side-effect-free (no Application.dataPath/AssetDatabase access) so it — and the appended line's
        /// shape — can be asserted directly from an EditMode test
        /// (Tests/EditMode/AssetIntegrationManifestNoteTests.cs; mirrors AudioMixerSetup.EnsureMixer's
        /// explicit-path testability pattern).</summary>
        public static string BuildManifestDegradationNoteLine(string assetId, string rawFile, string fileSha256,
            string reason, string storyId, DateTime generatedAtUtc)
        {
            // CR-CODE s-18 iter2 minor指摘#2: pin InvariantCulture (a non-Gregorian OS/CI culture must not
            // be able to corrupt the formatted digits of a provenance timestamp) and normalize Kind so a
            // caller that accidentally passes local/unspecified time doesn't get silently mislabeled "Z".
            DateTime normalizedUtc = generatedAtUtc.Kind switch
            {
                DateTimeKind.Utc => generatedAtUtc,
                DateTimeKind.Local => generatedAtUtc.ToUniversalTime(),
                _ => DateTime.SpecifyKind(generatedAtUtc, DateTimeKind.Utc), // Unspecified: preserve prior behavior (treat digits as already-UTC) but make the assumption explicit
            };
            return "{"
                + "\"asset_id\":\"" + EscapeJson(assetId) + "\","
                + "\"file\":\"" + EscapeJson(rawFile) + "\","
                + "\"note\":\"integration_degradation\","
                + "\"revision_of_sha256\":\"" + EscapeJson(fileSha256) + "\","
                + "\"revision_reason\":\"" + EscapeJson(reason) + "\","
                + "\"story\":\"" + EscapeJson(storyId) + "\","
                + "\"generated_at\":\"" + normalizedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture) + "\""
                + "}";
        }

        /// <summary>True if <paramref name="manifestPath"/> already contains a degradation note for the
        /// same (asset_id, revision_of_sha256, revision_reason, story) tuple (CR-CODE s-18 iter2 minor指摘#1,
        /// tightened by CR-CODE s-21 iter2 minor指摘#3: the original (asset_id, revision_of_sha256)-only key
        /// silently swallowed a genuinely *different* reason/story recorded against the same unchanged file
        /// bytes — e.g. two distinct degradation causes discovered on separate runs against the same
        /// still-broken raw asset — because the append-only provenance ledger is the only durable record of
        /// "why"; the per-run asset-integration-report.txt Log() trail is overwritten every run). Including
        /// reason+story in the key means only a byte-for-byte repeat of an already-recorded note is deduped;
        /// any new reason or a different story attributing degradation to the same file is still appended.
        /// Uses plain substring matching against the fields this tool itself writes (not a general JSON
        /// parser) — sufficient because every line in this ledger's degradation notes is produced exclusively
        /// by <see cref="BuildManifestDegradationNoteLine"/>.</summary>
        public static bool ManifestHasDegradationNote(string manifestPath, string assetId, string fileSha256,
            string reason, string storyId)
        {
            if (!File.Exists(manifestPath))
            {
                return false;
            }
            string idMarker = "\"asset_id\":\"" + EscapeJson(assetId) + "\"";
            string noteMarker = "\"note\":\"integration_degradation\"";
            string shaMarker = "\"revision_of_sha256\":\"" + EscapeJson(fileSha256) + "\"";
            string reasonMarker = "\"revision_reason\":\"" + EscapeJson(reason) + "\"";
            string storyMarker = "\"story\":\"" + EscapeJson(storyId) + "\"";
            foreach (string line in File.ReadLines(manifestPath))
            {
                if (line.Contains(idMarker) && line.Contains(noteMarker) && line.Contains(shaMarker)
                    && line.Contains(reasonMarker) && line.Contains(storyMarker))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Appends the line built by <see cref="BuildManifestDegradationNoteLine"/> to
        /// <paramref name="manifestPath"/> (append-only — rules/assets.md「既存行の書き換え・削除も禁止」),
        /// unless <see cref="ManifestHasDegradationNote"/> already finds a byte-for-byte identical
        /// (asset_id, revision_of_sha256, revision_reason, story) note (dedupe — CR-CODE s-18 iter2 minor指摘
        /// #1, key widened per CR-CODE s-21 iter2 minor指摘#3 so a genuinely new reason/story against the
        /// same file is never silently swallowed).
        /// storyId defaults to "S-18" (this tool's own story) but is a parameter for testability/reuse.</summary>
        public static void AppendManifestDegradationNote(string manifestPath, string assetId, string rawFile,
            string fileSha256, string reason, string storyId = "S-18")
        {
            if (ManifestHasDegradationNote(manifestPath, assetId, fileSha256, reason, storyId))
            {
                Log($"AppendManifestDegradationNote: skipped — a byte-for-byte identical degradation note (asset_id={assetId}, revision_of_sha256={fileSha256}, revision_reason={reason}, story={storyId}) already exists in {manifestPath}.");
                return;
            }
            string line = BuildManifestDegradationNoteLine(assetId, rawFile, fileSha256, reason, storyId, DateTime.UtcNow);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.AppendAllText(manifestPath, line + Environment.NewLine);
        }

        /// <summary>Wires ConfigureHeroModel's Generic-degrade path: computes the raw MDL-01 FBX's sha256
        /// (or the "unknown-source-file-missing" sentinel when the raw file is absent) and appends a
        /// MANIFEST degradation note. Public + parameterized on manifestPath/rawModelFullPath (mirrors
        /// BuildManifestDegradationNoteLine's testability pattern) so this wiring — not just the two leaf
        /// helpers above — is covered by an EditMode test (CR-CODE s-18 iter2 minor指摘#4).</summary>
        public static void RecordHeroAvatarDegradation(string manifestPath, string rawModelFullPath)
        {
            bool rawModelExists = File.Exists(rawModelFullPath);
            string rawModelSha256 = rawModelExists ? ComputeSha256(rawModelFullPath) : "unknown-source-file-missing";
            if (!rawModelExists)
            {
                // CR-CODE S-21 iter2 minor指摘#4: the raw MDL-01 FBX (game/_generated/ — the provenance
                // source-of-truth per assets-config.md) being absent is itself a serious integration
                // problem, not just a routine Avatar-degrade. Surface it explicitly in this run's own log
                // stream (Debug.LogWarning is captured by the batchmode Editor.log / qa/evidence/*.log CI
                // artifacts) rather than leaving it discoverable only by grepping the MANIFEST sentinel
                // string "unknown-source-file-missing" after the fact.
                Debug.LogWarning("[AssetIntegration] RecordHeroAvatarDegradation: raw source file not found at \""
                    + rawModelFullPath + "\" — MDL-01 provenance source is missing. Recording MANIFEST "
                    + "degradation note with sentinel sha256 \"unknown-source-file-missing\".");
            }
            AppendManifestDegradationNote(
                manifestPath,
                "MDL-01",
                "_generated/" + HeroRawModelRelative,
                rawModelSha256,
                "Unity Humanoid Avatar generation failed or produced an invalid/non-human Avatar during " +
                "AssetIntegration.IntegrateAll (ConfigureHeroModel) — ModelImporter.animationType fell back " +
                "to Generic (tech-stack-unity.md「資産の取り扱い」/ S-18 acceptance). See " +
                "qa/evidence/asset-integration-report.txt for the full integration log from this run.");
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return string.Empty;
            }
            // CR-CODE s-18 iter2 minor指摘#3: escape every control char (< 0x20), not just \\/\"/\r/\n —
            // an unescaped raw control byte (e.g. a stray tab in a future reason string) produces a
            // structurally invalid MANIFEST.jsonl line that breaks strict JSON parsers (jq / python json)
            // reading the whole provenance ledger.
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\r':
                        // Reason strings are recorded single-line; drop CR (preserves prior behavior/tests).
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string ComputeSha256(string filePath)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hash = sha.ComputeHash(stream);
            var sb = new System.Text.StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------
        // Reporting
        // ------------------------------------------------------------------

        private static void WriteReport(bool heroAvatarValid, Vector3 heroBounds, Vector3 swarmerBounds, bool heroClipsComplete, bool hitVfxBuilt,
            bool crystalGlowVfxBuilt, bool crystalCollectVfxBuilt)
        {
            Report.AppendLine("hero_avatar_valid=" + heroAvatarValid);
            Report.AppendLine("hero_bounds_size=" + heroBounds.x.ToString("F4") + "," + heroBounds.y.ToString("F4") + "," + heroBounds.z.ToString("F4"));
            Report.AppendLine("swarmer_bounds_size=" + swarmerBounds.x.ToString("F4") + "," + swarmerBounds.y.ToString("F4") + "," + swarmerBounds.z.ToString("F4"));
            Report.AppendLine("hero_clips_complete=" + heroClipsComplete);
            // CR-CODE S-17 iter1 minor finding fix: hit_vfx_built is now a structured key=value line (was
            // only visible as the free-text "# Hit VFX prefab built: ..." Log() line above), so a
            // key=value-parsing caller (gates.md Integrate 実施者の構造化返却 responsibility) can detect a
            // degraded VFX without scraping comment text.
            Report.AppendLine("hit_vfx_built=" + hitVfxBuilt);
            // S-29: structured key=value lines for the crystal glow-halo/collect-flash prefab build
            // outcomes (mirrors hit_vfx_built above).
            Report.AppendLine("crystal_glow_vfx_built=" + crystalGlowVfxBuilt);
            Report.AppendLine("crystal_collect_vfx_built=" + crystalCollectVfxBuilt);
            // CR-CODE s-19 iteration 2 minor finding: structured key=value lines for the S-19 audio wiring
            // outcomes (previously only visible as free-text "# ..." comment lines above), one per
            // AssignClip call site, so a key=value-parsing caller can tell Assigned/SkippedPlanned/Failed
            // apart without scraping comment text. Values are the AssignClipResult enum names verbatim.
            Report.AppendLine("bgm_clip=" + _bgmClipResult);
            Report.AppendLine("sfx_attack_hit_clip=" + _sfxAttackHitResult);
            Report.AppendLine("sfx_dash_clip=" + _sfxDashResult);
            Report.AppendLine("sfx_player_hit_clip=" + _sfxPlayerHitResult);
            Report.AppendLine("sfx_crystal_pickup_clip=" + _sfxCrystalPickupResult);
            Report.AppendLine("sfx_wave_start_clip=" + _sfxWaveStartResult);
            Report.AppendLine("sfx_upgrade_purchase_clip=" + _sfxUpgradePurchaseResult);
            // S-26: structured key=value line for the arena backdrop Skybox texture wiring outcome
            // (mirrors the bgm_clip/sfx_*_clip lines above — same AssignClipResult vocabulary).
            Report.AppendLine("arena_backdrop_texture=" + _arenaBackdropTextureResult);
            // S-28: structured key=value line for the outline material build outcome.
            Report.AppendLine("outline_material_built=" + _outlineMaterialBuilt);
            // CR-CODE S-28 iter1 minor finding: structured key=value lines for the ApplyRoleColorBlocking-
            // AndRimHighlight outcomes (previously only visible as free-text "# ..." comment lines).
            Report.AppendLine("material_finish_hero=" + _materialFinishHeroResult);
            Report.AppendLine("material_finish_swarmer=" + _materialFinishSwarmerResult);
            // CR-CODE S-30 iter1 minor finding fix: structured key=value line for the ConfigureUiFrameKit-
            // Sprites outcome (previously only visible as the free-text "# UI frame kit sprites sliced:
            // allOk=..." Log() line above), mirroring outline_material_built/crystal_*_vfx_built above.
            Report.AppendLine("ui_frame_kit_sprites_sliced=" + _uiFrameKitSpritesSliced);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
            File.WriteAllText(ReportPath, Report.ToString());
        }

        private static void Log(string message)
        {
            Debug.Log("[AssetIntegration] " + message);
            Report.AppendLine("# " + message);
        }

        private static void Fail(string message)
        {
            Debug.LogError("[AssetIntegration] " + message);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!);
                File.WriteAllText(ReportPath, Report + "\nFAILED: " + message);
            }
            catch
            {
                // best-effort report write — the exit code below is the authoritative failure signal.
            }
            EditorApplication.Exit(1);
        }
    }
}
