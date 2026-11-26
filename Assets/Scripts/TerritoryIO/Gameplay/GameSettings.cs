using UnityEngine;

namespace TerritoryIO.Gameplay
{
    public enum MapShape { Rectangle, Circle, Diamond, Hexagon, Donut, Cross, CustomImage }
    public enum StartShape { Circle, Square, Diamond }
    public enum ControlScheme { FloatingJoystick, PointTowardFinger }
    public enum ExpansionStyle { Random, TowardCenter, AwayFromCenter, TowardPlayer, AwayFromOthers }
    public enum AntiAliasing { Off = 1, X2 = 2, X4 = 4, X8 = 8 }

    /// <summary>
    /// Every tunable value of the game in one asset.
    /// Create it: Project window > right-click > Create > Territory.io > Game Settings,
    /// then drag it into the "Settings" slot of the GameManager.
    /// Values marked (live) can be changed while playing.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Territory.io/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        [Header("Start screen")]
        [Tooltip("Show the start screen (Start, Settings, High Scores, Quit) when you press Play. " +
                 "Untick to jump straight into the game using exactly these Inspector values.")]
        public bool showStartScreen = true;

        [Header("Display / sharpness (applied when a game starts)")]
        [Tooltip("LAND SHARPNESS. Texture pixels per grid cell. 1 = old soft/blurry edges, 4 = crisp smooth " +
                 "edges (recommended), 6-8 = extra crisp on big PC screens (more memory). Needs Smooth Land Edges.")]
        [Range(1, 8)] public int landDetail = 4;
        [Tooltip("Anti-aliasing: smooths the jagged edges of trails and heads. X4 is a good default, Off on weak phones.")]
        public AntiAliasing antiAliasing = AntiAliasing.X4;
        [Tooltip("Render scale (URP). 1 = screen resolution. Above 1 = sharper but heavier (supersampling). " +
                 "Below 1 = faster but blurrier.")]
        [Range(0.5f, 2f)] public float renderScale = 1f;
        [Tooltip("Phones: render at the screen's full native resolution (many phones start at a lower one).")]
        public bool nativeResolutionOnMobile = true;
        [Tooltip("Frames per second target. Phones default to 30 (choppy). 60 = smooth.")]
        [Range(30, 144)] public int targetFrameRate = 60;

        [Header("Arena (applied when you press Play)")]
        [Tooltip("Arena size in world units (1 unit = the size of one block in the old blocky version). " +
                 "Bigger = longer games.")]
        public int arenaWidth = 120;
        public int arenaHeight = 120;
        [Tooltip("SMOOTHNESS. Grid cells per world unit. 1 = old blocky look, 4 = smooth (recommended), " +
                 "6-8 = very smooth but heavier (PC). Grid size = arena size x resolution.")]
        [Range(1, 8)] public int resolution = 4;
        [Tooltip("Shape of the playable area. Everything outside is wall.")]
        public MapShape mapShape = MapShape.Rectangle;
        [Tooltip("Only for CustomImage: white = playable, black/transparent = wall. " +
                 "In the texture's Import Settings tick Read/Write.")]
        public Texture2D customMapImage;
        [Tooltip("Soften land edges (removes the last bit of staircase).")]
        public bool smoothLandEdges = true;

        [Header("Trails")]
        [Tooltip("Draw trails as smooth lines that follow the exact path, instead of grid cells.")]
        public bool smoothTrails = true;
        [Tooltip("Trail line width in units (only for smooth trails).")]
        [Range(0.1f, 2f)] public float trailWidth = 0.6f;

        [Header("Starting land (applied on spawn)")]
        public StartShape startShape = StartShape.Circle;
        [Tooltip("Radius of your starting land, in units.")]
        [Range(2, 10)] public int startSize = 3;

        [Header("Human player")]
        public string humanName = "You";
        [Tooltip("(live) Units per second.")]
        [Range(2f, 25f)] public float humanSpeed = 9f;
        [Tooltip("(live) Degrees per second. Higher = tighter circles. 720+ feels almost instant.")]
        [Range(60f, 1080f)] public float humanTurnRate = 300f;
        [Tooltip("FloatingJoystick: drag from where you touch. PointTowardFinger: head toward your finger/cursor.")]
        public ControlScheme controlScheme = ControlScheme.FloatingJoystick;
        public Color humanColor = new Color(0f, 0.9f, 1f);

        [Header("Player images (applied on spawn)")]
        [Tooltip("Your head image. Leave empty for the default square. Draw it facing RIGHT.")]
        public Sprite humanSprite;
        [Tooltip("Images for bots, used in order. Leave empty for squares.")]
        public Sprite[] botSprites;
        [Tooltip("Colour the image with the player colour. Untick to keep the image's own colours.")]
        public bool tintSprites = false;
        [Tooltip("Head size in units.")]
        [Range(0.5f, 4f)] public float headSize = 1.4f;
        [Tooltip("Rotate the image toward the movement direction.")]
        public bool rotateSprite = true;
        [Tooltip("Extra rotation if your image does not face right: -90 if it faces up, 90 if it faces down, 180 if it faces left.")]
        public float spriteAngleOffset = 0f;

        [Header("Bots")]
        [Range(0, 15)] public int botCount = 9;
        [Tooltip("(live) Units per second.")]
        [Range(2f, 25f)] public float botSpeed = 8f;
        [Tooltip("(live) Degrees per second.")]
        [Range(60f, 1080f)] public float botTurnRate = 240f;
        [Tooltip("How much bots chase and cut other trails. Each bot gets a random value in this range.")]
        [Range(0f, 1f)] public float aggressionMin = 0.2f;
        [Range(0f, 1f)] public float aggressionMax = 0.7f;
        [Tooltip("How far bots venture out of their land (bigger loops, longer trails).")]
        [Range(0f, 1f)] public float greedMin = 0.2f;
        [Range(0f, 1f)] public float greedMax = 0.9f;
        [Tooltip("How close (units) an enemy may get before a bot runs home. Higher = more careful.")]
        [Range(1, 15)] public int caution = 5;
        [Tooltip("Which way bots like to expand.")]
        public ExpansionStyle expansion = ExpansionStyle.Random;
        [Tooltip("Bots go after YOUR trail first and from further away.")]
        public bool botsHuntPlayer = false;
        [Tooltip("Seconds before a dead bot comes back.")]
        public float respawnDelay = 3f;

        [Header("Rules")]
        [Range(5f, 100f)] public float winPercent = 50f;

        [Header("Camera")]
        [Tooltip("(live) Bigger = zoomed out.")]
        [Range(5f, 60f)] public float cameraSize = 16f;

        [Header("Random")]
        [Tooltip("0 = different every game. Any other number = same bots every game.")]
        public int seed = 0;

        /// <summary>Copies the (live) fields, so Inspector changes during Play still apply to the running copy.</summary>
        public void CopyLiveFrom(GameSettings src, float botSpeedFactor)
        {
            humanSpeed = src.humanSpeed;
            humanTurnRate = src.humanTurnRate;
            botSpeed = src.botSpeed * botSpeedFactor;
            botTurnRate = src.botTurnRate;
            cameraSize = src.cameraSize;
            respawnDelay = src.respawnDelay;
        }
    }
}
