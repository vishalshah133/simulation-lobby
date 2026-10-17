# terr-002 — Country Wars (Multiply or Release)

**Format:** `terr` · **Length:** 10-15 min run (forced decision at 20) · **Aspect:** 16:9 · **Status:** `building`
**Brief:** `KnowledgeBase/formats/territory-multiply-release.md` · **Base video:** [terr-001](terr-001-four-color-territory.md)
(rules, pacing v8, board, drum, camera and every contract not listed here are unchanged: read it for those)

## Premise

India, Australia, West Indies and South Africa each get a cannon and a Plinko board. ×2 doubles your ammo, R fires
it all, the drum can blow up a rival's cannon or make it rain. **Which country takes the board?**

Why this video: it replicates v15 (the only long upload, 819 views at day 2.3) with enough changes that it isn't a
re-upload. n=1 can't tell "long form works" from "`terr` got lucky" (`channel-strategy.md` → *10-15 read*). The
country skin adds a reason to pick a side in the first second (patriotism beats picking a colour). The cricket
nations are a deliberate audience bet, and the cost is mostly reuse.

## Storyboard

| Beat | On screen | Caption / counter |
|---|---|---|
| Hook 0:00 | Whole table, four small flags waving on the cannons, each board painted with its country name and flag. Drum idle. Countdown | **WHICH COUNTRY TAKES THE BOARD?** · rules subline · "Pick your country before it starts." · `STARTS IN 5` |
| Act 1 0:05-3:00 | Boards drop, ×2 chips punch, first releases, tiles flip from the four corners | "INDIA RELEASES 64!" · share bar `IND 26% · AUS 25% ...` |
| First SPIN | Drum spins in the team's colour, header "AUSTRALIA is Spinning" | result label beside the drum |
| **BOOM beat** | The leader's cannon blows up: fireball, debris, camera push and shake. The barrel droops and goes black, its flag falls, smoke rises, a ring timer drains around the base with a big 10…1 above it. Then it pops back up with sparks | "INDIA BLOWS UP AUSTRALIA'S CANNON!" … "AUSTRALIA'S CANNON IS BACK!" |
| **RAIN beat** | A storm cloud tinted in the team's colour over the grid, and drops streak down onto enemy land. Each splash paints a small blob, and **never lands near a cannon** | "SOUTH AFRICA MAKES IT RAIN!" |
| 6:00+ | Escalation | "SPEED UP!" |
| Midpoint twist | First cannon tile captured: the cannon vanishes, the flag at half-mast, the share bar shows OUT | "WEST INDIES IS OUT" |
| Finale | Two countries left, the share bar swinging | releases and BOOMs land harder |
| Payoff | Winner: camera on its cannon, its flag waving | "**INDIA WINS!**" |

Cold-viewer check (sound off):
- [x] The question names things visible from frame one: four country names painted on the boards, flags, share-bar labels
- [x] Every cause happens on screen: the drum lands on BOOM/RAIN, then the explosion/cloud plays at the spot it affects
- [x] One question all video (which country), and every beat moves the share bar
- [x] The only number per side is its share (bank numbers belong to the boards, as in terr-001)
- [ ] Not a curiosity format, so no *why* line. The winner caption is the close

## What changes from terr-001

| # | Change | Why |
|---|---|---|
| 1 | Teams: **SOUTH AFRICA** green TL · **AUSTRALIA** gold TR · **INDIA** blue BL (front-left, nearest camera) · **WEST INDIES** red BR | Patriotism: someone to root for in a second |
| 2 | Spin face **+ LINE → RAIN** | User idea: a rain of colour instead of a line |
| 3 | Spin face **SELF DESTRUCT → BOOM (cannon strike)**: the cannon is down for 10 s | User idea. A new kind of beat: a team goes silent and the board reacts |
| 4 | Theme: "night stadium". Navy **hex-slab floor**, navy table, violet ×2 chips and drum, gold accents, a rim light, ambient dust | New look, same readability. Background pattern changed |
| 5 | Identity: country name and flag decal on each board, a small waving flag on each cannon, `IND 34%` share-bar labels | The question names countries, so countries must be on screen |
| 6 | HUD text: `WHICH COUNTRY TAKES THE BOARD?`, "Pick your country before it starts." | |

