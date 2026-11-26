using System;
using System.Collections.Generic;
using TerritoryIO.Controls;
using TerritoryIO.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TerritoryIO.View
{
    /// <summary>
    /// The start screen, built in code: START, SETTINGS, HIGH SCORES, QUIT.
    /// GameManager creates it when you press Play (if GameSettings > Show Start Screen is on)
    /// and when you choose "Menu" after a round.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        private GameManager game;
        private PlayerOptions options;

        private GameObject mainPanel;
        private GameObject settingsPanel;
        private GameObject scoresPanel;
        private Button startButton;
        private Button settingsBack;
        private Button scoresBack;
        private Button clearButton;
        private bool clearArmed;
        private InputField nameField;

        private readonly List<Action> refreshers = new List<Action>();
        private readonly List<Text[]> scoreRows = new List<Text[]>();
        private Text statsText;

        // Floating squares in the background
        private readonly List<RectTransform> floaters = new List<RectTransform>();
        private readonly List<Vector2> floaterVel = new List<Vector2>();

        public void Init(GameManager gameManager)
        {
            game = gameManager;
            options = PlayerOptions.Load(game.settings);

            UiKit.MakeCanvas(gameObject, 200);

            // Background
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            UiKit.Stretch((RectTransform)bg.transform);
            bg.GetComponent<Image>().color = Palette.Background;
            MakeFloaters(bg.transform);

            BuildMain();
            BuildSettings();
            BuildScores();
            Show(mainPanel);
        }

        // ------------------------------------------------------------------ main

        private void BuildMain()
        {
            mainPanel = Container("Main");
            Transform t = mainPanel.transform;

            var title = UiKit.Text(t, "Title", "<b>TERRITORY<color=#00E5FF>.IO</color></b>", 150,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 310f), new Vector2(1600, 200));
            title.color = Color.white;
            UiKit.Text(t, "Subtitle", "Leave your land. Draw a loop. Claim the arena.", 36,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0f, 195f), new Vector2(1400, 60))
                .color = new Color(1f, 1f, 1f, 0.6f);

            var size = new Vector2(560, 104);
            startButton = UiKit.Button(t, "START", new Vector2(0f, 50f), size, StartGame, 54);
            UiKit.Button(t, "SETTINGS", new Vector2(0f, -75f), size, () => Show(settingsPanel), 44);
            UiKit.Button(t, "HIGH SCORES", new Vector2(0f, -200f), size, () => { RefreshScores(); Show(scoresPanel); }, 44);
            UiKit.Button(t, "QUIT", new Vector2(0f, -325f), size, Quit, 44);

            var table = HighScores.Load();
            if (table.games > 0)
                UiKit.Text(t, "Best", $"Best {table.bestPercent:0.0}%   ·   Games {table.games}   ·   Win rate {table.WinRate:0}%", 30,
                    TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1400, 50))
                    .color = new Color(1f, 1f, 1f, 0.5f);
        }

        private void StartGame()
        {
            if (nameField != null) options.playerName = nameField.text;
            options.Save();
            game.StartFromMenu();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ settings

        private void BuildSettings()
        {
            settingsPanel = Container("Settings");
            var box = UiKit.Panel(settingsPanel.transform, "Box", new Vector2(1200, 960), UiKit.PanelColor);
            Transform t = box;

            UiKit.Text(t, "Header", "<b>SETTINGS</b>", 64, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000, 90));

            float y = 290f;
            const float step = 100f;

            Label(t, "Name", y);
            nameField = UiKit.InputField(t, options.playerName, new Vector2(170f, y), new Vector2(560, 80), 38, 16,
                v => { options.playerName = v; options.Clamped(); options.Save(); });
            y -= step;

            Stepper(t, "Bots", y, () => options.botCount.ToString(),
                d => options.botCount = Mathf.Clamp(options.botCount + d, 0, 15));
            y -= step;

            Stepper(t, "Difficulty", y, () => PlayerOptions.DifficultyNames[options.difficulty],
                d => options.difficulty = Wrap(options.difficulty + d, PlayerOptions.DifficultyNames.Length));
            y -= step;

            string[] shapes = Enum.GetNames(typeof(MapShape));
            Stepper(t, "Map", y, () => shapes[options.mapShape],
                d => options.mapShape = Wrap(options.mapShape + d, shapes.Length));
            y -= step;

            Stepper(t, "Graphics", y, () => PlayerOptions.GraphicsNames[options.graphics],
                d => options.graphics = Wrap(options.graphics + d, PlayerOptions.GraphicsNames.Length));
            y -= step;

            Stepper(t, "Controls", y, () => PlayerOptions.ControlNames[options.controls],
                d => options.controls = Wrap(options.controls + d, PlayerOptions.ControlNames.Length));

            UiKit.Text(t, "Hint", "\"Default\" uses the values from your GameSettings file.", 26,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0f, 175f), new Vector2(1100, 40))
                .color = new Color(1f, 1f, 1f, 0.45f);

            UiKit.Button(t, "RESET", new Vector2(-200f, 90f), new Vector2(340, 90), () =>
            {
                PlayerOptions.Reset();
                options = PlayerOptions.FromSettings(game.settings);
                nameField.text = options.playerName;
                RefreshSettings();
            }, 38, new Vector2(0.5f, 0f));
            settingsBack = UiKit.Button(t, "BACK", new Vector2(200f, 90f), new Vector2(340, 90), () =>
            {
                options.playerName = nameField.text;
                options.Save();
                Show(mainPanel);
            }, 38, new Vector2(0.5f, 0f));
        }

        private static void Label(Transform parent, string text, float y)
        {
            UiKit.Text(parent, "Label_" + text, text, 40, TextAnchor.MiddleLeft,
                new Vector2(0.5f, 0.5f), new Vector2(-330f, y), new Vector2(400, 80));
        }

        private void Stepper(Transform parent, string label, float y, Func<string> value, Action<int> change)
        {
            Label(parent, label, y);
            var valueText = UiKit.Text(parent, "Value_" + label, value(), 40, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(170f, y), new Vector2(400, 80));
            valueText.color = UiKit.Accent;
            UiKit.Button(parent, "<", new Vector2(-60f, y), new Vector2(90, 80), () => { change(-1); options.Save(); RefreshSettings(); }, 44);
            UiKit.Button(parent, ">", new Vector2(400f, y), new Vector2(90, 80), () => { change(+1); options.Save(); RefreshSettings(); }, 44);
            refreshers.Add(() => valueText.text = value());
        }

        private void RefreshSettings()
        {
            foreach (Action r in refreshers) r();
        }

        private static int Wrap(int v, int count) => ((v % count) + count) % count;

        // ------------------------------------------------------------------ high scores

        private void BuildScores()
        {
            scoresPanel = Container("Scores");
            var box = UiKit.Panel(scoresPanel.transform, "Box", new Vector2(1300, 960), UiKit.PanelColor);
            Transform t = box;

            UiKit.Text(t, "Header", "<b>HIGH SCORES</b>", 64, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000, 90));
            statsText = UiKit.Text(t, "Stats", "", 32, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1200, 50));
            statsText.color = UiKit.Accent;

            Row(t, -205f, 30, new Color(0.55f, 0.58f, 0.68f), "#", "Name", "Land", "Kills", "Result", "Date");
            for (int i = 0; i < HighScores.MaxEntries; i++)
                scoreRows.Add(Row(t, -260f - i * 54f, 34, Color.white, "", "", "", "", "", ""));

            clearButton = UiKit.Button(t, "CLEAR", new Vector2(-200f, 90f), new Vector2(380, 90), () =>
            {
                if (!clearArmed)
                {
                    clearArmed = true;
                    UiKit.SetLabel(clearButton, "TAP AGAIN");
                    return;
                }
                HighScores.Clear();
                RefreshScores();
            }, 38, new Vector2(0.5f, 0f));
            scoresBack = UiKit.Button(t, "BACK", new Vector2(200f, 90f), new Vector2(380, 90), () => Show(mainPanel),
                38, new Vector2(0.5f, 0f));
        }

        // Column x positions (from the left edge of the box) and alignment.
        private static readonly float[] ColX = { 90f, 170f, 640f, 820f, 960f, 1110f };
        private static readonly TextAnchor[] ColAlign =
        {
            TextAnchor.MiddleRight, TextAnchor.MiddleLeft, TextAnchor.MiddleRight,
            TextAnchor.MiddleRight, TextAnchor.MiddleCenter, TextAnchor.MiddleCenter
        };

        private static Text[] Row(Transform parent, float y, int size, Color color, params string[] cells)
        {
            var texts = new Text[cells.Length];
            for (int c = 0; c < cells.Length; c++)
            {
                float w = c == 1 ? 440f : 200f;
                // Pivot is the anchor: right-aligned columns end at ColX, left-aligned start there.
                Vector2 pivot = ColAlign[c] == TextAnchor.MiddleRight ? new Vector2(1f, 0.5f)
                    : ColAlign[c] == TextAnchor.MiddleLeft ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
                var t = UiKit.Text(parent, "Cell", cells[c], size, ColAlign[c], new Vector2(0f, 1f),
                    new Vector2(ColX[c], y), new Vector2(w, 50));
                t.rectTransform.pivot = pivot;
                t.color = color;
                texts[c] = t;
            }
            return texts;
        }

        private void RefreshScores()
        {
            clearArmed = false;
            UiKit.SetLabel(clearButton, "CLEAR");

            HighScores.Table table = HighScores.Load();
            statsText.text = table.games == 0
                ? "No games yet. Play a round!"
                : $"Games {table.games}   ·   Wins {table.wins}   ·   Win rate {table.WinRate:0}%   ·   Kills {table.totalKills}";

            for (int i = 0; i < scoreRows.Count; i++)
            {
                Text[] row = scoreRows[i];
                bool has = i < table.entries.Count;
                HighScores.Entry e = has ? table.entries[i] : null;
                string name = has ? e.name ?? "" : "";
                if (name.Length > 16) name = name.Substring(0, 16);
                row[0].text = has ? $"{i + 1}." : "";
                row[1].text = name;
                row[2].text = has ? $"{e.percent:0.0}%" : "";
                row[3].text = has ? e.kills.ToString() : "";
                row[4].text = has ? (e.won ? "<color=#7CFF5C>WIN</color>" : "-") : "";
                row[5].text = has ? e.date : "";
                Color color = i == 0 ? new Color(1f, 0.84f, 0.29f) : i < 3 ? Color.white : new Color(0.78f, 0.8f, 0.86f);
                foreach (Text cell in row) cell.color = color;
            }
        }

        // ------------------------------------------------------------------ helpers

        private GameObject Container(string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            UiKit.Stretch((RectTransform)go.transform);
            return go;
        }

        private void Show(GameObject panel)
        {
            mainPanel.SetActive(panel == mainPanel);
            settingsPanel.SetActive(panel == settingsPanel);
            scoresPanel.SetActive(panel == scoresPanel);
            if (panel == settingsPanel) RefreshSettings();

            // Select a button so keyboard / gamepad (arrows + Enter) works too.
            if (EventSystem.current == null) return;
            GameObject first = panel == mainPanel ? startButton.gameObject
                : panel == settingsPanel ? settingsBack.gameObject : scoresBack.gameObject;
            EventSystem.current.SetSelectedGameObject(first);
        }

        private void MakeFloaters(Transform parent)
        {
            var rng = new System.Random();
            for (int i = 0; i < 14; i++)
            {
                var go = new GameObject("Floater", typeof(RectTransform), typeof(Image));
                float s = 40 + (float)rng.NextDouble() * 120f;
                var rt = UiKit.Rect(go, parent, new Vector2(0.5f, 0.5f),
                    new Vector2((float)rng.NextDouble() * 1800f - 900f, (float)rng.NextDouble() * 1000f - 500f),
                    new Vector2(s, s));
                Color c = Palette.ForPlayer(i + 1);
                c.a = 0.12f;
                var img = go.GetComponent<Image>();
                img.color = c;
                img.raycastTarget = false;
                rt.localRotation = Quaternion.Euler(0, 0, (float)rng.NextDouble() * 90f);
                floaters.Add(rt);
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                floaterVel.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (20f + (float)rng.NextDouble() * 40f));
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < floaters.Count; i++)
            {
                RectTransform rt = floaters[i];
                Vector2 p = rt.anchoredPosition + floaterVel[i] * dt;
                Vector2 v = floaterVel[i];
                if (Mathf.Abs(p.x) > 1000f) v.x = -v.x;
                if (Mathf.Abs(p.y) > 580f) v.y = -v.y;
                floaterVel[i] = v;
                rt.anchoredPosition = p;
                rt.Rotate(0f, 0f, dt * 10f * (i % 2 == 0 ? 1f : -1f));
            }

            if (InputUtil.BackPressed() && !mainPanel.activeSelf)
            {
                options.playerName = nameField.text;
                options.Save();
                Show(mainPanel);
            }
        }
    }
}
