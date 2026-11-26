using UnityEngine;

namespace TerritoryIO.Gameplay
{
    /// <summary>Builds the playable-cell mask for each MapShape.</summary>
    public static class MapShapes
    {
        public static bool[] Build(GameSettings s, int w, int h)
        {
            var mask = new bool[w * h];
            Color[] custom = null;
            int cw = 0, ch = 0;
            MapShape shape = s.mapShape;

            if (shape == MapShape.CustomImage)
            {
                if (s.customMapImage == null || !s.customMapImage.isReadable)
                {
                    Debug.LogWarning("Territory.io: Custom map image is missing or not readable " +
                                     "(select the image > Import Settings > tick Read/Write > Apply). Using a rectangle.");
                    shape = MapShape.Rectangle;
                }
                else
                {
                    custom = s.customMapImage.GetPixels();
                    cw = s.customMapImage.width;
                    ch = s.customMapImage.height;
                }
            }

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Normalised coordinates: -1..1 across the grid.
                float u = (x + 0.5f) / w * 2f - 1f;
                float v = (y + 0.5f) / h * 2f - 1f;
                bool on;
                switch (shape)
                {
                    case MapShape.Circle:
                        on = u * u + v * v <= 1f;
                        break;
                    case MapShape.Diamond:
                        on = Mathf.Abs(u) + Mathf.Abs(v) <= 1f;
                        break;
                    case MapShape.Hexagon:
                        float au = Mathf.Abs(u), av = Mathf.Abs(v);
                        on = av <= 0.866f && au * 0.866f + av * 0.5f <= 0.866f;
                        break;
                    case MapShape.Donut:
                        float r2 = u * u + v * v;
                        on = r2 <= 1f && r2 >= 0.3f * 0.3f;
                        break;
                    case MapShape.Cross:
                        on = Mathf.Abs(u) <= 0.4f || Mathf.Abs(v) <= 0.4f;
                        break;
                    case MapShape.CustomImage:
                        int px = Mathf.Clamp((int)((float)x / w * cw), 0, cw - 1);
                        int py = Mathf.Clamp((int)((float)y / h * ch), 0, ch - 1);
                        Color c = custom[py * cw + px];
                        on = c.a > 0.5f && c.grayscale > 0.5f;
                        break;
                    default:
                        on = true;
                        break;
                }

                // Non-rectangular maps keep a one-cell wall ring so the edge is always visible.
                if (shape != MapShape.Rectangle && (x == 0 || y == 0 || x == w - 1 || y == h - 1))
                    on = false;

                mask[y * w + x] = on;
            }
            return mask;
        }
    }
}
