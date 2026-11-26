using UnityEngine;

namespace TerritoryIO.View
{
    /// <summary>Generates simple sprites at runtime so the project needs no art assets.</summary>
    public static class SpriteFactory
    {
        private static Sprite square;
        private static Sprite crown;
        private static Sprite arrow;

        private static Sprite roundedSquare;

        // All shapes below are drawn from a "signed distance" (negative inside, positive outside, in
        // texture pixels), so every edge gets exactly one pixel of anti-aliasing and mipmaps keep them
        // smooth when they are drawn small. No jagged stairs at any size.

        /// <summary>A gold arrow pointing RIGHT with a dark outline, 1 world unit, pivot in the centre.</summary>
        public static Sprite Arrow
        {
            get
            {
                if (arrow != null) return arrow;
                const int n = 64;
                var gold = new Color32(255, 204, 51, 255);
                arrow = Build("Arrow", n, n, (x, y) =>
                {
                    // Triangle: tip at the right (x = 58), base at x = 6, half height 26.
                    float d = Triangle(x, y, new Vector2(58f, 32f), new Vector2(6f, 58f), new Vector2(6f, 6f));
                    return Layers(d, 5f, gold, Palette.HeadOutline);
                });
                return arrow;
            }
        }

        /// <summary>A gold crown with a dark outline, 1 world unit wide, pivot in the centre.</summary>
        public static Sprite Crown
        {
            get
            {
                if (crown != null) return crown;
                const int n = 128;
                var gold = new Color32(255, 204, 51, 255);
                var shade = new Color32(214, 150, 20, 255);
                crown = Build("Crown", n, n, (x, y) =>
                {
                    float d = CrownDistance(x, y);
                    // Darker band at the bottom gives it a bit of depth (soft one-pixel step).
                    Color32 body = Color32.Lerp(shade, gold, Mathf.Clamp01(y - 31.5f));
                    return Layers(d, 6f, body, Palette.HeadOutline);
                });
                return crown;
            }
        }

        /// <summary>White square with softly rounded corners, 1 world unit, pivot in the centre (player heads).</summary>
        public static Sprite RoundedSquare
        {
            get
            {
                if (roundedSquare != null) return roundedSquare;
                const int n = 64, pad = 2;
                const float half = (n - 2 * pad) * 0.5f, radius = 10f;
                var white = new Color32(255, 255, 255, 255);
                roundedSquare = Build("RoundedSquare", n, n - 2 * pad, (x, y) =>
                {
                    float qx = Mathf.Abs(x - n * 0.5f) - (half - radius);
                    float qy = Mathf.Abs(y - n * 0.5f) - (half - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    return Fill(d, white);
                });
                return roundedSquare;
            }
        }

        /// <summary>Like CreateSquare, but with rounded, anti-aliased corners.</summary>
        public static SpriteRenderer CreateRoundedSquare(string name, Transform parent, Color color, int sortingOrder)
        {
            var sr = CreateSquare(name, parent, color, sortingOrder);
            sr.sprite = RoundedSquare;
            return sr;
        }

        // ------------------------------------------------------------------ drawing helpers

        /// <summary>
        /// Makes an n×n sprite with mipmaps. shade(x, y) gets the pixel centre in texture pixels.
        /// unitPixels = how many texture pixels make 1 world unit.
        /// </summary>
        private static Sprite Build(string name, int n, float unitPixels, System.Func<float, float, Color32> shade)
        {
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                pixels[y * n + x] = shade(x + 0.5f, y + 0.5f);

            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true)
            {
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 1,
                name = name
            };
            tex.SetPixels32(pixels);
            tex.Apply(true, true); // build mipmaps, then free the CPU copy
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), unitPixels, 0,
                SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }

        /// <summary>A shape filled with one colour; outside pixels keep the colour (alpha 0) so edges don't darken.</summary>
        private static Color32 Fill(float d, Color32 color)
        {
            color.a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(0.5f - d));
            return color;
        }

        /// <summary>A shape with an outline of the given width around it.</summary>
        private static Color32 Layers(float d, float outlineWidth, Color32 body, Color32 outline)
        {
            Color32 c = Color32.Lerp(outline, body, Mathf.Clamp01(0.5f - d));
            c.a = (byte)Mathf.RoundToInt(255f * Mathf.Clamp01(outlineWidth + 0.5f - d));
            return c;
        }

        /// <summary>Signed distance to the crown (on a 128×128 grid): a band, three spikes and a ball on each tip.</summary>
        private static float CrownDistance(float x, float y)
        {
            float d = Box(x, y, 16f, 16f, 112f, 50f); // band
            float[] tips = { 26f, 64f, 102f };
            foreach (float cx in tips)
            {
                // Spike: wide at the band (y = 48), narrow at the top (y = 96).
                float spike = Polygon4(x, y,
                    new Vector2(cx - 20f, 46f), new Vector2(cx + 20f, 46f),
                    new Vector2(cx + 2f, 96f), new Vector2(cx - 2f, 96f));
                float ball = new Vector2(x - cx, y - 102f).magnitude - 10f;
                d = Mathf.Min(d, Mathf.Min(spike, ball));
            }
            return d;
        }

        private static float Box(float x, float y, float x0, float y0, float x1, float y1)
        {
            float qx = Mathf.Max(x0 - x, x - x1);
            float qy = Mathf.Max(y0 - y, y - y1);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f);
        }

        /// <summary>Distance to a convex shape given counter-clockwise (exact inside, close enough outside).</summary>
        private static float Polygon4(float x, float y, Vector2 a, Vector2 b, Vector2 c, Vector2 e)
        {
            var p = new Vector2(x, y);
            return Mathf.Max(Mathf.Max(Side(p, a, b), Side(p, b, c)), Mathf.Max(Side(p, c, e), Side(p, e, a)));
        }

        private static float Triangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
        {
            var p = new Vector2(x, y);
            return Mathf.Max(Side(p, a, b), Mathf.Max(Side(p, b, c), Side(p, c, a)));
        }

        /// <summary>Signed distance to the line a→b; positive on the right side (outside for counter-clockwise shapes).</summary>
        private static float Side(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 dir = (b - a).normalized;
            Vector2 rel = p - a;
            return dir.y * rel.x - dir.x * rel.y;
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
