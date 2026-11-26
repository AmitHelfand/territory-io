using System.Collections.Generic;
using System.Text;
using TerritoryIO.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace TerritoryIO.View
{
    /// <summary>
    /// HUD built entirely in code (no prefabs): your score, a leaderboard, a pause button,
    /// the pause panel, the game-over / victory panel and the minimap. Uses uGUI with Unity's built-in font.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        private GameManager game;
        private GameObject pausePanel;
        private GameObject pauseButton;
        private Text scoreText;
        private Text boardText;
        private Text crownText;
        private Text newsText;
        private GameObject panel;
        private Text panelTitle;
        private Text panelBody;
        private float nextRefresh;
        private readonly List<Player> sorted = new List<Player>();
        private readonly StringBuilder sb = new StringBuilder(256);

        public void Init(GameManager gameManager)
        {
            game = gameManager;
            UiKit.MakeCanvas(gameObject, 100);

            // Title tag
            var title = MakeText(transform, "Title", 30, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(600, 50));
            title.text = "<b>TERRITORY.IO</b>";
            title.color = new Color(1f, 1f, 1f, 0.35f);

            scoreText = MakeText(transform, "Score", 40, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(30f, -24f), new Vector2(700, 120));

            boardText = MakeText(transform, "Leaderboard", 30, TextAnchor.UpperRight,
                new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(500, 400));

            // Crown mode timer (under the score) and a short news line (top centre).
            crownText = MakeText(transform, "CrownTimer", 34, TextAnchor.UpperLeft,
                new Vector2(0f, 1f), new Vector2(30f, -140f), new Vector2(800, 100));
            newsText = MakeText(transform, "News", 34, TextAnchor.UpperCenter,
                new Vector2(0.5f, 1f), new Vector2(0f, -190f), new Vector2(1000, 60));
            newsText.color = new Color(1f, 0.84f, 0.29f);

            // Pause button (top centre, under the title). Esc / Android Back also pauses.
            pauseButton = UiKit.Button(transform, "II", new Vector2(0f, -100f), new Vector2(110, 80),
                () => game.SetPaused(true), 40, new Vector2(0.5f, 1f)).gameObject;

            // Pause panel
            var pp = UiKit.Panel(transform, "PausePanel", new Vector2(700, 560), UiKit.PanelColor);
            pausePanel = pp.gameObject;
            MakeText(pp, "PauseTitle", 72, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 190f), new Vector2(680, 100)).text = "<b>PAUSED</b>";
            UiKit.Button(pp, "RESUME", new Vector2(0f, 50f), new Vector2(440, 96), () => game.SetPaused(false), 42);
            UiKit.Button(pp, "RESTART", new Vector2(0f, -70f), new Vector2(440, 96), () => game.StartRound(), 42);
            UiKit.Button(pp, "MENU", new Vector2(0f, -190f), new Vector2(440, 96), () => game.ShowMenu(), 42);
            pausePanel.SetActive(false);

            // Game over / victory panel
            var prt = UiKit.Panel(transform, "EndPanel", new Vector2(900, 520), UiKit.PanelColor);
            panel = prt.gameObject;
            panelTitle = MakeText(panel.transform, "PanelTitle", 72, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(880, 100));
            panelBody = MakeText(panel.transform, "PanelBody", 36, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(860, 180));
            UiKit.Button(panel.transform, "PLAY AGAIN", new Vector2(-200f, -170f), new Vector2(360, 96),
                () => game.StartRound(), 40);
            UiKit.Button(panel.transform, "MENU", new Vector2(200f, -170f), new Vector2(360, 96),
                () => game.ShowMenu(), 40);
            panel.SetActive(false);

            // Minimap of the whole arena (top right, under the leaderboard by default).
            new GameObject("Minimap", typeof(RectTransform)).AddComponent<Minimap>().Init(game, transform);
            // Panels must stay on top of the minimap.
            pausePanel.transform.SetAsLastSibling();
            panel.transform.SetAsLastSibling();
        }

        private Text MakeText(Transform parent, string name, int size, TextAnchor align,
            Vector2 anchor, Vector2 pos, Vector2 boxSize)
        {
            return UiKit.Text(parent, name, "", size, align, anchor, pos, boxSize);
        }

        private void Update()
        {
            if (game == null || game.Grid == null || game.Human == null) return;

            bool playing = game.State == GameState.Playing;
            if (pausePanel.activeSelf != game.Paused) pausePanel.SetActive(game.Paused);
            if (pauseButton.activeSelf != (playing && !game.Paused)) pauseButton.SetActive(playing && !game.Paused);

            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;

            Player human = game.Human;
            float pct = human.Alive ? game.Grid.AreaPercent(human.Id) : 0f;
            scoreText.text = $"<color=#{Hex(human.Color)}><b>{pct:0.00}%</b></color>\n" +
                             $"<size=26>Kills {human.Kills}   Goal {game.Settings.winPercent:0}%</size>";

            sorted.Clear();
            foreach (Player p in game.Players) if (p.Alive) sorted.Add(p);
            sorted.Sort((a, b) => game.Grid.Area(b.Id).CompareTo(game.Grid.Area(a.Id)));

            sb.Clear();
            int shown = 0;
            bool humanShown = false;
            for (int i = 0; i < sorted.Count && shown < 5; i++, shown++)
            {
                AppendRow(i + 1, sorted[i]);
                if (sorted[i] == human) humanShown = true;
            }
            if (!humanShown && human.Alive)
                AppendRow(sorted.IndexOf(human) + 1, human);
            boardText.text = sb.ToString();

            crownText.text = human.Alive && human.HasCrown
                ? $"<color=#FFD54A><b>CROWN {Mathf.CeilToInt(human.CrownTimeLeft)}s</b></color>\n" +
                  "<size=24>Kill an enemy to take ALL their land!</size>"
                : "";
            bool showNews = playing && !string.IsNullOrEmpty(game.News) && game.RoundTime - game.NewsTime < 2.5f;
            newsText.text = showNews ? $"<b>{game.News}</b>" : "";

            bool ended = game.State != GameState.Playing;
            panel.SetActive(ended);
            if (ended)
            {
                bool won = game.State == GameState.Victory;
                panelTitle.text = won ? "<b>VICTORY</b>" : "<b>GAME OVER</b>";
                panelTitle.color = won ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.4f, 0.5f);
                string rank = game.EndRank == 1 ? "<color=#FFD54A><b>NEW HIGH SCORE!</b></color>"
                    : game.EndRank > 1 ? $"<color=#FFD54A>#{game.EndRank} in your high scores</color>" : "";
                panelBody.text = $"{game.EndReason}\nBest territory: {game.EndPercent:0.00}%   Kills: {human.Kills}\n{rank}\n" +
                                 "<size=26><color=#8890A8>Space / Enter = play again   ·   Esc = menu</color></size>";
            }
        }

        private void AppendRow(int rank, Player p)
        {
            string name = p.IsHuman ? $"<b>{p.DisplayName}</b>" : p.DisplayName;
            sb.Append($"<color=#{Hex(p.Color)}>{rank}. {name}  {game.Grid.AreaPercent(p.Id):0.0}%</color>\n");
        }

        private static string Hex(Color32 c) => UiKit.Hex(c);
    }
}
