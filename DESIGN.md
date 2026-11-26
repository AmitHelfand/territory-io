# Territory.io — Design Schematic (v0.1)

An original territory-capture game for Unity. You steer a block around a grid
arena. Leaving your zone draws a trail; returning to your zone closes the loop
and claims everything the loop encloses. If anyone touches your trail while you
are outside, you are cut and lose everything.

> **Assumptions (v0.1)** — change any of these and the design still holds:
> 1. **2D top-down**, orthographic camera, grid-based logic (1 cell = 1 world unit).
> 2. **4-direction grid movement** (turns happen on cell boundaries). Crisp, readable,
>    and easy to make deterministic for multiplayer later.
> 3. **Single player vs bots**, offline. Rules code is plain C# with no rendering, so it can
>    later run on a server.
> 4. **Platform: PC + mobile.** Keyboard (WASD / arrows) and swipe / mouse drag.
> 5. **Walls kill.** Driving into the arena edge is death.
> 6. **Win at 50% of the map** (configurable), like a "round" goal.
> 7. Unity **2022.3 LTS or Unity 6**, built-in or URP renderer; works with the old Input
>    Manager, the new Input System, or both.

---

## 1. Architecture

```
                         ┌──────────────────────────┐
                         │       GameManager         │  only component you add to the scene:
                         │                           │  builds everything, game loop, rules
                         └─┬──────┬──────┬──────┬───┘
         ┌─────────────────┘      │      │      └─────────────────┐
 ┌───────▼───────┐   ┌────────────▼─┐  ┌─▼──────────────┐  ┌───────▼────────┐
 │   GridMap     │   │ CaptureSolver │  │ Player (xN)    │  │ HudController  │
 │ owner[] trail[]│◄──│ flood fill    │  │ cell, dir,     │  │ score, board,  │
 │ area counts   │   └───────────────┘  │ trail list     │  │ game over      │
 └───────┬───────┘                      └──┬──────────┬──┘  └────────────────┘
         │ CellChanged event               │ brain    │ visuals
 ┌───────▼──────────┐            ┌─────────▼──┐  ┌────▼─────────┐
 │ TerritoryRenderer │            │ IPlayerBrain│  │ head sprite  │
 │ Texture2D 1px/cell│            ├────────────┤  └──────────────┘
 └──────────────────┘            │ HumanInput  │
                                 │ BotBrain    │
                                 └────────────┘
        CameraFollow ── follows the human player
```

| Layer | Files | Unity dependency |
|---|---|---|
| **Model** (pure rules data) | `GridMap`, `CaptureSolver` | none (plain C#) |
| **Simulation** | `GameManager`, `Player`, `BotBrain` | MonoBehaviour for the tick only |
| **Input** | `HumanInput`, `InputUtil` | old or new input system |
| **View** | `TerritoryRenderer`, `CameraFollow`, `HudController`, `SpriteFactory`, `Palette` | rendering + uGUI |

Why this split: the model never touches GameObjects, so it is testable in edit-mode
tests and can be moved to a server for multiplayer.

---

## 2. Game loop

```
GameManager.Start
  └─ build grid, renderer, camera, HUD → StartRound()

StartRound
  ├─ clear grid
  ├─ spawn human + N bots on free spots, each with a 7×7 disc of territory
  └─ state = Playing

Every frame (Update)
  for each alive player:
      steps = player.Advance(dt)          // speed × dt, in cells
      repeat steps times: StepPlayer(p)   // discrete rules happen here
  update head visuals (lerp between cells)
  HUD refresh (4× per second)
  if state != Playing and restart pressed → StartRound()

LateUpdate
  TerritoryRenderer uploads dirty pixels once
```

### StepPlayer(p) — the core rule pipeline

```
target = p.Cell + p.Dir
if target outside arena            → Kill(p, "hit the wall")
p.Cell = target
t = grid.Trail[target]
if t == p.Id                       → Kill(p, "crossed own trail")
if t != 0                          → Kill(owner of t, killer = p)   // cut!
head-on: another head q on target  → q is in its own zone, so p dies
                                     (a head outside its zone sits on its own
                                      trail, so it was already cut above)
if grid.Owner[target] == p.Id
     if p.Trail not empty          → Capture(p)
else
     grid.Trail[target] = p.Id; p.Trail.Add(target)
p.Dir = brain.ChooseDirection()    // no 180° reversal
```

---

## 3. Core systems

### 3.1 Grid / territory (`GridMap`)
- Two flat `int[]` arrays of size W×H (default 160×160):
  - `owner[i]` — player id owning the cell (0 = neutral)
  - `trail[i]` — player id whose trail is on the cell (0 = none)
- `area[id]` keeps a running cell count per player, so score is O(1).
- Every write raises `CellChanged(x, y)`; the renderer listens.
- Trail and owner are independent: you can draw a trail over someone else's land.

### 3.2 Player movement (`Player`)
- State: `Cell` (Vector2Int), `Dir`, `progress` (0..1 toward the next cell), `Speed` (cells/s).
- `Advance(dt)` accumulates progress and returns how many cell boundaries were crossed,
  so slow frames never skip rules.
- Visual position = `lerp(Cell, Cell + Dir, progress)`, so motion is smooth while rules stay
  on the grid.
- Direction changes apply only on arrival at a cell. A reverse (180°) is ignored.

### 3.3 Trail
- `Player.Trail` is an ordered `List<Vector2Int>`; the same cells are stamped into `grid.trail`.
- The trail starts at the first cell **outside** your zone.
- On capture or death the trail is wiped from the grid.

### 3.4 Capture fill algorithm (`CaptureSolver`)
When the player re-enters their zone with a trail:

1. Convert every trail cell to owner = p.
2. Take the bounding box of p's land, grown by 1 cell on each side (the ring may be outside
   the arena; it counts as "outside").
