using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Core.Explore;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// The player's profile (Phase 12 step 2), from Settings and the Explore pause menu: the avatar's name with Edit avatar,
    /// the title and level with progress to the next, coins and reputation, and what the player has done.
    /// </summary>
    public sealed class ProfileScreen : MonoBehaviour
    {
        private readonly Dictionary<string, TextMeshProUGUI> _values = new Dictionary<string, TextMeshProUGUI>();
        private RectTransform _overlay;
        private TextMeshProUGUI _name;
        private TextMeshProUGUI _title;
        private RectTransform _bar;
        private TextMeshProUGUI _next;
        private PlayerService _player;

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public string Name => _name != null ? _name.text : string.Empty;

        public string TitleLine => _title != null ? _title.text : string.Empty;

        /// <summary>A stat's shown value by its label ("Coins", "Explored", ...).</summary>
        public string Value(string label) => _values.TryGetValue(label, out TextMeshProUGUI t) ? t.text : string.Empty;

        private void Awake() => Build();

        private void OnDestroy()
        {
            if (_player != null)
            {
                _player.Changed -= Refresh;
            }

            if (_overlay != null)
            {
                InputBlockers.Unregister(_overlay);
            }
        }

        public void Open()
        {
            if (_player == null && Game.Services.TryGet(out _player))
            {
                _player.Changed += Refresh;
            }

            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            InputBlockers.Register(_overlay);
            Refresh();
        }

        public void Close()
        {
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        /// <summary>Same as tapping Edit avatar: the creator opens over the profile.</summary>
        public void EditAvatar()
        {
            Close();
            ExploreMode explore = FindFirstObjectByType<ExploreMode>();
            if (explore != null && explore.IsOpen)
            {
                explore.Exit(); // the creator is a HUD screen; Explore steps back for it
            }

            AvatarCreator creator = FindFirstObjectByType<AvatarCreator>(FindObjectsInactive.Include);
            if (creator != null)
            {
                creator.gameObject.SetActive(true);
                creator.Open();
            }
        }

        private void Refresh()
        {
            if (!IsOpen)
            {
                return;
            }

            PlayerRecord r = _player != null ? _player.Current : new PlayerRecord();
            string avatarName = Game.Services.TryGet(out ProfileService profiles) && profiles.Current.created ? profiles.Current.name : null;
            _name.text = string.IsNullOrWhiteSpace(avatarName) ? "Your avatar" : avatarName;
            int level = Reputation.Level(r.reputation);
            _title.text = $"Level {level} · {Reputation.Title(r.reputation)}";
            _bar.anchorMax = new Vector2(Mathf.Clamp01(Reputation.Progress(r.reputation)), 1f);
            int toNext = Reputation.ToNext(r.reputation);
            _next.text = toNext > 0 ? $"{toNext:N0} reputation to {Reputation.Titles[level]}" : "The highest level";
            Set("Coins", r.coins.ToString("N0", CultureInfo.InvariantCulture));
            Set("Reputation", r.reputation.ToString("N0", CultureInfo.InvariantCulture));
            Set("Explored", r.metresExplored >= 1000f ? $"{r.metresExplored / 1000f:0.0} km" : $"{r.metresExplored:0} m");
            TimeSpan t = TimeSpan.FromSeconds(r.secondsExplored);
            Set("Time exploring", t.TotalHours >= 1 ? $"{(int)t.TotalHours} h {t.Minutes} min" : $"{t.Minutes} min");
            Set("Jumps", r.jumps.ToString("N0", CultureInfo.InvariantCulture));
            Set("Crackers lit", r.crackersLit.ToString("N0", CultureInfo.InvariantCulture));
            Set("Shows played", r.showsPlayed.ToString("N0", CultureInfo.InvariantCulture));
            Set("Midnights", r.midnights.ToString("N0", CultureInfo.InvariantCulture));
            Set("Homes visited", r.homesVisited.ToString("N0", CultureInfo.InvariantCulture));
            Set("Jobs done", r.jobsDone.ToString("N0", CultureInfo.InvariantCulture));
            Set("Playing since", r.firstPlayedUnix > 0 ? DateTimeOffset.FromUnixTimeSeconds(r.firstPlayedUnix).ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "today");
        }

        private void Set(string label, string value)
        {
            if (_values.TryGetValue(label, out TextMeshProUGUI t))
            {
                t.text = value;
            }
        }

        private void Build()
        {
            _overlay = UiKit.Rect("ProfileScreen", transform.parent != null ? transform.parent : transform);
            UiKit.Stretch(_overlay);
            UiKit.Panel(_overlay, new Color(0.09f, 0.06f, 0.2f, 0.99f));
            UiKit.Text(Area(_overlay, "Heading", 0f, 1f, 1f, 1f, new Vector2(150f, -UiTheme.MinTouch - 20f), new Vector2(-150f, -20f)), "Your profile",
                UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);
            Clickable(Area(_overlay, "Close", 0f, 1f, 0f, 1f, new Vector2(28f, -UiTheme.MinTouch - 20f), new Vector2(248f, -20f)), "Close", UiTheme.IndigoLight, Close);

            RectTransform card = Area(_overlay, "Card", 0.05f, 1f, 0.95f, 1f, new Vector2(0f, -UiTheme.MinTouch * 2f - 300f), new Vector2(0f, -UiTheme.MinTouch - 50f));
            UiKit.Panel(card, UiTheme.IndigoLight).sprite = UiKit.Rounded;
            _name = UiKit.Text(Area(card, "Name", 0f, 0.62f, 0.62f, 1f, new Vector2(28f, 0f), new Vector2(0f, -12f)), string.Empty, UiTheme.TitleText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            Clickable(Area(card, "EditAvatar", 0.64f, 0.66f, 0.97f, 0.94f, Vector2.zero, Vector2.zero), "Edit avatar", UiTheme.Saffron, EditAvatar);
            _title = UiKit.Text(Area(card, "Title", 0f, 0.38f, 1f, 0.62f, new Vector2(28f, 0f), new Vector2(-28f, 0f)), string.Empty, UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.Marigold);
            RectTransform track = Area(card, "Track", 0f, 0.24f, 1f, 0.34f, new Vector2(28f, 0f), new Vector2(-28f, 0f));
            UiKit.Panel(track, new Color(0f, 0f, 0f, 0.35f), raycast: false).sprite = UiKit.Rounded;
            _bar = Area(track, "Bar", 0f, 0f, 0f, 1f, Vector2.zero, Vector2.zero);
            UiKit.Panel(_bar, UiTheme.Saffron, raycast: false).sprite = UiKit.Rounded;
            _next = UiKit.Text(Area(card, "Next", 0f, 0f, 1f, 0.22f, new Vector2(28f, 8f), new Vector2(-28f, 0f)), string.Empty, UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.OffWhite);

            RectTransform grid = Area(_overlay, "Stats", 0.05f, 0f, 0.95f, 1f, new Vector2(0f, 40f), new Vector2(0f, -UiTheme.MinTouch * 2f - 330f));
            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(480f, 130f);
            layout.spacing = new Vector2(20f, 16f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;
            foreach (string label in new[] { "Coins", "Reputation", "Explored", "Time exploring", "Jumps", "Crackers lit", "Shows played", "Midnights", "Homes visited", "Jobs done", "Playing since" })
            {
                RectTransform cell = UiKit.Rect(label, grid);
                UiKit.Panel(cell, new Color(1f, 1f, 1f, 0.06f), raycast: false).sprite = UiKit.Rounded;
                UiKit.Text(Area(cell, "Label", 0f, 0.55f, 1f, 1f, new Vector2(20f, 0f), new Vector2(-20f, -8f)), label, UiTheme.SmallText, TextAlignmentOptions.Left, UiTheme.Muted);
                _values[label] = UiKit.Text(Area(cell, "Value", 0f, 0f, 1f, 0.6f, new Vector2(20f, 8f), new Vector2(-20f, 0f)), "0", UiTheme.BodyText, TextAlignmentOptions.Left, UiTheme.OffWhite);
            }

            _overlay.gameObject.SetActive(false);
        }

        private static void Clickable(RectTransform rect, string label, Color background, Action action)
        {
            Image image = UiKit.Panel(rect, background);
            image.sprite = UiKit.Rounded;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);
        }

        private static RectTransform Area(RectTransform parent, string name, float x0, float y0, float x1, float y1, Vector2 offsetMin, Vector2 offsetMax)
        {
            RectTransform rect = UiKit.Rect(name, parent);
            rect.anchorMin = new Vector2(x0, y0);
            rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }
    }
}
