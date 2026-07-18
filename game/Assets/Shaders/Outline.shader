// ForgeGame/Outline — S-28: art-bible.json style_block "clean bold 2-3px dark-navy outlines on every
// silhouette edge". Classic inverse-hull silhouette outline: renders a solid-color, back-face-only
// (Cull Front) copy of the mesh with vertices pushed outward along their object-space normal, so only
// the outward-facing silhouette rim peeks out from behind the regular (front-face) material passes.
//
// This shader is applied as an EXTRA material slot appended to a Renderer's existing sharedMaterials
// array (Editor/AssetIntegration.ApplyOutlineToRenderers), not as a full-screen Renderer Feature — when
// a Renderer has more materials than the mesh has submeshes, Unity draws the extra material(s) against
// submesh 0 again, so no duplicate GameObject/hierarchy is needed and this works identically for both
// static MeshRenderer (swarmer) and SkinnedMeshRenderer (hero — Unity's GPU skinning resolves object-
// space positions once per frame and every material pass reads that same skinned output).
//
// _OutlineColor/_OutlineWidth are always set at runtime from GameConfig.Outline by
// Editor/AssetIntegration.BuildOutlineMaterial (マジックナンバー禁止) — the Properties defaults below are
// authoring-time fallbacks only (e.g. for previewing this shader/material directly in the editor).
Shader "ForgeGame/Outline"
{
    Properties
    {
        _OutlineColor("Outline Color", Color) = (0.0863, 0.2706, 0.5137, 1) // #164583 fallback
        _OutlineWidth("Outline Width (world-space meters)", Float) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "SilhouetteOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            // Extrude in WORLD space, not object space: hero and swarmer share this single material, but
            // their source FBX roots carry very different baked scale factors (Editor/AssetIntegration.
            // BuildSwarmerPrefab's own comment documents the swarmer FBX root's "non-identity baked scale
            // — Blender/Meshy export unit convention"). An object-space offset of _OutlineWidth would be
            // multiplied by whatever that per-model scale happens to be when the object-to-world matrix is
            // applied, so the *same* _OutlineWidth value would read as a crisp thin rim on one model and a
            // huge solid blob that swallows the whole silhouette on the other (observed on swarmer during
            // S-28 QA — qa/evidence/qa-game-swarm.png before this fix). Converting to world space first
            // and extruding there makes _OutlineWidth mean "meters in world space" for every model
            // regardless of its own local/root scale.
            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 extrudedPositionWS = positionWS + normalize(normalWS) * _OutlineWidth;
                OUT.positionHCS = TransformWorldToHClip(extrudedPositionWS);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