3. **Flood fill (BFS) from the ring** through every cell that is *not* owned by p.
   These cells are reachable from outside, so they are not enclosed.
4. Every cell in the box that was **not reached** and is not already p's → becomes p's.
5. Any player whose area drops to 0 is eliminated (credited to p).

Cost: O(box area), only on capture (not per frame). 160×160 worst case = 25.6k cells, well under 1 ms.

```
  before close          flood from ring (.)       result
  . . . . . .           . . . . . .               . . . . . .
  . # # # # .           . # # # # .               . # # # # .
  . # . . T .    →      . # ? ? T .       →       . # # # # .
  . # T T T .           . # T T T .               . # # # # .
  . . . . . .           . . . . . .               . . . . . .
  # = own land, T = trail, ? = not reached → captured
```

### 3.5 Collision / death
| Event | Result |
|---|---|
| Head enters own trail | You die |
| Head enters enemy trail | Enemy dies, you get a kill |
| Head enters arena edge | You die |
| Head enters an enemy head | Enemy outside its zone: enemy is cut. Enemy inside its zone: you die |
| All your land captured | You die |

Death clears your trail and **all your land** returns to neutral. Bots respawn after 3 s;
the human gets the game-over panel.

*Known v0.1 limitation:* two heads swapping cells in the same frame pass through each other.

### 3.6 Bots (`BotBrain`)
A small state machine that runs on every cell arrival:

```
        ┌──────────┐  plan done & inside   ┌────────────┐
        │  Roam     │◄──────────────────────│   Return    │
        │ (in zone) │──── leaves zone ─────►│ Expedition  │
        └──────────┘                        └─────┬──────┘
             ▲                                     │ legs done / danger / trail too long
             │          back in zone               ▼
             └──────────────────────────────── ReturnHome (BFS to nearest own cell)
```
- **Expedition:** a planned rectangle: go out L1 cells, turn, go L2 cells, then return.
  L1/L2 scale with the bot's `greed` (randomised per bot).
- **ReturnHome:** BFS from the head to the nearest own cell, never through own trail,
  never reversing. Takes the first step of the path.
- **Danger:** if an enemy head is within `fear` cells of the bot's head or trail → ReturnHome now.
- **Hunt:** if an enemy trail cell is within 6 BFS steps → go for it.
- **Safety filter:** whatever is chosen, the final direction must not hit a wall or own trail;
  otherwise it tries straight / left / right.

### 3.7 Scoring
- Score = owned cells ÷ total cells, shown as %.
- Kills counted per player.
- Leaderboard: top 5 by area, the human is always shown.
- Win: reach `winPercent` (default 50%).

### 3.8 UI (`HudController`, built in code — no prefabs needed)
- Top-left: your area % and kills.
- Top-right: leaderboard with colour chips.
- Centre: game-over / victory panel with the cause ("Cut by Vortex"), final %, and
  "Press Space or tap to play again".

---

## 4. Visual identity (original)
- Dark ink-blue arena with a subtle two-tone checker, bright "neon" player colours.
- Land is drawn at full colour, trails at a lighter tint over whatever is underneath.
- Heads are squares with a dark outline. Bots have their own name list (Vortex, Pixel,
  Comet, …).

## 5. Data / config (GameManager inspector)
| Field | Default |
|---|---|
| gridWidth / gridHeight | 160 / 160 |
| botCount | 9 |
| startRadius | 3 |
| humanSpeed / botSpeed | 8 / 7 cells/s |
| respawnDelay | 3 s |
| winPercent | 50 |
| cameraSize | 14 |

## 6. Roadmap after v0.1
1. Main menu with name + colour picker, skins.
2. Free-angle movement option (sub-cell trail rasterising).
3. Online multiplayer: run `GridMap` + rules on a server tick (Netcode for GameObjects / Photon Fusion),
   clients send direction only.
4. Power-ups (speed boost, shield), sounds, particles on capture.
5. Edit-mode unit tests for `CaptureSolver` and the collision table.

---

## v0.2 changes (2026-11-23)