Everything else (grid 36×48, boards, slots `SPIN R ×2 R ×2`, lane gate, drops, escalation, bomb, boosts, camera
pose) is terr-001 v8 as published.

### BOOM (cannon strike), rules

- **Target:** `config.cannonStrikeTarget`, default **Leader**: the live enemy with the most tiles (ties go to the lower
  index). It's a built-in comeback: the drum punishes whoever is winning. `Self` (a penalty face) and `RandomEnemy`
  (spin stream) are available as tunables. *This choice was mine, not the user's; flip it in the config if BOOM was
  meant to hurt the spinner.*
- **While down** (`cannonDownSeconds` 10): no shots, aim frozen. Queued ammo and pending bombs **wait** and fire after
  the repair (a comeback burst). The board keeps dropping, so a team that is firing stays locked, as in terr-001.
  Live shots keep flying. The base can still be captured, so a downed team can be eliminated.
- A strike on a cannon that is already down restarts the full timer (no stacking). Elimination clears the timer and
  raises no Repaired event. If there's no valid target, the face fizzles (event with target = -1 is NOT raised;
  `SpinResolved` alone).
- Events: `CannonDestroyed(team = target, other = spinner, after = down ticks)`, `CannonRepaired(team)`.

### RAIN, rules

- On resolve, schedule `rainDrops` (36) drops, sampled **without replacement** (partial Fisher-Yates on the spin
  stream) from cells the team does not own **and** that are more than `rainCannonClearance` (3, Chebyshev) from **any**
  base cell. Fewer candidates means fewer drops, and zero means it fizzles (`RainStarted.slot = 0`).
- Drop *i* lands at `resolveTick + leadTicks + floor(i × rainTicks / max(1, n − 1))`
  (`rainLeadSeconds` 0.8, `rainSeconds` 4). Each drop is appended to `RainDrops` (append-only, with its land tick).
- Landing: `GridPainter.PaintDisc(centre, rainSplashRadius 1, team)` with a protect mask = the terr-001 base
  protection **plus** the clearance zone, so a splash never paints near a cannon. Raises `RainDropLanded(slot = cell,
  after = changed)`. After the team's last drop: `RainEnded`.
- If the team is eliminated mid-rain, its remaining drops never paint, and `RainEnded(other = 1)`.
- Balance: 36 drops × ~5 cells ≈ 180 cells, about the same as + LINE (2 × (36 + 48) ≈ 168).

### Tick order (determinism contract; terr-001 steps unchanged)

`TickBoosts` → **`TickCannons`** (down timers, Repaired) → Drop → Slots → Recover → Spinner (resolves BOOM/RAIN,
schedules drops) → **`LandRain`** (drops whose landTick == tick, in schedule order) → Fire (skips downed teams) →
MoveShots → stats → Eliminations (also clears down timers and cancels rain) → Escalate → End.

## Blocks used

- [x] **REUSE** [determinism-core](../blocks/determinism-core.md), [territory-grid](../blocks/territory-grid.md) (`GridPainter`), [slot-board-economy](../blocks/slot-board-economy.md)
- [x] **REUSE** [material-contact-audio](../blocks/material-contact-audio.md): procedural only (`ProceduralToneBank`)
- [x] **REUSE** terr-001 Presentation: `TileGridView.HoldAndReveal`, `BlastFx`, `DrumReelView`, `FocusCameraRig`, `ShareBarHud`
- [ ] **BUILD** (Presentation, format-agnostic): `RainFx`, `CloudFx`, `SmokePlumeFx`, `RadialTimerView`, `WavingFlag`, `FlagPainter` (editor)
- [ ] **BUILD** (Territory): `TerritoryTheme`, a theme-driven builder, CannonStrike + Rain rules

`TerritoryTheme` is the first step toward re-skinning `terr` from config alone. A third terr video should be a theme
asset plus a menu line. If it isn't, extract a "territory-theme" block then.

