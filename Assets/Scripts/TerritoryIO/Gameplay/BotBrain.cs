using System.Collections.Generic;
using TerritoryIO.Core;
using UnityEngine;

namespace TerritoryIO.Gameplay
{
    /// <summary>
    /// AI player with free steering. Thinks each time it enters a new cell:
    ///   1. Hunt      : an enemy trail within reach -> drive at it (aggression = reach).
    ///   2. Retreat   : enemy too close (caution) or trail too long (greed) -> ReturnHome.
    ///   3. Roam      : follow a planned circular loop of waypoints out of its land
    ///                  (greed = loop size, ExpansionStyle = which way).
    ///   4. ReturnHome: BFS path to the nearest own cell, never through its own trail.
    ///   5. Safety    : look a few cells ahead and swerve away from walls / own trail.
    /// </summary>
    public class BotBrain : IPlayerBrain
    {
        private enum Mode { Roam, ReturnHome }

        private static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private int res = 1;        // grid cells per world unit (from GameManager)
        private int neckCells = 4;
        private int thinkCounter;

        private readonly System.Random rng;
        public readonly float Aggression; // 0..1
        public readonly float Greed;      // 0..1

        private Mode mode;
        private Vector2 targetHeading;
        private readonly List<Vector2> waypoints = new List<Vector2>();
        private int planBudget;

        // BFS scratch (stamp-based, no clearing needed).
        private int[] stamp = new int[0];
        private int[] parent = new int[0];
        private int generation;
        private readonly Queue<int> bfs = new Queue<int>(256);
        private readonly List<Vector2Int> path = new List<Vector2Int>(64);

        public BotBrain(int seed, float aggression, float greed)
        {
            rng = new System.Random(seed);
            Aggression = Mathf.Clamp01(aggression);
            Greed = Mathf.Clamp01(greed);
        }

        private float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public void OnSpawn(Player self)
        {
            mode = Mode.Roam;
            waypoints.Clear();
            thinkCounter = int.MaxValue - 1; // think on the very first cell
            targetHeading = self.Heading;
        }

        public Vector2 DesiredHeading(Player self, GameManager game) => targetHeading;

        public void OnCellEntered(Player self, GameManager game)
        {
            res = game.Res;
            neckCells = game.NeckCells;
            // With a fine grid the head enters many cells per unit; think about twice per unit.
            if (++thinkCounter < Mathf.Max(1, res / 2)) return;
            thinkCounter = 0;

            GridMap grid = game.Grid;
            GameSettings s = game.Settings;
            bool inside = grid.GetOwner(self.Cell.x, self.Cell.y) == self.Id;

            if (inside && self.Trail.Count == 0 && mode == Mode.ReturnHome)
            {
                mode = Mode.Roam;
                waypoints.Clear();
            }

            // 1. Hunt
            if (TryHunt(self, game, out Vector2 huntHeading))
            {
                targetHeading = Safe(self, grid, huntHeading);
                return;
            }

            // 2. Retreat
            int maxTrail = Mathf.RoundToInt(Mathf.Lerp(12f, 70f, Greed) * res);
            int fear = Mathf.Max(1, Mathf.RoundToInt(s.caution * res * (1f - Aggression * 0.4f)));
            if (self.Trail.Count > 0 && mode != Mode.ReturnHome &&
                (self.Trail.Count >= maxTrail || InDanger(self, game, fear)))
            {
                mode = Mode.ReturnHome;
                waypoints.Clear();
            }

            // 3. Roam along the planned loop
            Vector2 desired = self.Heading;
            if (mode == Mode.Roam)
            {
                while (waypoints.Count > 0 && (waypoints[0] - self.Position).magnitude < 1.5f * res)
                    waypoints.RemoveAt(0);

                if (waypoints.Count == 0 || --planBudget <= 0)
                {
                    waypoints.Clear();
                    if (self.Trail.Count > 0) mode = Mode.ReturnHome;
                    else MakePlan(self, game);
                }
                if (mode == Mode.Roam && waypoints.Count > 0)
                    desired = waypoints[0] - self.Position;
            }

            // 4. Return home
            if (mode == Mode.ReturnHome)
            {
                if (FindPath(self, grid, 40000, 300 * res, c => grid.GetOwner(c.x, c.y) == self.Id))
                    desired = AimAlongPath(self);
            }

            // 5. Safety
            targetHeading = Safe(self, grid, desired);
        }