**Free movement.** Players no longer move only along X/Y. Each player has a continuous
`Position` and a `Heading` that turns toward the wanted direction at `TurnRate` degrees per second
(`Player.Steer`). Speed ÷ turn rate sets the tightest circle you can drive.
The rules still run on the grid: `GameManager.Move` advances in sub-steps of 0.4 cells, and
every time the head crosses into a new cell, `EnterCell` runs the same pipeline as before
(wall, own trail, enemy trail, head-on, capture). Two extra rules make curves work:
- **Corner fill:** a diagonal cell change also visits the corner cell between, so trails are
  always 4-connected and nobody can slip through a diagonal trail.
- **Neck grace:** the last 4 trail cells behind the head can't kill you, so tight turns don't
  count as crossing your own trail.

**Human input:** keyboard (WASD/arrows, diagonals allowed), floating joystick (touch or mouse drag),
or point-toward-finger. Holding a sideways drag drives a circle.

**Bots** plan circular loops of waypoints (size from *greed*, direction from *ExpansionStyle*),
aim a few cells down BFS paths when returning home or hunting (smooth curves, no stairs),
and look 3 cells ahead to swerve from walls and their own trail.

**GameSettings asset** (`ScriptableObject`) holds every tunable value; GameManager only has a
`Settings` slot. Speeds, turn rates and camera zoom apply live.

**Map shapes:** Rectangle, Circle, Diamond, Hexagon, Donut, Cross, or a custom black/white image.
`GridMap` has a playable mask; wall cells are never claimed and count as "outside" for the
capture fill. Score % is a share of playable cells.

**Skins:** optional sprite for the human and a list for bots, with tint, size, rotation and
angle offset.

---

## v0.3 changes (2026-11-23): smooth resolution

- **Resolution R** (`GameSettings.resolution`, default 4) = grid cells per world unit. The game
  still runs in cell space (1 Unity unit = 1 cell), and every setting given in units (arena size,
  speeds, head size, start size, caution, camera size, trail width) is multiplied by R at start.
  Bot distances (loop radius, max trail, hunt range, look-ahead, path aim) are scaled by R too.
- **Smooth trails:** each player has a `LineRenderer` that follows the real head path; trail
  cells are still stored in the grid for the rules but not painted in the texture.
- **Neck grace** scales with R (`2 + ceil(2.5·R)` cells ≈ 3 units).
- **Head-on** uses a touch radius of 0.5 units instead of "same cell".
- **Performance:** the renderer writes straight into the texture memory (`GetRawTextureData`),
  skips unchanged pixels, and uploads once per frame only when something changed.
  Bots think about twice per unit travelled instead of every cell. Default arena 120×120 units
  at R=4 = 480×480 cells.

---

## v0.4 changes (2026-11-26): start screen and sharper picture

**Start screen** (`View/MainMenu.cs`), built in code in the same scene, shown when
`GameSettings.showStartScreen` is on: START, SETTINGS, HIGH SCORES, QUIT.
- `GameManager` now has a lifecycle: `ShowMenu()` → `StartFromMenu()` → `BuildWorld()`;
  `TearDownWorld()` destroys players, renderer and HUD so the menu can start a fresh game.
- The game runs on a **copy** of the GameSettings asset (`Instantiate`). Start-screen options are
  applied to the copy, so the asset never changes; (live) fields are copied from the asset every frame.
- **Settings** (`Gameplay/PlayerOptions.cs`, PlayerPrefs JSON): name, bots, difficulty
  (Default/Easy/Normal/Hard → aggression, greed, caution, hunt, bot speed factor), map shape,
  graphics preset (Default/Low/Medium/High/Ultra), controls.
- **High scores** (`Gameplay/HighScores.cs`, PlayerPrefs JSON): top 10 by land %, then kills;
  games, wins, win rate, total kills. A round is recorded when the human dies or wins.
- **Pause:** Esc / Android Back / II button → `Time.timeScale = 0`, panel with Resume, Restart, Menu.
  Game over panel has Play again / Menu buttons; Space/Enter still restarts (tap-anywhere restart
  removed so buttons can be pressed).
- `View/UiKit.cs` builds canvases, texts, buttons, panels, input fields, and an EventSystem with the
  right input module for the active input system.

**Sharper picture**
- **Land Detail** (`landDetail`, default 4): the territory texture has D×D pixels per cell. Each pixel
  takes the key (owner / empty / wall) with the biggest bilinear weight among its 4 nearest cell
  centres (ties → its own cell). This is a 0.5-threshold of the bilinear field, i.e. crisp smooth
  contours instead of a bilinear blur. Changed cells mark their 3×3 neighbourhood dirty; dirty blocks
  are redrawn in `LateUpdate`. D is capped so the texture stays ≤ 2048 px (mobile) / 4096 px (PC).
- `View/DisplaySetup.cs`: MSAA (URP asset `msaaSampleCount`, or QualitySettings for built-in),
  URP `renderScale`, native screen resolution on phones, `Application.targetFrameRate`.
  Original URP values are restored when GameManager is destroyed (stop Play).
