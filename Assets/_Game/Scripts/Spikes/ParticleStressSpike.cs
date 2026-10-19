using System;
using UnityEngine;
using UnityEngine.Pool;

namespace UtsavNights.Spikes
{
    [Serializable]
    public struct EffectSlot
    {
        public string label;
        public ParticleSystem prefab;
        public Vector3 position;
        public float spinDegreesPerSecond;
    }

    /// <summary>
    /// Spike B / Gate A: runs five pooled Particle System effects (about 3,000 particles in total)
    /// plus one short-lived flash light. Effects are ticked centrally here (PRD 13.3), not per object.
    /// </summary>
    public sealed class ParticleStressSpike : MonoBehaviour
    {
        [SerializeField] private EffectSlot[] slots = Array.Empty<EffectSlot>();
        [SerializeField] private Light flashLight;
        [SerializeField] private float flashPeakIntensity = 6f;
        [SerializeField] private float flashIntervalSeconds = 2.2f;
        [SerializeField] private float flashDecayPerSecond = 4f;

        private ObjectPool<ParticleSystem>[] _pools;
        private ParticleSystem[] _active;
        private float _flashTimer;
        private float _flashLevel;

        public bool IsRunning { get; private set; }

        public int LiveParticles { get; private set; }

        public int PeakParticles { get; private set; }

        public int ActiveEffects { get; private set; }

        public int MaxParticlesBudget { get; private set; }

        public void Configure(EffectSlot[] effectSlots, Light flash)
        {
            slots = effectSlots;
            flashLight = flash;
        }

        private void Awake()
        {
            _pools = new ObjectPool<ParticleSystem>[slots.Length];
            _active = new ParticleSystem[slots.Length];
            int budget = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                _pools[i] = new ObjectPool<ParticleSystem>(
                    () => CreateInstance(index),
                    ps => ps.gameObject.SetActive(true),
                    ps =>
                    {
                        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        ps.gameObject.SetActive(false);
                    },
                    ps => Destroy(ps.gameObject),
                    false,
                    1,
                    2);
                if (slots[i].prefab != null)
                {
                    budget += slots[i].prefab.main.maxParticles;
                }
            }

            MaxParticlesBudget = budget;
            if (flashLight != null)
            {
                flashLight.intensity = 0f;
                flashLight.enabled = false;
            }
        }

        private ParticleSystem CreateInstance(int index)
        {
            EffectSlot slot = slots[index];
            ParticleSystem ps = Instantiate(slot.prefab, slot.position, slot.prefab.transform.rotation, transform);
            ps.gameObject.SetActive(false);
            return ps;
        }

        public void Toggle()
        {
            if (IsRunning)
            {
                StopAll();
            }
            else
            {
                StartAll();
            }
        }

        public void StartAll()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (_active[i] != null || slots[i].prefab == null)
                {
                    continue;
                }

                ParticleSystem ps = _pools[i].Get();
                ps.transform.SetPositionAndRotation(slots[i].position, slots[i].prefab.transform.rotation);
                ps.Play(true);
                _active[i] = ps;
            }

            IsRunning = true;
            _flashTimer = 0f;
        }

        public void StopAll()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (_active[i] == null)
                {
                    continue;
                }

                _pools[i].Release(_active[i]);
                _active[i] = null;
            }

            IsRunning = false;
        }

        public void ResetPeak()
        {
            PeakParticles = 0;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            int live = 0;
            int count = 0;
            for (int i = 0; i < _active.Length; i++)
            {
                ParticleSystem ps = _active[i];
                if (ps == null)
                {
                    continue;
                }

                count++;
                live += ps.particleCount;
                float spin = slots[i].spinDegreesPerSecond;
                if (spin != 0f)
                {
                    ps.transform.Rotate(0f, spin * dt, 0f, Space.World);
                }
            }

            LiveParticles = live;
            ActiveEffects = count;
            if (live > PeakParticles)
            {
                PeakParticles = live;
            }

            UpdateFlash(dt);
        }

        private void UpdateFlash(float dt)
        {
            if (flashLight == null)
            {
                return;
            }

            if (IsRunning)
            {
                _flashTimer -= dt;
                if (_flashTimer <= 0f)
                {
                    _flashTimer += flashIntervalSeconds;
                    _flashLevel = 1f;
                }
            }

            _flashLevel = Mathf.Max(0f, _flashLevel - dt * flashDecayPerSecond);
            bool on = _flashLevel > 0f;
            if (flashLight.enabled != on)
            {
                flashLight.enabled = on;
            }

            flashLight.intensity = _flashLevel * flashPeakIntensity;
        }
    }
}
