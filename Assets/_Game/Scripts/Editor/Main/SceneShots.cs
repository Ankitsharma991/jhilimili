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

        /// <summary>
        /// Phase 6 art review: the twelve preset avatars in a row in the courtyard, the faces close up, and one avatar at
        /// slim, average and broad. Output: captures/visual/avatars-*.png.
        /// </summary>
        [MenuItem("Utsav/Phase 6/Capture Avatar Shots", priority = 90)]
        public static void CaptureAvatars()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[SceneShots] No GPU (-nographics); nothing rendered.");
                return;
            }

            EditorSceneManager.OpenScene(MainSceneBuilder.MainScenePath, OpenSceneMode.Single);
            GameObject.Find("PlacementGhost")?.SetActive(false);
            Camera camera = Camera.main;
            Directory.CreateDirectory(Folder);
            var stage = new GameObject("AvatarStage").transform;
            var presets = Core.Avatar.AvatarPresets.All;
            for (int i = 0; i < presets.Count; i++)
            {
                int row = i / 6, col = i % 6;
                Gameplay.AvatarView view = Gameplay.AvatarView.Create(stage, presets[i], "Preset" + i);
                view.transform.position = new Vector3((col - 2.5f) * 0.62f, 0f, 1.2f + row * 1.3f);
                view.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // face the camera
            }

            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.25f, -2.6f), Quaternion.Euler(6f, 0f, 0f));
            camera.fieldOfView = 40f;
            Render(camera, "avatars-presets", 1920, 1080);

            // Faces: the first row's heads close up.
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.66f * 1.0f, 0.15f), Quaternion.identity);
            camera.fieldOfView = 42f;
            Render(camera, "avatars-faces", 1920, 720);
            Object.DestroyImmediate(stage.gameObject);

            // Body styles: the same avatar slim, average, broad, front and side.
            stage = new GameObject("AvatarStage").transform;
            float[] styles = { 0f, 1f, 2f };
            for (int i = 0; i < 6; i++)
            {
                Core.Avatar.AvatarProfile p = presets[0].Clone();
                p.bodyStyle = styles[i % 3];
                Gameplay.AvatarView view = Gameplay.AvatarView.Create(stage, p, "Body" + i);
                view.transform.position = new Vector3((i - 2.5f) * 0.62f, 0f, 1.2f);
                view.transform.rotation = Quaternion.Euler(0f, i < 3 ? 180f : 90f, 0f);
            }

            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.0f, -2.2f), Quaternion.Euler(2f, 0f, 0f));
            camera.fieldOfView = 44f;
            Render(camera, "avatars-bodies", 1920, 1080);
            Object.DestroyImmediate(stage.gameObject);
            // Poses: each clip at a telling moment, in three-quarter view; long skirts mid-stride to check the hem follows the legs.
            stage = new GameObject("AvatarStage").transform;
            var poses = new (int preset, Core.Avatar.AvatarClip clip, float time, Core.Avatar.AnimationStyle style, string extra)[]
            {
                (0, Core.Avatar.AvatarClip.Idle, 2f, Core.Avatar.AnimationStyle.Calm, null),
                (4, Core.Avatar.AvatarClip.Walk, 0.26f, Core.Avatar.AnimationStyle.Calm, null),
                (2, Core.Avatar.AvatarClip.Walk, 0.79f, Core.Avatar.AnimationStyle.Lively, null),
                (9, Core.Avatar.AvatarClip.Ignite, 0.9f, Core.Avatar.AnimationStyle.Calm, null),
                (5, Core.Avatar.AvatarClip.Celebrate, 0.6f, Core.Avatar.AnimationStyle.Calm, null),
                (10, Core.Avatar.AvatarClip.Celebrate, 0.5f, Core.Avatar.AnimationStyle.Lively, null),
                (3, Core.Avatar.AvatarClip.Walk, 0.26f, Core.Avatar.AnimationStyle.Lively, "lehenga"),
                (1, Core.Avatar.AvatarClip.Walk, 0.79f, Core.Avatar.AnimationStyle.Calm, "sari_nivi"),
                (6, Core.Avatar.AvatarClip.Walk, 0.26f, Core.Avatar.AnimationStyle.Lively, "abaya"),
                (1, Core.Avatar.AvatarClip.Ignite, 0.9f, Core.Avatar.AnimationStyle.Calm, "sari_nivi"),
            };
            for (int i = 0; i < poses.Length; i++)
            {
                Core.Avatar.AvatarProfile p = presets[poses[i].preset].Clone();
                p.animationStyle = poses[i].style;
                if (poses[i].extra != null)
                {
                    Core.Avatar.OutfitRules.Equip(p, Core.Avatar.WardrobeCatalog.Default, poses[i].extra, 0);
                }

                Gameplay.AvatarView view = Gameplay.AvatarView.Create(stage, p, "Pose" + i);
                view.transform.position = new Vector3((i - 4.5f) * 0.6f, 0f, 1.2f);
                view.transform.rotation = Quaternion.Euler(0f, 135f, 0f);
                view.gameObject.AddComponent<Gameplay.AvatarAnimator>().Sample(poses[i].clip, poses[i].time);
            }

            var poseLight = new GameObject("ReviewLight").AddComponent<Light>();
            poseLight.transform.SetParent(stage, false);
            poseLight.type = LightType.Directional;
            poseLight.transform.rotation = Quaternion.Euler(25f, 160f, 0f);
            poseLight.intensity = 1.1f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.1f, -3.2f), Quaternion.Euler(3f, 0f, 0f));
            camera.fieldOfView = 46f;
            Render(camera, "avatars-poses", 1920, 1080);
            Object.DestroyImmediate(stage.gameObject);

            // Close look at two clothed presets celebrating (sleeves, drapes and raised arms).
            stage = new GameObject("AvatarStage").transform;
            for (int i = 0; i < 2; i++)
            {
                Core.Avatar.AvatarProfile bare = presets[i == 0 ? 5 : 10].Clone();
                Gameplay.AvatarView view = Gameplay.AvatarView.Create(stage, bare, "Bare" + i);
                view.transform.position = new Vector3(i == 0 ? -0.5f : 0.5f, 0f, 1.2f);
                view.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                view.gameObject.AddComponent<Gameplay.AvatarAnimator>().Sample(Core.Avatar.AvatarClip.Celebrate, 0.6f);
            }

            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, -1.6f), Quaternion.Euler(0f, 0f, 0f));
            camera.fieldOfView = 55f;
            Render(camera, "avatars-joints", 1280, 1080);
            Object.DestroyImmediate(stage.gameObject);

            // Wardrobe: every garment on its own mannequin over a neutral ivory t-shirt and beige trousers, 10 per image.
            var catalog = Core.Avatar.WardrobeCatalog.Default.All;
            for (int page = 0; page * 10 < catalog.Count; page++)
            {
                stage = new GameObject("AvatarStage").transform;
                for (int i = 0; i < 10 && page * 10 + i < catalog.Count; i++)
                {
                    Core.Avatar.GarmentInfo garment = catalog[page * 10 + i];
                    Core.Avatar.AvatarProfile p = presets[(page * 10 + i) % presets.Count].Clone();
                    p.outfit.Clear();
                    Core.Avatar.OutfitRules.Equip(p, Core.Avatar.WardrobeCatalog.Default, "tshirt", 4);
                    Core.Avatar.OutfitRules.Equip(p, Core.Avatar.WardrobeCatalog.Default, "trousers", 1);
                    Core.Avatar.OutfitRules.Equip(p, Core.Avatar.WardrobeCatalog.Default, garment.Id, i);
                    Gameplay.AvatarView view = Gameplay.AvatarView.Create(stage, p, garment.Id);
                    view.transform.position = new Vector3((i - 4.5f) * 0.55f, 0f, 1.2f);
                    view.transform.rotation = Quaternion.Euler(0f, 160f, 0f);
                }

                var key = new GameObject("ReviewLight").AddComponent<Light>();
                key.transform.SetParent(stage, false);
                key.type = LightType.Directional;
                key.transform.rotation = Quaternion.Euler(25f, 160f, 0f);
                key.intensity = 1.1f;
                camera.transform.SetPositionAndRotation(new Vector3(0f, 1.1f, -3.2f), Quaternion.Euler(3f, 0f, 0f));
                camera.fieldOfView = 46f;
                Render(camera, "avatars-wardrobe-" + (page + 1), 1920, 1080);
                Object.DestroyImmediate(stage.gameObject);
            }

            Debug.Log($"[SceneShots] avatar shots saved to {Folder}");
        }

        public static void CiCaptureAvatars()
        {
            CaptureAvatars();
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
