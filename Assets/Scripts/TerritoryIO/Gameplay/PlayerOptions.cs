using System;
using UnityEngine;

namespace TerritoryIO.Gameplay
{
    /// <summary>
    /// The choices made in the start screen's Settings panel, saved on the device (PlayerPrefs).
    /// The first time (or after Reset) they are copied from your GameSettings asset.
    /// They override the matching GameSettings fields only when the game is started from the start screen.
    /// </summary>
    [Serializable]
    public class PlayerOptions
    {
        public const string PrefsKey = "TerritoryIO.Options";

        public static readonly string[] DifficultyNames = { "Default", "Easy", "Normal", "Hard" };
        public static readonly string[] GraphicsNames = { "Default", "Low", "Medium", "High", "Ultra" };
        public static readonly string[] ControlNames = { "Joystick", "Point to finger" };

        public string playerName = "You";
        public int botCount = 9;
        /// <summary>Index into DifficultyNames. 0 = use the GameSettings values.</summary>
        public int difficulty;
        /// <summary>Index into GraphicsNames. 0 = use the GameSettings values.</summary>
        public int graphics;
        public int controls;
        public int mapShape;

        public static PlayerOptions Load(GameSettings defaults)
        {
            string json = PlayerPrefs.GetString(PrefsKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var o = JsonUtility.FromJson<PlayerOptions>(json);
                    if (o != null) return o.Clamped();
                }
                catch (Exception) { /* corrupt prefs: fall back to defaults */ }
            }
            return FromSettings(defaults);
        }

        public static PlayerOptions FromSettings(GameSettings s)
        {
            return new PlayerOptions
            {
                playerName = s.humanName,
                botCount = s.botCount,
                difficulty = 0,
                graphics = 0,
                controls = (int)s.controlScheme,
                mapShape = (int)s.mapShape
            }.Clamped();
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(Clamped()));
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
        }

        public PlayerOptions Clamped()
        {
            if (string.IsNullOrWhiteSpace(playerName)) playerName = "You";
            if (playerName.Length > 16) playerName = playerName.Substring(0, 16);
            botCount = Mathf.Clamp(botCount, 0, 15);
            difficulty = Mathf.Clamp(difficulty, 0, DifficultyNames.Length - 1);
            graphics = Mathf.Clamp(graphics, 0, GraphicsNames.Length - 1);
            controls = Mathf.Clamp(controls, 0, ControlNames.Length - 1);
            mapShape = Mathf.Clamp(mapShape, 0, Enum.GetValues(typeof(MapShape)).Length - 1);
            return this;
        }

        /// <summary>
        /// Writes these options into the running copy of the settings.
        /// Returns the bot speed factor of the difficulty (applied every frame to the live bot speed).
        /// </summary>
        public float ApplyTo(GameSettings run)
        {
            Clamped();
            run.humanName = playerName;
            run.botCount = botCount;
            run.controlScheme = (ControlScheme)controls;
            var shape = (MapShape)mapShape;
            run.mapShape = shape == MapShape.CustomImage && run.customMapImage == null ? MapShape.Rectangle : shape;

            // Graphics presets: resolution (grid), land detail (pixels per cell), anti-aliasing, render scale.
            switch (graphics)
            {
                // With the land shader, land detail only matters on devices that can't run it.
                case 1: SetGraphics(run, 4, 3, AntiAliasing.Off, 1f); break;
                case 2: SetGraphics(run, 5, 4, AntiAliasing.X2, 1f); break;
                case 3: SetGraphics(run, 6, 6, AntiAliasing.X4, 1f); break;
                case 4: SetGraphics(run, 8, 8, AntiAliasing.X8, 1.25f); break;
            }

            switch (difficulty)
            {
                case 1: // Easy: calm, slow bots
                    SetBots(run, 0f, 0.15f, 0.1f, 0.5f, 8, false);
                    return 0.85f;
                case 2: // Normal
                    SetBots(run, 0.2f, 0.6f, 0.2f, 0.8f, 5, false);
                    return 1f;
                case 3: // Hard: bots hunt you and are a bit faster
                    SetBots(run, 0.6f, 1f, 0.4f, 1f, 3, true);
                    return 1.1f;
                default:
                    return 1f;
            }
        }

        private static void SetGraphics(GameSettings s, int resolution, int detail, AntiAliasing aa, float scale)
        {
            s.resolution = resolution;
            s.landDetail = detail;
            s.smoothLandEdges = true;
            s.antiAliasing = aa;
            s.renderScale = scale;
        }

        private static void SetBots(GameSettings s, float aggrMin, float aggrMax, float greedMin, float greedMax,
            int caution, bool hunt)
        {
            s.aggressionMin = aggrMin;
            s.aggressionMax = aggrMax;
            s.greedMin = greedMin;
            s.greedMax = greedMax;
            s.caution = caution;
            s.botsHuntPlayer = hunt;
        }
    }
}
