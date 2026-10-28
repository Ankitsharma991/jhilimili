using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UtsavNights.Content;

namespace UtsavNights.EditorTools.Main
{
    /// <summary>
    /// Renders the Main scene to PNGs for art reviews (Phase 5 visual pass), without a device and without saving
    /// the scene: the game camera's view, the same view with every cracker lined up on the ground, and a close
    /// look at the crackers. Needs a GPU (batch mode without -nographics):
    ///   Unity -batchmode -projectPath . -executeMethod UtsavNights.EditorTools.Main.SceneShots.CiCapture
    /// Output: captures/visual/*.png (not committed).
    /// </summary>
    public static class SceneShots
    {
        public const string Folder = "captures/visual";
        private const int Width = 1080;
        private const int Height = 1920;

        [MenuItem("Utsav/Phase 2/Capture Scene Shots", priority = 82)]
        public static void Capture()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[SceneShots] No GPU (-nographics); nothing rendered.");
                return;
            }

            EditorSceneManager.OpenScene(MainSceneBuilder.MainScenePath, OpenSceneMode.Single);
            Camera camera = Camera.main;

            // The placement ghost hides itself when the game starts; hide it here too.
            GameObject.Find("PlacementGhost")?.SetActive(false);
            Directory.CreateDirectory(Folder);
            Render(camera, "1-scene");

            var lineup = new GameObject("Lineup");
            CrackerCatalog catalog = AssetDatabase.LoadAssetAtPath<CrackerCatalog>(
                AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:" + nameof(CrackerCatalog))[0]));
            var definitions = new System.Collections.Generic.List<CrackerDefinition>();
            foreach (CrackerCatalog.Entry entry in catalog.Entries)
            {
                if (entry?.definition != null)
                {
                    definitions.Add(entry.definition);
                }
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i].ModelPrefab == null)
                {
                    continue;
                }

                // Two rows across the ground in front of the camera.
                int row = i < 5 ? 0 : 1;
                int col = row == 0 ? i : i - 5;
                int cols = row == 0 ? 5 : definitions.Count - 5;
                float x = (col - (cols - 1) * 0.5f) * 0.32f;
                GameObject model = Object.Instantiate(definitions[i].ModelPrefab, lineup.transform);
                model.transform.position = new Vector3(x, 0f, row == 0 ? 0.25f : 0.75f);
                model.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
            }

            Render(camera, "2-lineup");

            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float fov = camera.fieldOfView;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 0.55f, -0.75f), Quaternion.Euler(24f, 0f, 0f));
            camera.fieldOfView = 50f;
            Render(camera, "3-closeup", 1600, 1200);
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fov;
            Debug.Log($"[SceneShots] saved to {Folder}");
        }

        public static void CiCapture()
        {
            Capture();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// One shot per cracker mid-show: every emitter that has started by half a second after the last burst
        /// begins, placed at its anchor (base, fuse or apex) and simulated to that moment. Aerials are framed
        /// looking up at the burst. Spin and launch motion are not simulated, so spinners and trails look static.
        /// </summary>
        [MenuItem("Utsav/Phase 2/Capture Effect Shots", priority = 83)]
        public static void CaptureEffects()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[SceneShots] No GPU (-nographics); nothing rendered.");
                return;
            }

            EditorSceneManager.OpenScene(MainSceneBuilder.MainScenePath, OpenSceneMode.Single);
            GameObject.Find("PlacementGhost")?.SetActive(false);
            Camera camera = Camera.main;
            Vector3 home = camera.transform.position;
            Quaternion homeRotation = camera.transform.rotation;
            Directory.CreateDirectory(Folder);
            CrackerCatalog catalog = AssetDatabase.LoadAssetAtPath<CrackerCatalog>(
                AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:" + nameof(CrackerCatalog))[0]));
            foreach (CrackerCatalog.Entry entry in catalog.Entries)
            {
                CrackerDefinition def = entry?.definition;
                if (def == null || def.ModelPrefab == null)
                {
                    continue;
                }

                var stage = new GameObject("EffectStage");
                Vector3 basePosition = new Vector3(0f, 0f, 1.0f);
                GameObject model = Object.Instantiate(def.ModelPrefab, stage.transform);
                model.transform.position = basePosition;

                float showTime = 0f;
                foreach (StageDefinition s in def.Stages)
                {
                    if (s.emitter != null && !IsSecondary(s.emitter.name))
                    {
                        showTime = Mathf.Max(showTime, s.time);
                    }
                }

                showTime += 0.5f;
                Vector3 focus = basePosition + Vector3.up * 0.8f;
                foreach (StageDefinition s in def.Stages)
                {
                    if (s.emitter == null || s.time > showTime)
                    {
                        continue;
                    }

                    Vector3 at = s.anchor == StageAnchor.Apex ? basePosition + Vector3.up * s.launchHeight + s.launchDrift
                        : s.anchor == StageAnchor.Fuse ? basePosition + def.FuseAnchor
                        : basePosition;
                    if (s.anchor == StageAnchor.Apex)
                    {
                        focus = at;
                        model.SetActive(!s.hideModel);
                    }

                    ParticleSystem fx = Object.Instantiate(s.emitter, at, s.emitter.transform.rotation, stage.transform);
                    fx.Simulate(showTime - s.time, true, true);
                }

                camera.transform.SetPositionAndRotation(home, homeRotation);
                if (focus.y > 3f)
                {
                    // Aerials: tilt up so the burst sits in the upper middle, as the camera rig does in play.
                    camera.transform.LookAt(Vector3.Lerp(basePosition, focus, 0.75f));
                }

                Render(camera, "fx-" + def.Id, 720, 1280);
                Object.DestroyImmediate(stage);
            }

            // The empty sky as the aerial follow sees it (looking about 60 degrees up), to judge the panorama alone.
            camera.transform.SetPositionAndRotation(home + Vector3.up * 0.6f, Quaternion.Euler(-60f, 0f, 0f));
            Render(camera, "fx-sky-up", 720, 1280);
            camera.transform.SetPositionAndRotation(home, homeRotation);
            Debug.Log($"[SceneShots] effect shots saved to {Folder}");
        }

        public static void CiCaptureEffects()
        {
            CaptureEffects();
            EditorApplication.Exit(0);
        }

        /// <summary>Smoke and fuse sparks don't decide when the show peaks.</summary>
        private static bool IsSecondary(string emitterName) => emitterName.Contains("Smoke") || emitterName.Contains("FuseSpark");

        private static void Render(Camera camera, string name, int width = Width, int height = Height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            camera.aspect = (float)width / height;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            camera.ResetAspect();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
