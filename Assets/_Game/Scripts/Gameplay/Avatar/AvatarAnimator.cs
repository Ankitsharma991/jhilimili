using UnityEngine;
using UtsavNights.Core.Avatar;

namespace UtsavNights.Gameplay
{
    /// <summary>
    /// Plays the procedural clips (<see cref="AvatarMotion"/>) on an <see cref="AvatarView"/>: idle or walk while nothing else plays,
    /// one-shot ignite and celebrate that return to it, and a 0.25 s crossfade between any two. Allocates nothing per frame.
    /// </summary>
    [RequireComponent(typeof(AvatarView))]
    public sealed class AvatarAnimator : MonoBehaviour
    {
        public const float FadeSeconds = 0.25f;

        private readonly AvatarPose _pose = new AvatarPose();
        private readonly AvatarPose _from = new AvatarPose();
        private readonly AvatarPose _target = new AvatarPose();
        private AvatarView _view;
        private float _time;
        private float _fade = 1f;
        private bool _walking;
        private bool _longHem;
        private AvatarProfile _hemProfile;

        public AvatarClip Clip { get; private set; } = AvatarClip.Idle;

        public float ClipTime => _time;

        /// <summary>Plays a clip from its start, fading from the current pose.</summary>
        public void Play(AvatarClip clip)
        {
            _from.CopyFrom(_pose);
            Clip = clip;
            _time = 0f;
            _fade = 0f;
        }

        /// <summary>Walking or standing while no one-shot plays.</summary>
        public void SetWalking(bool walking)
        {
            _walking = walking;
            AvatarClip loop = walking ? AvatarClip.Walk : AvatarClip.Idle;
            if (AvatarMotion.Loops(Clip) && Clip != loop)
            {
                Play(loop);
            }
        }

        /// <summary>Sets an exact pose at once (art captures and tests).</summary>
        public void Sample(AvatarClip clip, float time)
        {
            Clip = clip;
            _time = time;
            _fade = 1f;
            Tick(0f);
        }

        public void Tick(float deltaSeconds)
        {
            if (_view == null)
            {
                _view = GetComponent<AvatarView>();
            }

            _time += deltaSeconds;
            if (!AvatarMotion.Loops(Clip) && _time >= AvatarMotion.Length(Clip))
            {
                Play(_walking ? AvatarClip.Walk : AvatarClip.Idle);
            }

            AnimationStyle style = _view.Profile != null ? _view.Profile.animationStyle : AnimationStyle.Calm;
            if (!ReferenceEquals(_view.Profile, _hemProfile))
            {
                _hemProfile = _view.Profile;
                _longHem = _hemProfile != null && AvatarMotion.HasLongHem(_hemProfile);
            }

            AvatarMotion.Evaluate(Clip, _time, style, _target, _longHem);
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + deltaSeconds / FadeSeconds);
                _pose.CopyFrom(_from);
                _pose.BlendTowards(_target, Mathf.SmoothStep(0f, 1f, _fade));
            }
            else
            {
                _pose.CopyFrom(_target);
            }

            for (int i = 0; i < AvatarSkeleton.Count; i++)
            {
                _view.Bone(i).localRotation = Quaternion.Euler(_pose.X[i], _pose.Y[i], _pose.Z[i]);
            }

            V3 offset = _pose.HipsOffset;
            _view.Bone(AvatarSkeleton.Hips).localPosition = _view.HipsBase + new Vector3(offset.X, offset.Y, offset.Z);
        }

        private void LateUpdate() => Tick(Time.deltaTime);
    }
}