## Contracts (stubs are already in the tree; they compile. Implement bodies, never change a signature without reporting it)

**Runtime data (done by the planner):** `TerritoryTypes` (new `SpinFaceKind.CannonStrike = 5`, `Rain = 6`;
`CannonStrikeTarget`; events `CannonDestroyed, CannonRepaired, RainStarted, RainDropLanded, RainEnded`; `RainDrop`),
`TerritoryConfig` (`cannonStrikeTarget`, `cannonDownSeconds`, `rainDrops`, `rainSeconds`, `rainLeadSeconds`,
`rainSplashRadius`, `rainCannonClearance`, `theme` and `Theme` (never null), `TerritoryTeamDef.shortName`/`Short`,
`CountryWarsTeams()`, `CountryWarsSpinFaces()`), `TerritoryTheme` (classic defaults = terr-001; `ApplyCountryWars()`),
`TerritoryTeamRig` (`cannonRoot`, `cannonTintRenderers`, `downText`, `nameText`, `flagRoot`), `ShareBarHud.showNames`,
`ShareBarCanvasBuilder.BuildFramed(parent, n, frameColor, …)`.
`layoutVersion` is **not** bumped, so `TerritoryConfig_001` is never migrated by this work.

**Simulation API (A):** `CurrentTick`, `TickSeconds`, `RainDrops`, `IsCannonDown(t)`, `CannonDownSecondsLeft(t)`,
`CannonDownFraction(t)` (1 just struck → 0), `IsRaining(t)`.

**Presentation (B):** `RainFx.Play(from, to, seconds, color, splashRadiusWorld)` · `CloudFx.Show(centre, radius, tint, fade)
→ handle`, `Hide(handle, fade)` · `SmokePlumeFx.Begin(pos, scale) → handle`, `End(handle)` · `RadialTimerView.Build(radius,
thickness, fill, track)`, `SetFraction`, `SetVisible` · `WavingFlag.Build(texture, w, h, flyLeft, segX, segY)` ·
`FlagPainter` (fills, `SavePng`).

## Work split (parallel; disjoint files)

| Agent | Owns (only these files) | Delivers |
|---|---|---|
| **A: rules** | `TerritorySimulation.cs` (+ `TerritoryConfig.Validate` if needed) | BOOM and RAIN per the rules above, metrics and recorder events, determinism |
| **B: VFX kit** | `Presentation/VFX/{RainFx,CloudFx,SmokePlumeFx,RadialTimerView,WavingFlag}.cs`, `Presentation/Editor/FlagPainter.cs` | The six pieces, format-agnostic, URP, no assets |
| **C: look and build** | `Territory/Editor/*` (+ new `CountryFlags.cs`), the Territory.Editor csproj | Theme-driven builders; terr-001 output unchanged; menu **Country Wars (terr-002)**; scene, config, theme and flag PNGs created on build |
| **D: presenter and sound** | `TerritoryPresenter.cs`, `TerritoryAudioKit.cs` | Theme at runtime, the BOOM and RAIN animations, names and flags on the HUD, the new sounds |

### C: look and build, detail
- Menu `Simulation Lobby ▸ 3D Formats ▸ Territory (terr) ▸ Country Wars (terr-002)` (priority 212) builds
  `Assets/Scenes/Territory_CountryWars_3D.unity` from `Assets/Settings/Configs/Territory/TerritoryConfig_002.asset`. The config is created
  if missing (`ResetLayoutToDefaults`, `CountryWarsTeams`, `CountryWarsSpinFaces`, theme = `TerritoryTheme_CountryWars.asset`,
  itself created with `ApplyCountryWars()` if missing) and never overwritten after that. Both menu items share one build path.
- Every material name is `theme.materialPrefix + <old suffix>`, so the classic prefix `Territory` gives exactly
  today's names. The post profile is at `theme.postProfileName`. The rebuilt terr-001 scene must not change.
- `HexTiles` floor: one in-scene flat-top hex prism mesh (never an asset), circumradius ~0.7 m, ~0.06 m gaps, three
  shades plus grout plane, seeded height variance, culled to the frame like the slabs. Optional rim light. `AmbientDriftField` dust.
