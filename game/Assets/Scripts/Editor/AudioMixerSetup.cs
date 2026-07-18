// AudioMixerSetup — creates (once) or loads the shared BGM/SFX AudioMixer asset that the Menu 設定タブ
// (S-13) drives immediately on every A/D volume change, and that S-19 (音声統合) will later route all
// BGM/SFX AudioSources through.
//
// Unity does not expose a public runtime/Editor API to author an AudioMixer asset with exposed float
// parameters from script — that machinery lives entirely on the internal `UnityEditor.Audio.
// AudioMixerController` type (used by Unity's own "Assets > Create > Audio Mixer" menu command). This
// tool reaches it via reflection (Type.GetType + MethodInfo.Invoke) so ForgeGame.Editor never takes a
// compile-time dependency on an internal Unity type — every method/property/field name and signature
// used below was confirmed against the actual UnityEditor.dll (Unity 6000.3.16f1, this project's pinned
// editor — contract §11 "実行中のバージョン再解決禁止") by disassembling it, not guessed.
//
// Idempotent: if the asset already exists at the target path, it is loaded and returned unchanged —
// groups/exposed parameters are only created on the very first run (mirrors SceneWiring/AssetIntegration's
// idempotent-rerun pattern). Any reflection failure degrades to a logged error + null return rather than
// throwing out of SceneWiring.WireMenu (the AudioMixer is best-effort infrastructure: a missing mixer
// means Settings-tab volume changes still save to SaveData correctly, just without a live mixer bus to
// push into immediately — Components/MenuController.ApplyMixerVolumes no-ops + logs when _mixer is null).
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace ForgeGame.EditorTools
{
    public static class AudioMixerSetup
    {
        public static AudioMixer EnsureMixer(
            string assetPath, string bgmGroupName, string sfxGroupName, string bgmParam, string sfxParam)
        {
            AudioMixer existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(assetPath);
            if (existing != null)
            {
                // CR-CODE S-13 iteration 1, finding minor (EnsureMixer partial-failure): a mixer asset can
                // exist on disk yet lack the exposed parameters a prior run tried to author (e.g. an
                // earlier CreateMixer call threw after CreateMixerControllerAtPath had already written the
                // asset). Validate both expected parameters resolve via GetFloat before trusting the
                // fast path — an asset that fails this check is deleted and re-authored below instead of
                // being silently returned as if it were complete (which would turn one logged failure into
                // a permanent silent one: MenuController.ApplyMixerVolumes would keep hitting the
                // SetFloat-returns-false branch every session with no path to self-heal).
                if (existing.GetFloat(bgmParam, out _) && existing.GetFloat(sfxParam, out _))
                {
                    return existing;
                }
                Debug.LogError("[Wiring] AudioMixerSetup.EnsureMixer: existing AudioMixer '" + assetPath +
                    "' is missing expected exposed parameter '" + bgmParam + "' or '" + sfxParam +
                    "' (likely a half-authored asset from a prior failed run) — deleting and re-authoring");
                // CR-CODE S-13 iteration 2, finding minor: AssetDatabase.DeleteAsset does not throw on
                // failure (read-only/locked file, VCS-controlled path, etc.) — it silently returns false.
                // Ignoring that return value would let a half-authored asset survive this "delete and
                // re-author" step: CreateMixerControllerAtPath below would then fail against the still-
                // present broken asset and get logged as a generic "reflection authoring failed" error,
                // hiding the real cause (the delete never happened). Checking + logging here attributes
                // the failure correctly and gives the operator something actionable to fix on disk.
                if (!AssetDatabase.DeleteAsset(assetPath))
                {
                    Debug.LogError("[Wiring] AudioMixerSetup.EnsureMixer: failed to delete broken mixer asset '" +
                        assetPath + "' — subsequent re-author may fail or reuse the broken asset");
                }
            }

            try
            {
                return CreateMixer(assetPath, bgmGroupName, sfxGroupName, bgmParam, sfxParam);
            }
            catch (Exception e)
            {
                Debug.LogError("[Wiring] AudioMixerSetup.EnsureMixer: failed to author AudioMixer '" + assetPath +
                    "' via UnityEditor.Audio.AudioMixerController reflection — " + e);
                // CR-CODE S-13 iteration 1, finding minor (EnsureMixer partial-failure): CreateMixer can
                // throw after CreateMixerControllerAtPath already wrote a half-authored asset to disk
                // (e.g. group creation or parameter exposure failed mid-way). Without this cleanup, the
                // next EnsureMixer call would hit the `existing != null` fast path above and — prior to
                // the validation added above — return the broken asset as if it were success. Deleting
                // here ensures a broken partial asset never lingers even before that validation runs, and
                // guarantees the next call starts from a clean CreateMixer attempt.
                //
                // CR-CODE S-13 iteration 2, finding minor: same DeleteAsset-return-value gap as the
                // half-authored-asset path above — a false return here (delete failed) must be logged,
                // not swallowed, or the "clean CreateMixer attempt next time" guarantee this comment
                // claims silently does not hold.
                if (AssetDatabase.LoadAssetAtPath<AudioMixer>(assetPath) != null)
                {
                    if (!AssetDatabase.DeleteAsset(assetPath))
                    {
                        Debug.LogError("[Wiring] AudioMixerSetup.EnsureMixer: failed to delete broken mixer asset '" +
                            assetPath + "' after CreateMixer threw — subsequent re-author may fail or reuse the broken asset");
                    }
                }
                return null;
            }
        }

        private static AudioMixer CreateMixer(
            string assetPath, string bgmGroupName, string sfxGroupName, string bgmParam, string sfxParam)
        {
            Type controllerType = Type.GetType("UnityEditor.Audio.AudioMixerController, UnityEditor");
            if (controllerType == null)
            {
                throw new InvalidOperationException("UnityEditor.Audio.AudioMixerController type not found via reflection");
            }

            MethodInfo createAtPath = controllerType.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static);
            object controllerObj = createAtPath?.Invoke(null, new object[] { assetPath });
            if (controllerObj == null)
            {
                throw new InvalidOperationException("AudioMixerController.CreateMixerControllerAtPath returned null for " + assetPath);
            }

            PropertyInfo masterGroupProp = controllerType.GetProperty("masterGroup", BindingFlags.Public | BindingFlags.Instance);
            object masterGroup = masterGroupProp?.GetValue(controllerObj);
            if (masterGroup == null)
            {
                throw new InvalidOperationException("AudioMixerController.masterGroup was null immediately after CreateMixerControllerAtPath");
            }

            object bgmGroup = CreateChildGroup(controllerType, controllerObj, masterGroup, bgmGroupName);
            object sfxGroup = CreateChildGroup(controllerType, controllerObj, masterGroup, sfxGroupName);

            ExposeGroupVolume(controllerType, controllerObj, bgmGroup, bgmParam);
            ExposeGroupVolume(controllerType, controllerObj, sfxGroup, sfxParam);

            var mixer = (AudioMixer)controllerObj; // AudioMixerController : AudioMixer (public base type)
            EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            // AudioMixer.SetFloat/GetFloat operate against the native audio-mixer runtime state, which
            // is only guaranteed live for the instance AssetDatabase currently considers canonical —
            // the in-memory `mixer` reference above (held across SaveAssets/ImportAsset/Refresh) is not
            // reliable for that (SetFloat silently returns false against it, confirmed empirically via
            // AudioMixerSetupTests). Re-resolving via LoadAssetAtPath after the reimport returns the
            // actually-live instance.
            AudioMixer reloaded = AssetDatabase.LoadAssetAtPath<AudioMixer>(assetPath);
            if (reloaded == null)
            {
                throw new InvalidOperationException("AudioMixer asset '" + assetPath + "' failed to reload after creation");
            }
            return reloaded;
        }

        private static object CreateChildGroup(Type controllerType, object controllerObj, object parentGroup, string name)
        {
            MethodInfo createGroup = controllerType.GetMethod("CreateNewGroup", BindingFlags.Public | BindingFlags.Instance);
            object group = createGroup?.Invoke(controllerObj, new object[] { name, true });
            if (group == null)
            {
                throw new InvalidOperationException("AudioMixerController.CreateNewGroup('" + name + "') returned null");
            }

            // CR-CODE S-13 iteration 1, finding minor: previously `addChild?.Invoke(...)` silently no-oped
            // if GetMethod returned null (a future Unity version renaming/removing the method), leaving the
            // new group created but never attached under Master — EnsureMixer would then report success
            // while authoring a structurally broken mixer, undetected by EditMode tests (which only read
            // the managed exposed-parameter table via GetFloat). Matches this file's own convention for
            // every other reflection lookup (createAtPath/masterGroupProp/createGroup/getGuidForVolume).
            MethodInfo addChild = controllerType.GetMethod("AddChildToParent", BindingFlags.Public | BindingFlags.Instance);
            if (addChild == null)
            {
                throw new InvalidOperationException("AudioMixerController.AddChildToParent method not found via reflection (group '" + name + "' would be created but never attached under Master)");
            }
            addChild.Invoke(controllerObj, new object[] { group, parentGroup });
            return group;
        }

        private static void ExposeGroupVolume(Type controllerType, object controllerObj, object group, string exposedName)
        {
            Type groupType = group.GetType(); // UnityEditor.Audio.AudioMixerGroupController (resolved at runtime, not compile-time)
            MethodInfo getGuidForVolume = groupType.GetMethod("GetGUIDForVolume", BindingFlags.Public | BindingFlags.Instance);
            object guid = getGuidForVolume?.Invoke(group, null);
            if (guid == null)
            {
                throw new InvalidOperationException("AudioMixerGroupController.GetGUIDForVolume() returned null for group exposing '" + exposedName + "'");
            }

            Type pathType = Type.GetType("UnityEditor.Audio.AudioGroupParameterPath, UnityEditor");
            if (pathType == null)
            {
                throw new InvalidOperationException("UnityEditor.Audio.AudioGroupParameterPath type not found via reflection");
            }
            object path = Activator.CreateInstance(pathType, new object[] { group, guid });

            // CR-CODE S-13 iteration 1, finding minor: same pattern/rationale as AddChildToParent above —
            // `addExposed?.Invoke(...)` previously no-oped silently on a null MethodInfo, which today only
            // surfaced indirectly (and confusingly) via RenameExposedParameter's "no exposed parameter
            // matched the newly-added GUID" exception rather than naming the actual missing method.
            MethodInfo addExposed = controllerType.GetMethod("AddExposedParameter", BindingFlags.Public | BindingFlags.Instance);
            if (addExposed == null)
            {
                throw new InvalidOperationException("AudioMixerController.AddExposedParameter method not found via reflection (parameter '" + exposedName + "' would never be exposed)");
            }
            addExposed.Invoke(controllerObj, new object[] { path });

            RenameExposedParameter(controllerType, controllerObj, guid, exposedName);
        }

        /// <summary>AddExposedParameter names every newly exposed parameter "MyExposedParam" (made unique
        /// by Unity's own FindUniqueParameterName) — this renames the just-added entry (matched by GUID)
        /// to the caller's chosen exposed-parameter name (e.g. "BgmVolume"), which is the string
        /// AudioMixer.SetFloat/GetFloat expect at runtime.</summary>
        private static void RenameExposedParameter(Type controllerType, object controllerObj, object guid, string newName)
        {
            PropertyInfo exposedParamsProp = controllerType.GetProperty("exposedParameters", BindingFlags.Public | BindingFlags.Instance);
            var exposedParams = (Array)exposedParamsProp?.GetValue(controllerObj);
            if (exposedParams == null)
            {
                throw new InvalidOperationException("AudioMixerController.exposedParameters was null while renaming to '" + newName + "'");
            }

            Type entryType = exposedParams.GetType().GetElementType();
            FieldInfo guidField = entryType?.GetField("guid");
            FieldInfo nameField = entryType?.GetField("name");
            if (guidField == null || nameField == null)
            {
                throw new InvalidOperationException("UnityEditor.Audio.ExposedAudioParameter guid/name field not found via reflection");
            }

            for (int i = 0; i < exposedParams.Length; i++)
            {
                object entry = exposedParams.GetValue(i);
                object entryGuid = guidField.GetValue(entry);
                if (Equals(entryGuid, guid))
                {
                    nameField.SetValue(entry, newName);
                    exposedParams.SetValue(entry, i);
                    exposedParamsProp.SetValue(controllerObj, exposedParams);
                    return;
                }
            }
            throw new InvalidOperationException("RenameExposedParameter: no exposed parameter matched the newly-added GUID (rename to '" + newName + "' skipped)");
        }
    }
}
