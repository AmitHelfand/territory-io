using System;

namespace TerritoryIO.Core
{
    /// <summary>
    /// Pure data model of the arena. No Unity dependencies, so it can be unit tested
    /// or moved to a server later.
    /// owner[i] = player id that owns the cell (0 = neutral)
    /// trail[i] = player id whose trail is on the cell (0 = none)
    /// </summary>
    public class GridMap
    {
        public readonly int Width;
        public readonly int Height;
        public readonly int MaxPlayers;

        private readonly int[] owner;
        private readonly int[] trail;
        private readonly int[] area; // indexed by player id
        private readonly bool[] playable; // false = wall (map shape)

        /// <summary>Raised whenever the owner or trail of a cell changes.</summary>
        public event Action<int, int> CellChanged;

        /// <param name="playableMask">Optional width*height mask; false cells are walls.</param>
        public GridMap(int width, int height, int maxPlayers, bool[] playableMask = null)
        {
            Width = width;
            Height = height;
            MaxPlayers = maxPlayers;
            owner = new int[width * height];
            trail = new int[width * height];
            area = new int[maxPlayers + 1];
            playable = new bool[width * height];
            PlayableCount = 0;
            for (int i = 0; i < playable.Length; i++)
            {
                playable[i] = playableMask == null || playableMask[i];
                if (playable[i]) PlayableCount++;
            }
        }

        public int CellCount => Width * Height;

        /// <summary>Number of non-wall cells. Scores are a share of this.</summary>
        public int PlayableCount { get; }

        /// <summary>Inside the grid and not a wall of the map shape.</summary>
        public bool IsPlayable(int x, int y) => InBounds(x, y) && playable[Index(x, y)];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public int Index(int x, int y) => y * Width + x;

        public int GetOwner(int x, int y) => owner[Index(x, y)];
        public int GetTrail(int x, int y) => trail[Index(x, y)];

        public int Area(int playerId) => area[playerId];

        public float AreaPercent(int playerId) => 100f * area[playerId] / PlayableCount;

        public void SetOwner(int x, int y, int playerId)
        {
            int i = Index(x, y);
            int old = owner[i];
            if (old == playerId) return;
            if (old != 0) area[old]--;
            if (playerId != 0) area[playerId]++;
            owner[i] = playerId;
            CellChanged?.Invoke(x, y);
        }

        public void SetTrail(int x, int y, int playerId)
        {
            int i = Index(x, y);
            if (trail[i] == playerId) return;
            trail[i] = playerId;
            CellChanged?.Invoke(x, y);
        }

        /// <summary>Removes all land of a player. Returns cells cleared.</summary>
        public int ClearOwner(int playerId)
        {
            int cleared = 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (owner[Index(x, y)] == playerId)
                {
                    SetOwner(x, y, 0);
                    cleared++;
                }
            }
            return cleared;
        }

        /// <summary>Gives all land of one player to another (crown power-up). Returns cells moved.</summary>
        public int TransferOwner(int fromId, int toId)
        {
            int moved = 0;
            for (int i = 0; i < owner.Length; i++)
            {
                if (owner[i] != fromId) continue;
                SetOwner(i % Width, i / Width, toId);
                moved++;
            }
            return moved;
        }

        public void ClearAll()
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                SetOwner(x, y, 0);
                SetTrail(x, y, 0);
            }
        }

        /// <summary>Bounding box of all cells owned by playerId. False if none.</summary>
        public bool GetOwnerBounds(int playerId, out int minX, out int minY, out int maxX, out int maxY)
        {
            minX = Width; minY = Height; maxX = -1; maxY = -1;
            for (int y = 0; y < Height; y++)
            {
                int row = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    if (owner[row + x] != playerId) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            return maxX >= 0;
        }
    }
}
