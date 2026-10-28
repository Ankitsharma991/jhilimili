using UnityEngine;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// A small photo studio for the avatar creator, far below the courtyard: the avatar idling on its own, two short-range
    /// lights that never reach the scene, and a camera that renders into a texture for the creator's preview. The main
    /// camera's far plane (50 m) never reaches it. Exists only while the creator is open.
    /// </summary>
    public sealed class AvatarPreviewStage : MonoBehaviour
    {
        public static readonly Vector3 Position = new Vector3(0f, -120f, 0f);

        private Camera _camera;
        private float _yaw = 180f;

        public AvatarView View { get; private set; }

        public RenderTexture Texture { get; private set; }

        public static AvatarPreviewStage Create(AvatarProfile profile, int width, int height)
        {
            var go = new GameObject("AvatarPreviewStage");
            go.transform.position = Position;
            var stage = go.AddComponent<AvatarPreviewStage>();
            stage.Build(profile, width, height);
            return stage;
        }

        public void Show(AvatarProfile profile) => View.Apply(profile);

        /// <summary>Turns the avatar by a drag, in degrees.</summary>
        public void Rotate(float degrees)
        {
            _yaw = (_yaw + degrees) % 360f;
            View.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        public float Yaw => _yaw;

        private void Build(AvatarProfile profile, int width, int height)
        {
            View = AvatarView.Create(transform, profile, "PreviewAvatar");
            View.gameObject.AddComponent<AvatarAnimator>();
            Rotate(0f);

            // No MSAA on the preview: URP would otherwise request its own sample count and clash with the texture's.
            Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "AvatarPreview", antiAliasing = 1 };
            var camGo = new GameObject("PreviewCamera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.05f, 2.9f);
            camGo.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
            _camera = camGo.AddComponent<Camera>();
            _camera.fieldOfView = 38f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 8f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.16f, 0.11f, 0.3f, 1f);
            _camera.allowMSAA = false;
            _camera.targetTexture = Texture;

            AddLight("Key", new Vector3(-1.2f, 2.4f, 2.2f), new Color(1f, 0.9f, 0.78f), 3.2f);
            AddLight("Fill", new Vector3(1.6f, 1.4f, 1.8f), new Color(0.75f, 0.8f, 1f), 1.4f);
            AddLight("Rim", new Vector3(0f, 2.2f, -1.5f), new Color(1f, 0.7f, 0.4f), 1.6f);
        }

        private void AddLight(string name, Vector3 local, Color color, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.color = color;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void OnDestroy()
        {
            if (_camera != null)
            {
                _camera.targetTexture = null;
            }

            if (Texture != null)
            {
                Texture.Release();
                Destroy(Texture);
            }
        }
    }
}
