using System.Collections.Generic;

namespace TerritoryIO.Core
{
    /// <summary>
    /// Claims every cell enclosed by a player's land.
    /// Call it after the player's trail cells have been converted to land.
    ///
    /// Algorithm: take the bounding box of the player's land grown by one cell
    /// (the ring may lie outside the arena, which counts as "outside").
    /// Flood fill from that ring through every cell NOT owned by the player.
    /// Anything in the box that the fill did not reach is enclosed and is claimed.
    /// Cost is O(box area) and runs only on capture.
    /// </summary>
    public class CaptureSolver
    {
        private bool[] visited = new bool[0];
        private readonly Queue<int> queue = new Queue<int>(1024);

        /// <returns>Number of newly claimed cells.</returns>
        public int FillEnclosed(GridMap grid, int playerId)
        {
            if (!grid.GetOwnerBounds(playerId, out int minX, out int minY, out int maxX, out int maxY))
                return 0;

            // Box in "box space": includes a 1-cell ring around the land.
            int bx0 = minX - 1, by0 = minY - 1;
            int bw = (maxX - minX + 1) + 2;
            int bh = (maxY - minY + 1) + 2;
            int size = bw * bh;

            if (visited.Length < size) visited = new bool[size];
            System.Array.Clear(visited, 0, size);
            queue.Clear();

            // Seed the ring.
            for (int x = 0; x < bw; x++)
            {
                TrySeed(grid, playerId, bx0, by0, bw, x, 0);
                TrySeed(grid, playerId, bx0, by0, bw, x, bh - 1);
            }
            for (int y = 1; y < bh - 1; y++)
            {
                TrySeed(grid, playerId, bx0, by0, bw, 0, y);
                TrySeed(grid, playerId, bx0, by0, bw, bw - 1, y);
            }

            // BFS through non-owned cells.
            while (queue.Count > 0)
            {
                int i = queue.Dequeue();
                int lx = i % bw, ly = i / bw;
                Visit(grid, playerId, bx0, by0, bw, bh, lx + 1, ly);
                Visit(grid, playerId, bx0, by0, bw, bh, lx - 1, ly);
                Visit(grid, playerId, bx0, by0, bw, bh, lx, ly + 1);
                Visit(grid, playerId, bx0, by0, bw, bh, lx, ly - 1);
            }

            // Unreached cells inside the real box are enclosed.
            int claimed = 0;
            for (int ly = 1; ly < bh - 1; ly++)
            for (int lx = 1; lx < bw - 1; lx++)
            {
                if (visited[ly * bw + lx]) continue;
                int gx = bx0 + lx, gy = by0 + ly;
                if (!grid.IsPlayable(gx, gy)) continue; // walls are never claimed
                if (grid.GetOwner(gx, gy) == playerId) continue;
                grid.SetOwner(gx, gy, playerId);
                claimed++;
            }
            return claimed;
        }

        private void TrySeed(GridMap grid, int id, int bx0, int by0, int bw, int lx, int ly)
        {
            int li = ly * bw + lx;
            if (visited[li]) return;
            if (IsBlocked(grid, id, bx0 + lx, by0 + ly)) return;
            visited[li] = true;
            queue.Enqueue(li);
        }

        private void Visit(GridMap grid, int id, int bx0, int by0, int bw, int bh, int lx, int ly)
        {
            if (lx < 0 || ly < 0 || lx >= bw || ly >= bh) return;
            int li = ly * bw + lx;
            if (visited[li]) return;
            if (IsBlocked(grid, id, bx0 + lx, by0 + ly)) return;
            visited[li] = true;
            queue.Enqueue(li);
        }

        // Only the player's own land blocks the fill. Off-grid and wall cells are open "outside".
        private static bool IsBlocked(GridMap grid, int id, int gx, int gy)
        {
            return grid.InBounds(gx, gy) && grid.GetOwner(gx, gy) == id;
        }
    }
}
