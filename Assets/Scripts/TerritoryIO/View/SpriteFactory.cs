using UnityEngine;

namespace TerritoryIO.View
{
    /// <summary>Generates simple sprites at runtime so the project needs no art assets.</summary>
    public static class SpriteFactory
    {
        private static Sprite square;

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
