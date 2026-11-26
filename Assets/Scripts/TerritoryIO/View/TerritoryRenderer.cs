using System;
using System.Collections.Generic;
using TerritoryIO.Core;
using Unity.Collections;
using UnityEngine;

namespace TerritoryIO.View
{
    /// <summary>
    /// Draws the whole arena.
    ///
    /// SHADER MODE (default, GameSettings.landShader): the texture holds one texel per cell with the
    /// owner's id, and the TerritoryLand shader (Assets/Resources) draws smooth, anti-aliased borders
    /// for every screen pixel. Sharp at any zoom or screen size, and cheaper than the old way
    /// (a 4x smaller texture, and only the changed cell is redrawn).
    ///
    /// TEXTURE MODE (fallback when the shader is off or not supported):
    /// detail = texture pixels per grid cell (GameSettings.landDetail).
    ///  - detail 1: one pixel per cell (the old look; blurry with Bilinear, blocky with Point).
    ///  - detail 2-8: each cell is drawn as detail×detail pixels. Every pixel looks at the 4 nearest
    ///    cell centres and takes the colour that "wins" by distance, so staircase edges become crisp,
    ///    smooth curves instead of a blur.
    /// Listens to GridMap.CellChanged, redraws only the changed cells (and their neighbours),
    /// and uploads the texture once per frame when something changed.
    /// The sprite's pivot is bottom-left at world (0,0), so cell (x,y) covers [x,x+1]×[y,y+1].
    /// </summary>
    public class TerritoryRenderer : MonoBehaviour
    {
        private const int WallKey = -1;

        private GridMap grid;
        private Func<int, Color32> playerColor;
        private Texture2D texture;
        private Sprite sprite;
        private NativeArray<Color32> pixels; // points straight into the texture memory (no copy)
        private bool dirty;
        private bool drawTrailCells = true;
        private int checkerCells = 4;

        private int detail = 1;     // pixels per cell
        private int texWidth;
        private float[] subT;       // per sub-pixel: blend weight toward the next cell
        private int[] subBase;      // per sub-pixel: -1 = blend with previous cell, 0 = with next
        private bool[] cellDirty;
        private readonly List<int> dirtyCells = new List<int>(4096);
        private readonly int[] keys = new int[9]; // 3×3 neighbourhood of the cell being drawn
        private Color32[] landColors;

        // Shader mode
        private const byte EdgeWallKey = 254;
        private const byte OutsideKey = 255;
        private bool useShader;
        private int bytesPerTexel = 4;
        private NativeArray<byte> keyBytes;
        private Texture2D paletteTexture;
        private Material material;

        /// <summary>True when the smooth land shader is drawing the land.</summary>
        public bool UsesShader => useShader;

        /// <summary>Pixels per cell actually used (may be lower than asked, to respect texture size limits).</summary>
        public int Detail => detail;

        /// <param name="drawTrails">Paint trail cells into the texture (off when trails are drawn as smooth lines).</param>
        /// <param name="checkerSize">Size of the floor checker squares, in cells.</param>
        /// <param name="pixelsPerCell">Land sharpness (GameSettings.landDetail). Ignored when smoothEdges is off.</param>
        /// <param name="preferShader">Use the smooth land shader when the device supports it (GameSettings.landShader).</param>
        public void Init(GridMap map, Func<int, Color32> colorForPlayer, bool smoothEdges, bool drawFrame,
            bool drawTrails, int checkerSize, int pixelsPerCell = 1, bool preferShader = true)
        {
            drawTrailCells = drawTrails;
            checkerCells = Mathf.Max(1, checkerSize);
            grid = map;
            playerColor = colorForPlayer;

            landColors = new Color32[grid.MaxPlayers + 1];
            for (int id = 1; id <= grid.MaxPlayers; id++) landColors[id] = Palette.Land(playerColor(id));

            var sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0;

            if (preferShader && smoothEdges && grid.MaxPlayers < EdgeWallKey && TryInitShader(sr))
            {
                useShader = true;
                detail = 1;
            }
            else
            {
                InitTexture(sr, smoothEdges, pixelsPerCell);
            }

            // Wall frame around a rectangular arena (other shapes draw their walls in the texture).
            if (drawFrame)
            {
                var wall = SpriteFactory.CreateSquare("Wall", transform, Palette.Wall, -1);
                wall.transform.localPosition = new Vector3(grid.Width * 0.5f, grid.Height * 0.5f, 0f);
                float border = Mathf.Max(1f, checkerCells / 4f); // about 0.5 world units on each side
                wall.transform.localScale = new Vector3(grid.Width + border, grid.Height + border, 1f);
            }

            grid.CellChanged += OnCellChanged;
            RedrawAll();
        }

