using NUnit.Framework;
using UnityEngine;

namespace SimpleBlend.Editor.Tests
{
    internal sealed class TerrainContractTests
    {
        [Test]
        public void First_four_layers_are_available_through_public_terrain_data()
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = 33,
                alphamapResolution = 16,
                size = new Vector3(120f, 25f, 80f)
            };
            var layers = new TerrainLayer[4];
            var textures = new Texture2D[12];
            GameObject terrainObject = null;

            try
            {
                for (var index = 0; index < layers.Length; index++)
                {
                    var textureOffset = index * 3;
                    textures[textureOffset] = NewTexture($"Diffuse {index}");
                    textures[textureOffset + 1] = NewTexture($"Normal {index}");
                    textures[textureOffset + 2] = NewTexture($"Mask {index}");
                    layers[index] = new TerrainLayer
                    {
                        diffuseTexture = textures[textureOffset],
                        normalMapTexture = textures[textureOffset + 1],
                        maskMapTexture = textures[textureOffset + 2],
                        tileSize = new Vector2(8f + index, 12f + index),
                        tileOffset = new Vector2(index, -index),
                        normalScale = 0.5f + index * 0.1f,
                        metallic = index * 0.1f,
                        smoothness = 0.25f + index * 0.1f
                    };
                }

                terrainData.terrainLayers = layers;
                var weights = new float[16, 16, 4];
                for (var y = 0; y < 16; y++)
                {
                    for (var x = 0; x < 16; x++)
                    {
                        weights[y, x, (x + y) % 4] = 1f;
                    }
                }

                terrainData.SetAlphamaps(0, 0, weights);
                Assert.That(terrainData.alphamapTextureCount, Is.EqualTo(1));
                Assert.That(terrainData.alphamapTextures, Has.Length.EqualTo(1));
                Assert.That(
                    terrainData.GetAlphamapTexture(0),
                    Is.SameAs(terrainData.alphamapTextures[0]));
                Assert.That(terrainData.terrainLayers, Has.Length.EqualTo(4));

                for (var index = 0; index < layers.Length; index++)
                {
                    Assert.That(terrainData.terrainLayers[index].diffuseTexture,
                        Is.SameAs(textures[index * 3]));
                    Assert.That(terrainData.terrainLayers[index].normalMapTexture,
                        Is.SameAs(textures[index * 3 + 1]));
                    Assert.That(terrainData.terrainLayers[index].maskMapTexture,
                        Is.SameAs(textures[index * 3 + 2]));
                    Assert.That(terrainData.terrainLayers[index].tileSize,
                        Is.EqualTo(new Vector2(8f + index, 12f + index)));
                    Assert.That(terrainData.terrainLayers[index].tileOffset,
                        Is.EqualTo(new Vector2(index, -index)));
                }

                terrainObject = Terrain.CreateTerrainGameObject(terrainData);
                terrainObject.transform.position = new Vector3(14f, 3f, -9f);
                var terrain = terrainObject.GetComponent<Terrain>();
                Assert.That(terrain.terrainData, Is.SameAs(terrainData));
                Assert.That(terrain.transform.position, Is.EqualTo(new Vector3(14f, 3f, -9f)));
                Assert.That(terrainData.GetInterpolatedNormal(0.5f, 0.5f).y, Is.GreaterThan(0f));
            }
            finally
            {
                if (terrainObject != null)
                {
                    Object.DestroyImmediate(terrainObject);
                }

                foreach (var layer in layers)
                {
                    if (layer != null)
                    {
                        Object.DestroyImmediate(layer);
                    }
                }

                foreach (var texture in textures)
                {
                    if (texture != null)
                    {
                        Object.DestroyImmediate(texture);
                    }
                }

                Object.DestroyImmediate(terrainData);
            }
        }

        [Test]
        public void Fifth_layer_requires_a_second_control_texture()
        {
            var terrainData = new TerrainData
            {
                heightmapResolution = 33,
                alphamapResolution = 16
            };
            var layers = new TerrainLayer[5];

            try
            {
                for (var index = 0; index < layers.Length; index++)
                {
                    layers[index] = new TerrainLayer();
                }

                terrainData.terrainLayers = layers;
                var weights = new float[16, 16, 5];
                for (var y = 0; y < 16; y++)
                {
                    for (var x = 0; x < 16; x++)
                    {
                        weights[y, x, 4] = 1f;
                    }
                }

                terrainData.SetAlphamaps(0, 0, weights);
                Assert.That(terrainData.alphamapTextureCount, Is.EqualTo(2));
                Assert.That(terrainData.alphamapTextures, Has.Length.EqualTo(2));
            }
            finally
            {
                foreach (var layer in layers)
                {
                    if (layer != null)
                    {
                        Object.DestroyImmediate(layer);
                    }
                }

                Object.DestroyImmediate(terrainData);
            }
        }

        private static Texture2D NewTexture(string name)
        {
            return new Texture2D(2, 2)
            {
                name = name
            };
        }
    }
}
