using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UtsavNights.Content;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// Renders each cracker's model to a transparent 256 px tray icon (Assets/_Game/Art/Icons) and assigns it
    /// to the definition. Needs a GPU: in batch mode run without -nographics. Failures leave the icon empty
    /// (the tray then shows a marigold dot).
    /// </summary>
    public static class TrayIconRenderer
    {
        public const string Folder = "Assets/_Game/Art/Icons";
        private const int Size = 256;
        private static readonly Vector3 StagePosition = new Vector3(0f, -500f, 0f);

        public static void RenderAll(params CrackerDefinition[] definitions)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[Utsav setup] No GPU (-nographics); tray icons not rendered.");
                return;
            }

            EditorAssetUtil.EnsureFolder(Folder);
            foreach (CrackerDefinition def in definitions)
            {
                if (def != null && def.ModelPrefab != null)
                {
                    Render(def);
                }
            }

            AssetDatabase.SaveAssets();
        }

        private static void Render(CrackerDefinition def)
        {
            var root = new GameObject("IconStage");
            root.transform.position = StagePosition;
            GameObject model = Object.Instantiate(def.ModelPrefab, root.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, -25f, 0f);

            Bounds bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(r.bounds);
            }

            var camGo = new GameObject("IconCamera");
            camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.orthographic = false;
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 20f;
            float radius = Mathf.Max(bounds.extents.magnitude, 0.05f);
            float distance = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
            Vector3 direction = new Vector3(0f, 0.45f, -1f).normalized;
            cam.transform.position = bounds.center - direction * distance;
            cam.transform.LookAt(bounds.center);
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;

            var keyGo = new GameObject("Key");
            keyGo.transform.SetParent(root.transform, false);
            keyGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.92f, 0.82f);
            key.intensity = 1.6f;

            Color ambient = RenderSettings.ambientLight;
            AmbientMode ambientMode = RenderSettings.ambientMode;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.42f, 0.5f);

            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientMode = ambientMode;
            Object.DestroyImmediate(root);

            string path = $"{Folder}/Icon_{def.Id}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = Size;
            importer.SaveAndReimport();
            def.TrayIcon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(def);
        }
    }
}