        // ------------------------------------------------------------------ planning

        /// <summary>Plans a circular loop that starts at the head and bulges out of the land.</summary>
        private void MakePlan(Player self, GameManager game)
        {
            waypoints.Clear();
            float baseAngle = PreferredAngle(self, game) + Rand(-35f, 35f);
            Vector2 dir = new Vector2(Mathf.Cos(baseAngle * Mathf.Deg2Rad), Mathf.Sin(baseAngle * Mathf.Deg2Rad));

            float radius = Mathf.Lerp(2.5f, 14f, Greed) * Rand(0.7f, 1.2f) * res;
            float side = rng.Next(2) == 0 ? 1f : -1f;
            float sweep = Rand(250f, 330f);
            Vector2 center = self.Position + dir * radius;

            // Points on the circle, starting at the head and going around.
            Vector2 start = -dir;
            for (float a = 40f; a <= sweep; a += 40f)
            {
                float r = a * side * Mathf.Deg2Rad;
                Vector2 p = center + new Vector2(
                    start.x * Mathf.Cos(r) - start.y * Mathf.Sin(r),
                    start.x * Mathf.Sin(r) + start.y * Mathf.Cos(r)) * radius;
                if (game.Grid.IsPlayable(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y))) waypoints.Add(p);
            }
            planBudget = Mathf.CeilToInt((2f * Mathf.PI * radius * 1.6f + 10f * res) / Mathf.Max(1, res / 2));
        }

