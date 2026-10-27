using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UtsavNights.Audio;
using UtsavNights.Content;
using UtsavNights.Core.Diagnostics;
using UtsavNights.Core.Runtime;
using UtsavNights.Gameplay;

namespace UtsavNights.UI
{
    /// <summary>
    /// Phase 3 step 13 measurement tools, opened from the developer overlay (triple-tap top-left):
    /// the blind sound test (F03: testers name 6 of 8 crackers by sound alone) and a stress finale for
    /// device performance captures. Results go to persistentDataPath/blind_sound_test.jsonl and to logcat
    /// with the tag [UTSAV_SOUND]. Pull with tools/pull_sound_test.sh.
    /// </summary>
    public sealed class MeasureTools : MonoBehaviour
    {
        public const string LogTag = "[UTSAV_SOUND]";
        public const string FileName = "blind_sound_test.jsonl";
        private const string TesterKey = "utsav.soundtest.tester";
        private const float NextSoundDelay = 0.8f;

        // Stress finale: six ground and aerial items on known-good spots around the starting flowerpot.
        private static readonly string[] StressIds = { "flowerpot_gold", "chakri_classic", "bomb_single", "chain_lar", "rocket_basic", "cake_twelve" };

        private static readonly Vector3[] StressSpots =
        {
            new Vector3(0f, 0f, 0.6f), new Vector3(-1.1f, 0f, 0.3f), new Vector3(1.1f, 0f, 0.3f),
            new Vector3(-0.9f, 0f, 1.2f), new Vector3(0.9f, 0f, 1.6f), new Vector3(-0.2f, 0f, 1.9f),
        };

        private CrackerCatalog _catalog;
        private SoundAudition _audition;
        private BlindSoundTest _test;
        private string _testId;
        private int _tester;
        private float _playAt = -1f;

        private RectTransform _overlay;
        private TextMeshProUGUI _title;
        private TextMeshProUGUI _body;
        private RectTransform _grid;
        private RectTransform _replay;
        private TextMeshProUGUI _actionLabel;
        private Action _action;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public bool IsOpen => _overlay != null && _overlay.gameObject.activeSelf;

        public BlindSoundTest Test => _test;

        public bool IsPlayingSound => _audition != null && _audition.IsPlaying;

        private void Start()
        {
            TrayView tray = FindFirstObjectByType<TrayView>();
            _catalog = tray != null ? tray.Catalog : null;
            if (Game.Services.TryGet(out AudioService audio))
            {
                _audition = new SoundAudition(audio);
            }

            Build(tray != null ? tray.transform.parent : transform);
            DevOverlay overlay = FindFirstObjectByType<DevOverlay>();
            if (overlay != null)
            {
                overlay.AddTool("Sound test", OpenSoundTest);
                overlay.AddTool("Stress finale", () => StartStressFinale());
            }
        }

