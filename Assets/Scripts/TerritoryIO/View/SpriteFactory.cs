using UnityEngine;

namespace TerritoryIO.View
{
    /// <summary>Generates simple sprites at runtime so the project needs no art assets.</summary>
    public static class SpriteFactory
    {
        private static Sprite square;
        private static Sprite crown;

        /// <summary>A gold crown with a dark outline, 1 world unit wide, pivot in the centre.</summary>
        public static Sprite Crown
        {
            get
            {
                if (crown != null) return crown;
                const int n = 64;
                var inside = new bool[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    inside[y * n + x] = InCrown(x, y);

                var gold = new Color32(255, 204, 51, 255);
                var shade = new Color32(214, 150, 20, 255);
                var outline = Palette.HeadOutline;
                var clear = new Color32(0, 0, 0, 0);
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    if (inside[y * n + x])
                    {
                        // Darker band at the bottom gives it a bit of depth.
                        pixels[y * n + x] = y < 16 ? shade : gold;
                        continue;
                    }
                    bool edge = false;
                    for (int dy = -3; dy <= 3 && !edge; dy++)
                    for (int dx = -3; dx <= 3 && !edge; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (dx * dx + dy * dy <= 9 && xx >= 0 && yy >= 0 && xx < n && yy < n && inside[yy * n + xx])
                            edge = true;
                    }
                    pixels[y * n + x] = edge ? outline : clear;
                }

                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                tex.SetPixels32(pixels);
                tex.Apply();
                crown = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
                crown.name = "Crown";
                return crown;
            }
        }

        /// <summary>Crown shape on a 64x64 grid: a band, three spikes and a ball on each tip.</summary>
        private static bool InCrown(int x, int y)
        {
            if (x >= 8 && x <= 55 && y >= 8 && y <= 24) return true; // band
            int[] tips = { 13, 32, 51 };
            foreach (int cx in tips)
            {
                if (y > 24 && y <= 48 && Mathf.Abs(x - cx) <= 9f * (48 - y) / 24f + 1f) return true; // spike
                int bx = x - cx, by = y - 51;
                if (bx * bx + by * by <= 25) return true; // ball
            }
            return false;
        }

        /// <summary>1×1 world-unit white square, pivot in the centre.</summary>
        public static Sprite Square
        {
            get
            {
                if (square != null) return square;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(pixels);
                tex.Apply();
                square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                square.name = "TerritorySquare";
                return square;
            }
        }

        public static SpriteRenderer CreateSquare(string name, Transform parent, Color color, int sortingOrder)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Square;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
