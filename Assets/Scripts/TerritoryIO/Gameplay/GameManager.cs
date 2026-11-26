using System.Collections;
using System.Collections.Generic;
using TerritoryIO.Controls;
using TerritoryIO.Core;
using TerritoryIO.View;
using UnityEngine;

namespace TerritoryIO.Gameplay
{
    public enum GameState { Playing, GameOver, Victory }

    /// <summary>
    /// The only component you need in the scene. Shows the start screen, builds the arena, camera,
    /// players and HUD, runs the game loop and owns every rule (trail, capture, collisions, death, win).
    /// All tuning lives in the GameSettings asset assigned to "Settings".
    /// The game runs on a copy of that asset (so the start screen's choices never change your file);
    /// fields marked (live) are copied from your file every frame, so Inspector tuning still works.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Tooltip("Create one: Project window > right-click > Create > Territory.io > Game Settings. " +
                 "Empty = default values.")]
        public GameSettings settings;

        /// <summary>The settings of the running game (your asset + start-screen choices).</summary>
        public GameSettings Settings => run != null ? run : settings;
        public GridMap Grid { get; private set; }
        public IReadOnlyList<Player> Players => players;
        public Player Human { get; private set; }
        public GameState State { get; private set; }
        public string EndReason { get; private set; }
        public float EndPercent { get; private set; }
        /// <summary>Place of the last finished round in the high score table (1-10), 0 = not in the top 10.</summary>
        public int EndRank { get; private set; }
        public bool Paused { get; private set; }
        /// <summary>True while a game is built (false on the start screen).</summary>
        public bool InGame { get; private set; }
        /// <summary>Seconds played in this round (stops while paused).</summary>
        public float RoundTime { get; private set; }
        /// <summary>The crown lying on the map, or null.</summary>
        public Pickup Crown => crown;
        /// <summary>Short news line for the HUD (crown appeared, land stolen...) and when it was set (RoundTime).</summary>
        public string News { get; private set; }
        public float NewsTime { get; private set; }

        /// <summary>Grid cells per world unit (GameSettings.resolution). All "units" settings are multiplied by it.</summary>
        public int Res { get; private set; } = 1;
        /// <summary>Own trail cells right behind the head that can't kill you (needed for curves).</summary>
        public int NeckCells { get; private set; } = 4;
        /// <summary>Max distance moved per sub-step, in cells. Below 1 so no cell is ever skipped.</summary>
        private const float SubStep = 0.4f;

        private readonly List<Player> players = new List<Player>();
        private readonly CaptureSolver solver = new CaptureSolver();
        private GameSettings run;
        private float botSpeedFactor = 1f;
        private TerritoryRenderer territory;
        private HudController hud;
        private MainMenu menu;
        private CameraFollow cameraFollow;
        private Camera cam;
        private System.Random rng;
        private float endTime;
        private Pickup crown;
        private float nextCrownTime;

        private static readonly string[] BotNames =
        {
            "Vortex", "Pixel", "Comet", "Nimbus", "Quark", "Ember", "Glitch", "Orbit",
            "Rune", "Sable", "Tango", "Zephyr", "Flux", "Mosaic", "Juno"
        };

        // ------------------------------------------------------------------ setup

        private void Start()
        {
            if (settings == null)
            {
                Debug.Log("Territory.io: no GameSettings assigned, using defaults. " +
                          "Create one via Create > Territory.io > Game Settings to tune the game.");
                settings = ScriptableObject.CreateInstance<GameSettings>();
            }

            SetupCamera();
            if (settings.showStartScreen) ShowMenu();
            else BuildWorld(false);
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            DisplaySetup.Restore();
            if (run != null) Destroy(run);
        }

        // ------------------------------------------------------------------ menu / game lifecycle

        /// <summary>Removes the running game (if any) and shows the start screen.</summary>
        public void ShowMenu()
        {
            TearDownWorld();
            if (menu != null) return;
            menu = new GameObject("StartScreen").AddComponent<MainMenu>();
            menu.Init(this);
        }

        /// <summary>Called by the START button.</summary>
        public void StartFromMenu()
        {
            if (menu != null) Destroy(menu.gameObject);
            menu = null;
            BuildWorld(true);
        }

        public void SetPaused(bool paused)
        {
            if (!InGame) return;
            Paused = paused;
            Time.timeScale = paused ? 0f : 1f;
        }

        /// <param name="useMenuOptions">Apply the start screen's Settings (name, bots, difficulty, map, graphics, controls).</param>
        private void BuildWorld(bool useMenuOptions)
        {
            TearDownWorld();

            // Work on a copy so nothing here changes your GameSettings file.
            run = Instantiate(settings);
            run.name = settings.name + " (running)";
            botSpeedFactor = useMenuOptions ? PlayerOptions.Load(settings).ApplyTo(run) : 1f;
            run.CopyLiveFrom(settings, botSpeedFactor);

            rng = run.seed == 0 ? new System.Random() : new System.Random(run.seed);

            DisplaySetup.Apply((int)run.antiAliasing, run.renderScale, run.nativeResolutionOnMobile, run.targetFrameRate);

            // Everything runs in "cell space": 1 world unit in Unity = 1 grid cell.
            // Resolution R makes the grid R times finer, so settings in units are multiplied by R.
            Res = Mathf.Clamp(run.resolution, 1, 8);
            NeckCells = 2 + Mathf.CeilToInt(2.5f * Res);
            int w = Mathf.Max(20, run.arenaWidth) * Res;
            int h = Mathf.Max(20, run.arenaHeight) * Res;
            Grid = new GridMap(w, h, run.botCount + 1, MapShapes.Build(run, w, h));

            SetupCamera();
            CreatePlayers();

            territory = new GameObject("Territory").AddComponent<TerritoryRenderer>();
            territory.Init(Grid, id => players[id - 1].Color, run.smoothLandEdges,
                run.mapShape == MapShape.Rectangle, !run.smoothTrails, 4 * Res, run.landDetail);

            hud = new GameObject("HUD").AddComponent<HudController>();
            hud.Init(this);

            InGame = true;
            StartRound();
        }

        /// <summary>Destroys everything BuildWorld created.</summary>
        private void TearDownWorld()
        {
            StopAllCoroutines();
            Paused = false;
            Time.timeScale = 1f;
            InGame = false;
            foreach (Player p in players)
                if (p != null) Destroy(p.gameObject);
            players.Clear();
            Human = null;
            if (territory != null) Destroy(territory.gameObject);
            if (hud != null) Destroy(hud.gameObject);
            territory = null;
            hud = null;
            Grid = null;
            RemoveCrown();
            if (cameraFollow != null) cameraFollow.Target = null;
            if (run != null) Destroy(run);
            run = null;
        }

        private void SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = Settings.cameraSize * Res;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.allowMSAA = true;
            if (Grid != null) cam.transform.position = new Vector3(Grid.Width * 0.5f, Grid.Height * 0.5f, -10f);
            cam.transform.rotation = Quaternion.identity;

            cameraFollow = cam.GetComponent<CameraFollow>();
            if (cameraFollow == null) cameraFollow = cam.gameObject.AddComponent<CameraFollow>();
        }

        private void CreatePlayers()
        {
            int total = run.botCount + 1;
            for (int id = 1; id <= total; id++)
            {
                bool human = id == 1;
                var go = new GameObject(human ? "Player_Human" : $"Player_Bot_{id}");
                var p = go.AddComponent<Player>();

                IPlayerBrain brain;
                Sprite sprite;
                Color32 color;
                if (human)
                {
                    var input = go.AddComponent<HumanInput>();
                    input.Scheme = run.controlScheme;
                    brain = input;
                    sprite = run.humanSprite;
                    color = run.humanColor;
                }
                else
                {
                    float aggression = Mathf.Lerp(run.aggressionMin, run.aggressionMax, (float)rng.NextDouble());
                    float greed = Mathf.Lerp(run.greedMin, run.greedMax, (float)rng.NextDouble());
                    brain = new BotBrain(rng.Next(), aggression, greed);
                    var sprites = run.botSprites;
                    sprite = sprites != null && sprites.Length > 0 ? sprites[(id - 2) % sprites.Length] : null;
                    color = Palette.ForPlayer(id);
                }

                string name = human ? run.humanName : BotNames[(id - 2) % BotNames.Length];
                p.Setup(id, name, color, human, brain, sprite, run.tintSprites,
                    run.headSize * Res, run.rotateSprite, run.spriteAngleOffset,
                    run.smoothTrails ? run.trailWidth * Res : 0f);
                players.Add(p);
                if (human) Human = p;
            }
            cameraFollow.Target = Human.transform;
        }

        public void StartRound()
        {
            StopAllCoroutines();
            foreach (Player p in players) p.Die();
            Grid.ClearAll();
            RemoveCrown();
            RoundTime = 0f;
            nextCrownTime = run.crownInterval;
            News = null;
            foreach (Player p in players) SpawnPlayer(p);

            State = GameState.Playing;
            EndReason = null;
            EndRank = 0;
            SetPaused(false);
            cameraFollow.SnapToTarget();
        }

        private void SpawnPlayer(Player p)
        {
            Vector2Int center = FindSpawnCell();
            int r = run.startSize * Res;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                bool inShape;
                switch (run.startShape)
                {
                    case StartShape.Square: inShape = true; break;
                    case StartShape.Diamond: inShape = Mathf.Abs(dx) + Mathf.Abs(dy) <= r; break;
                    default: inShape = dx * dx + dy * dy <= r * r + r; break; // rounded disc
                }
                int x = center.x + dx, y = center.y + dy;
                if (inShape && Grid.IsPlayable(x, y) && Grid.GetTrail(x, y) == 0) Grid.SetOwner(x, y, p.Id);
            }

            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            p.Spawn(center, new Vector2(Mathf.Cos(a), Mathf.Sin(a)));
            ApplyLiveSettings(p);
        }

        private Vector2Int FindSpawnCell()
        {
            int r = run.startSize * Res;
            int margin = r + 4 * Res;
            Vector2Int candidate = new Vector2Int(Grid.Width / 2, Grid.Height / 2);
            for (int attempt = 0; attempt < 400; attempt++)
            {
                candidate = new Vector2Int(rng.Next(margin, Grid.Width - margin), rng.Next(margin, Grid.Height - margin));
                if (IsAreaFree(candidate, r + 2 * Res) && !HeadNear(candidate, r + 8 * Res)) return candidate;
            }
            // Crowded map: any playable free cell.
            for (int attempt = 0; attempt < 2000; attempt++)
            {
                candidate = new Vector2Int(rng.Next(1, Grid.Width - 1), rng.Next(1, Grid.Height - 1));
                if (IsAreaFree(candidate, 1)) return candidate;
            }
            return candidate;
        }

        private bool IsAreaFree(Vector2Int c, int r)
        {
            for (int y = c.y - r; y <= c.y + r; y++)
            for (int x = c.x - r; x <= c.x + r; x++)
            {
                if (!Grid.IsPlayable(x, y)) return false;
                if (Grid.GetOwner(x, y) != 0 || Grid.GetTrail(x, y) != 0) return false;
            }
            return true;
        }

        private bool HeadNear(Vector2Int c, int dist)
        {
            foreach (Player q in players)
                if (q.Alive && Mathf.Abs(q.Cell.x - c.x) + Mathf.Abs(q.Cell.y - c.y) <= dist) return true;
            return false;
        }

        /// <summary>Settings that may be changed while playing.</summary>
        private void ApplyLiveSettings(Player p)
        {
            p.Speed = (p.IsHuman ? run.humanSpeed : run.botSpeed) * Res;
            p.TurnRate = p.IsHuman ? run.humanTurnRate : run.botTurnRate;
        }

        // ------------------------------------------------------------------ loop

        private void Update()
        {
            if (!InGame) return;

            if (InputUtil.BackPressed())
            {
                if (State == GameState.Playing) SetPaused(!Paused);
                else ShowMenu();
                return;
            }
            if (Paused) return;

            run.CopyLiveFrom(settings, botSpeedFactor); // (live) fields follow your GameSettings file
            cam.orthographicSize = run.cameraSize * Res;

            if (State != GameState.Victory)
            {
                float dt = Mathf.Min(Time.deltaTime, 0.1f);
                for (int i = 0; i < players.Count; i++)
                {
                    Player p = players[i];
                    if (!p.Alive) continue;
                    ApplyLiveSettings(p);
                    p.Steer(p.Brain.DesiredHeading(p, this), dt);
                    Move(p, p.Speed * dt);
                }
                foreach (Player p in players)
                    if (p.Alive) p.UpdateVisual();

                if (State == GameState.Playing) UpdateCrown(dt);

                if (Human.Alive)
                    Human.BestPercent = Mathf.Max(Human.BestPercent, Grid.AreaPercent(Human.Id));
            }

            if (State != GameState.Playing && Time.unscaledTime - endTime > 0.75f && InputUtil.KeyRestartPressed())
                StartRound();
        }

        /// <summary>Moves in small sub-steps and runs the rules for every cell the head enters.</summary>
        private void Move(Player p, float distance)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / SubStep));
            float stepLen = distance / steps;
            for (int s = 0; s < steps && p.Alive; s++)
            {
                p.Position += p.Heading * stepLen;
                var cell = new Vector2Int(Mathf.FloorToInt(p.Position.x), Mathf.FloorToInt(p.Position.y));
                if (cell == p.Cell) continue;

                // Moving diagonally: also visit the corner cell between, so trails are always
                // 4-connected. Otherwise players could slip through a diagonal trail.
                if (cell.x != p.Cell.x && cell.y != p.Cell.y)
                {
                    var a = new Vector2Int(cell.x, p.Cell.y);
                    var b = new Vector2Int(p.Cell.x, cell.y);
                    Vector2 back = p.Position - p.Heading * (stepLen * 0.5f);
                    Vector2Int corner = (Player.CellCenter(a) - back).sqrMagnitude <= (Player.CellCenter(b) - back).sqrMagnitude ? a : b;
                    EnterCell(p, corner);
                    if (!p.Alive) return;
                }
                EnterCell(p, cell);
            }
        }

        /// <summary>The rule pipeline, run each time a head enters a new cell.</summary>
        private void EnterCell(Player p, Vector2Int cell)
        {
            if (!Grid.IsPlayable(cell.x, cell.y))
            {
                Kill(p, null, "You hit the wall");
                return;
            }
            p.Cell = cell;

            // Trails
            int trailOwner = Grid.GetTrail(cell.x, cell.y);
            if (trailOwner == p.Id)
            {
                if (p.IsRecentTrail(cell, NeckCells)) return; // just wobbling over our own neck
                Kill(p, null, "You crossed your own trail");
                return;
            }
            if (trailOwner != 0)
            {
                Player victim = players[trailOwner - 1];
                Kill(victim, p, $"Cut by {p.DisplayName}");
            }

            // Head-on: touching the head of a player standing in its own land kills you;
            // touching a head that is outside its land cuts that player.
            int cellOwner = Grid.GetOwner(cell.x, cell.y);
            float touch = 0.5f * Res;
            for (int i = 0; i < players.Count; i++)
            {
                Player q = players[i];
                if (q == p || !q.Alive || (q.Position - p.Position).sqrMagnitude > touch * touch) continue;
                bool qHome = Grid.GetOwner(q.Cell.x, q.Cell.y) == q.Id;
                bool pHome = cellOwner == p.Id;
                if (qHome && pHome) continue; // both safe at home
                if (qHome)
                {
                    Kill(p, q, $"Crashed into {q.DisplayName}");
                    return;
                }
                Kill(q, p, $"Crashed into {p.DisplayName}"); // q was outside: it gets cut
            }

            // Territory
            if (cellOwner == p.Id)
            {
                if (p.Trail.Count > 0) Capture(p);
            }
            else
            {
                Grid.SetTrail(cell.x, cell.y, p.Id);
                p.Trail.Add(cell);
            }

            if (p.Alive) p.Brain.OnCellEntered(p, this);
        }

        private void Capture(Player p)
        {
            foreach (Vector2Int c in p.Trail)
            {
                Grid.SetTrail(c.x, c.y, 0);
                Grid.SetOwner(c.x, c.y, p.Id);
            }
            p.ClearTrail();
            solver.FillEnclosed(Grid, p.Id);

            foreach (Player q in players)
                if (q != p && q.Alive && Grid.Area(q.Id) == 0)
                    Kill(q, p, $"{p.DisplayName} took all your land");

            CheckVictory(p);
        }

        private void CheckVictory(Player p)
        {
            if (p == Human && p.Alive && State == GameState.Playing && Grid.AreaPercent(p.Id) >= run.winPercent)
                EndRound(GameState.Victory, $"You own {run.winPercent:0}% of the arena!", Grid.AreaPercent(p.Id));
        }

        private void Kill(Player victim, Player killer, string reason)
        {
            if (!victim.Alive) return;

            float percent = Grid.AreaPercent(victim.Id);
            foreach (Vector2Int c in victim.Trail)
                if (Grid.GetTrail(c.x, c.y) == victim.Id) Grid.SetTrail(c.x, c.y, 0);

            // Crown mode: the killer gets ALL of the victim's land instead of it turning neutral.
            bool steal = killer != null && killer != victim && killer.Alive && killer.HasCrown;
            int stolen = steal ? Grid.TransferOwner(victim.Id, killer.Id) : Grid.ClearOwner(victim.Id);
            victim.Die();

            if (killer != null && killer != victim) killer.Kills++;
            if (steal && stolen > 0)
            {
                SetNews(killer == Human ? $"You took {victim.DisplayName}'s land!"
                    : victim == Human ? $"{killer.DisplayName} took your land with the crown!"
                    : $"{killer.DisplayName} took {victim.DisplayName}'s land!");
                CheckVictory(killer);
            }

            if (victim == Human)
            {
                if (State == GameState.Playing)
                    EndRound(GameState.GameOver, reason, Mathf.Max(percent, victim.BestPercent));
            }
            else
            {
                StartCoroutine(RespawnLater(victim));
            }
        }

        /// <summary>Ends the round and records it in the high scores.</summary>
        private void EndRound(GameState state, string reason, float percent)
        {
            State = state;
            EndReason = reason;
            EndPercent = percent;
            endTime = Time.unscaledTime;
            EndRank = HighScores.Add(Human.DisplayName, percent, Human.Kills, state == GameState.Victory);
        }

        // ------------------------------------------------------------------ crown power-up

        private void SetNews(string text)
        {
            News = text;
            NewsTime = RoundTime;
        }

        /// <summary>Runs the crown: counts down crown mode, spawns, expires and hands out crowns.</summary>
        private void UpdateCrown(float dt)
        {
            RoundTime += dt;
            float headSize = run.headSize * Res;
            foreach (Player p in players)
                if (p.Alive) p.TickCrown(dt, headSize);

            if (!run.enableCrown)
            {
                RemoveCrown();
                return;
            }

            if (crown == null)
            {
                if (RoundTime >= nextCrownTime)
                {
                    SpawnCrown();
                    nextCrownTime = RoundTime + Mathf.Max(1f, run.crownInterval);
                }
                return;
            }

            if (!crown.Tick(dt))
            {
                RemoveCrown();
                return;
            }

            float r = run.crownPickupRadius * Res;
            foreach (Player p in players)
            {
                if (!p.Alive || !(p.IsHuman || run.botsCanGrabCrown)) continue;
                if ((p.Position - crown.Position).sqrMagnitude > r * r) continue;
                p.SetCrown(run.crownDuration);
                SetNews(p == Human ? "CROWN! Kill enemies now to take ALL their land!"
                    : $"{p.DisplayName} grabbed the crown!");
                RemoveCrown();
                return;
            }
        }

        private void SpawnCrown()
        {
            Vector2Int cell = FindCrownCell();
            crown = new GameObject("Crown").AddComponent<Pickup>();
            crown.Setup(PickupKind.Crown, SpriteFactory.Crown, SpriteFactory.Arrow, Player.CellCenter(cell),
                run.crownSize * Res, run.crownLifetime, run.crownBlinkTime);
            SetNews("A crown appeared!");
        }

        private void RemoveCrown()
        {
            if (crown != null) Destroy(crown.gameObject);
            crown = null;
        }

        /// <summary>
        /// Early in the round: a random free spot. As the round goes on it gets more and more likely
        /// to appear just outside an enemy's land (always, after crownNearEnemyAfter seconds).
        /// </summary>
        private Vector2Int FindCrownCell()
        {
            float progress = run.crownNearEnemyAfter <= 0f ? 1f : Mathf.Clamp01(RoundTime / run.crownNearEnemyAfter);
            if (rng.NextDouble() < progress && TryCellNearEnemy(out Vector2Int near)) return near;
            return FindFreeCrownCell();
        }

        private bool TryCellNearEnemy(out Vector2Int result)
        {
            result = default;
            var enemies = new List<Player>();
            float reach = CrownReach;
            foreach (Player q in players)
            {
                if (!q.Alive || q == Human || Grid.Area(q.Id) == 0) continue;
                if (reach > 0f && Grid.GetOwnerBounds(q.Id, out int x0, out int y0, out int x1, out int y1))
                {
                    // Skip enemies whose land is nowhere near you.
                    float nx = Mathf.Clamp(Human.Position.x, x0, x1 + 1), ny = Mathf.Clamp(Human.Position.y, y0, y1 + 1);
                    if ((new Vector2(nx, ny) - Human.Position).sqrMagnitude > reach * reach) continue;
                }
                enemies.Add(q);
            }
            if (enemies.Count == 0) return false;

            int extra = Mathf.Max(1, Mathf.RoundToInt(run.crownEnemyDistance * Res));
            for (int attempt = 0; attempt < 60; attempt++)
            {
                Player enemy = enemies[rng.Next(enemies.Count)];
                if (!Grid.GetOwnerBounds(enemy.Id, out int minX, out int minY, out int maxX, out int maxY)) continue;

                // A random cell of the enemy's land...
                Vector2Int start = default;
                bool found = false;
                for (int i = 0; i < 60 && !found; i++)
                {
                    start = new Vector2Int(rng.Next(minX, maxX + 1), rng.Next(minY, maxY + 1));
                    found = Grid.GetOwner(start.x, start.y) == enemy.Id;
                }
                if (!found) continue;

                // ...then walk in a random direction until we leave their land, and a little further.
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 pos = Player.CellCenter(start);
                int outside = -1;
                for (int step = 0; step < Grid.Width + Grid.Height; step++)
                {
                    pos += dir;
                    int x = Mathf.FloorToInt(pos.x), y = Mathf.FloorToInt(pos.y);
                    if (!Grid.IsPlayable(x, y)) break;
                    if (outside < 0 && Grid.GetOwner(x, y) != enemy.Id) outside = 0;
                    if (outside >= 0 && ++outside >= extra)
                    {
                        var c = new Vector2Int(x, y);
                        if (IsGoodCrownCell(c, enemy.Id) && InCrownReach(c)) { result = c; return true; }
                        break;
                    }
                }
            }
            return false;
        }

        private Vector2Int FindFreeCrownCell()
        {
            // A random spot within reach of you: first on empty land, then anywhere that isn't a trail.
            for (int pass = 0; pass < 2; pass++)
            for (int attempt = 0; attempt < 300; attempt++)
            {
                Vector2Int c = RandomCellInReach();
                if (IsGoodCrownCell(c, -1) && (pass == 1 || Grid.GetOwner(c.x, c.y) == 0)) return c;
            }
            for (int attempt = 0; attempt < 2000; attempt++)
            {
                var c = new Vector2Int(rng.Next(0, Grid.Width), rng.Next(0, Grid.Height));
                if (Grid.IsPlayable(c.x, c.y)) return c;
            }
            return new Vector2Int(Grid.Width / 2, Grid.Height / 2);
        }

        /// <summary>Max distance from the human (in cells) a crown may appear at. 0 = no limit.</summary>
        private float CrownReach => Human != null && Human.Alive && run.crownMaxDistance > 0f ? run.crownMaxDistance * Res : 0f;

        private bool InCrownReach(Vector2Int c)
        {
            float reach = CrownReach;
            return reach <= 0f || (Player.CellCenter(c) - Human.Position).sqrMagnitude <= reach * reach;
        }

        private Vector2Int RandomCellInReach()
        {
            float reach = CrownReach;
            if (reach <= 0f) return new Vector2Int(rng.Next(1, Grid.Width - 1), rng.Next(1, Grid.Height - 1));
            // Not right on top of you either: between 30% and 100% of the reach.
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = reach * Mathf.Lerp(0.3f, 1f, (float)rng.NextDouble());
            Vector2 p = Human.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            return new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
        }

        /// <summary>Playable, not on a trail, not on the given player's land, and not hugging a wall.</summary>
        private bool IsGoodCrownCell(Vector2Int c, int notOwner)
        {
            if (!Grid.IsPlayable(c.x, c.y) || Grid.GetTrail(c.x, c.y) != 0) return false;
            if (notOwner > 0 && Grid.GetOwner(c.x, c.y) == notOwner) return false;
            int m = 2 * Res;
            return Grid.IsPlayable(c.x - m, c.y) && Grid.IsPlayable(c.x + m, c.y) &&
                   Grid.IsPlayable(c.x, c.y - m) && Grid.IsPlayable(c.x, c.y + m);
        }

        private IEnumerator RespawnLater(Player p)
        {
            yield return new WaitForSeconds(run.respawnDelay);
            if (!p.Alive) SpawnPlayer(p);
        }
    }
}