- Board identity (`boardNameDecals`): a top row in the counter zone with a flag decal (a quad with the flag texture)
  plus the country name (TMP decal), then the counter, then FIRING, with no overlap. Grow the counter zone only when names are on.
- Cannon: fill `cannonRoot`, `cannonTintRenderers` (cap, barrel, ring) and `downText` (an upright TMP ~0.6 m tall above
  the cannon, facing the camera, empty). With `showFlags`, a thin mast on the base disc on the outer-corner side, about
  1 m above the base, with a ~0.6 × 0.4 m `WavingFlag` flying outward (`flyLeft` on left quadrants) → `flagRoot`.
- `CountryFlags`: India, South Africa and Australia drawn to their real constructions (as close as the primitives
  allow). West Indies is a generic maroon flag with a gold sun over a green island, **deliberately not** the CWI
  crest. Saved to `Assets/Settings/Flags/Flag_<NAME>.png` on every build and assigned to `theme.flags`.
- HUD: the caption style, question, rules and prompt from the theme; share bar frame colour, `showNames`, short names.

### D: presenter and sound, detail
- The theme wins over the presenter's own colour and text fields (tile neutral, eliminated counter, drum idle and
  outline, bomb, self-destruct, question, rules). The classic theme equals today's defaults.
- **BOOM:** caption `{SPINNER} BLOWS UP {TARGET}'S CANNON!` (self: `{TEAM}'S CANNON BLOWS UP!`). `BlastFx` in
  `cannonBlastColor` at the base, a camera focus and shake. The barrel droops ~35° and the cap squashes, the tint goes
  to `cannonDownTint`, the flag tips over, a smoke plume rises, and a `RadialTimerView` in the team colour drains on
  `CannonDownFraction`, with `downText` = the ceiling of the seconds left.
  **Repair:** an overshoot pop (1.25 → 1 over ~0.35 s), the tint restored, the flag back up, the smoke ended, the ring
  hidden, a small white sparkle, and the caption `{TEAM}'S CANNON IS BACK!`.
- **RAIN:** caption `{TEAM} MAKES IT RAIN!`, and a `CloudFx` in the team tint over the grid centre (~4 m up) until
  `RainEnded`. Each drop launches when `(landTick − CurrentTick) × TickSeconds ≤ fallSeconds` (0.6) and drops from
  ~6 m up straight onto the cell, so it lands on the tick the sim paints. Skip launches for eliminated teams.
- Eliminated: the board name dims and the flag drops to half-mast. Winner: the flag stays and the camera pushes in on it.
- Drum result text: BOOM → `CANNON STRIKE!`, RAIN → `RAIN!`.
- Audio (procedural): cannon down = a big boom plus a metallic clank; repair = a ratchet and rising chime; rain start = a
  low thunder roll; a drop = a soft, rate-limited patter.

## Config + tuning targets (only what differs from terr-001)

| Knob | Start at | Note |
|---|---|---|
| `teams` | `CountryWarsTeams()` | names, colours, short names |
| `spinFaces` | `CountryWarsSpinFaces()` | RAIN weight 3, BOOM weight 2 |
| `cannonStrikeTarget` | Leader | the comeback lever |
| `cannonDownSeconds` | 10 | user spec |
| `rainDrops` / `rainSeconds` / `rainLeadSeconds` | 36 / 4 / 0.8 | ~180 cells per rain |
| `rainSplashRadius` / `rainCannonClearance` | 1 / 3 | |

## Acceptance criteria

- [ ] All asmdefs compile, and deleting `Simulations/Territory` leaves everything else compiling
- [ ] **terr-001 unchanged:** rebuilding *Multiply or Release* gives the same look (same material names and values)
- [ ] Same seed twice gives the same winner, elimination order and the tick of every CannonDestroyed and RainDropLanded
- [ ] Rain never paints within `rainCannonClearance` of a base cell; BOOM never eliminates on its own
- [ ] Readable at 480px wide: share bar `IND 34%`, board names, the down countdown
- [ ] A 10-15 min typical run with at least two lead changes (playtest)
- [ ] The sound passes a listening check

