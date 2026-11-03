using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Audio;
using UtsavNights.Core.Festivals;
using UtsavNights.Core.Runtime;
using UtsavNights.Core.Settings;
using UtsavNights.Core.Shows;
using UtsavNights.Gameplay;
using UtsavNights.Haptics;

namespace UtsavNights.UI
{
    /// <summary>
    /// The New Year countdown (Phase 11 step 1). On a New Year night a Countdown button sits left of Shows. Ten seconds
    /// count down in big numbers; at midnight the greeting shows, the Midnight show plays at the spot (when it fits
    /// there), a crowd cheers and the neighbourhood's sky fills with bursts for a while.
    /// </summary>
    public sealed class NewYearCountdown : MonoBehaviour
    {
        private RectTransform _launcher;
        private RectTransform _overlay;
        private TextMeshProUGUI _number;
        private TextMeshProUGUI _clock;
        private AudioClip _cheer;
        private float _elapsed = -1f;
        private bool _midnightDone;
        private int _shown = -1;

        public bool Running => _elapsed >= 0f;

        public bool AtMidnight => Running && MidnightCountdown.IsMidnight(_elapsed);

        /// <summary>The big text now: a number, the greeting, or empty.</summary>
        public string Shown => _number != null && _overlay.gameObject.activeSelf ? _number.text : string.Empty;

        public bool ShowPlayed { get; private set; }

        public RectTransform Launcher => _launcher;

        private void Awake() => Build();

        private void Start()
        {
            Game.Events.Subscribe<SettingsChangedEvent>(OnSettings);
            InputBlockers.Register(_launcher);
            Refresh(Game.Services.TryGet(out SettingsService settings) ? settings.Current.festival : null);
        }

        private void OnDestroy()
        {
            Game.Events.Unsubscribe<SettingsChangedEvent>(OnSettings);
            InputBlockers.Unregister(_launcher);
        }

        private void Update()
        {
            if (Running)
            {
                Advance(Time.unscaledDeltaTime);
            }
        }

        /// <summary>Same as tapping Countdown.</summary>
        public bool Begin()
        {
            if (Running || !Festivals.Find(Current()).Countdown)
            {
                return false;
            }

            _elapsed = 0f;
            _midnightDone = false;
            _shown = -1;
            ShowPlayed = false;
            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            _launcher.gameObject.SetActive(false);
            Advance(0f);
            return true;
        }

        /// <summary>Moves the countdown on (Update calls it; tests can skip time).</summary>
        public void Advance(float seconds)
        {
            if (!Running)
            {
                return;
            }

            _elapsed += seconds;
            _clock.text = MidnightCountdown.Clock(_elapsed);
            if (!MidnightCountdown.IsMidnight(_elapsed))
            {
                int n = MidnightCountdown.Shown(_elapsed);
                if (n != _shown)
                {
                    _shown = n;
                    _number.text = n.ToString();
                    _number.fontSize = 260f;
                    if (Game.Services.TryGet(out HapticsService haptics))
                    {
                        haptics.Play(Content.HapticPattern.Tick);
                    }
                }

                return;
            }

            if (!_midnightDone)
            {
                _midnightDone = true;
                Midnight();
            }

            if (_elapsed >= MidnightCountdown.Seconds + MidnightCountdown.GreetingSeconds)
            {
                _elapsed = -1f;
                _overlay.gameObject.SetActive(false);
                Refresh(Current());
            }
        }

        private void Midnight()
        {
            _number.text = Festivals.NewYear.Greeting;
            _number.fontSize = 140f;
            if (Game.Services.TryGet(out ShowPlayer player) && player.State == ShowPlayer.PlayerState.Idle && player.Check(ShowTemplates.NewYear).Count == 0)
            {
                ShowPlayed = player.Play(ShowTemplates.NewYear);
            }

            if (Game.Services.TryGet(out AudioService audio))
            {
                _cheer ??= MakeCheer();
                audio.Play(_cheer, 0.9f, SoundPriority.Payoff);
            }

            AmbientLife ambient = FindFirstObjectByType<AmbientLife>();
            if (ambient != null)
            {
                ambient.Surge(MidnightCountdown.SurgeSeconds, MidnightCountdown.SurgeBurstsPerMinute);
            }

            if (Game.Services.TryGet(out HapticsService haptics))
            {
                haptics.Play(Content.HapticPattern.Thud);
            }

            Game.Events.Publish(new MidnightEvent(ShowPlayed));
        }

        private static AudioClip MakeCheer()
        {
            const int rate = 44100;
            float[] data = CheerSound.Generate(rate, 2027);
            var clip = AudioClip.Create("new_year_cheer", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static string Current() => Game.Services.TryGet(out SettingsService settings) ? settings.Current.festival : Festivals.Default.Id;

        private void OnSettings(SettingsChangedEvent e) => Refresh(e.Settings.festival);

        private void Refresh(string festivalId) => _launcher.gameObject.SetActive(!Running && Festivals.Find(festivalId).Countdown);

        private void Build()
        {
            _launcher = UiKit.Rect("CountdownButton", transform);
            _launcher.anchorMin = _launcher.anchorMax = _launcher.pivot = new Vector2(1f, 0f);
            _launcher.sizeDelta = new Vector2(240f, UiTheme.MinTouch);
            _launcher.anchoredPosition = new Vector2(-32f - 280f - 20f - 220f - 20f, TrayView.TrayHeight + 150f); // left of Shows
            Image image = UiKit.Panel(_launcher, UiTheme.Saffron);
            var button = _launcher.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => Begin());
            UiKit.Text(_launcher, "Countdown", UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.Indigo);

            // The countdown shows over the sky and lets taps through, so crackers can still be lit at midnight.
            _overlay = UiKit.Rect("Countdown", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            RectTransform number = UiKit.Rect("Number", _overlay);
            number.anchorMin = new Vector2(0f, 0.55f);
            number.anchorMax = new Vector2(1f, 0.9f);
            number.offsetMin = number.offsetMax = Vector2.zero;
            _number = UiKit.Text(number, string.Empty, 260f, TextAlignmentOptions.Center, UiTheme.Marigold);
            _number.raycastTarget = false;
            RectTransform clock = UiKit.Rect("Clock", _overlay);
            clock.anchorMin = new Vector2(0f, 0.5f);
            clock.anchorMax = new Vector2(1f, 0.56f);
            clock.offsetMin = clock.offsetMax = Vector2.zero;
            _clock = UiKit.Text(clock, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
            _clock.raycastTarget = false;
            _overlay.gameObject.SetActive(false);
        }
    }
}
