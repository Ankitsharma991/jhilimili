using System.Collections.Generic;
using UnityEngine;
using UtsavNights.Content;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// The placement ghost (PRD 6.3, 9.1): a copy of the cracker's model tinted green or red, with a ring
    /// on the ground. Models are cached per definition so moving the ghost allocates nothing.
    /// </summary>
    public sealed class GhostPreview : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color ValidTint = new Color(0.45f, 1f, 0.55f);
        private static readonly Color InvalidTint = new Color(1f, 0.35f, 0.3f);

        [SerializeField] private Renderer ring;
        [SerializeField] private Material validRing;
        [SerializeField] private Material invalidRing;

        [Tooltip("Soft column showing an aerial cracker's sky corridor (PRD 10).")]
        [SerializeField] private Renderer beam;
        [SerializeField] private Material validBeam;
        [SerializeField] private Material invalidBeam;

        private readonly Dictionary<CrackerDefinition, GameObject> _models = new Dictionary<CrackerDefinition, GameObject>();
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private MaterialPropertyBlock _block;
        private GameObject _current;
        private bool? _shownValid;

        public bool IsVisible => gameObject.activeSelf;

        public void Configure(Renderer ringRenderer, Material valid, Material invalid)
        {
            ring = ringRenderer;
            validRing = valid;
            invalidRing = invalid;
        }

        public void ConfigureBeam(Renderer beamRenderer, Material valid, Material invalid)
        {
            beam = beamRenderer;
            validBeam = valid;
            invalidBeam = invalid;
        }

        public bool BeamVisible => beam != null && beam.gameObject.activeSelf;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();
            gameObject.SetActive(false);
        }

        public void Show(CrackerDefinition definition)
        {
            if (_current != null)
            {
                _current.SetActive(false);
            }

            _current = ModelFor(definition);
            if (_current != null)
            {
                _current.SetActive(true);
            }

            if (ring != null)
            {
                float size = Mathf.Max(definition.Footprint.x, definition.Footprint.y) * 1.6f;
                ring.transform.localScale = new Vector3(size, 0.004f, size);
            }

            if (beam != null)
            {
                // Aerials show their corridor: as wide and as tall as the effect volume they need clear.
                bool aerial = definition.IsAerial;
                beam.gameObject.SetActive(aerial);
                if (aerial)
                {
                    Vector3 v = definition.EffectVolume;
                    float width = Mathf.Max(0.2f, Mathf.Min(v.x, v.z));
                    beam.transform.localScale = new Vector3(width, v.y * 0.5f, width);
                    beam.transform.localPosition = new Vector3(0f, v.y * 0.5f, 0f);
                }
            }

            _shownValid = null;
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Move(Vector3 position, bool valid)
        {
            transform.position = position;
            if (_shownValid == valid)
            {
                return;
            }

            _shownValid = valid;
            if (ring != null)
            {
                ring.sharedMaterial = valid ? validRing : invalidRing;
            }

            if (beam != null && beam.gameObject.activeSelf)
            {
                beam.sharedMaterial = valid ? validBeam : invalidBeam;
            }

            if (_current != null)
            {
                _current.GetComponentsInChildren(true, _renderers);
                _block.SetColor(BaseColorId, valid ? ValidTint : InvalidTint);
                foreach (Renderer r in _renderers)
                {
                    r.SetPropertyBlock(_block);
                }
            }
        }

        private GameObject ModelFor(CrackerDefinition definition)
        {
            if (definition == null || definition.ModelPrefab == null)
            {
                return null;
            }

            if (!_models.TryGetValue(definition, out GameObject model))
            {
                model = Instantiate(definition.ModelPrefab, transform);
                model.name = "Ghost_" + definition.Id;
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
                {
                    Destroy(c);
                }

                _models.Add(definition, model);
            }

            return model;
        }
    }
}