## Render + publish

1920x1080 long form. Thumbnail: India's cannon exploding mid-board with a big `BOOM`, four flags. Pinned comment:
"Pick your country 👇 Who wins?" Record the logged seed.

## Retro

`KnowledgeBase/retros/YYYY-MM-DD-terr-country-wars.md`, then update `PROJECTS.md`.
Open questions: does a second long upload get v15's push (format vs luck)? Do country teams lift comments or likes
over colours?

## Build log

- 2026-10-15: plan written, contracts stubbed and compiling, four Sonnet agents dispatched (A rules, B VFX, C build, D presenter).
- 2026-10-15: all four landed and were reviewed. Every assembly compiles (dotnet and Unity's own recompile, 0 errors). Not yet built or played in Unity.
  Notes from review: left-quadrant cannon flags fly outward and show their reverse (mirrored South Africa, correct for a real flag;
  the board decal shows the obverse). The theme prompt line shows only when a theme asset is assigned, so terr-001 is unchanged.
  First-playtest eyeball list: name decal size at 480px ("SOUTH AFRICA"), mast vs barrel sweep, hex floor density, dust
  alpha, the down countdown size, rain streak brightness, cloud density, smoke contrast, and the mix of the new sounds.
- 2026-10-15, **pacing v2** (first playtest: "very slow"):
  - **Multiplier ramp:** `multiplierRampSeconds` 600 / `multiplierRampStep` 1 (first set to 300; 5 min felt wrong, moved to 10). Every 10 battle minutes, every multiplier gains +1:
    the board ×2 slots, the drum's ×2 face, and the ×3/×5 boosts. Applied in `ApplyEffect`, so a boosted slot gets it too.
  - **HUD for the ramp:** a `MultipliersRaised` event and caption ("MULTIPLIERS UP! ×2 IS NOW ×3"). The chips and drum faces relabel to the live factor.
  - **SPIN lane gate:** open 2 s / closed 5 s (was 1 s / 5 s), so spins come about 1.7x as often.
  - **Lane gate progression:** `laneGateOpenPerElimination` 2. Each elimination adds 2 s to every open window (2 s, then 4 s, then 6 s), so the endgame spins more. Off (0) for terr-001.
  - **Elimination target = the base plate** (`basePlateRadiusCells` 2.1, a 1.4 m disc with a team-coloured rim): an enemy ball painting any of its 21 cells knocks the team out (was the one 0.33 m cell hidden under the cannon). BOMB / LINE / RAIN still can't paint the plate. terr-001 keeps 0, so the old one-cell rule is unchanged.
  - **Config:** written into the existing `TerritoryConfig_002.asset` and into the builder's new-config defaults. The ramp
    defaults to 0 (off), so terr-001's published config is unchanged.
- 2026-10-15, **progression v1** (playtest: "takes very long"). Every value is off by default, so terr-001 is unchanged:
  - **Knock-out reward** (`knockoutReward`): the team whose ball reached the plate takes the loser's bank and a free spin. The subline under "X IS OUT" says so.
  - **Faster cannons** (`cannonSpeedupPerElimination` 0.25): sweep and fire rate x1.25 after the first knock-out, x1.5 after the second.
  - **Final showdown** (`showdownPlateRadiusCells` 3.5): at two teams left, both plates grow from 1.4 m to 2.3 m. They claim the cells under them (a growing plate never knocks anyone out by itself), and the SPIN lanes stay open. A "FINAL SHOWDOWN!" caption follows the "IS OUT" caption.
  - **Earlier escalation:** `escalateAfterSeconds` 210 (was 360), so ball drops start speeding up at 3:30.
  - **Heavy shots** (`heavyShotsAfterSeconds` 600, radius 1): from 10:00 every hit splashes a radius-1 disc. Plates stay protected, so only a ball still knocks a team out.
  - To check in playtest: typical length, whether the reward snowballs the first team to get a knock-out, and whether the showdown ends in under about 3 min.
