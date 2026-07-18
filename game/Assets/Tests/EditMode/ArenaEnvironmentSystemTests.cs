// ArenaEnvironmentSystemTests — S-20: アリーナ環境ビジュアル(境界/スポーンリング)の pure geometry.
// conventions.md §9: new pure Systems get EditMode coverage.
using ForgeGame.Systems;
using NUnit.Framework;
using UnityEngine;

namespace ForgeGame.Tests.EditMode
{
    public sealed class ArenaEnvironmentSystemTests
    {
        [Test]
        public void BuildRing_ProducesExpectedVertexAndTriangleCounts()
        {
            const int segments = 16;
            ArenaEnvironmentSystem.BuildRing(
                radius: 10f, width: 1f, height: 0.02f, segments: segments,
                out Vector3[] vertices, out Vector3[] normals, out int[] triangles);

            Assert.AreEqual(segments * 2, vertices.Length, "one inner + one outer vertex per segment");
            Assert.AreEqual(segments * 2, normals.Length);
            Assert.AreEqual(segments * 6, triangles.Length, "two triangles (6 indices) per segment quad");
        }

        [Test]
        public void BuildRing_AllVerticesLieAtRequestedHeight()
        {
            const float height = 0.03f;
            ArenaEnvironmentSystem.BuildRing(
                radius: 13.5f, width: 0.3f, height: height, segments: 24,
                out Vector3[] vertices, out _, out _);

            foreach (Vector3 v in vertices)
            {
                Assert.AreEqual(height, v.y, 1e-5f, "ring must be flat (constant Y) — lies on the XZ plane");
            }
        }

        [Test]
        public void BuildRing_VerticesFallWithinInnerAndOuterRadiusBounds()
        {
            const float radius = 12f;
            const float width = 0.4f;
            float innerRadius = radius - width * 0.5f;
            float outerRadius = radius + width * 0.5f;

            ArenaEnvironmentSystem.BuildRing(
                radius: radius, width: width, height: 0f, segments: 32,
                out Vector3[] vertices, out _, out _);

            foreach (Vector3 v in vertices)
            {
                float radial = new Vector2(v.x, v.z).magnitude;
                Assert.GreaterOrEqual(radial, innerRadius - 1e-4f);
                Assert.LessOrEqual(radial, outerRadius + 1e-4f);
            }
        }

        [Test]
        public void BuildRing_NormalsAllPointUp()
        {
            ArenaEnvironmentSystem.BuildRing(
                radius: 12f, width: 0.4f, height: 0.02f, segments: 8,
                out _, out Vector3[] normals, out _);

            foreach (Vector3 n in normals)
            {
                Assert.AreEqual(Vector3.up, n, "ring must read as an up-facing floor marker, not a wall");
            }
        }

        [Test]
        public void BuildRing_TriangleIndicesStayWithinVertexBounds()
        {
            const int segments = 20;
            ArenaEnvironmentSystem.BuildRing(
                radius: 12f, width: 0.4f, height: 0.02f, segments: segments,
                out Vector3[] vertices, out _, out int[] triangles);

            foreach (int index in triangles)
            {
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, vertices.Length, "triangle index must reference a valid vertex (no out-of-range index)");
            }
        }
    }
}