        /// <summary>Sets up the smooth land shader. Returns false (and changes nothing) if it can't run here.</summary>
        private bool TryInitShader(SpriteRenderer sr)
        {
            var shader = Resources.Load<Shader>("TerritoryLand");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("Territory.io: smooth land shader not available, using the texture fallback.");
                return false;
            }
            if (Mathf.Max(grid.Width, grid.Height) > SystemInfo.maxTextureSize) return false;

            // One texel per cell. R8 = owner only (smallest upload); RGBA32 when trails are drawn as cells too.
            var format = drawTrailCells ? TextureFormat.RGBA32 : TextureFormat.R8;
            if (!SystemInfo.SupportsTextureFormat(format)) format = TextureFormat.RGBA32;
            bytesPerTexel = format == TextureFormat.R8 ? 1 : 4;

            // linear: true = the ids are data, not colours, so no sRGB conversion may touch them.
            texture = new Texture2D(grid.Width, grid.Height, format, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                name = "TerritoryKeys"
            };
            keyBytes = texture.GetRawTextureData<byte>();
            texWidth = grid.Width;
            cellDirty = new bool[grid.CellCount];

            // Palette: row 0 = land colour per key, row 1 = trail colour per player.
            paletteTexture = new Texture2D(256, 2, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "TerritoryPalette"
            };
            var pal = new Color32[512];
            for (int id = 1; id <= grid.MaxPlayers; id++)
            {
                pal[id] = landColors[id];
                pal[256 + id] = Color32.Lerp(playerColor(id), new Color32(255, 255, 255, 255), 0.35f); // same as Palette.Trail
            }
            pal[EdgeWallKey] = Palette.Wall;
            pal[OutsideKey] = Palette.Background;
            paletteTexture.SetPixels32(pal);
            paletteTexture.Apply(false, true); // no longer needs to be readable

            material = new Material(shader) { name = "TerritoryLand" };
            material.SetTexture("_MainTex", texture);
            material.SetTexture("_Palette", paletteTexture);
            material.SetVector("_GridSize", new Vector4(grid.Width, grid.Height, 1f / grid.Width, 1f / grid.Height));
            material.SetColor("_FloorA", Palette.EmptyA);
            material.SetColor("_FloorB", Palette.EmptyB);
            material.SetFloat("_Checker", checkerCells);
            material.SetFloat("_TrailCells", drawTrailCells ? 1f : 0f);

            // pixelsPerUnit = 1 keeps 1 cell = 1 world unit. FullRect: one plain quad.
            sprite = Sprite.Create(texture, new Rect(0, 0, grid.Width, grid.Height), Vector2.zero, 1f, 0,
                SpriteMeshType.FullRect);
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            return true;
        }

