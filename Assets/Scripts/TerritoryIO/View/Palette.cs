using UnityEngine;

namespace TerritoryIO.View
{
    /// <summary>Territory.io colour theme: dark ink-blue arena, neon players.</summary>
    public static class Palette
    {
        public static readonly Color32 Background = new Color32(10, 14, 28, 255);
        public static readonly Color32 Wall = new Color32(255, 70, 110, 255);
        public static readonly Color32 EmptyA = new Color32(22, 28, 48, 255);
        public static readonly Color32 EmptyB = new Color32(26, 33, 56, 255);
        public static readonly Color32 HeadOutline = new Color32(8, 10, 20, 255);

        public static readonly Color32[] Players =
        {
            new Color32(0, 229, 255, 255),   // cyan (human by default)
            new Color32(255, 196, 0, 255),   // amber
            new Color32(124, 255, 92, 255),  // lime
            new Color32(255, 92, 205, 255),  // magenta
            new Color32(160, 120, 255, 255), // violet
            new Color32(255, 128, 64, 255),  // orange
            new Color32(64, 160, 255, 255),  // azure
            new Color32(255, 240, 120, 255), // lemon
            new Color32(64, 255, 190, 255),  // mint
            new Color32(255, 96, 96, 255),   // coral
            new Color32(200, 255, 255, 255), // ice
            new Color32(190, 140, 90, 255),  // copper
        };

        public static Color32 ForPlayer(int playerId)
        {
            return Players[(playerId - 1) % Players.Length];
        }

        /// <summary>Land tint: slightly darker than the head colour.</summary>
        public static Color32 Land(Color32 c) => Scale(c, 0.78f);

        /// <summary>Trail: lighter tint blended over what is underneath.</summary>
        public static Color32 Trail(Color32 player, Color32 under)
        {
            Color32 light = Color32.Lerp(player, new Color32(255, 255, 255, 255), 0.35f);
            return Color32.Lerp(under, light, 0.75f);
        }

        private static Color32 Scale(Color32 c, float k)
        {
            return new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), 255);
        }
    }
}
