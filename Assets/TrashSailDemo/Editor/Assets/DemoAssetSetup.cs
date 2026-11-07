using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace TrashSailDemo.Editor
{
    public static class DemoAssetSetup
    {
        const string Root = "Assets/TrashSailDemo/Imported";
        const string Prefabs = Root + "/Resources/TrashSailModels";
        [Serializable] sealed class MaterialAudit
        {
            public string asset,shader,baseColorMap,normalMap,maskMap;
            public string[] keywords,meshes;
            public Color baseColor,emissiveColor;
            public bool validated,baseColorSrgb,allMeshesHaveUv0;
            public int renderQueue;
        }
        [Serializable] sealed class MaterialAuditReport { public List<MaterialAudit> materials = new List<MaterialAudit>(); }
        static MaterialAuditReport materialAudit;
        [MenuItem("Trash Sail Demo/Create or Update Imported Assets")]
        public static void CreateOrUpdateAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before importing models.");
            Directory.CreateDirectory(Prefabs);
            AssetDatabase.Refresh();
            materialAudit = new MaterialAuditReport();
            var report = new List<string> { "key,sourceBoundsXYZ_m,normalizedBoundsXYZ_m,scale,waterlineOffsetY_m" };
            foreach (string key in new[] { "plastic_bottle_gallon", "multi_cleaner_bottle", "cleaner_tin_01", "trashbag" })
                Create(key, Root + "/" + key + "/" + key + ".fbx", key == "trashbag" ? .55f : key == "cleaner_tin_01" ? .32f : .42f, Vector3.zero, .07f, report);
            Create("BarramundiFish", Root + "/BarramundiFish/BarramundiFish.fbx", .8f, new Vector3(0, 0, -90), 0, report);
            Create("marus_boat", "Assets/Models/marus_boat/marus_boat_model.fbx", 3, Vector3.zero, .4f, report);
            File.WriteAllLines(Root + "/normalized-bounds.csv", report);
            File.WriteAllText(Root + "/material-audit.json", JsonUtility.ToJson(materialAudit, true));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void Create(string key, string path, float longestMetres, Vector3 euler, float waterline, List<string> report)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new FileNotFoundException("Model not yet imported: " + path);
            var root = new GameObject(key);
            try
            {
                var visual = UnityEngine.Object.Instantiate(model, root.transform, false);
                visual.transform.localRotation = Quaternion.Euler(euler);
                foreach (Collider collider in visual.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
                foreach (Camera camera in visual.GetComponentsInChildren<Camera>()) UnityEngine.Object.DestroyImmediate(camera.gameObject);
                foreach (Light light in visual.GetComponentsInChildren<Light>()) UnityEngine.Object.DestroyImmediate(light.gameObject);
                var renderers = visual.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException("No imported mesh: " + path);
                Bounds initial = BoundsOf(renderers);
                float scale = longestMetres / Mathf.Max(initial.size.x, initial.size.y, initial.size.z);
                visual.transform.localScale *= scale;
                Bounds scaled = BoundsOf(renderers);
                visual.transform.localPosition -= scaled.center;
                visual.transform.localPosition += Vector3.up * waterline;
                Material material = MaterialFor(key);
                Mesh[] meshes = visual.GetComponentsInChildren<MeshFilter>().Select(filter => filter.sharedMesh)
                    .Concat(visual.GetComponentsInChildren<SkinnedMeshRenderer>().Select(renderer => renderer.sharedMesh))
                    .Where(mesh => mesh != null).ToArray();
                MaterialAudit audit = materialAudit.materials.Last();
                audit.meshes = meshes.Select(mesh => mesh.name + ": " + mesh.vertexCount + " vertices; UV0=" + mesh.HasVertexAttribute(VertexAttribute.TexCoord0)).ToArray();
                audit.allMeshesHaveUv0 = meshes.Length > 0 && meshes.All(mesh => mesh.HasVertexAttribute(VertexAttribute.TexCoord0));
                if (!audit.allMeshesHaveUv0) Debug.LogError("Imported asset lacks UV0: " + key);
                foreach (Renderer renderer in renderers)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                var lod = root.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.001f, renderers) });
                lod.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + key + ".prefab");
                report.Add(key + ",\"" + initial.size.ToString("F5") + "\",\"" + BoundsOf(renderers).size.ToString("F5") + "\"," + scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," + waterline.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static Bounds BoundsOf(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static string Find(string directory, string pattern)
            => Directory.Exists(directory) ? Directory.GetFiles(directory, pattern, SearchOption.AllDirectories).FirstOrDefault(file => !file.EndsWith(".meta"))?.Replace('\\', '/') : null;

        static Material MaterialFor(string key)
        {
            string directory = Root + "/" + key;
            Directory.CreateDirectory(directory);
            string path = directory + "/" + key + "_HDRP.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null) throw new InvalidOperationException("HDRP/Lit shader unavailable in host project.");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader;
            string diffuse;
            if (key == "marus_boat")
            {
                // Own the texture importer settings without mutating the installed MARUS asset.
                diffuse = directory + "/marus_boat_texture.png";
                if (!File.Exists(diffuse)) File.Copy("Assets/Models/marus_boat/marus_boat_texture.png", diffuse);
                AssetDatabase.ImportAsset(diffuse);
            }
            else diffuse = Find(directory, "*_diff_*") ?? Find(directory, "*BaseColor.png");
            string normal = Find(directory, "*_nor_gl_*") ?? Find(directory, "*Normal.png");
            string rough = Find(directory, "*_rough_*");
            string metallicRoughness = Find(directory, "*MetallicRoughness.png");
            var diffuseTexture = LoadTexture(diffuse, false, false);
            if (diffuseTexture == null) throw new InvalidOperationException("Missing diffuse texture for " + key);
            material.SetTexture("_BaseColorMap", diffuseTexture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", key == "cleaner_tin_01" ? .6f : 0);
            material.SetFloat("_Smoothness", key == "BarramundiFish" ? .55f : .25f);
            if (normal != null)
            {
                material.SetTexture("_NormalMap", LoadTexture(normal, true, false));
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
                material.EnableKeyword("_NORMALMAP");
            }
            if (rough != null || metallicRoughness != null)
            {
                Texture2D source = LoadTexture(rough ?? metallicRoughness, false, true);
                Color[] colors = source.GetPixels();
                for (int i = 0; i < colors.Length; i++)
                {
                    float metallic = rough == null ? colors[i].b : key == "cleaner_tin_01" ? .6f : 0;
                    float roughness = rough == null ? colors[i].g : colors[i].r;
                    colors[i] = new Color(metallic, 1, 1, 1 - roughness);
                }
                var mask = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
                mask.SetPixels(colors); mask.Apply();
                string maskPath = directory + "/" + key + "_HDRPMask.png";
                File.WriteAllBytes(maskPath, mask.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(mask);
                AssetDatabase.ImportAsset(maskPath);
                material.SetTexture("_MaskMap", LoadTexture(maskPath, false, true));
                material.EnableKeyword("_MASKMAP");
                material.SetFloat("_SmoothnessRemapMin", 0); material.SetFloat("_SmoothnessRemapMax", 1);
            }
            // HDRP runtime API maintains its derived stencil/pass/keyword state.
            // Setting textures and hand-enabling a subset of keywords is insufficient.
            bool validated = HDMaterial.ValidateMaterial(material);
            if (!validated) throw new InvalidOperationException("HDRP material validation failed: " + key);
            materialAudit.materials.Add(new MaterialAudit
            {
                asset = key, shader = material.shader.name, validated = validated,
                baseColorMap = AssetDatabase.GetAssetPath(material.GetTexture("_BaseColorMap")),
                normalMap = AssetDatabase.GetAssetPath(material.GetTexture("_NormalMap")),
                maskMap = AssetDatabase.GetAssetPath(material.GetTexture("_MaskMap")),
                baseColor = material.GetColor("_BaseColor"), emissiveColor = material.GetColor("_EmissiveColor"),
                baseColorSrgb = (AssetImporter.GetAtPath(diffuse) as TextureImporter)?.sRGBTexture ?? false,
                keywords = material.shaderKeywords, renderQueue = material.renderQueue
            });
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D LoadTexture(string path, bool normal, bool linear)
        {
            if (path == null) return null;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = !linear && !normal;
                importer.isReadable = linear;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}

