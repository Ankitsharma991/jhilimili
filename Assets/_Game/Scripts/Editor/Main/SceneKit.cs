using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>Helpers for building blockout scenes and placeholder models from code.</summary>
    public static class SceneKit
    {
        public const string MaterialFolder = "Assets/_Game/Materials/Main";

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            if (!keepCollider)
            {
                Object.DestroyImmediate(go.GetComponent<Collider>());
            }

            return go;
        }

        public static void MakeStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }

        public static Material LitMaterial(string name, Color color, float smoothness)
        {
            Material m = LoadOrCreate(name);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material EmissiveMaterial(string name, Color color, float intensity)
        {
            Material m = LoadOrCreate(name);
            m.SetColor("_BaseColor", color);
            m.SetColor("_EmissionColor", color * intensity);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material LoadOrCreate(string name)
        {
            EditorAssetUtil.EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