        private void InitTexture(SpriteRenderer sr, bool smoothEdges, int pixelsPerCell)
        {

            // Keep the texture within what the device can handle (phones: 2048, PC: 4096).
            detail = smoothEdges ? Mathf.Clamp(pixelsPerCell, 1, 8) : 1;
            int maxTex = Mathf.Min(SystemInfo.maxTextureSize, Application.isMobilePlatform ? 2048 : 4096);
            while (detail > 1 && Mathf.Max(grid.Width, grid.Height) * detail > maxTex) detail--;

            subT = new float[detail];
            subBase = new int[detail];
            for (int s = 0; s < detail; s++)
            {
                float off = (s + 0.5f) / detail - 0.5f; // pixel centre relative to the cell centre, -0.5..0.5
                subBase[s] = off < 0f ? -1 : 0;
                subT[s] = off < 0f ? off + 1f : off;
            }

            texWidth = grid.Width * detail;
            texture = new Texture2D(texWidth, grid.Height * detail, TextureFormat.RGBA32, false)
            {
                filterMode = smoothEdges ? FilterMode.Bilinear : FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0,
                name = "TerritoryTexture"
            };
            pixels = texture.GetRawTextureData<Color32>();
            cellDirty = new bool[grid.CellCount];

            // pixelsPerUnit = detail keeps 1 cell = 1 world unit. FullRect: one plain quad.
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero, detail, 0,
                SpriteMeshType.FullRect);
            sr.sprite = sprite;
        }

        private void OnDestroy()
        {
            if (grid != null) grid.CellChanged -= OnCellChanged;
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
            if (paletteTexture != null) Destroy(paletteTexture);
            if (material != null) Destroy(material);
        }

        public void RedrawAll()
        {
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                DrawCell(x, y);
            dirtyCells.Clear();
            Array.Clear(cellDirty, 0, cellDirty.Length);
            dirty = true;
        }

        private void OnCellChanged(int x, int y)
        {
            if (detail == 1 || useShader)
            {
                MarkDirty(x, y);
                return;
            }
            // In detail mode a cell's colour bleeds half a cell into its neighbours.
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                MarkDirty(x + dx, y + dy);
        }

        private void MarkDirty(int x, int y)
        {
            if (!grid.InBounds(x, y)) return;
            int i = grid.Index(x, y);
            if (cellDirty[i]) return;
            cellDirty[i] = true;
            dirtyCells.Add(i);
        }

        private void LateUpdate()
        {
            if (dirtyCells.Count > 0)
            {
                foreach (int i in dirtyCells)
                {
                    cellDirty[i] = false;
                    DrawCell(i % grid.Width, i / grid.Width);
                }
                dirtyCells.Clear();
                dirty = true;
            }

            if (!dirty) return;
            texture.Apply(false); // upload to the GPU once per frame, only when something changed
            dirty = false;
        }

        // ------------------------------------------------------------------ drawing

        private void DrawCell(int cx, int cy)
        {
            if (useShader)
            {
                // Owner key (+ trail key) only; the shader does the rest.
                int i = (cy * texWidth + cx) * bytesPerTexel;
                byte key;
                if (grid.IsPlayable(cx, cy)) key = (byte)grid.GetOwner(cx, cy);
                else
                {
                    bool edge = grid.IsPlayable(cx + 1, cy) || grid.IsPlayable(cx - 1, cy) ||
                                grid.IsPlayable(cx, cy + 1) || grid.IsPlayable(cx, cy - 1);
                    key = edge ? EdgeWallKey : OutsideKey;
                }
                keyBytes[i] = key;
                if (bytesPerTexel == 4)
                {
                    keyBytes[i + 1] = drawTrailCells && key < EdgeWallKey ? (byte)grid.GetTrail(cx, cy) : (byte)0;
                    keyBytes[i + 2] = 0;
                    keyBytes[i + 3] = 255;
                }
                return;
            }

            if (detail == 1)
            {
                pixels[grid.Index(cx, cy)] = ColorOf(cx, cy);
                return;
            }

            // Keys of the 3×3 neighbourhood: owner id, 0 = empty floor, -1 = wall / outside.
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                keys[(dy + 1) * 3 + dx + 1] = KeyOf(cx + dx, cy + dy);

            int ownKey = keys[4];
            int trail = drawTrailCells && ownKey >= 0 ? grid.GetTrail(cx, cy) : 0;
            Color32 floor = ((cx / checkerCells + cy / checkerCells) & 1) == 0 ? Palette.EmptyA : Palette.EmptyB;
            Color32 wallColor = ownKey == WallKey ? ColorOf(cx, cy) : Palette.Wall;

            // Fast path: all 9 cells equal → the whole block is one colour.
            bool uniform = true;
            for (int k = 0; k < 9 && uniform; k++) uniform = keys[k] == ownKey;

            int px0 = cx * detail, py0 = cy * detail;
            for (int sy = 0; sy < detail; sy++)
            {
                int row = (py0 + sy) * texWidth + px0;
                int by = subBase[sy] + 1; // row in keys[] of the lower pair
                float ty = subT[sy];
                for (int sx = 0; sx < detail; sx++)
                {
                    int key;
                    if (uniform) key = ownKey;
                    else
                    {
                        int bx = subBase[sx] + 1;
                        float tx = subT[sx];
                        key = Vote(keys[by * 3 + bx], keys[by * 3 + bx + 1], keys[(by + 1) * 3 + bx], keys[(by + 1) * 3 + bx + 1],
                            (1f - tx) * (1f - ty), tx * (1f - ty), (1f - tx) * ty, tx * ty, ownKey);
                    }

                    Color32 c = key == WallKey ? wallColor : key == 0 ? floor : landColors[key];
                    if (trail != 0) c = Palette.Trail(playerColor(trail), c);
                    pixels[row + sx] = c;
                }
            }
        }

        /// <summary>The key with the largest total weight among the 4 nearest cells (ties go to the pixel's own cell).</summary>
        private static int Vote(int k0, int k1, int k2, int k3, float w0, float w1, float w2, float w3, int own)
        {
            float s0 = w0 + (k1 == k0 ? w1 : 0f) + (k2 == k0 ? w2 : 0f) + (k3 == k0 ? w3 : 0f);
            float s1 = w1 + (k0 == k1 ? w0 : 0f) + (k2 == k1 ? w2 : 0f) + (k3 == k1 ? w3 : 0f);
            float s2 = w2 + (k0 == k2 ? w0 : 0f) + (k1 == k2 ? w1 : 0f) + (k3 == k2 ? w3 : 0f);
            float s3 = w3 + (k0 == k3 ? w0 : 0f) + (k1 == k3 ? w1 : 0f) + (k2 == k3 ? w2 : 0f);
            const float bias = 0.0001f;
            if (k0 == own) s0 += bias;
            if (k1 == own) s1 += bias;
            if (k2 == own) s2 += bias;
            if (k3 == own) s3 += bias;

            int best = k0;
            float bestS = s0;
            if (s1 > bestS) { best = k1; bestS = s1; }
            if (s2 > bestS) { best = k2; bestS = s2; }
            if (s3 > bestS) best = k3;
            return best;
        }

        private int KeyOf(int x, int y) => grid.IsPlayable(x, y) ? grid.GetOwner(x, y) : WallKey;

        /// <summary>Colour of one whole cell (used for detail 1 and for wall cells).</summary>
        private Color32 ColorOf(int x, int y)
        {
            if (!grid.IsPlayable(x, y))
            {
                bool edge = grid.IsPlayable(x + 1, y) || grid.IsPlayable(x - 1, y) ||
                            grid.IsPlayable(x, y + 1) || grid.IsPlayable(x, y - 1);
                return edge ? Palette.Wall : Palette.Background;
            }

            int owner = grid.GetOwner(x, y);
            Color32 baseColor = owner != 0
                ? landColors[owner]
                : ((x / checkerCells + y / checkerCells) & 1) == 0 ? Palette.EmptyA : Palette.EmptyB;

            if (!drawTrailCells) return baseColor;
            int trail = grid.GetTrail(x, y);
            return trail != 0 ? Palette.Trail(playerColor(trail), baseColor) : baseColor;
        }
    }
}