        private void OnDestroy()
        {
            _audition?.Stop();
            if (_overlay != null)
            {
                InputBlockers.Unregister(_overlay);
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            _audition?.Tick(Time.unscaledDeltaTime);
            if (_playAt >= 0f && Time.unscaledTime >= _playAt)
            {
                _playAt = -1f;
                PlayCurrent();
            }
        }

        /// <summary>Shows the intro for the next tester; nothing plays until Start.</summary>
        public void OpenSoundTest()
        {
            if (_catalog == null || _audition == null)
            {
                return;
            }

            if (Game.Services.TryGet(out PlacementController placement))
            {
                placement.Cancel();
            }

            // Silence the scene so only the test sound is heard.
            if (Game.Services.TryGet(out CrackerSpawner spawner))
            {
                spawner.ClearAll();
            }

            if (Game.Services.TryGet(out AudioService audio))
            {
                audio.StopAll();
            }

            _tester = PlayerPrefs.GetInt(TesterKey, 0) + 1;
            _test = null;
            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            InputBlockers.Register(_overlay);
            _title.text = $"Blind sound test: tester {_tester}";
            _body.text = "Each sound plays once. Pick the cracker you hear, or Not sure.\nPlay again repeats the sound.";
            ShowChoices(false);
            SetAction("Start", BeginTest);
        }

        /// <summary>Same as tapping Start on the intro.</summary>
        public void BeginTest()
        {
            var ids = new List<string>();
            foreach (CrackerCatalog.Entry entry in _catalog.Entries)
            {
                if (entry.definition != null && !entry.locked)
                {
                    ids.Add(entry.definition.Id);
                }
            }

            int seed = Environment.TickCount;
            _test = new BlindSoundTest(ids, seed);
            _testId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            PlayerPrefs.SetInt(TesterKey, _tester);
            PlayerPrefs.Save();
            Write(Line("test_start").Num("seed", seed).Num("sounds", ids.Count).Str("device", SystemInfo.deviceModel));
            ShowChoices(true);
            SetAction("Stop test", Close);
            ShowQuestion();
            PlayCurrent();
        }

        /// <summary>Records a guess for the current sound (a cracker id, or null for not sure).</summary>
        public void Guess(string crackerId)
        {
            if (_test == null || _test.IsComplete)
            {
                return;
            }

            _audition.Stop();
            CrackerDefinition played = _catalog.FindById(_test.Current);
            BlindSoundTest.Answer answer = _test.Guess(crackerId);
            Write(Line("answer").Num("index", answer.Index).Str("played", answer.PlayedId).Str("guess", answer.GuessId)
                .Bool("correct", answer.Correct).Num("replays", answer.Replays)
                .Str("audio_credits", played != null ? played.CreditsAudio : string.Empty));
            if (_test.IsComplete)
            {
                Finish();
                return;
            }

            ShowQuestion();
            _playAt = Time.unscaledTime + NextSoundDelay;
        }

        public void PlayAgain()
        {
            if (_test == null || _test.IsComplete)
            {
                return;
            }

            _test.RecordReplay();
            _playAt = -1f;
            PlayCurrent();
        }

        public void Close()
        {
            if (_test != null && !_test.IsComplete)
            {
                Write(Line("test_abandoned").Num("answered", _test.Index));
            }

            _audition?.Stop();
            _playAt = -1f;
            _test = null;
            InputBlockers.Unregister(_overlay);
            _overlay.gameObject.SetActive(false);
        }

        /// <summary>
        /// Clears the scene, places six varied crackers and starts a finale (repeatable load for perf
        /// captures). Returns false when the scene limit or the finale rules do not allow it.
        /// </summary>
        public bool StartStressFinale()
        {
            if (_catalog == null || !Game.Services.TryGet(out CrackerSpawner spawner) || !Game.Services.TryGet(out FinaleController finale))
            {
                return false;
            }

            if (finale.State == FinaleController.FinaleState.Running)
            {
                return false;
            }

            spawner.ClearAll();
            for (int i = 0; i < StressIds.Length && i < spawner.ItemLimit; i++)
            {
                CrackerDefinition definition = _catalog.FindById(StressIds[i]);
                if (definition != null)
                {
                    spawner.Spawn(definition, StressSpots[i], Quaternion.identity);
                }
            }

            bool started = finale.StartFinale();
            Debug.Log($"{LogTag} stress finale started={started} items={spawner.ActiveCount}");
            return started;
        }

        private void PlayCurrent()
        {
            if (_test == null || _test.IsComplete)
            {
                return;
            }

            _audition.Play(_catalog.FindById(_test.Current));
        }

        private void ShowQuestion()
        {
            _title.text = $"Sound {_test.Index + 1} of {_test.Count}";
            _body.text = "Which cracker is this?";
        }

        private void Finish()
        {
            Write(Line("test_complete").Num("correct", _test.CorrectCount).Num("total", _test.Count)
                .Num("required", _test.RequiredCorrect).Bool("passed", _test.Passed));
            ShowChoices(false);
            _title.text = $"Done: {_test.CorrectCount} of {_test.Count}";
            _body.text = "Thank you! Results are saved on this device.";
            SetAction("Close", Close);
        }

        private void ShowChoices(bool visible)
        {
            _grid.gameObject.SetActive(visible);
            _replay.gameObject.SetActive(visible);
        }

        private void SetAction(string label, Action action)
        {
            _actionLabel.text = label;
            _action = action;
        }

        private void Build(Transform parent)
        {
            _overlay = UiKit.Rect("MeasureTools", parent);
            UiKit.Stretch(_overlay);
            UiKit.Panel(_overlay, new Color(0.04f, 0.03f, 0.1f, 1f));

            RectTransform column = UiKit.Rect("Column", _overlay);
            UiKit.Stretch(column, 40f);
            MaxWidth.Apply(column, 1100f);

            RectTransform title = UiKit.Rect("Title", column);
            title.anchorMin = new Vector2(0f, 1f);
            title.anchorMax = new Vector2(1f, 1f);
            title.pivot = new Vector2(0.5f, 1f);
            title.sizeDelta = new Vector2(0f, 120f);
            title.anchoredPosition = new Vector2(0f, -60f);
            _title = UiKit.Text(title, string.Empty, UiTheme.TitleText, TextAlignmentOptions.Center, UiTheme.Marigold);

            RectTransform body = UiKit.Rect("Body", column);
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.sizeDelta = new Vector2(0f, 160f);
            body.anchoredPosition = new Vector2(0f, -190f);
            _body = UiKit.Text(body, string.Empty, UiTheme.BodyText, TextAlignmentOptions.Center, UiTheme.OffWhite);

            _replay = Button(column, "Play again", PlayAgain, UiTheme.IndigoLight, UiTheme.OffWhite);
            _replay.anchorMin = _replay.anchorMax = _replay.pivot = new Vector2(0.5f, 1f);
            _replay.sizeDelta = new Vector2(420f, UiTheme.MinTouch);
            _replay.anchoredPosition = new Vector2(0f, -370f);

            _grid = UiKit.Rect("Choices", column);
            _grid.anchorMin = new Vector2(0f, 1f);
            _grid.anchorMax = new Vector2(1f, 1f);
            _grid.pivot = new Vector2(0.5f, 1f);
            _grid.sizeDelta = new Vector2(0f, 3 * (UiTheme.MinTouch + 24f));
            _grid.anchoredPosition = new Vector2(0f, -540f);
            var layout = _grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(320f, UiTheme.MinTouch);
            layout.spacing = new Vector2(24f, 24f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.UpperCenter;

            // Names in catalog order: the order says nothing about which sound is playing.
            if (_catalog != null)
            {
                foreach (CrackerCatalog.Entry entry in _catalog.Entries)
                {
                    if (entry.definition == null || entry.locked)
                    {
                        continue;
                    }

                    string id = entry.definition.Id;
                    string name = string.IsNullOrEmpty(entry.definition.DisplayName.en) ? id : entry.definition.DisplayName.en;
                    Button(_grid, name, () => Guess(id), UiTheme.IndigoLight, UiTheme.OffWhite);
                }
            }

            Button(_grid, "Not sure", () => Guess(null), UiTheme.Indigo, UiTheme.Muted);

            RectTransform action = Button(column, string.Empty, () => _action?.Invoke(), UiTheme.Saffron, UiTheme.Indigo);
            action.anchorMin = action.anchorMax = action.pivot = new Vector2(0.5f, 0f);
            action.sizeDelta = new Vector2(420f, UiTheme.MinTouch);
            action.anchoredPosition = new Vector2(0f, 80f);
            _actionLabel = action.GetComponentInChildren<TextMeshProUGUI>();

            _overlay.gameObject.SetActive(false);
        }

        private static RectTransform Button(Transform parent, string label, Action onClick, Color background, Color text)
        {
            RectTransform rect = UiKit.Rect(label, parent);
            Image image = UiKit.Panel(rect, background);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => onClick());
            TextMeshProUGUI tmp = UiKit.Text(rect, label, UiTheme.BodyText, TextAlignmentOptions.Center, text);
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 24f;
            tmp.fontSizeMax = UiTheme.BodyText * UiKit.TextScale;
            return rect;
        }

        private JsonLine Line(string type) => new JsonLine(_testId, _tester, type);

        private static void Write(JsonLine line)
        {
            string text = line.ToString();
            Debug.Log($"{LogTag} {text}");
            try
            {
                File.AppendAllText(FilePath, text + "\n");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogTag} could not write results: {e.Message}");
            }
        }

        private sealed class JsonLine
        {
            private readonly StringBuilder _sb = new StringBuilder(160);

            public JsonLine(string test, int tester, string type)
            {
                Str("test", test);
                Num("tester", tester);
                Num("t", Time.realtimeSinceStartup);
                Str("type", type);
            }

            public JsonLine Str(string key, string value)
            {
                Key(key).Append('"');
                foreach (char c in value ?? string.Empty)
                {
                    if (c == '"' || c == '\\')
                    {
                        _sb.Append('\\');
                    }

                    if (c >= ' ')
                    {
                        _sb.Append(c);
                    }
                }

                _sb.Append('"');
                return this;
            }

            public JsonLine Num(string key, float value)
            {
                Key(key).Append(value.ToString("0.##", CultureInfo.InvariantCulture));
                return this;
            }

            public JsonLine Bool(string key, bool value)
            {
                Key(key).Append(value ? "true" : "false");
                return this;
            }

            private StringBuilder Key(string key) => _sb.Append(_sb.Length == 0 ? "{" : ",").Append('"').Append(key).Append("\":");

            public override string ToString() => _sb + "}";
        }
    }
}
