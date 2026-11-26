# Territory.io — Beginner's Guide

This guide explains how the project works and how to change it. It has four parts:

1. **How the game is built** — what happens when you press Play, and what each file does.
2. **Changing parameters in the Inspector** — no code, the easy way.
3. **Changing things in code** — for values that are not in the Inspector, with examples.
4. **Working with Git** — how updates arrive, and how to save your own changes to GitHub.

---

## Part 1 — How the game is built

### Unity words you need

| Word | Meaning |
|---|---|
| **Scene** | A level. Your scene has a `Game` object and a `Main Camera`. The start screen is built by code in the same scene, so you don't need a second scene. |
| **GameObject** | Any "thing" in the scene (the camera, a player, the HUD). |
| **Component** | A script or feature attached to a GameObject. `GameManager` is a component on `Game`. |
| **Inspector** | The panel on the right that shows the fields of the selected object or file. |
| **ScriptableObject** | A data file in your Project that holds settings. `GameSettings` is one. |
| **Play mode** | When you press Play. Objects made by code appear in the Hierarchy while it runs. |

### What happens when you press Play

Your scene only contains one script: **GameManager**. It builds everything else from code:

```
Press Play
   │
   ▼
GameManager.Start()
   ├─ Show Start Screen ticked? → MainMenu (START / SETTINGS / HIGH SCORES / QUIT)
   │        START pressed → BuildWorld() with your Settings-menu choices
   └─ not ticked            → BuildWorld() with exactly the Inspector values

BuildWorld()
   ├─ copies your GameSettings file (the game never changes your file)
   ├─ DisplaySetup → anti-aliasing, render scale, phone resolution, 60 fps
   ├─ MapShapes   → decides which cells are playable (circle, donut…)
   ├─ GridMap     → creates the arena: a big table of cells
   ├─ Camera      → sets up the Main Camera and adds CameraFollow
   ├─ Players     → creates you (HumanInput) and the bots (BotBrain)
   ├─ TerritoryRenderer → creates the picture of the arena
   ├─ HudController     → creates the score, leaderboard and Game Over text
   └─ StartRound() → gives everyone starting land and starts the game

End of a round (you die or win) → saved in HIGH SCORES → PLAY AGAIN or MENU
Esc / Android Back / the II button → pause (RESUME, RESTART, MENU)

Every frame (GameManager.Update)
   for each player:
      1. ask its "brain" which way it wants to go   (HumanInput or BotBrain)
      2. turn toward that direction                 (Player.Steer)
      3. move forward                               (GameManager.Move)
      4. every time the head enters a new cell → apply the rules (GameManager.EnterCell):
            wall? → die
            my own trail? → die
            someone else's trail? → they die
            back on my land with a trail? → capture! (CaptureSolver)
            outside my land? → leave a trail
```

While the game is running, look at the **Hierarchy**: you will see `Territory`, `HUD`,
`Player_Human`, `Player_Bot_2`… These were all created by GameManager.

### How the arena works (the "grid")

The arena is a grid of cells, like graph paper. Its size is set in **world units**
(default 120 × 120), and **Resolution** says how many cells fit in one unit (default 4).
So the default grid is 480 × 480 cells. More cells = smoother curves and edges.
Each cell remembers two numbers:

- **owner** — which player's land it is (0 = nobody)
- **trail** — which player's trail is on it (0 = none)

Players move smoothly, but the rules only look at which cell the head is in.
The picture you see is one image, coloured by the owner. With **Land Detail** 4 (default) every cell
is drawn as 4 × 4 pixels, and each pixel takes the colour of the nearest cells, so land edges
come out as sharp, smooth curves instead of a blur. (Land Detail 1 = the old 1 pixel per cell.)
Trails are drawn on top as smooth lines (one `LineRenderer` per player) that follow the exact path.

Inside the code everything is measured in cells, so settings given in units (speed, head size,
start size, camera size…) are multiplied by Resolution when the game starts.

### What each file does

