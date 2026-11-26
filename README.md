# Territory.io — Unity project (v0.4)

Original territory-capture game: leave your zone, draw a trail, close the loop to claim land,
and don't let anyone touch your trail. Single player against bots, PC and Android.

- Design schematic: [DESIGN.md](DESIGN.md) (v0.2, v0.3 and v0.4 changes at the bottom)
- Beginner's guide: [GUIDE.md](GUIDE.md)
- Code: `Assets/Scripts/TerritoryIO/` (20 C# files)
- This folder is the whole Unity project (Unity 6000.0.61f1), kept on GitHub:
  https://github.com/AmitHelfand/territory-io

## Getting updates (Git)

Updates now come through GitHub instead of zip files. Each change arrives as a **pull request**.

1. Open the pull request link on GitHub, read what it changes, and click **Merge pull request** → **Confirm merge**.
2. Save your scene in Unity (Ctrl+S).
3. In GitHub Desktop click **Fetch origin**, then **Pull origin**.
4. Go back to Unity and wait for it to compile. Any extra steps are written in the pull request.

After you change something yourself in Unity (a scene, settings, a new image), save it, then in
GitHub Desktop type a short Summary, click **Commit to main**, then **Push origin**.
GUIDE.md → "Part 4 — Working with Git" has the details.

The update steps below are from the zip days and are kept for history only.

## Update to v0.4 (start screen + sharper picture) — old zip steps

1. Stop Play mode. Extract `TerritoryIO_Scripts.zip` into `Assets` and choose **Replace the files**.
   Five files are new (`MainMenu.cs`, `UiKit.cs`, `DisplaySetup.cs`, `PlayerOptions.cs`, `HighScores.cs`).
2. Wait for Unity to compile. No scene changes are needed: the start screen is built by code.
3. Click your `GameSettings` file. New fields at the top: **Show Start Screen** and the
   **Display / sharpness** group (Land Detail, Anti Aliasing, Render Scale, Native Resolution On Mobile,
   Target Frame Rate). Your old values are kept.
4. Press Play. The start screen appears: START, SETTINGS, HIGH SCORES, QUIT.
5. In game: **II** button, Esc or Android Back = pause. After a round: PLAY AGAIN or MENU.

Where to change the sharpness: GUIDE.md → "Sharpness and resolution: where to change it".

## Update to v0.3 (smooth resolution) — old zip steps

1. Stop Play mode. Extract `TerritoryIO_Scripts.zip` into `Assets` and choose **Replace the files**.
2. Click your `GameSettings` file. New fields: **Resolution** (smoothness, default 4),
   **Smooth Trails**, **Trail Width**, and **Arena Width / Height** (in units, replaces Grid Width / Height).
3. Tick **Smooth Land Edges** (your existing file still has it off).
4. Press Play.

## Update from v0.1 to v0.2 — old zip steps

1. Close Play mode in Unity.
2. Extract `TerritoryIO_Scripts.zip` into your project's `Assets` folder again, and choose
   **Replace the files in the destination**. Two new files are added (`GameSettings.cs`, `MapShapes.cs`).
3. Back in Unity, wait for it to compile. Your `Game` object keeps its `GameManager` component.
4. **Create the settings asset:** in the Project window, right-click inside `Assets` →
   **Create → Territory.io → Game Settings**. A file called `GameSettings` appears.
5. Click the `Game` object, and drag the `GameSettings` file into the **Settings** slot of `GameManager`.
6. Press Play. To change anything, click the `GameSettings` file and edit it in the Inspector.

Without a settings asset the game still runs with default values.

## Where each setting is (click the GameSettings file)

Hover over any field in the Inspector to see a tooltip. Fields marked *live* change while you play,
and are kept after you stop (that's how ScriptableObjects work, so you can tune while playing).

### Player speed
| Field | What it does |
|---|---|
| Human Speed *(live)* | Your speed in cells per second |
| Human Turn Rate *(live)* | How fast you turn, in degrees per second. Higher = tighter circles |
| Bot Speed / Bot Turn Rate *(live)* | Same for bots |

Speeds are in units per second. Tip: speed 9 with turn rate 300 makes a circle about 3.5 units wide.

### Bot behaviour
| Field | What it does |
|---|---|
| Aggression Min / Max | How far bots look for trails to cut (0 = never attack, 1 = attack from ~12 cells). Each bot gets a random value between Min and Max |
| Bots Hunt Player | Bots chase **your** trail first, from twice as far |
| Greed Min / Max | How big a loop bots make, and how long their trail may get before going home |
| Caution | How close an enemy may come before a bot runs home. Higher = safer, slower bots |
| Expansion | Which way bots dig: Random, TowardCenter, AwayFromCenter, TowardPlayer (they grow at you), AwayFromOthers |
| Bot Count | Number of bots (0–15) |
| Respawn Delay | Seconds before a dead bot returns |

Aggression, greed and expansion are applied when a game starts (press Space after game over,
or stop and Play again).

### Player image (skin)
1. Drag a PNG into your Project (for example into `Assets/Art`).
2. Click the PNG → Inspector → **Texture Type: Sprite (2D and UI)** → Apply.
3. Draw the image **facing right**. If it faces up, set **Sprite Angle Offset** to -90.
4. Drag the sprite into **Human Sprite**. For bots, set the size of **Bot Sprites** and drag sprites in.
5. **Tint Sprites** colours the image with the player colour. **Head Size** sets its size in cells.
   **Rotate Sprite** turns it with the movement.
6. **Human Color** is the colour of your land and trail.

### Land shape
| Field | What it does |
|---|---|
| Map Shape | Rectangle, Circle, Diamond, Hexagon, Donut (hole in the middle), Cross, CustomImage |
| Custom Map Image | For CustomImage: a black and white picture, **white = playable**. Click the image → Inspector → tick **Read/Write** → Apply |
| Arena Width / Height | Arena size in units (default 120) |
| **Resolution** | **Smoothness**: cells per unit. 1 = blocky, 4 = smooth (default), 6–8 = very smooth (PC) |
| Start Shape / Start Size | The land you start with: Circle, Square or Diamond, and its radius |
| Smooth Land Edges | Softer land edges |
| Smooth Trails / Trail Width | Trails drawn as smooth lines, and their thickness |

Arena settings apply when you press Play.

### Other
| Field | What it does |
|---|---|
| Win Percent | Share of the map you need to win |
| Camera Size *(live)* | Zoom. Bigger = see more |
| Seed | 0 = different every game; any other number = the same bots every time |

## Controls (free movement)

| Platform | How to steer |
|---|---|
| PC keyboard | WASD or arrows. Hold two keys for diagonals. The head turns smoothly |
| PC mouse | Click and drag in a direction, like a joystick |
| Android | Touch anywhere and drag. Hold a sideways drag to drive a circle |
| Play again | Space, Enter, or tap |

**Control Scheme** in settings: *FloatingJoystick* (default, drag from where you touch) or
*PointTowardFinger* (the head turns toward your finger or cursor while pressed).

## First-time setup (new computer)

1. Install GitHub Desktop and sign in. **File → Clone repository** → `territory-io` → **Clone**.
2. Unity Hub → **Add → Add project from disk** → choose the cloned `territory-io` folder.
   Open it with Unity 6000.0.61f1 (Unity Hub offers to install it if it's missing).
3. Open `Assets/Scenes/SampleScene` and press Play. The scene and settings are already in Git.

## Building for Android
File → Build Settings → Android → Switch Platform → Add Open Scenes → Build.
Player Settings → Resolution and Presentation → Landscape.

## Status
- 2026-11-26: the project moved to Git. All changes now come as pull requests.
- v0.4 (start screen, high scores, pause, sharper land, anti-aliasing) compiles against Unity API
  stubs with both input systems; not yet run in the Unity editor. Send any Console errors.
- Compiled against Unity API stubs with both input systems; the capture algorithm is unit tested.
  v0.3 has not been run in the Unity editor yet. Send any Console errors.
