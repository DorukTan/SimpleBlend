using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SimpleBlend.Editor.Tests
{
    public static class TeachingSampleAuthoring
    {
        public static void Generate()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Author samples in a dedicated batch process.");
            var args = Environment.GetCommandLineArgs();
            var argument = Array.IndexOf(args, "-simpleBlendSampleRoot");
            if (argument < 0 || argument + 1 >= args.Length)
                throw new ArgumentException("Supply -simpleBlendSampleRoot Assets/__SimpleBlendSamples_<guid>.");
            var root = args[argument + 1];
            const string prefix = "Assets/__SimpleBlendSamples_";
            if (!root.StartsWith(prefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(root.Substring(prefix.Length), "N", out _)
                || AssetDatabase.IsValidFolder(root))
                throw new ArgumentException("Use a new GUID-owned sample authoring folder.");
            AssetDatabase.CreateFolder("Assets", root.Substring("Assets/".Length));
            var startup = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(startup.path))
            {
                if (startup.isDirty || startup.rootCount != 0 || SceneManager.sceneCount != 1)
                    throw new InvalidOperationException("The authoring batch must start with a saved or clean empty scene.");
                EditorSceneManager.SaveScene(startup, root + "/Bootstrap.unity");
            }
            Create(root, "RockMoss", 0);
            Create(root, "RoofSnow", 1);
            Create(root, "WallGrime", 2);
            Debug.Log("Teaching samples authored: " + root);
        }

        private static void Create(string root, string name, int kind)
        {
            AssetDatabase.CreateFolder(root, name);
            var folder = root + "/" + name;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var mesh = CreateMesh(kind);
                AssetDatabase.CreateAsset(mesh, folder + "/Surface.asset");
                var material = new Material(Shader.Find("SimpleBlend/Layer Blend 3")) { name = name };
                var palette = kind == 0
                    ? new[] { new Color(0.35f,0.32f,0.28f), new Color(0.16f,0.38f,0.08f), new Color(0.13f,0.16f,0.09f) }
                    : kind == 1
                    ? new[] { new Color(0.45f,0.17f,0.09f), new Color(0.9f,0.94f,1), new Color(0.2f,0.35f,0.45f) }
                    : new[] { new Color(0.65f,0.52f,0.37f), new Color(0.15f,0.12f,0.08f), new Color(0.12f,0.25f,0.1f) };
                for (var layer = 0; layer < 3; layer++)
                {
                    var albedo = Texture(name + " Albedo " + layer, false,
                        (x,y) => palette[layer] * (0.8f + 0.2f * Pattern(x,y,kind + layer)));
                    var height = Texture(name + " Height " + layer, true,
                        (x,y) => new Color(0.05f * Pattern(x,y,kind + layer), 0, 0, 1));
                    AssetDatabase.CreateAsset(albedo, folder + "/Albedo" + layer + ".asset");
                    AssetDatabase.CreateAsset(height, folder + "/Height" + layer + ".asset");
                    material.SetTexture("_Layer" + layer + "Albedo", albedo);
                    material.SetTexture("_Layer" + layer + "Height", height);
                    material.SetFloat("_Layer" + layer + "Smoothness", layer == 2 ? 0.45f : 0.2f);
                    material.SetFloat("_Layer" + layer + "Metallic", 0);
                }
                material.SetFloat("_BlendTransition", 0.3f);
                AssetDatabase.CreateAsset(material, folder + "/Blend.mat");
                var surface = new GameObject(name + " — select to paint");
                surface.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = surface.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var paint = surface.AddComponent<VertexPaintData>();
                var result = VertexPaintDataAuthoring.Initialize(paint, renderer, false);
                if (!result.IsValid) throw new InvalidOperationException(result.Failure.ToString());
                var colors = mesh.vertices.Select(v => Paint(kind, v)).ToArray();
                paint.ReplaceColors(colors);
                if (!VertexPaintDataAuthoring.Apply(paint).IsValid)
                    throw new InvalidOperationException("Could not apply teaching paint.");
                EditorUtility.SetDirty(paint);
                var camera = new GameObject("Sample Camera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.transform.position = kind == 2 ? new Vector3(5,3,-7) : new Vector3(5,4,-7);
                camera.transform.LookAt(new Vector3(0,1,0));
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.07f,0.09f,0.12f,1);
                var light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(45,-35,0);
                light.intensity = 1;
                var caption = new GameObject("Channel mapping and brush starting points").AddComponent<TextMesh>();
                caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                caption.GetComponent<MeshRenderer>().sharedMaterial = caption.font.material;
                caption.fontSize = 36;
                caption.characterSize = 0.045f;
                caption.anchor = TextAnchor.UpperCenter;
                caption.alignment = TextAlignment.Center;
                caption.transform.position = new Vector3(0,-0.5f,0);
                caption.transform.rotation = camera.transform.rotation;
                var mapping = kind == 0 ? "Base: rock | Red: moss | Green: damp patches"
                    : kind == 1 ? "Base: roof | Red: snow | Green: wet roof"
                    : "Base: wall | Red: grime | Green: moss";
                caption.text = name + "\n" + mapping
                    + "\nPaint Red: radius 0.45m, strength 0.35, flow 1, accumulate on."
                    + "\nFalloff: EaseInOut; try Erase and Smooth; alpha is unused."
                    + "\nStarting paint and textures are procedural. See README.";
                AssetDatabase.SaveAssetIfDirty(material);
                EditorSceneManager.SaveScene(scene, folder + "/" + name + ".unity");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Texture2D Texture(string name, bool linear, Func<int,int,Color> pixel)
        {
            var texture = new Texture2D(16,16,TextureFormat.RGBA32,true,linear) { name = name };
            for (var y=0;y<16;y++) for (var x=0;x<16;x++) texture.SetPixel(x,y,pixel(x,y));
            texture.Apply();
            return texture;
        }

        private static float Pattern(int x, int y, int seed) =>
            0.5f + 0.25f * Mathf.Sin(x * 1.7f + seed) + 0.25f * Mathf.Cos(y * 1.3f + seed * 2);

        private static Color32 Paint(int kind, Vector3 v)
        {
            var detail = 0.5f + 0.5f * Mathf.Sin(v.x * 4 + v.z * 3 + kind);
            var red = kind == 0 ? Mathf.Clamp01((v.y - 0.8f) * 0.65f) * (0.7f + detail * 0.3f)
                : kind == 1 ? Mathf.Clamp01(0.2f + (v.x + 2) * 0.15f + detail * 0.15f)
                : Mathf.Clamp01((1.2f - v.y) * 0.5f + detail * 0.15f);
            var green = (1 - red) * (kind == 2 ? 0.2f * Mathf.Clamp01(1 - v.y) : detail * 0.08f);
            return new Color(red,green,0,0);
        }

        private static Mesh CreateMesh(int kind)
        {
            const int columns = 33;
            const int rows = 25;
            var vertices = new Vector3[columns*rows + (kind == 0 ? 2 : 0)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[(columns-1)*(rows-1)*6 + (kind == 0 ? (columns-1)*6 : 0)];
            for (var y=0;y<rows;y++) for (var x=0;x<columns;x++)
            {
                var u=x/(float)(columns-1);
                var v=y/(float)(rows-1);
                var index=y*columns+x;
                uv[index]=new Vector2(u,v);
                if (kind == 0)
                {
                    var theta=u*Mathf.PI*2;
                    var phi=0.02f+v*(Mathf.PI-0.04f);
                    var radius=1+0.12f*Mathf.Sin(theta*3)*Mathf.Sin(phi*4);
                    vertices[index]=new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta)*radius*1.5f,
                        1+Mathf.Cos(phi)*radius, Mathf.Sin(phi)*Mathf.Sin(theta)*radius);
                }
                else if (kind == 1)
                    vertices[index]=new Vector3((u-0.5f)*4,1+1.2f*(1-Mathf.Abs(u-0.5f)*2),(v-0.5f)*3);
                else vertices[index]=new Vector3((u-0.5f)*4,v*3,0);
            }
            var cursor=0;
            for(var y=0;y<rows-1;y++) for(var x=0;x<columns-1;x++)
            {
                var a=y*columns+x;
                triangles[cursor++]=a; triangles[cursor++]=a+columns; triangles[cursor++]=a+1;
                triangles[cursor++]=a+1; triangles[cursor++]=a+columns; triangles[cursor++]=a+columns+1;
            }
            if (kind == 0)
            {
                for (var index = 0; index < cursor; index += 3)
                {
                    var swap = triangles[index + 1];
                    triangles[index + 1] = triangles[index + 2];
                    triangles[index + 2] = swap;
                }
                var top = columns * rows;
                var bottom = top + 1;
                vertices[top] = new Vector3(0,2.02f,0);
                vertices[bottom] = new Vector3(0,-0.02f,0);
                uv[top] = new Vector2(0.5f,0);
                uv[bottom] = new Vector2(0.5f,1);
                for (var x = 0; x < columns - 1; x++)
                {
                    triangles[cursor++] = top; triangles[cursor++] = x+1; triangles[cursor++] = x;
                    triangles[cursor++] = bottom; triangles[cursor++] = (rows-1)*columns+x;
                    triangles[cursor++] = (rows-1)*columns+x+1;
                }
            }
            var mesh=new Mesh { name="Teaching Surface",vertices=vertices,uv=uv,triangles=triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
