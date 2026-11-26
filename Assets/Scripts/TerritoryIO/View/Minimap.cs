using System.Collections.Generic;
using TerritoryIO.Core;
using TerritoryIO.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace TerritoryIO.View
{
    /// <summary>
    /// Small map of the whole arena in a corner of the HUD.
    ///  - Land and trails: one small texture, redrawn from the grid a few times per second
    ///    (GameSettings > Minimap Updates Per Second). No second camera, so it is cheap on phones.
    ///  - Player dots and the crown: UI images moved every frame, so they glide smoothly.
    /// Nothing on it catches touches, so it never gets in the way of steering.
    /// </summary>
    public class Minimap : MonoBehaviour
    {
        private const float TopMargin = 270f;   // top corners: below the score / leaderboard
        private const float SideMargin = 30f;
        private const float BottomMargin = 30f;
        private const float Border = 6f;        // dark frame around the map

        private GameManager game;
        private RectTransform root;
        private Image frame;
        private RawImage map;
        private RectTransform mapRect;
        private Image crownIcon;
        private Image humanRing;
        private readonly List<Image> dots = new List<Image>();
        private readonly List<Image> carriedCrowns = new List<Image>();

        private Texture2D texture;
        private Color32[] pixels;
        private Color32[] landColor;   // indexed by player id
        private Color32[] trailColor;  // indexed by player id
        private int step;              // grid cells per texel
        private int texW, texH;
        private float nextRedraw;

        private static readonly Color32 Empty = new Color32(34, 42, 70, 255);
        private static readonly Color32 Outside = new Color32(0, 0, 0, 0);

        public void Init(GameManager gameManager, Transform hudCanvas)
        {
            game = gameManager;
            GridMap grid = game.Grid;

            root = (RectTransform)transform;
            root.SetParent(hudCanvas, false);

            frame = gameObject.AddComponent<Image>();
            frame.color = new Color(0.02f, 0.03f, 0.07f, 1f);
            frame.raycastTarget = false;

            var mapGo = new GameObject("Map", typeof(RectTransform), typeof(RawImage));
            mapRect = (RectTransform)mapGo.transform;
            mapRect.SetParent(root, false);
            UiKit.Stretch(mapRect);
            mapRect.offsetMin = new Vector2(Border, Border);
            mapRect.offsetMax = new Vector2(-Border, -Border);
            map = mapGo.GetComponent<RawImage>();
            map.raycastTarget = false;

            // Texture: at most minimapDetail texels on the long side, each texel covers step×step cells.
            int detail = Mathf.Clamp(game.Settings.minimapDetail, 16, 1024);
            step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(grid.Width, grid.Height) / (float)detail));
            texW = Mathf.CeilToInt(grid.Width / (float)step);
            texH = Mathf.CeilToInt(grid.Height / (float)step);
            texture = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "MinimapTexture"
            };
            pixels = new Color32[texW * texH];
            landColor = new Color32[grid.MaxPlayers + 1];
            trailColor = new Color32[grid.MaxPlayers + 1];
            map.texture = texture;

            // Icons, in drawing order (later = on top): bot dots, your ring + dot, carried crowns, crown on the map.
            Player human = game.Human;
            IReadOnlyList<Player> players = game.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == human) humanRing = MakeIcon("YouRing", SpriteFactory.Dot, Color.white);
                dots.Add(MakeIcon("Dot_" + players[i].DisplayName, SpriteFactory.Dot, Color.white));
            }
            if (humanRing == null) humanRing = MakeIcon("YouRing", SpriteFactory.Dot, Color.white);
            if (human != null && human.Id - 1 < dots.Count)
            {
                humanRing.rectTransform.SetAsLastSibling();
                dots[human.Id - 1].rectTransform.SetAsLastSibling(); // your dot above the bots' dots
            }
            for (int i = 0; i < players.Count; i++)
                carriedCrowns.Add(MakeIcon("CarriedCrown", SpriteFactory.Crown, Color.white));
            crownIcon = MakeIcon("Crown", SpriteFactory.Crown, Color.white);
            crownIcon.gameObject.SetActive(false);

            Layout();
            Redraw();
        }

        private Image MakeIcon(string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(mapRect, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero; // positions are from the map's bottom-left corner
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void Update()
        {
            if (game == null || game.Grid == null) return;
            GameSettings s = game.Settings;

            if (map.enabled != s.showMinimap) SetVisible(s.showMinimap);
            if (!s.showMinimap) return;

            Layout();

            if (Time.unscaledTime >= nextRedraw && !game.Paused)
            {
                nextRedraw = Time.unscaledTime + 1f / Mathf.Clamp(s.minimapUpdatesPerSecond, 0.5f, 30f);
                Redraw();
            }

            UpdateIcons();
        }

        private void SetVisible(bool visible)
        {
            // Hide the images (not the GameObject) so this Update keeps running and can switch it back on.
            frame.enabled = visible;
            map.enabled = visible;
            foreach (Transform child in mapRect) child.gameObject.SetActive(visible);
        }

        /// <summary>Size, corner and opacity from GameSettings (all live).</summary>
        private void Layout()
        {
            GameSettings s = game.Settings;
            GridMap grid = game.Grid;

            // Long side = minimapSize, short side follows the arena's shape.
            float size = Mathf.Max(60f, s.minimapSize);
            float aspect = grid.Width / (float)grid.Height;
            Vector2 inner = aspect >= 1f ? new Vector2(size, size / aspect) : new Vector2(size * aspect, size);
            Vector2 outer = inner + Vector2.one * (2f * Border);

            Vector2 anchor;
            Vector2 pos;
            switch (s.minimapCorner)
            {
                case ScreenCorner.TopLeft: anchor = new Vector2(0f, 1f); pos = new Vector2(SideMargin, -TopMargin); break;
                case ScreenCorner.BottomRight: anchor = new Vector2(1f, 0f); pos = new Vector2(-SideMargin, BottomMargin); break;
                case ScreenCorner.BottomLeft: anchor = new Vector2(0f, 0f); pos = new Vector2(SideMargin, BottomMargin); break;
                default: anchor = new Vector2(1f, 1f); pos = new Vector2(-SideMargin, -TopMargin); break;
            }
            root.anchorMin = root.anchorMax = root.pivot = anchor;
            root.anchoredPosition = pos;
            root.sizeDelta = outer;

            float a = Mathf.Clamp01(s.minimapOpacity);
            frame.color = new Color(0.02f, 0.03f, 0.07f, a);
            map.color = new Color(1f, 1f, 1f, a);
        }

        /// <summary>Repaints land and trails into the texture. Cost: one look-up per texel + one per trail cell.</summary>
        private void Redraw()
        {
            GridMap grid = game.Grid;
            IReadOnlyList<Player> players = game.Players;

            for (int id = 1; id < landColor.Length; id++)
            {
                Color32 c = id - 1 < players.Count ? players[id - 1].Color : Palette.ForPlayer(id);
                landColor[id] = Palette.Land(c);
                trailColor[id] = Color32.Lerp(c, new Color32(255, 255, 255, 255), 0.35f);
            }

            // Land: sample the cell in the middle of each texel's block.
            int half = step / 2;
            for (int ty = 0; ty < texH; ty++)
            {
                int cy = Mathf.Min(ty * step + half, grid.Height - 1);
                int row = ty * texW;
                for (int tx = 0; tx < texW; tx++)
                {
                    int cx = Mathf.Min(tx * step + half, grid.Width - 1);
                    Color32 c;
                    if (!grid.IsPlayable(cx, cy)) c = Outside;
                    else
                    {
                        int owner = grid.GetOwner(cx, cy);
                        c = owner > 0 && owner < landColor.Length ? landColor[owner] : Empty;
                    }
                    pixels[row + tx] = c;
                }
            }

            // Trails: paint every trail cell, so thin trails never fall between samples.
            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                if (p == null || !p.Alive || p.Trail.Count == 0) continue;
                Color32 c = p.Id < trailColor.Length ? trailColor[p.Id] : (Color32)Color.white;
                List<Vector2Int> trail = p.Trail;
                for (int k = 0; k < trail.Count; k++)
                {
                    int tx = trail[k].x / step, ty = trail[k].y / step;
                    if (tx < 0 || ty < 0 || tx >= texW || ty >= texH) continue;
                    pixels[ty * texW + tx] = c;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        /// <summary>Player dots, your ring and the crown (every frame, very cheap).</summary>
        private void UpdateIcons()
        {
            GridMap grid = game.Grid;
            IReadOnlyList<Player> players = game.Players;
            Vector2 mapSize = mapRect.rect.size;
            Vector2 scale = new Vector2(mapSize.x / grid.Width, mapSize.y / grid.Height);
            float longSide = Mathf.Max(mapSize.x, mapSize.y);
            float dotSize = Mathf.Max(9f, longSide * 0.045f);
            float youSize = Mathf.Max(13f, longSide * 0.065f);
            float crownSize = Mathf.Max(16f, longSide * 0.09f);

            bool ringShown = false;
            for (int i = 0; i < dots.Count && i < carriedCrowns.Count; i++)
            {
                Image dot = dots[i];
                Image carried = carriedCrowns[i];
                Player p = i < players.Count ? players[i] : null;
                bool show = p != null && p.Alive;
                if (dot.gameObject.activeSelf != show) dot.gameObject.SetActive(show);
                bool hasCrown = show && p.HasCrown;
                if (carried.gameObject.activeSelf != hasCrown) carried.gameObject.SetActive(hasCrown);
                if (!show) continue;

                Vector2 at = Vector2.Scale(p.Position, scale);
                float size = p.IsHuman ? youSize : dotSize;
                Place(dot.rectTransform, at, size);
                dot.color = p.Color;

                if (p.IsHuman)
                {
                    // Pulsing white ring behind your dot.
                    ringShown = true;
                    float pulse = 1.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 6f);
                    Place(humanRing.rectTransform, at, size * pulse);
                }
                if (hasCrown)
                {
                    Place(carried.rectTransform, at + new Vector2(0f, size * 0.9f), crownSize * 0.75f);
                }
            }

            if (humanRing.gameObject.activeSelf != ringShown) humanRing.gameObject.SetActive(ringShown);

            // Crown lying on the map: blinks together with the real one.
            Pickup crown = game.Crown;
            bool crownShown = crown != null && crown.Visible;
            if (crownIcon.gameObject.activeSelf != crownShown) crownIcon.gameObject.SetActive(crownShown);
            if (crownShown)
            {
                float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);
                Place(crownIcon.rectTransform, Vector2.Scale(crown.Position, scale), crownSize * pulse);
            }
        }

        private static void Place(RectTransform rt, Vector2 at, float size)
        {
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(size, size);
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}