```
Assets/Scripts/TerritoryIO/
│
├─ Gameplay/              ← the game itself
│   ├─ GameManager.cs     The boss. Builds everything, runs the game loop, has all the rules.
│   ├─ GameSettings.cs    The list of settings you see in the GameSettings file.
│   ├─ Player.cs          One player's head: position, direction, speed, trail, image.
│   ├─ HumanInput.cs      Your controls: keyboard, mouse, touch → a direction.
│   ├─ BotBrain.cs        The AI: decides where each bot goes.
│   ├─ IPlayerBrain.cs    A small "contract" both HumanInput and BotBrain follow.
│   ├─ MapShapes.cs       Builds the map shape (circle, donut, your own image…).
│   ├─ PlayerOptions.cs   The start screen's Settings (name, bots, difficulty, map, graphics, controls).
│   └─ HighScores.cs      Top 10 rounds + games, wins, win rate. Saved on the device.
│
├─ Core/                  ← pure logic, no graphics
│   ├─ GridMap.cs         The arena table (owner + trail per cell) and land counting.
│   └─ CaptureSolver.cs   The "fill" that claims everything inside your loop.
│
├─ View/                  ← what you see
│   ├─ TerritoryRenderer.cs  Draws the arena image from the grid (sharp edges with Land Detail).
│   ├─ HudController.cs      Score, leaderboard, pause button/panel, Game Over / Victory panel.
│   ├─ MainMenu.cs           The start screen and its Settings and High Scores pages.
│   ├─ UiKit.cs              Helpers that build buttons, texts and panels in code.
│   ├─ DisplaySetup.cs       Anti-aliasing, render scale, phone resolution, frame rate.
│   ├─ CameraFollow.cs       Makes the camera follow you smoothly.
│   ├─ Palette.cs            All colours (background, walls, bot colours).
│   └─ SpriteFactory.cs      Makes the default square image in code.
│
└─ Controls/
    └─ InputUtil.cs       Reads keys / mouse / touch for both of Unity's input systems.
```

### How the files talk to each other

```
           GameSettings (your file)
                  │ read by
                  ▼
 HumanInput ─┐  GameManager ──► GridMap ──(cell changed)──► TerritoryRenderer
 BotBrain  ──┴─►   │   │            ▲
  "which way?"     │   └─ capture ──┘ CaptureSolver
                   │
                   ├──► Player (x10)  ──► head image
                   ├──► HudController (reads scores from GridMap)
                   └──► CameraFollow  (follows your Player)
```

- **GameManager** asks each player's brain which way to go, moves the player, and changes
  cells in **GridMap**.
- **GridMap** announces "cell changed", and **TerritoryRenderer** repaints that pixel.
- **HudController** reads the land counts from GridMap 4 times a second to show percentages.

### How capturing works (CaptureSolver)

When you come back to your land with a trail:
1. Your trail cells become your land.
2. The solver "pours water" from outside your land. Water spreads to every cell it can reach
   without crossing your land.
3. Every cell the water **could not reach** is enclosed by you, so it becomes yours.

### The start screen

```
           TERRITORY.IO
   [ START ]          → starts a game with your Settings choices
   [ SETTINGS ]       → Name, Bots, Difficulty, Map, Graphics, Controls (+ RESET, BACK)
   [ HIGH SCORES ]    → top 10 rounds, games, wins, win rate (+ CLEAR, BACK)
   [ QUIT ]           → closes the game (in the Editor it stops Play mode)
```

