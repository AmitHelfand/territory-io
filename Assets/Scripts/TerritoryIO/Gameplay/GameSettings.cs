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
        [Tooltip("SMOOTH LAND SHADER (recommended). Land borders are drawn as smooth curves, anti-aliased " +
                 "to exactly one screen pixel: no visible pixels at any zoom. Cheap, works on phones. " +
                 "Untick only if the land looks pink/missing on some device (then Land Detail is used).")]
        public bool landShader = true;
        [Tooltip("Only used when Land Shader is off or not supported. Texture pixels per grid cell. " +
                 "1 = soft/blurry edges, 4 = crisp, 6-8 = extra crisp on big PC screens (more memory).")]
        [Range(1, 8)] public int landDetail = 6;
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
        [Tooltip("SMOOTHNESS. Grid cells per world unit: how small the steps in a land border are. " +
                 "1 = old blocky look, 4 = OK, 6 = smooth (default), 8 = very smooth (strong PC). " +
                 "Higher = a bit more work for bots and captures. Grid size = arena size x resolution.")]
        [Range(1, 8)] public int resolution = 6;
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

        [Header("Crown power-up")]
        [Tooltip("Crowns appear on the map. Grab one, then every enemy you kill gives you ALL of their land.")]
        public bool enableCrown = true;
        [Tooltip("(live) Seconds between crowns. The first one appears this long after the round starts.")]
        [Range(5f, 120f)] public float crownInterval = 30f;
        [Tooltip("(live) Seconds a crown stays on the map before it disappears.")]
        [Range(3f, 30f)] public float crownLifetime = 10f;
        [Tooltip("(live) The crown blinks during its last this-many seconds.")]
        [Range(0f, 10f)] public float crownBlinkTime = 3f;
        [Tooltip("(live) Seconds of crown mode after you grab it: kill enemies in this time to take their land.")]
        [Range(3f, 30f)] public float crownDuration = 10f;
        [Tooltip("(live) Seconds into the round until crowns ALWAYS appear right next to an enemy's land. " +
                 "Before that the chance grows slowly from 0 (random spots at the start). 0 = always next to an enemy.")]
        [Range(0f, 600f)] public float crownNearEnemyAfter = 120f;
        [Tooltip("How far outside the enemy's border the crown appears, in units.")]
        [Range(0.5f, 10f)] public float crownEnemyDistance = 2f;
        [Tooltip("Max distance (units) from you where a crown may appear, so you can reach it in time. " +
                 "The screen is about 16 units from the middle to the top. 0 = anywhere on the map.")]
        [Range(0f, 100f)] public float crownMaxDistance = 25f;
        [Tooltip("Crown size on the map, in units.")]
        [Range(1f, 6f)] public float crownSize = 2.5f;
        [Tooltip("How close (units) your head must get to grab it.")]
        [Range(0.5f, 4f)] public float crownPickupRadius = 1.5f;
        [Tooltip("Bots can grab crowns too (and steal land with them).")]
        public bool botsCanGrabCrown = false;

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
            crownInterval = src.crownInterval;
            crownLifetime = src.crownLifetime;
            crownBlinkTime = src.crownBlinkTime;
            crownDuration = src.crownDuration;
            crownNearEnemyAfter = src.crownNearEnemyAfter;
        }
    }
}