        private float PreferredAngle(Player self, GameManager game)
        {
            GridMap grid = game.Grid;
            Vector2 mapCenter = new Vector2(grid.Width * 0.5f, grid.Height * 0.5f);
            Vector2 v;
            switch (game.Settings.expansion)
            {
                case ExpansionStyle.TowardCenter:
                    v = mapCenter - self.Position;
                    break;
                case ExpansionStyle.AwayFromCenter:
                    v = self.Position - mapCenter;
                    break;
                case ExpansionStyle.TowardPlayer:
                    v = game.Human != null && game.Human.Alive ? game.Human.Position - self.Position : Vector2.zero;
                    break;
                case ExpansionStyle.AwayFromOthers:
                    v = Vector2.zero;
                    float best = float.MaxValue;
                    foreach (Player q in game.Players)
                    {
                        if (q == self || !q.Alive) continue;
                        float d = (q.Position - self.Position).sqrMagnitude;
                        if (d < best) { best = d; v = self.Position - q.Position; }
                    }
                    break;
                default:
                    v = Vector2.zero;
                    break;
            }
            if (v.sqrMagnitude < 0.01f) return Rand(0f, 360f);
            return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        // ------------------------------------------------------------------ perception

        private bool TryHunt(Player self, GameManager game, out Vector2 heading)
        {
            heading = Vector2.zero;
            GridMap grid = game.Grid;
            int range = (2 + Mathf.RoundToInt(Aggression * 10f)) * res;
            Player human = game.Human;

            // Player-first hunting: longer reach for the human's trail.
            if ((game.Settings.botsHuntPlayer || rng.NextDouble() < Aggression * 0.3) &&
                human != null && human.Alive && human.Trail.Count > 0)
            {
                if (FindPath(self, grid, range * range * 8, range * 2, c => grid.GetTrail(c.x, c.y) == human.Id))
                {
                    heading = AimAlongPath(self);
                    return true;
                }
            }

            if (Aggression <= 0.01f) return false;
            if (FindPath(self, grid, range * range * 4, range,
                    c => { int t = grid.GetTrail(c.x, c.y); return t != 0 && t != self.Id; }))
            {
                heading = AimAlongPath(self);
                return true;
            }
            return false;
        }

        private bool InDanger(Player self, GameManager game, int fear)
        {
            foreach (Player q in game.Players)
            {
                if (q == self || !q.Alive) continue;
                if (Manhattan(q.Cell, self.Cell) <= fear) return true;
                for (int i = 0; i < self.Trail.Count; i += 2 * res)
                    if (Manhattan(q.Cell, self.Trail[i]) <= fear - res) return true;
            }
            return false;
        }

        /// <summary>
        /// BFS from the head through cells that are playable and not our own trail, not starting
        /// backwards. Fills `path` (head excluded) toward the nearest goal cell within maxDepth.
        /// </summary>
        private bool FindPath(Player self, GridMap grid, int maxNodes, int maxDepth, System.Func<Vector2Int, bool> goal)
        {
            path.Clear();
            int n = grid.CellCount;
            if (stamp.Length != n)
            {
                stamp = new int[n];
                parent = new int[n];
                generation = 0;
            }
            if (++generation == int.MaxValue) { System.Array.Clear(stamp, 0, n); generation = 1; }

            bfs.Clear();
            int startIndex = grid.Index(self.Cell.x, self.Cell.y);
            stamp[startIndex] = generation;
            parent[startIndex] = -1;

            foreach (Vector2Int d in Dirs)
            {
                if (Vector2.Dot(d, self.Heading) < -0.5f) continue; // no U-turn on the first step
                Vector2Int c = self.Cell + d;
                if (!Passable(self, grid, c)) continue;
                int i = grid.Index(c.x, c.y);
                stamp[i] = generation;
                parent[i] = startIndex;
                if (goal(c)) return BuildPath(grid, i);
                bfs.Enqueue(i);
            }

            int visited = 0;
            while (bfs.Count > 0 && visited++ < maxNodes)
            {
                int ci = bfs.Dequeue();
                Vector2Int c = new Vector2Int(ci % grid.Width, ci / grid.Width);
                if (Manhattan(c, self.Cell) >= maxDepth) continue;
                foreach (Vector2Int d in Dirs)
                {
                    Vector2Int nb = c + d;
                    if (!Passable(self, grid, nb)) continue;
                    int i = grid.Index(nb.x, nb.y);
                    if (stamp[i] == generation) continue;
                    stamp[i] = generation;
                    parent[i] = ci;
                    if (goal(nb)) return BuildPath(grid, i);
                    bfs.Enqueue(i);
                }
            }
            return false;
        }

        private bool BuildPath(GridMap grid, int goalIndex)
        {
            path.Clear();
            for (int i = goalIndex; i >= 0 && parent[i] >= 0; i = parent[i])
                path.Add(new Vector2Int(i % grid.Width, i / grid.Width));
            path.Reverse();
            return path.Count > 0;
        }

        /// <summary>Aim a few cells down the path so the bot drives smooth curves, not stairs.</summary>
        private Vector2 AimAlongPath(Player self)
        {
            Vector2Int target = path[Mathf.Min(3 * res, path.Count - 1)];
            return Player.CellCenter(target) - self.Position;
        }

        private static bool Passable(Player self, GridMap grid, Vector2Int c)
        {
            return grid.IsPlayable(c.x, c.y) && grid.GetTrail(c.x, c.y) != self.Id;
        }

        /// <summary>Look ahead along a heading; swerve to the nearest clear direction if blocked.</summary>
        private Vector2 Safe(Player self, GridMap grid, Vector2 desired)
        {
            if (desired.sqrMagnitude < 0.0001f) desired = self.Heading;
            desired.Normalize();
            if (Clear(self, grid, desired)) return desired;

            float side = rng.Next(2) == 0 ? 1f : -1f;
            for (float a = 25f; a <= 180f; a += 25f)
            {
                for (int k = 0; k < 2; k++)
                {
                    float r = a * side * (k == 0 ? 1f : -1f) * Mathf.Deg2Rad;
                    Vector2 d = new Vector2(desired.x * Mathf.Cos(r) - desired.y * Mathf.Sin(r),
                                            desired.x * Mathf.Sin(r) + desired.y * Mathf.Cos(r));
                    if (Clear(self, grid, d)) return d;
                }
            }
            return desired;
        }

        private bool Clear(Player self, GridMap grid, Vector2 dir)
        {
            for (float dist = 0.8f; dist <= 3.2f * res; dist += 0.8f)
            {
                Vector2 p = self.Position + dir * dist;
                var c = new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y));
                if (!grid.IsPlayable(c.x, c.y)) return false;
                if (grid.GetTrail(c.x, c.y) == self.Id && !self.IsRecentTrail(c, neckCells)) return false;
            }
            return true;
        }

        private static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }
}