- Mouse, touch, or keyboard (arrows + Enter) all work.
- Settings and high scores are saved on the device (Unity's PlayerPrefs), so they stay after you quit.
- **Default** in Difficulty or Graphics = use the values from your GameSettings file.
  Easy / Normal / Hard and Low / Medium / High / Ultra override them.
- Name, Bots, Map and Controls start from your GameSettings file. **RESET** copies them again.
- To skip the start screen while you test, untick **Show Start Screen** in GameSettings.
  Then the game uses exactly your Inspector values (Settings-menu choices are ignored).

---

## Part 2 — Changing parameters in the Inspector (no code)

### Open the settings
1. In the **Project** window, click the `GameSettings` file.
2. The **Inspector** shows all the fields, grouped under headings.
3. Hover over a field name to read its tooltip.

If you don't have the file yet: right-click in Assets → **Create → Territory.io → Game Settings**,
then click `Game` and drag the file into the **Settings** slot of GameManager.

### When do changes take effect?

| Kind of field | When it applies |
|---|---|
| Speeds, turn rates, camera size | **Immediately**, even while playing |
| Bots, images, start shape | Next round (press Space after Game Over) or next Play |
| Arena size, map shape | Next time you press Play |

Important: changes to the **GameSettings file stay after you stop Play**. (Changes to normal
components during Play mode are lost when you stop, but this is a file, so it is kept.)

### Sharpness and resolution: where to change it

"Resolution" is really 5 different things. Here is each one, from most to least visible.
In the GameSettings file:

| Field (GameSettings) | What it does | Try |
|---|---|---|
| **Land Detail** | Pixels per cell for the land picture. Biggest effect on blurry edges | 4 (default), 6–8 on PC |
| **Anti Aliasing** | Smooths jagged edges of trails and heads | X4 (default), X8 on PC, Off on old phones |
| **Render Scale** | Renders the whole screen bigger, then shrinks it (supersampling) | 1 (default), 1.5 on PC = very sharp |
| **Native Resolution On Mobile** | Phones often render below the screen's real resolution. This forces full resolution | On |
| **Target Frame Rate** | Phones default to 30 fps, which looks choppy | 60 |
| **Resolution** | Grid cells per unit: how fine the *gameplay* grid is (smaller "steps" in land) | 4 (default), 6 on PC |
| Smooth Land Edges | Must be ticked for Land Detail to work. Untick = blocky pixel look | On |
| Smooth Trails / Trail Width | Trails as smooth lines, and their thickness | On / 0.6 |

The in-game **Settings → Graphics** presets set these for you:

| Preset | Resolution | Land Detail | Anti Aliasing | Render Scale |
|---|---|---|---|---|
| Low | 3 | 2 | Off | 1 |
| Medium | 4 | 3 | X2 | 1 |
| High | 4 | 4 | X4 | 1 |
| Ultra | 6 | 4 | X8 | 1.5 |

Change the presets in code: `Gameplay/PlayerOptions.cs`, search for `SetGraphics(run,`.
The numbers are `(resolution, landDetail, antiAliasing, renderScale)`.

Costs: Resolution multiplies the number of cells by Resolution², and Land Detail multiplies the
picture's memory by Land Detail². The game automatically lowers Land Detail if the picture would
be bigger than 2048 pixels on phones or 4096 on PC (code: `View/TerritoryRenderer.cs`, search `maxTex`).
On phones keep Resolution 4 and Land Detail 4 or less, with the arena at about 120 × 120.

**Unity settings outside the code** (only if you still want more):
1. **Game view** (Editor only): at the top of the Game tab, set the size to *Full HD (1920×1080)*
   and the **Scale** slider to 1x. A "Low Resolution Aspect Ratios" tick or a scale above 1x makes
   the Editor look blurry even when the build is sharp.
2. **URP asset** (Project window, search `t:UniversalRenderPipelineAsset`): *Anti Aliasing (MSAA)*
   and *Render Scale* live here. The game sets them while it runs and puts your values back when you stop.
3. **Player Settings → Android → Resolution and Presentation → Resolution Scaling Mode**:
   set to *Disabled* (or tick Native Resolution On Mobile, which does it from code).
4. **Your own images** (head sprites): click the PNG → Inspector → *Filter Mode: Bilinear*,
   *Compression: None* (or High Quality), *Max Size* at least the image size → Apply.
   Draw them at least 256 × 256 pixels.

### Recipes

**Faster, twitchier game**
- Human Speed 12, Human Turn Rate 500, Bot Speed 11, Camera Size 20.

**Very aggressive bots that hunt you**
- Aggression Min 0.7, Aggression Max 1, Bots Hunt Player ✔, Caution 2, Expansion = TowardPlayer.

**Calm bots that grow peacefully**
- Aggression Min 0, Aggression Max 0.1, Caution 10, Expansion = AwayFromOthers.

**Small round arena, big start**
- Map Shape = Circle, Arena Width 80, Arena Height 80, Start Shape = Circle, Start Size 6.

**Your own map from a picture**
1. Draw a black and white PNG (white = playable). 200 × 200 pixels is a good size.
2. Drag it into Assets, click it, tick **Read/Write** in the Inspector, click **Apply**.
3. In GameSettings: Map Shape = CustomImage, drag the PNG into Custom Map Image.

**Your own player image**
1. Drag a PNG into Assets. Click it → Texture Type = **Sprite (2D and UI)** → Apply.
2. Drag it into **Human Sprite**. Draw the image facing **right**, or use Sprite Angle Offset.

**Save several setups**
Create more settings files (e.g. `Settings_Easy`, `Settings_Hard`) and drag the one you want
into GameManager's Settings slot.

---

## Part 3 — Changing things in code

Open a script by double-clicking it in the Project window (it opens in Visual Studio or Rider).
After you save, go back to Unity and wait for it to compile. If something is red in the
**Console** window, there is a typo; press Ctrl+Z in the editor and save again.

Tip: use **Ctrl+F** in the code editor to find the line shown in each example.

### Example 0 — Change the start screen
File: `View/MainMenu.cs`.
- Title text: search for `TERRITORY<color`.
- Buttons: search for `UiKit.Button(t, "START"`. The second value is the position (x, y) on a
  1920 × 1080 screen, the third is the size, and the last number is the font size.
- Difficulty presets: `Gameplay/PlayerOptions.cs`, search for `case 1: // Easy`.
  `SetBots(run, aggressionMin, aggressionMax, greedMin, greedMax, caution, huntPlayer)`, and the
  number after `return` is the bot speed factor (0.85 = 15% slower).
- How many high scores are kept: `Gameplay/HighScores.cs`, `MaxEntries = 10`.

### Example 1 — Rename the bots
File: `Gameplay/GameManager.cs`, search for `BotNames`:
```csharp
private static readonly string[] BotNames =
{
    "Vortex", "Pixel", "Comet", ...
};
```
Change the names in quotes. Keep the commas and quotes.

### Example 2 — Change the colours
File: `View/Palette.cs`. Colours are `new Color32(red, green, blue, 255)`, each 0–255.
```csharp
public static readonly Color32 Wall = new Color32(255, 70, 110, 255);   // wall (pink-red)
public static readonly Color32 EmptyA = new Color32(22, 28, 48, 255);   // empty floor, square 1
public static readonly Color32 EmptyB = new Color32(26, 33, 56, 255);   // empty floor, square 2
public static readonly Color32[] Players = { ... }                      // bot colours, in order
```
Example: a white wall → `new Color32(255, 255, 255, 255)`.
Your own colour is **Human Color** in GameSettings.

### Example 3 — Bigger or smaller floor squares
File: `Gameplay/GameManager.cs`, search for `4 * Res`:
```csharp
settings.mapShape == MapShape.Rectangle, !settings.smoothTrails, 4 * Res);
```
`4 * Res` means floor squares of 4 units. Use `8 * Res` for bigger squares, `2 * Res` for smaller.

### Example 4 — How long bot trails can get
File: `Gameplay/BotBrain.cs`, search for `maxTrail`:
```csharp
int maxTrail = Mathf.RoundToInt(Mathf.Lerp(12f, 70f, Greed) * res);
```
A bot with greed 0 goes home after a trail of 12 units, greed 1 after 70. Change 12 and 70.
(`res` is the Resolution, which converts units to cells. Leave it in.)

### Example 5 — Size of the bots' loops
Same file, search for `radius`:
```csharp
float radius = Mathf.Lerp(2.5f, 14f, Greed) * Rand(0.7f, 1.2f) * res;
float sweep = Rand(250f, 330f);
```
- `2.5f` / `14f` = smallest / largest circle radius in units.
- `sweep` = how much of a full circle (360) they drive before heading home.

### Example 6 — How far bots "see" your trail
Same file, search for `int range`:
```csharp
int range = (2 + Mathf.RoundToInt(Aggression * 10f)) * res;
```
With aggression 1 they see 12 units. Change `10f` to `20f` to double it.

### Example 7 — Text size on screen
File: `View/HudController.cs`. The number after the name is the font size:
```csharp
scoreText = MakeText(transform, "Score", 40, ...);        // your score
boardText = MakeText(transform, "Leaderboard", 30, ...);  // leaderboard
```

### Example 8 — Forgiveness for tight turns
File: `Gameplay/GameManager.cs`, search for `NeckCells =`:
```csharp
NeckCells = 2 + Mathf.CeilToInt(2.5f * Res);
```
The last few trail cells behind your head (about 3 units) can't kill you, so tight turns are safe.
Raise it if very tight circles kill you; lower it for stricter rules.

### Example 9 — Add a new setting to the Inspector
Let's add "bots respawn with bigger land":
1. In `Gameplay/GameSettings.cs`, under the `[Header("Bots")]` fields, add:
   ```csharp
   [Range(2, 10)] public int botStartSize = 3;
   ```
2. In `Gameplay/GameManager.cs`, inside `SpawnPlayer`, change
   ```csharp
   int r = settings.startSize * Res;
   ```
   to
   ```csharp
   int r = (p.IsHuman ? settings.startSize : settings.botStartSize) * Res;
   ```
3. Save, go back to Unity. The new field appears in GameSettings.

This is the general pattern: **add a field in GameSettings.cs, then read it with
`settings.yourField` in the code that needs it.**

---

## Part 4 — Working with Git (GitHub Desktop)

The whole Unity project lives on GitHub: https://github.com/AmitHelfand/territory-io.
The folder on your PC (`Documents\GitHub\territory-io`) is a copy of it. GitHub Desktop keeps the two in sync.

### Git words you need
| Word | Meaning |
|---|---|
| **Repository (repo)** | The project folder that Git keeps track of |
| **Commit** | A saved snapshot of your changes, with a short description |
| **Push** | Send your commits from your PC up to GitHub |
| **Pull** | Bring new commits from GitHub down to your PC |
| **Branch** | A separate line of work. `main` is the real game; Claude works on a side branch |
| **Pull request (PR)** | A proposal to bring a branch's changes into `main`. You read it and click Merge |

### Getting a new version from Claude
1. Claude sends you a pull request link. Open it. The **Files changed** tab shows every line that changes
   (green = added, red = removed).
2. Click **Merge pull request** → **Confirm merge**. Now `main` on GitHub has the change.
3. In Unity, save your scene (Ctrl+S).
4. In GitHub Desktop: **Fetch origin**, then **Pull origin**.
5. Back in Unity, wait for it to compile, then press Play.

If you don't like the change, don't merge it. Just tell Claude what to change, and the same pull request is updated.

### Saving your own changes
Whenever you change something in Unity (move an object in the scene, edit GameSettings, add an image):
1. Save in Unity (Ctrl+S).
2. GitHub Desktop → **Changes** tab lists the changed files.
3. Type a short **Summary** (for example "Faster bots"), click **Commit to main**, then **Push origin**.

Do this before you merge a pull request, so your work and Claude's don't collide.

### Which files go into Git
- Yes: `Assets` (including every `.meta` file), `Packages`, `ProjectSettings`.
- No: `Library`, `Temp`, `Logs`, `UserSettings`, `Build`. Unity rebuilds these, and `.gitignore` keeps them out.
- `.meta` files are Unity's ID cards for each asset. Always commit them together with their file,
  or the scene loses its links to scripts and images.

### Undoing things
- Undo a change you haven't committed yet: GitHub Desktop → **Changes** → right-click the file → **Discard changes**.
- Undo a commit: **History** tab → right-click the commit → **Revert changes in commit**, then **Push origin**.
- Older versions are never lost. The **History** tab shows every commit.

---

## If something goes wrong

| Problem | Fix |
|---|---|
| Red errors in the Console after editing code | Undo your last change (Ctrl+Z), save, check for a missing `;`, `}` or `"` |
| "no GameSettings assigned" message | Drag the GameSettings file into GameManager's Settings slot |
| Custom map is a rectangle | The image needs **Read/Write** ticked in its Import Settings |
| My image is huge / tiny | Change **Head Size** in GameSettings |
| Game is slow on the phone | Lower **Resolution** to 3, or make the arena smaller |
| Land or trails look blocky | Raise **Land Detail** and **Resolution**, tick **Smooth Land Edges** and **Smooth Trails** |
| Everything looks blurry in the Editor | Game tab: set **Scale** to 1x and the size to Full HD. See "Sharpness and resolution" |
| Blurry or low resolution on the phone | Tick **Native Resolution On Mobile**, raise **Render Scale** to 1.25 |
| No start screen | Tick **Show Start Screen** in GameSettings |
| Buttons don't react to clicks | There must be only one **EventSystem** in the Hierarchy. Delete any extra one you added by hand |
| Want to erase the high scores | HIGH SCORES → CLEAR (tap twice) |
| My image faces the wrong way | Change **Sprite Angle Offset** (-90, 90 or 180) |
| Settings changed by themselves | You edited them during Play mode. Settings files keep Play-mode changes |
| GitHub Desktop says "newer commits on remote" when pushing | Click **Pull origin** first, then **Push origin** |
| GitHub Desktop shows a "conflict" | Don't guess. Click **Abort merge** if it's offered, and send Claude a screenshot |
| Script is missing on an object after pulling | A `.meta` file was not committed. Tell Claude which object shows "Missing script" |
