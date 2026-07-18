// UrpShaderUtil — shared URP/Lit → Standard shader resolution (ship-review dedup): the identical
// Shader.Find("Universal Render Pipeline/Lit") →(warn)→ Shader.Find("Standard") fallback chain was
// triplicated across Components/ArenaEnvironment.CreateUrpSafeMaterial, Components/EnemyAgent.
// CreateFallbackMaterial and Components/DashTrailSpawner.CreateGhostMaterial. The session-scoped
// Shader cache DashTrailSpawner grew in a prior ship-review fix is folded in here (Shader.Find is a
// string lookup; the resolved Shader is immutable for the session), so all three callers now share
// it — static + SubsystemRegistration reset mirrors Components/CrystalPickup's precedent for statics
// under Enter Play Mode Options with domain reload disabled.
//
// Log split (rule 10/12): the URP/Lit-missing *fallback attempt* is a legitimate degraded-but-working
// path and stays a LogWarning — each caller passes its own message so the line keeps its original
// per-caller wording, and the warn-once flag keeps the (formerly per-call at two of the three sites)
// repeat spam bounded now that a shared cache would make repeats misleading anyway. The terminal
// "neither shader found" case stays with each CALLER (this helper just returns null): those messages
// are caller-specific LogErrors with their own one-shot semantics, per gates.md QA-PLAY's
// LogAssert.NoUnexpectedReceived contract.
using UnityEngine;

namespace ForgeGame.Components
{
    internal static class UrpShaderUtil
    {
        private const string UrpLitShaderName = "Universal Render Pipeline/Lit";
        private const string StandardShaderName = "Standard";

        private static Shader _litOrStandardCache;
        private static bool _urpMissingWarned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticStateForDomainReloadDisabled()
        {
            _litOrStandardCache = null;
            _urpMissingWarned = false;
        }

        /// <summary>Resolves URP/Lit, falling back to Standard (logging
        /// <paramref name="urpMissingWarning"/> once per session on the first fallback), and caches the
        /// resolved Shader for the rest of the session. Returns null when neither shader exists — the
        /// caller then logs its own terminal wiring error and degrades (see file header for why the
        /// terminal message is deliberately not unified here).</summary>
        public static Shader FindLitOrStandard(string urpMissingWarning)
        {
            Shader shader = _litOrStandardCache;
            if (shader != null)
            {
                return shader;
            }
            shader = Shader.Find(UrpLitShaderName);
            if (shader == null)
            {
                if (!_urpMissingWarned)
                {
                    Debug.LogWarning(urpMissingWarning);
                    _urpMissingWarned = true;
                }
                shader = Shader.Find(StandardShaderName);
            }
            if (shader == null)
            {
                return null;
            }
            _litOrStandardCache = shader;
            return shader;
        }
    }
}
