using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal static class TestMeshFactory
    {
        internal static Mesh CreateQuad(bool includeColors = false, bool includeSkinning = false)
        {
            var mesh = new Mesh
            {
                name = "SimpleBlend Test Quad",
                vertices = new[]
                {
                    new Vector3(-1f, 0f, -1f),
                    new Vector3(1f, 0f, -1f),
                    new Vector3(1f, 0f, 1f),
                    new Vector3(-1f, 0f, 1f)
                },
                normals = new[]
                {
                    Vector3.up,
                    Vector3.up,
                    Vector3.up,
                    Vector3.up
                },
                tangents = new[]
                {
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f),
                    new Vector4(1f, 0f, 0f, 1f)
                },
                uv = new[]
                {
                    new Vector2(0f, 0f),
                    new Vector2(1f, 0f),
                    new Vector2(1f, 1f),
                    new Vector2(0f, 1f)
                },
                subMeshCount = 2
            };
            mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            mesh.SetTriangles(new[] { 0, 2, 3 }, 1);

            if (includeColors)
            {
                mesh.colors32 = new[]
                {
                    new Color32(10, 20, 30, 40),
                    new Color32(50, 60, 70, 80),
                    new Color32(90, 100, 110, 120),
                    new Color32(130, 140, 150, 160)
                };
            }

            if (includeSkinning)
            {
                mesh.bindposes = new[] { Matrix4x4.identity };
                mesh.boneWeights = new[]
                {
                    SingleBoneWeight(),
                    SingleBoneWeight(),
                    SingleBoneWeight(),
                    SingleBoneWeight()
                };

                mesh.AddBlendShapeFrame(
                    "Raise",
                    100f,
                    new[]
                    {
                        Vector3.zero,
                        Vector3.up * 0.1f,
                        Vector3.up * 0.2f,
                        Vector3.zero
                    },
                    new Vector3[4],
                    new Vector3[4]);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        internal static Color32[] SolidColors(int count, Color32 color)
        {
            var colors = new Color32[count];
            for (var index = 0; index < colors.Length; index++)
            {
                colors[index] = color;
            }

            return colors;
        }

        private static BoneWeight SingleBoneWeight()
        {
            return new BoneWeight
            {
                boneIndex0 = 0,
                weight0 = 1f
            };
        }
    }
}
