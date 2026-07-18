// ArenaEnvironmentSystem — pure C# geometry for the arena floor's boundary/spawn-ring visual markers
// (gdd/art-bible.md「クリスタル・アリーナ環境の視覚表現方針」: 生成MDLを使わずUnityプリミティブ+マテリアル
// のみで構成、四方の敵を同時視認できる可読性を担保 — P-01; S-20). Engine-independent: no MonoBehaviour/
// scene API (rules/unity-code.md #3; Vector3/Mathf 等の値型のみ使用). Returns plain vertex/normal/triangle
// arrays that Components/ArenaEnvironment turns into an actual UnityEngine.Mesh — Mesh/MeshFilter/
// MeshRenderer construction itself is a Components-layer concern (mirrors how
// Systems/EnemyVisualMotionSystem returns plain Vector3/Quaternion math that Components/EnemyAgent applies
// to a Transform, and how Systems/ArenaCameraMath returns a plain pose that Components/ArenaCameraRig
// applies to a Camera's Transform).
using UnityEngine;

namespace ForgeGame.Systems
{
    public static class ArenaEnvironmentSystem
    {
        /// <summary>
        /// Builds a flat annulus (ring) mesh lying in the XZ plane at the given height, centered on the
        /// arena origin (gdd: 単一円形アリーナ, origin-centered — matches Components/ArenaCameraRig's
        /// ArenaCenter=Vector3.zero assumption). <paramref name="radius"/> is the ring's centerline;
        /// <paramref name="width"/> is split evenly to either side (inner/outer radius). Used for both the
        /// arena boundary (radius=ARENA_RADIUS) and the spawn ring marker (radius=ENEMY_SPAWN_RADIUS).
        /// Vertex normals are explicitly set to Vector3.up rather than left to Mesh.RecalculateNormals, so
        /// the up-facing read doesn't depend on getting the triangle winding order exactly right —
        /// Components/ArenaEnvironment additionally disables backface culling on the ring material, so the
        /// ring is visible from any angle (including the fixed top-down camera, gdd 固定俯瞰カメラ)
        /// regardless of winding.
        /// </summary>
        public static void BuildRing(
            float radius, float width, float height, int segments,
            out Vector3[] vertices, out Vector3[] normals, out int[] triangles)
        {
            float innerRadius = radius - width * 0.5f;
            float outerRadius = radius + width * 0.5f;

            vertices = new Vector3[segments * 2];
            normals = new Vector3[segments * 2];
            triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                vertices[i * 2] = new Vector3(innerRadius * cos, height, innerRadius * sin);
                vertices[i * 2 + 1] = new Vector3(outerRadius * cos, height, outerRadius * sin);
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;
            }

            for (int i = 0; i < segments; i++)
            {
                int innerA = i * 2;
                int outerA = i * 2 + 1;
                int next = (i + 1) % segments;
                int innerB = next * 2;
                int outerB = next * 2 + 1;

                int t = i * 6;
                triangles[t] = innerA;
                triangles[t + 1] = outerA;
                triangles[t + 2] = innerB;

                triangles[t + 3] = outerA;
                triangles[t + 4] = outerB;
                triangles[t + 5] = innerB;
            }
        }
    }
}
