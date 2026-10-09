using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimpleBlend.Editor.Tests
{
    public static class TerrainBlendReferenceFixture
    {
        // The caller owns an empty asset folder and decides whether to retain or remove it.
        public static string Create(string assetRoot)
        {
            if (!AssetDatabase.IsValidFolder(assetRoot)
                || AssetDatabase.FindAssets(string.Empty, new[] { assetRoot }).Length != 0)
                throw new ArgumentException("Use an existing empty asset folder.", nameof(assetRoot));
            var previous = SceneManager.GetActiveScene();
            for (var index = 0; index < SceneManager.sceneCount; index++)
                if (string.IsNullOrEmpty(SceneManager.GetSceneAt(index).path))
                    throw new InvalidOperationException("Save or close untitled scenes before creating a reference fixture. No scene was changed.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var data = new TerrainData
                {
                    heightmapResolution = 33, alphamapResolution = 16, size = new Vector3(16, 2, 16)
                };
                var layers = new TerrainLayer[4];
                var colors = new[] { new Color(0.2f, 0.4f, 0.15f), new Color(0.6f, 0.35f, 0.15f),
                    new Color(0.25f, 0.3f, 0.5f), new Color(0.6f, 0.6f, 0.6f) };
                for (var i = 0; i < 4; i++)
                {
                    var texture = new Texture2D(8, 8, TextureFormat.RGBA32, true, true);
                    for (var y = 0; y < 8; y++)
                        for (var x = 0; x < 8; x++)
                            texture.SetPixel(x, y, colors[i] * ((x + y) % 2 == 0 ? 1f : 0.7f));
                    texture.Apply();
                    AssetDatabase.CreateAsset(texture, assetRoot + "/TerrainDiffuse" + i + ".asset");
                    layers[i] = new TerrainLayer
                    {
                        diffuseTexture = texture, tileSize = new Vector2(4 + i, 3 + i),
                        tileOffset = new Vector2(i * 0.5f, -i * 0.25f),
                        smoothness = 0.2f, smoothnessSource = TerrainLayerSmoothnessSource.Constant
                    };
                    AssetDatabase.CreateAsset(layers[i], assetRoot + "/Layer" + i + ".terrainlayer");
                }
                data.terrainLayers = layers;
                var weights = new float[16, 16, 4];
                for (var y = 0; y < 16; y++)
                    for (var x = 0; x < 16; x++)
                    {
                        var horizontal = x / 15f;
                        var vertical = y / 15f;
                        weights[y, x, 0] = (1 - horizontal) * (1 - vertical);
                        weights[y, x, 1] = horizontal * (1 - vertical);
                        weights[y, x, 2] = (1 - horizontal) * vertical;
                        weights[y, x, 3] = horizontal * vertical;
                    }
                data.SetAlphamaps(0, 0, weights);
                AssetDatabase.CreateAsset(data, assetRoot + "/TerrainData.asset");
                var terrainObject = Terrain.CreateTerrainGameObject(data);
                SceneManager.MoveGameObjectToScene(terrainObject, scene);
                terrainObject.name = "Reference Terrain";
                terrainObject.transform.position = new Vector3(14, 2, -9);
                var terrain = terrainObject.GetComponent<Terrain>();
                var sourceMaterial = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                AssetDatabase.CreateAsset(sourceMaterial, assetRoot + "/Terrain.mat");
                terrain.materialTemplate = sourceMaterial;
                terrain.drawInstanced = false;

                var mesh = CreateStrip();
                AssetDatabase.CreateAsset(mesh, assetRoot + "/BlendStrip.asset");
                var material = new Material(Shader.Find("SimpleBlend/Terrain Blend")) { enableInstancing = true };
                AssetDatabase.CreateAsset(material, assetRoot + "/TerrainBlend.mat");
                AssetDatabase.SaveAssetIfDirty(data);
                if (!TerrainMaterialBinder.TryBind(terrain, material, out var reason))
                    throw new InvalidOperationException(reason);
                material.SetColor("_Layer1Color", new Color(0.7f, 0.1f, 0.05f));
                material.SetColor("_Layer2Color", new Color(0.1f, 0.7f, 0.15f));
                material.SetColor("_Layer3Color", new Color(0.1f, 0.2f, 0.8f));
                var strip = new GameObject("Terrain Seam and RGB Overlay Strip");
                SceneManager.MoveGameObjectToScene(strip, scene);
                strip.transform.position = terrainObject.transform.position + Vector3.up * 0.02f;
                strip.AddComponent<MeshFilter>().sharedMesh = mesh;
                strip.AddComponent<MeshRenderer>().sharedMaterial = material;

                var light = new GameObject("Directional Light").AddComponent<Light>();
                SceneManager.MoveGameObjectToScene(light.gameObject, scene);
                light.type = LightType.Directional;
                light.intensity = 1;
                light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(50, -30, 0);
                var camera = new GameObject("Reference Camera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.transform.position = new Vector3(38, 24, -20);
                camera.transform.LookAt(new Vector3(26, 2, -1));
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100;
                var path = assetRoot + "/TerrainBlendReference.unity";
                AssetDatabase.SaveAssetIfDirty(material);
                if (!EditorSceneManager.SaveScene(scene, path))
                    throw new InvalidOperationException("Could not save the terrain reference fixture.");
                return path;
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Mesh CreateStrip()
        {
            const int columns = 9;
            const int rows = 7;
            var vertices = new Vector3[columns * rows];
            var normals = new Vector3[vertices.Length];
            var tangents = new Vector4[vertices.Length];
            var uv = new Vector2[vertices.Length];
            var colors = new Color32[vertices.Length];
            var triangles = new int[(columns - 1) * (rows - 1) * 6];
            for (var z = 0; z < rows; z++)
                for (var x = 0; x < columns; x++)
                {
                    var index = z * columns + x;
                    var localX = 8 + x * 2;
                    vertices[index] = new Vector3(localX, 0, 2 + z * 2);
                    normals[index] = Vector3.up;
                    tangents[index] = new Vector4(1, 0, 0, -1);
                    uv[index] = new Vector2(localX / 16f, vertices[index].z / 16f);
                    var amount = Mathf.Clamp01((localX - 16) / 8f);
                    colors[index] = new Color(z < 2 ? amount : 0, z >= 2 && z < 4 ? amount : 0,
                        z >= 4 ? amount : 0, x / 8f);
                }
            var cursor = 0;
            for (var z = 0; z < rows - 1; z++)
                for (var x = 0; x < columns - 1; x++)
                {
                    var a = z * columns + x;
                    triangles[cursor++] = a;
                    triangles[cursor++] = a + columns;
                    triangles[cursor++] = a + 1;
                    triangles[cursor++] = a + 1;
                    triangles[cursor++] = a + columns;
                    triangles[cursor++] = a + columns + 1;
                }
            var mesh = new Mesh
            {
                name = "Terrain Blend Strip", vertices = vertices, normals = normals,
                tangents = tangents, uv = uv, colors32 = colors, triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
