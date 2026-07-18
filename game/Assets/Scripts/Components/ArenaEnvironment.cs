// ArenaEnvironment — Game シーンのアリーナ地面/境界/スポーンリング配線 (gdd/art-bible.md「クリスタル・
// アリーナ環境の視覚表現方針」「シルエット方針」; P-01 四方の敵を同時視認できる可読性; S-20). Thin by
// design (rule: Components はライフサイクルと配線のみ) — ring geometry math lives in
// Systems/ArenaEnvironmentSystem; this component only builds the actual Unity primitives/meshes/materials
// from GameConfig.Environment/Player/Enemy constants, once at Start (static, non-interactive — mirrors
// Components/ArenaCameraRig's one-shot Start() pose). No generated MDL/prop is used here by design
// (gdd決定: クリスタル・アリーナ環境はプリミティブ+マテリアルのみ、生成モデル不使用).
using ForgeGame.Systems;
using UnityEngine;

namespace ForgeGame.Components
{
    public sealed class ArenaEnvironment : MonoBehaviour
    {
        private const string FloorName = "ArenaFloor";
        private const string BoundaryRingName = "ArenaBoundary";
        private const string SpawnRingName = "SpawnRing";

        private void Start()
        {
            BuildFloor();
            BuildRing(BoundaryRingName, GameConfig.Player.ArenaRadius,
                GameConfig.Environment.BoundaryRingWidth, GameConfig.Environment.BoundaryRingHeight,
                GameConfig.Environment.BoundaryColor);
            BuildRing(SpawnRingName, GameConfig.Enemy.SpawnRadius,
                GameConfig.Environment.SpawnRingWidth, GameConfig.Environment.SpawnRingHeight,
                GameConfig.Environment.SpawnRingColor);
        }

        /// <summary>Circular floor disc (円柱プリミティブ + マットグリーン — gdd 決定). Unity's built-in
        /// Cylinder primitive has radius 0.5 and height 2 at localScale (1,1,1); scaled here so its top
        /// surface sits exactly at world Y=0 (the ground plane the player/enemy/crystal transforms already
        /// assume — conventions.md §3「アリーナはXZ平面（Yは上）」).</summary>
        private void BuildFloor()
        {
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = FloorName;
            floor.transform.SetParent(transform, worldPositionStays: false);
            float diameter = GameConfig.Player.ArenaRadius * 2f;
            floor.transform.localScale = new Vector3(diameter, GameConfig.Environment.FloorHeight * 0.5f, diameter);
            floor.transform.localPosition = new Vector3(0f, -GameConfig.Environment.FloorHeight * 0.5f, 0f);
            ApplyMatteMaterial(floor, GameConfig.Environment.FloorColor);
        }

        /// <summary>Boundary/spawn-ring marker (同心円マテリアル方式 — gdd 決定「同心円マテリアル or
        /// 半透明リング」). Geometry from Systems/ArenaEnvironmentSystem.BuildRing.</summary>
        private void BuildRing(string name, float radius, float width, float height, string hexColor)
        {
            GameObject ring = new GameObject(name);
            ring.transform.SetParent(transform, worldPositionStays: false);

            ArenaEnvironmentSystem.BuildRing(
                radius, width, height, GameConfig.Environment.RingSegments,
                out Vector3[] vertices, out Vector3[] normals, out int[] triangles);

            var mesh = new Mesh { name = name + "Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

            var filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = ring.AddComponent<MeshRenderer>();
            ApplyRingMaterial(renderer, hexColor);
        }

        // Matte, non-metallic material for the arena floor (art-bible.md「3D スタイル方針」: metallic/
        // roughness低振幅・マット寄り). Mirrors SceneWiring/CrystalPickup/WaveSpawner's URP-safe shader
        // lookup pattern. rule 10/12: the URP→Standard shader-fallback *attempt* below stays LogWarning
        // (legitimate degraded-but-working path); the three terminal, unrecoverable wiring-broken paths
        // (missing Renderer / hex parse failure / no shader found at all) are LogError, matching
        // EnemyAgent.cs:150-206's CR-CODE S-14 iteration 1 major precedent for the same 3 patterns.
        private static void ApplyMatteMaterial(GameObject go, string hexColor)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogError("[Wiring] arena floor material skipped: '" + go.name + "' has no Renderer.");
                return;
            }
            Material material = CreateUrpSafeMaterial(hexColor, go.name);
            if (material == null)
            {
                return;
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", GameConfig.Environment.FloorSurfaceSmoothness);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", GameConfig.Environment.FloorSurfaceMetallic);
            }
            renderer.sharedMaterial = material;
        }

        // Boundary/spawn-ring markers must read from any angle (fixed 65° top-down camera, plus the
        // RenderTexture QA capture) regardless of Systems/ArenaEnvironmentSystem.BuildRing's triangle
        // winding, so backface culling is disabled here (_Cull=Off) rather than relying on the winding
        // order alone.
        private static void ApplyRingMaterial(Renderer renderer, string hexColor)
        {
            Material material = CreateUrpSafeMaterial(hexColor, renderer.gameObject.name);
            if (material == null)
            {
                return;
            }
            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            }
            renderer.sharedMaterial = material;
        }

        private static Material CreateUrpSafeMaterial(string hexColor, string ownerName)
        {
            if (!ColorUtility.TryParseHtmlString(hexColor, out Color color))
            {
                Debug.LogError("[Wiring] arena environment material skipped: GameConfig hex parse failed for '" + ownerName + "' (hex='" + hexColor + "').");
                return null;
            }
            // Ship-review dedup: shared URP/Lit→Standard resolution (Components/UrpShaderUtil — the
            // fallback-attempt LogWarning lives there, wording preserved; the terminal error stays here).
            Shader shader = UrpShaderUtil.FindLitOrStandard(
                "[Wiring] URP/Lit shader not found; falling back to Standard (pink InternalErrorShader risk under URP).");
            if (shader == null)
            {
                Debug.LogError("[Wiring] arena environment material skipped: neither URP/Lit nor Standard shader found for '" + ownerName + "'.");
                return null;
            }
            return new Material(shader) { color = color };
        }
    }
}
