# Backrooms Game Plan

Working title: TBD (suggestions: *Copy of a Copy*, *Generational Loss*, *Misremembered*)
Engine: Unity 6000.6.9f1, HDRP 17.6 (moved from URP 2026-11-15), Input System 1.20 · Platform: PC (keyboard/mouse + gamepad)
Created 2026-11-13

## 1. Pitch

Survive an endless web of liminal levels. Every level is a copy of a real place, and each copy
is remembered a little worse than the last, as if someone described an office to a person who
had never seen one and asked them to draw it. The further you wander, the less the world
remembers what it is copying.

**Pillars**
1. **Calm liminal unease** first, horror second. Long quiet stretches; the monster is rare and the dread builds slowly.
2. **Never the same twice.** Millions of distinct, seeded levels that players can share by number.
3. **Sound is half the game.** The hum, the carpet, and distant noises carry the mood.
4. **Wrongness you can feel.** "Copy drift" makes levels subtly, then openly, incorrect.

## 2. Decisions so far

| Topic | Decision |
|---|---|
| Length | Truly endless |
| Feeling | Exploration and calm liminal unease |
| Release | itch.io or Steam if it's good |
| Code | Claude writes most of it; you review and understand it |
| Look | Photoreal, with found-footage camcorder moments |
| Levels | Mostly procedural; some hand-built (wiki "anchor" levels) |
| Layout | Never changes while you're in a level |
| Goal | Each level has a real exit somewhere |
| Lore | None (no notes or tapes) |
| Player | Unnamed character, first person |
| Monsters | A pool of creatures; they react, stalk, then chase; they appear rarely |
| Player vs monster | Hide and run only |
| Death | Restart from the last checkpoint |
| Survival | Stamina, sanity and thirst; almond water restores |
| Tools | Flashlight, camcorder (raised as a tool), almond water |
| Inventory | "Look into the bag": the backpack swings around, you grab 3D items, the game doesn't pause |
| Puzzles | Simple |
| No-clipping | Exists but very rare |
| Audio | Very important; ambient drones; no voice acting |
| Mode | Single-player, plus online co-op for 2–4 players (planned, see §19) |
| Saves | Autosave at checkpoints |
| Settings | Sensitivity, FOV, graphics quality, audio sliders, key rebinding |
| Achievements | Yes (levels and other milestones) |
| Assets | Free packs and simple Blender models |
| World seed | One shared universe by default, plus custom seeds |
| Level links | Branching exits (a web of levels) |
| Wiki levels | Yes: famous Backrooms Wiki levels, so the game is released under CC BY-SA 3.0 (see §10) |
| "AI" | Procedural generation and an AI Director, plus "copy drift" (things get remade worse) |

## 3. The hook: copy drift

Every generated level descends from a **source**: a real-world place archetype (office, school,
mall, pool, hotel hallway, parking garage, hospital, apartment block, warehouse, house, airport,
church, gym, ...) or a wiki anchor level.

Each level has a **parent** (the level it was copied from) and a **drift** value. A child's drift
is its parent's drift plus a random amount. Drift picks which **mutation operators** apply:

| Drift | Feels like | Example operators |
|---|---|---|
| 0–20% | Almost right | Wallpaper repeats too often, doors slightly too tall, outlets with no covers, one light color off |
| 20–50% | Misremembered purpose | Stairs into the ceiling, sinks with no pipes, signs in gibberish, desks fused together, windows showing a wall |
| 50–80% | Described, never seen | Wrong proportions, furniture replaced by box shapes, stretched textures, rooms built from one repeated prop |
| 80%+ | Barely a memory | Geometry hinting at a place, ghost outlines of objects, sound of the original playing wrong |

Audio drifts too: the source's ambience gets detuned, bit-crushed, slowed or reversed as drift rises.
Creatures drift as well: the same creature type looks and moves more "wrong" in high-drift levels.

Drift happens **between** levels, never inside one, because the layout must not change behind you.
Revisiting the same level number always gives the same level.

**Why this gives "millions":** about 15 archetypes × a subset of about 40 operators × materials ×
lighting × sound beds × layout parameters gives tens of millions of distinct combinations.
Level IDs run to about 2 billion.

The data model, for implementation:
- `LevelRecipe` (data): source archetype, module set, materials, props, light style, sound bed, hazards, creature pool, drift.
- `Mutate(recipe, rng, drift) → recipe`: a pure, deterministic function. The operators are small, data-driven classes.

## 4. World structure

- **Seeds.** `levelSeed = Hash(worldSeed, levelId)`. The default `worldSeed` is fixed (the shared universe); custom seeds are a new-game option.
- **Level 0** is the start: the classic mono-yellow office rooms, with the humming lights and damp carpet.
- **Anchor levels.** Hand-built versions of chosen Backrooms Wiki levels at their canon numbers (for example Levels 1, 2, 37, Fun). Generated levels near them can be drifted copies of them.
- **Exits.** Each level has 2–4 exits (doors, stairwells, elevators, ladders, a pool drain, ...). `Hash(levelSeed, exitIndex)` picks the destination level and makes the destination a child of this level. Exits are placed at deterministic chunks within a findable radius of the spawn point (the "real exit somewhere").
- **No-clip.** About 1 level in 200 has a weak spot in a wall that drops you into a random distant level.
- **Level log.** An in-game list of visited level numbers and the exits you found between them, drawn as a graph.

## 5. Generation tech

- **Infinite chunk grid** of about 16 m chunks, streamed around the player, with distant chunks pooled and disabled.
- **Determinism rule** (the common bug in other Backrooms games): a chunk's content depends **only** on `Hash(levelSeed, chunkX, chunkZ)`, never on which chunks were generated first.
- **Seams between chunks.** The doorways on each chunk edge come from `Hash(levelSeed, edge coordinates)`, so neighboring chunks always agree. Inside a chunk, rooms are cut with BSP (binary space partitioning) or a module grid.
- **Meshes.** Snap-together modules (walls, door frames, pillars, ceiling tiles), combined per chunk to keep draw calls low.
- **Lighting.** You can't bake lightmaps for procedural rooms, so we use HDRP real-time lights (rectangle area lights near you, lumens), volumetric fog, SSGI/SSAO/SSR, emissive ceiling panels, a cap on shadow-casting lights, SSAO and fog. One central flicker controller drives the lights.
- **Navigation.** Runtime NavMesh per chunk, using the AI Navigation package and async builds.
- **Debug level browser.** A dev tool for jumping to any level ID or seed and flying through it. It's essential for testing millions of levels.

## 6. AI Director

The Director acts as a hidden game master that keeps the "calm unease" pacing:
- It tracks **tension** from time since the last scare, sanity, darkness, noise and exploration speed.
- It runs in cycles: long quiet stretches, then small unease events (a distant door, a light dying, footsteps that stop when you stop), then rarely a creature.
- It spawns creatures only when tension and cooldowns allow, and almost never in the first minutes of a session.
- It picks events from the level's recipe, so a pool level has different scares than an office level.

## 7. Creatures

- **One shared brain**, with different data per creature type. States: dormant → aware → stalk (keeps distance and stays out of sight) → chase → search → leave.
- **Senses.** Hearing (sprinting, landing, dropping items, the camcorder beep) and sight (a vision cone affected by the flashlight and light level).
- **Hiding.** Generated hide spots (under desks, in closets, behind pillars, in darkness). Crouching and flashlight-off make you harder to detect.
- **The pool.** Build the framework for many creatures, ship one first, then add more. Wiki entities are allowed (with credit) since the game is CC BY-SA. Don't use creatures from Kane Pixels' series or the A24 film: those are copyrighted, not wiki content.

## 8. Player and items

- **Controller.** Unity CharacterController, walk/sprint (stamina)/crouch, head bob, footsteps by surface.
- **Flashlight.** Battery drains; it flickers when low.
- **Camcorder.** Raising it shows the REC overlay and timestamp, a VHS effect, zoom and night vision; it uses battery. Some anomalies render **only** on a camcorder-only camera layer.
- **Almond water.** Restores thirst and sanity.
- **Sanity.** Drops in darkness, isolation and near creatures. Low sanity brings hallucinations: fake sounds, fake creatures, extra drift effects.
- **Backpack.** A key swings the bag in front of you. A separate camera renders its 3D contents and you click or drag to grab an item. The game keeps running while it's open.

## 9. Audio and visuals

- **Audio Mixer** groups: ambience, hum, footsteps, creatures, SFX, UI. Snapshots for tension, the camcorder and low sanity.
- **The hum.** Spatial loops per light cluster, plus reverb zones sized to each room.
- **Drones.** One per archetype, processed by drift.
- **Visual post-processing.** Photoreal HDRP with a found-footage look: auto exposure, ACES, grain, slight colour fringing, vignette and volumetric fog. Ultra quality uses ray-traced GI where the GPU supports it. The camcorder adds lens distortion, chromatic aberration, scanlines and a timestamp.

## 10. Licensing and release (read before using any asset)

**Backrooms Wiki content** is CC BY-SA 3.0. Because we're using wiki levels, the wiki's licensing guide says the **whole game must be released under CC BY-SA 3.0** (or GPLv3). That means:
- A license statement on the store page and website, crediting the wiki and the authors of every page we adapt. We'll keep a `CREDITS.md` with each page's URL, authors and license.
- **No DRM.** Turn off Steam DRM wrapping.
- No EULA that restricts what the license allows.
- Anyone may legally redistribute the game, even for free. That's a real limit on selling it.
- Copying a page with only small changes isn't acceptable to the wiki; adapt the levels properly.
- Wiki *images* can carry other licenses. Don't use them; we make our own art.

**Generic Backrooms concept.** The 2019 4chan post isn't owned by Kane Parsons or A24. In 2026 A24 publicly said that community Backrooms content doesn't belong to it.

**Asset rule:** only use assets whose licenses work inside a CC BY-SA game:
- OK: CC0 (Poly Haven, ambientCG, Kenney), CC BY, CC BY-SA (credited), and our own Blender models.
- Avoid: anything NC (non-commercial) or ND (no derivatives). Avoid Unity Asset Store art and audio until checked, since its EULA forbids redistributing the assets.

**Steam AI disclosure.** Procedural generation and the Director aren't generative AI. If any AI-made art, sound or text ships in the game, Steam needs a "pre-generated AI content" disclosure.

**Market.** Steam has 150+ real Backrooms games and many low-effort ones. The copy-drift hook and a distinctive title are how this one stands out. Ship a free itch.io demo first to get feedback.

*Not legal advice. Confirm the wiki terms with the wiki staff before selling.*

## 11. Saves, settings, meta

Built 2026-11-15 (Phase 7). All files live in `Application.persistentDataPath`.
- **Save slots (unlimited):** `saves/slot_N.json` + `slot_N.jpg` thumbnail. Each is its own world (seed, level, checkpoint, meters, inventory, logbook, playtime). Autosave on entering a level, every 2 minutes, on Save & Quit and on closing the game. The old single `save.json` is moved into slot 1 automatically.
- **Profile (`profile.json`, shared by all slots):** 25 achievements, lifetime stats, and the level log (first visit, where you came from, visits, time spent).
- **Settings (`settings.json`):** resolution, fullscreen, V-Sync, quality Low/Medium/High/Ultra (Ultra = ray-traced GI if supported), FOV, brightness; mouse sensitivity, invert look; head bob, camera-effects strength, reduce flashing, sound captions; volumes for master, ambience (hum), effects, creatures, interface. Applied through a priority-0.5 settings Volume.
- **Key rebinding (`bindings.json`):** keyboard/mouse bindings for movement, jump, sprint, crouch and the tools (`Controls.cs`). Gamepad rebinding isn't done yet.
- **Main menu:** a live drifting camera through Level 0 with random events (flicker, lights die, footsteps behind, falling tile, a Faceling crossing, a grin in the dark, a ringing phone). It wanders further, events come more often, the title glitches more, and sometimes it shows other levels you've found, the more progress you have.
- **Pause (Esc / gamepad Start):** freezes time and audio. Resume, Settings, Level Log, Achievements, Stats, Save & Quit to Menu.
- Steam achievements later via Steamworks.NET.

## 12. Roadmap

Each phase ends with something playable. Rough times assume a few sessions a week.

| # | Phase | Done when | Est. |
|---|---|---|---|
| 0 | **Setup** | ✅ Folder layout, asmdefs, Forward+ on. Project stays in OneDrive (your call). Still open: git + Git LFS, AI Navigation package (needed in Phase 5) | 1 wk |
| 1 | **Walk Level 0** | ✅ 2026-11-14: open-plan maze of free-standing partition walls (L/T/U shapes, spirals, rooms with a gap) drawn by "walkers" that never close a loop, so every space is reachable (proved by the flood-fill self-check). Plus/square pillar halls in 64 m clusters, block grids, solid blocks, hum, flicker, footsteps. Not yet checked: 60 fps on mid-range, how it sounds | 2–3 wk |
| 1b | **Level 0 overhaul** | ✅ 2026-11-14: district architecture (see §14). 13 archetypes, per-room looks, varying ceilings, real places, curiosities with an adaptive layer, CC0 photo textures, pooled shadowed lights. All levels pass the self-checks. Not yet checked: frame rate on real mid-range hardware, long play sessions | — |
| 2 | **Levels and exits** | 🟡 Started 2026-11-14. Done: `LevelTheme` + `LevelCatalog` data; 9 levels (0, 0.2 Remodeled Mess, Frozen, Flooded, Blackout, Red Rooms, 1, 2, 37); Level 0 areas (pit fields, Manila Room, Red Rooms, blackouts); exits (flickering walls, doors, stairs, elevators); branching links; glitch transition; autosave on entry; custom seed; ~20 box-built props with glitched placement; CREDITS.md. Still open: in-game way to type a custom seed (menu, Phase 7), level log, Level 0.2's collapse, level-specific sound, playtest tuning of exit/prop rates | 3–4 wk |
| 3 | **Copy drift** | ✅ 2026-11-14: drift per district rises with distance (noticeable after ~300 m) plus noise and a per-level `baseDrift`. Operators: short walls, doubled walls, wrong door heights, sick-coloured light, missing ceiling, swapped wall/floor surfaces, lumpy ceilings, off-grid panels, hanging pillars, repeated props, more glitched props and wrongness. Hum detunes and fog thickens with drift. Still open: debug level browser, higher-tier operators (the 50%+ rows in §3) | 3–4 wk |
| 4 | **Survival and tools** | ✅ 2026-11-14: stamina, crouch, thirst, sanity (drains in the dark, low sanity smears the picture and plays phantom sounds), flashlight (F), camcorder night vision (right mouse), 3D bag (Tab), almond water and battery pickups (E) that stay taken, HUD meters, F8 debug readout. All saved. Still open: playtest tuning of drain rates | 3 wk |
| 5 | **Director and first creature** | ✅ 2026-11-14 (first pass): fear director (tension from quiet, darkness and sanity; 3-minute grace; footsteps that stop when you stop, faceless residents, rare hunts). Hunt = the lights die and a Smiler (wiki Entity 3) comes: drawn by your flashlight beam, running, or being seen; hide by breaking line of sight, crouching, light off, or outlast the 75 s dark. Caught = static, back to the checkpoint. Facelings (Entity 9) watch and step away. Shared creature brain + physics flow-field pathing (no navmesh). Still open: proper models and sounds, level-specific scare recipes, more creatures, playtest tuning | 4 wk |
| 6 | **Anchor levels** | 3–5 hand-built wiki levels with credits | ongoing |
| 7 | **Menus and meta** | ✅ 2026-11-15: live main menu with events that change as you progress, pause menu, unlimited save slots with thumbnails and autosave, settings (graphics/audio/controls/comfort), key rebinding, 25 achievements with toasts, wiki-style level log, stats page, sound captions. See §11. Still open: gamepad rebinding, custom seed entry, play-testing the menus by hand | 2–3 wk |
| 8 | **itch.io demo** | Free public demo, gather feedback | 1 wk |
| 9 | **Content expansion** | More archetypes, operators and creatures | ongoing |
| 10 | **Steam** | Store page early for wishlists, license statement, DRM off, AI disclosure if needed, Steam achievements | 2 wk |
| 11 | **Co-op multiplayer** | Planned: 2–4 players, host + join code, proximity voice. Phases M0–M7 in §19 | 3–4 mo |

Estimate: about 5–7 months to a strong demo.

**Folder layout:** `Assets/_Game/{Scripts/{Player,World,Drift,Director,Creatures,Items,UI,Save}, EditorTools, Data, Prefabs, Art, Audio, Scenes}`

**Project quirks**
- This Unity setup ignores scripts in folders named `Editor`, so editor code lives in `Assets/_Game/EditorTools` with its own asmdef (`Backrooms.Editor`, Editor-only). Runtime code is the `Backrooms` asmdef.
- The Game scene, every level's settings (`Assets/_Game/Data/Levels`), textures and materials are generated by **Backrooms > Create Game Scene** (`GameSceneSetup.cs`). Re-running it overwrites hand edits to those assets, so change levels in `GameSceneSetup.cs`.
- **Backrooms > Run Self-Checks** checks every level: deterministic, walls stay in their chunk, every open cell reachable.
- Dev keys in Play mode: **F6** next level, **F7** jump 512 m out, **F8** debug readout, **F9** delete the current slot and start a new world. Saves are `saves/slot_N.json` in `Application.persistentDataPath`. The game opens on the main menu.
- Our own sub-levels use names, not numbers (Level 0.1 on the wiki is "Zenith Station", so numbering ours would clash).
- Level 0 should capture what makes the iconic versions compelling (vast emptiness, irregular partitions, the hum, dread) using our own designs. No copying of the A24 film's or Kane Pixels' specific scenes, creatures or designs, which are copyrighted.

## 19. Co-op multiplayer (built 2026-11-16: M0–M6)

**Your decisions:** 2–4 players in co-op. Everyone is always in the same level. One player hosts, and friends join with a short code (Unity Relay, no port forwarding, works in both the itch.io and Steam builds). Proximity voice chat.
- **Exits on Easy and Normal:** the group leaves together. The exit only works when everyone still alive is gathered at it.
- **Exits on Hard and Extreme:** every player has to escape through the exit themselves.
- Each new co-op world still gets a fresh random seed that players can't pick (§15).

### Decisions round 2 (2026-11-15)
- **Deaths, Hard/Extreme:** one player dies → everyone dies and the group wakes at the checkpoint.
- **Deaths, Easy/Normal:** a dead player spectates a living friend in third person (click to switch). They come back when someone beats the level and the group moves on. If everyone is dead at once, the group wakes at the checkpoint.
- **Dead voice:** dead players hear and talk only to other dead.
- **Owner quits:** everyone returns to the menu (the run is saved). Owner setting, off by default: keep going instead, with the owner choosing who takes over. The world is handed to that player and everyone reconnects to them automatically.
- **Never drop anyone** unless the owner kicks or bans them. A dropped connection retries forever ("Reconnecting...") while their body waits where it stood. Network timeouts are raised so a slow link doesn't get cut.
- **Bans** are forever for that owner, with an unban list in settings.
- **Join approval:** an owner setting, off by default. When off, anyone with the code can join.
- **Saves:** the co-op world lives only in the owner's save. Any save can be opened to friends.
- **Looks:** the user's own hazmat-suit model, with a colour tint/stripe per player. No names in the world. Typed names appear only in the pause menu's player list (kick/ban).
- **Voice:** open mic by default (push-to-talk in settings), and creatures hear loud talking.
- **Items:** shared world (taken by one = gone for all), own bag, and you can hand almond water or batteries to a friend.

### Status (2026-11-16)
M0–M6 are built and were tested between the Editor (owner) and a development build (guest) over Unity Relay.
- **Tested and working:** join by code, seeing each other, moving levels together, the gather rule at exits (Normal), the item sync, hunts and blackouts on the guest's screen, the mirrored Smiler and Faceling, a death and spectating, coming back on the next level, crash-rejoin into the same body, kick, ban, join approval, the owner hand-off, and both games in the voice channel.
- **Not tested yet:**
  - A real dropped connection (only crashes were tested).
  - The other guests following a new owner (needs 3 players).
  - Hard/Extreme stepping through the exit and the one-death wipe.
  - 3–4 players.
  - How the voices actually sound.
- **Still local per player:** level events (Level Fun's party, Level 188's record, lures, Level 0.2's collapse) aren't synced yet.
- **Code:**
  - `Scripts/Net/`: CoopSession, CoopAvatar, SuitRig, CreatureProxy, VoiceChat, CoopBans, CoopDevTools.
  - `Player/PlayerAgent.cs` and `Player/Spectator.cs`.
  - The GameSession partials: `GameSessionCoop`, `GameSessionWorldSync`, `GameSessionGroup`, `GameSessionDirectorSync`, `GameSessionOwnership`.
  - `UI/MenusCoop.cs`.
- **Assets:** run **Backrooms > Build Co-op Assets** after changing the suit model. It builds the suit colour textures, the four materials, and `Resources/CoopAvatar` and `Resources/CoopCreature`.
- **Dev switches** (development builds and the Editor only): `-coopJoin CODE`, `-coopProfile NAME`, `-coopShots DIR SECS`.

### How it works
- **The world is never sent over the network.** Generation is deterministic from the seed, so each machine builds the same level itself. The host sends only the seed, the level id, the arrival point, and the things that change: pickups taken, boards pried, levers pulled, goals done, door pairs, level events.
- **The host is in charge.** The host runs creatures, the fear director, level events (party, courtyard, collapse, flicker) and anything random. Clients show what the host decides. This avoids two machines disagreeing about a random roll. Today about 86 `Random.Range`/`Random.value` calls in 11 files run on every machine; those that change shared state move to the host or to seeded `WorldHash` rolls.
- **Each player keeps their own body and stats:** thirst, sanity, batteries, the bag, flashlight and camcorder. Sanity drains from your own darkness. Achievements and the stats page stay per player.
- **The host's save slot holds the co-op world.** It stores the seed, level, picked items, goals and each player's stats keyed by their Unity player id, so a friend who rejoins gets their stats back. Joiners don't write the world into their own saves.
- **Late joining:** a player who joins mid-run gets a snapshot (seed, level, picked list, goals, active events) and spawns beside the host.
- **Pause:** pause doesn't freeze time in co-op (no `Time.timeScale = 0`). It only opens the menu.

### Stack
- `com.unity.services.multiplayer`: sessions with join codes, and the Relay connection.
- `com.unity.netcode.gameobjects` (Netcode for GameObjects): players, creatures and the shared state.
- `com.unity.services.vivox`: proximity voice.
- `com.unity.multiplayer.playmode` (Multiplayer Play Mode): runs 2–4 players inside the Editor for testing.
- Needs a Unity Cloud project linked in the Editor. You do this once: sign in, link the project, and turn on Relay and Vivox in the dashboard.
- Players sign in anonymously.
- **Costs:** check Unity's current free-tier limits for Relay and Vivox before release.
- **Licensing:** the game stays CC BY-SA. These are online services, not bundled assets.
- **itch.io:** the build still works offline for single-player.

### Things in the code that assume one player
- One `FirstPersonController` is wired in by hand. These files use it: Creature, FearDirector, CourtyardLevel, GameSession, LevelEvents, Objectives, PartyLevel, PoolDrain, ProgressTracker, PlayerTools, Ladder.
- `ICreatureWorld` has a single `Player`/`Eyes`/`Body`.
- `LevelTrigger.ExitEntered` changes level at once.
- Static state survives Play sessions (domain reload is off). Netcode's singletons need the same Play → stop → Play testing.

### Phases (each ends with something playable)

| # | Phase | Done when | Est. |
|---|---|---|---|
| M0 | **Ready the code (still single-player)** | A `Players` registry replaces the single wired player. Creatures, director, level scripts and objectives ask "which players are near / see this / are in the dark". Random rolls that change shared state come from one place. Single-player plays exactly as before and the self-checks pass. | 1–2 wk |
| M1 | **See each other** | Packages are in and the main menu has *Host co-op* (pick or start a world, difficulty) and *Join co-op* (type the code). Players appear with a simple third-person body (own Blender model, no film or Kane Pixels look). Other players' footsteps and flashlight beams are visible. A determinism check compares a hash of each loaded chunk between host and clients and logs any mismatch. | 2–3 wk |
| M2 | **One shared world** | Pickups, boards, levers (the pool pump), goal items, readables, Level 188's doors, Level Fun's food and balloons, and level events all match on every screen. Late join works. | 2–3 wk |
| M3 | **Exits and deaths** | **Easy/Normal:** the exit works when everyone alive is gathered at it; the dead spectate and come back on the next level. **Hard/Extreme:** each player steps through themselves and waits in a static "between" space for the rest; one death sends the whole group back to the checkpoint. | 1–2 wk |
| M4 | **Creatures for a group** | The host runs creatures. Hunters pick a target (nearest, loudest, flashlight on, alone), and anyone can break line of sight. The fear director builds tension per player and starts hunts for the group. A blackout hits everyone in the level. Being alone is scarier on purpose. | 2 wk |
| M5 | **Kick, ban, dropping out, owner leaving** | Kick/ban/unban, optional join approval, auto-reconnect with the body waiting, the owner-leaves rules above (menu by default, or a hand-off to a chosen player). | 1–2 wk |
| M6 | **Proximity voice** | You hear friends only nearby, muffled through walls. Push-to-talk or open mic in settings, plus a per-player volume. Loud talking counts as noise for creatures (the director hears you). | 1–2 wk |
| M7 | **Testing and release** | 2–4 player sessions over the internet. Bad-connection testing. A menu option to report the join code. Both itch.io and Steam builds. Store pages say "online co-op (2–4)". | 1–2 wk |

**Estimate:** about 3–4 months of sessions for the whole thing. The first time you can walk around together is the end of M1.

### Open questions (decide when we get there)
- A friendly-fire style mechanic (bumping, pushing, flashlights blinding each other)? Default: none.
- Shared or per-player loot? Default: an item taken by one player is gone for everyone, and you can hand almond water to a friend.
- Text chat? Default: none. Voice and Discord are enough.
- Steam friend invites on top of codes later (a Steam-only add-on)?

## 18. Wiki accuracy, movement and graphics pass (2026-11-15)

- **Levels now:** 0 (with Arches, Blackout Zones, Red Rooms as areas inside it, pillars, wet carpet, Holes, the Peripheral Shift), 0.2 Remodeled Mess (collapses around you after a while), 0.3 The Icy Rooms, 1 (halls, corridors, sectors, flickering blackouts), 2 (archived "Old Pipe Dreams": pipe tunnels, boilers, locked doors), 37 (swim slowly in the deep pools), -1 (our own flooded data centre) and 19 (attic). The Frozen, Flooded, Blackout and Red variants were removed or folded in.
- **Movement:** walking in water is normal speed; jump (0.6 m) with bunny hops (a jump right as you land costs no stamina and gives +25% speed); stamina doubled (14 s sprint, 4 s recovery); sprint is endless while something is chasing you.
- **Graphics:** HDRP with CC0 ambientCG photo materials (mask maps generated), Blender-made fixtures (troffers, outlets, switches, vents, doors) and props.
- **Holes (2026-11-15):** Level 0's pit rooms are a floor of real square pits (1.4 m openings, 0.6 m walkways), lined with the room's wallpaper, 2 m deep with a black bottom (the floor below's tallest ceiling ends 2.2 m down). Fall in and you're back at your checkpoint.
- **Chunk streaming (2026-11-15):** the load spike was the ceiling lights: every chunk copied the 5,000-vertex troffer model once per light (~250 ms). Troffers are now one shared mesh per light object (a chunk builds in ~20 ms), and layouts are generated on worker threads (the district cache and Peripheral Shift table are locked).
- **Bag (2026-11-15):** a Blender rucksack (Art/BagItems.blend) with real thermos and battery models (also used for world pickups). Clicking an item lifts it out: water goes to your mouth, batteries into the flashlight; it counts when the motion ends. Picked-up items fly to your hand and into the bag.
- **Polish pass (2026-11-15, see memory polish-pass-decisions):**
  - *Bugs fixed:* lower floors' ceilings now have a top side and shaft openings get full-height faces (no more seeing into rooms below, floating light frames or black void through stairwells and pitfalls); stairwells have a lit stepped ceiling; pillars never poke through a ceiling; Level 2 pipes run continuously along a wall and ducts run wall to wall without overlapping lights; other floors load a ring of chunks round any opening; view radius is 5 x 5 chunks.
  - *HUD:* camcorder-style readouts (battery blocks with %, thirst/mind bars, breath only when winded) that dim when fine and flash red when low; prompts show your real key bindings; a goal line in the top left.
  - *Papers:* notes and painting backs rise into view as aged, creased paper held in your hands (handwriting font, coffee rings, thumbs, world dims); the logbook is lined notebook paper you page through with the arrow keys or mouse wheel.
  - *Bag:* swings off your back, over your shoulder and in front of you (and back); half speed, no sprint or jump while it's open; real thermos and battery models you lift out.
  - *Movement:* vault/mantle (Jump at anything 0.4-1.35 m), slide (crouch while sprinting), climbable ladders.
  - *Difficulty (per save):* Easy / Normal / Hard / Extreme scales thirst, battery and sanity drain, how many supplies exist, the quiet before scares, and starting supplies. Saves are named at New Game.
  - *Goals per level:* find a few items 50-100 m out (map pages, a fuse, valve wheels, keys); a marked door, locked until then, becomes a guaranteed way out (+ water and a battery). Other exits still work. Saved per world.
  - *Level 1:* multi-storey parking garage (stacked decks 8 m apart) joined by switchback car ramps (real slopes) and service ladders; bays with lines, wheel stops, numbered pillars, deck signs pointing to the ramp, cars under tarps and the odd old car (procedural car bodies), oil, speed bumps, barriers; crates you pry open for supplies (some appear behind you after a blackout or vanish when you look away); dripping and clanging pipes; humid haze; new sectors Ouroboros (construction), Garden (overgrown) and Fabled (neon) as our own takes on the wiki's.
  - *Level 2:* exits are marked (ajar, warm light, lamp, destination number), more than twice as common, chalk arrows and notes lead to them, and some locked doors can be forced (about half lead out).
- **Level Fun =) (2026-11-15):** the user's hand-made 100 m level model (made with Claude; AI disclosure in CREDITS.md), loaded as a fixed level (LevelGenerator.Static). Its light panels become real lights; 3 of its exit signs are real exits (which ones depends on the world). Getting in: balloon-tied doors in Levels 0 and 1, or touching a stray balloon in Levels 0/1/2/37. Until the Partygoers come with the creature phase: music and laughter that are always a room away, balloons that drift closer when you look away, smiles that appear on walls behind you, and cake/drinks that restore thirst/stamina but cost sanity. Stair fix: the stepped stair ceiling has no top side any more (it looked like a floating tiled shelf from above). Held paper has no thumbs.
- **Level 188 "The Courtyard of Windows" (2026-11-15):** the user's hand-made atrium model (Claude-made; disclosed in CREDITS.md), a fixed level like Level Fun. All hand-made level materials are double-sided now, and the loader finds lights, window panes and odd objects in any model. Shadows stand behind windows at the edge of sight and vanish when looked at; a gramophone record is the signal to leave (lights dip, sanity drains); banging behind the locked doors; the odd objects (vases, books, brass) drain sanity up close; the one ground-floor window with warm light behind it leads back to where you came from. Way in: very rarely a lit window appears on a wall ahead in Levels 0/1/2 and is gone after 2.5 minutes. Model updated to the user's newer version (quartered meshes, furnished hotel rooms behind the atrium wall). Some doors open: about 360 of ~860 passable interior doors are cut out of the door mesh and hung open on their hinges (chosen per world). Since the atrium wall is sealed, 1–4 door pairs are added per world on clear stretches of wall (away from windows, furniture and the exit window); stepping into one brings you out of its partner on the room side, and back. Door work waits 0.5 s after the level loads because the model's colliders aren't settled on the first frame.
- **Level 0 walls (2026-11-16):** wallpaper is now a full material set from `ProceduralTextures.WallpaperSet`: 2 m tiles of four 0.5 m hung strips (roll-to-roll tone, pattern slightly off at each seam, faint seams that lift in places), soft-edged printed pattern with uneven ink, paper grain from the CC0 photo, a matching normal map (grain, raised print, seam grooves, bubbles) and mask (print sheen). Water damage (yellowing, stains with a faint tide line, drips) is a separate HDRP detail map (`WallDamage`) tiling every 6.3–7.9 m, so it never repeats with the 2 m pattern. Applies to all wallpaper variants; plaster gets no seams.
- **Next:** per-level wiki creatures, as their own phases inside Content expansion. Cars could get proper Blender models (Blender was closed during this pass).

## 17. Generation overhaul (started 2026-11-14)

Decided with the user (Q&A after research into Backrooms generators and liminal-space design):
- **Done (stage 1, layout):**
  - Irregular regions instead of 64 m squares: a warped, weighted Voronoi of scattered sites picks the archetype per cell, so areas are blobs of very different sizes. Region edges often get a partition with doorways. Code: DistrictRegions.cs.
  - Rhythm: mostly tight (free-standing partitions mixed with real rooms and corridors cut by doorways); rare **giant halls** (5.8 m ceilings, heavy pillars on a world-aligned grid that continues across districts).
  - **Long halls**: finite straight corridors (30 to 80 m, shortened after playtest feedback) with doorways along them; border walls open where they cross.
  - **Seamless borders** (2026-11-14): open areas take their wallpaper, carpet, ceiling height and lights from the region, so a place continues unchanged across a 64 m district line; light rows sit on a world grid; a hidden face covers any ceiling step at a border (no more low band every 64 m).
  - **Deliberate copies**: about 3% of districts are built from a nearby district's plan.
  - **Light moods**: broad dim stretches (half the lights dead) and dark patches with one light left on.
  - Backrooms > Render Level 0 Map draws a floor plan (Screenshots/level0-map*.png).
- **Done (stage 2, floors):** Level 0 has floors -3 to +3, 8 m apart, each with its own layout (DistrictFloors.cs).
  - Stairwells (40 steps in a walled shaft, railed opening above), balconies (railed openings over a drop), pitfalls (no railing), deep pits (a shaft through every floor), sunken rooms (three steps down inside each doorway).
  - Which features join two floors is a pure function of the level seed, so both floors agree. Consecutive floor pairs use opposite halves of a district so openings never land on stairs.
  - The streamer loads the player's floor all around, and the floors above/below only in chunks where a stairwell or drop joins them. Lights only come from the player's floor, so nothing leaks through floors.
  - Falling: land on the floor below winded (stamina) and a little shaken (sanity). Falling more than 1.6 floors (deep pits) noclips you out: to Level -1 once it exists (stage 3), to the checkpoint until then.
  - The self-check also walks floors 1, -1, 3 and -3.
- **Next stages (in order):**
- **Done (stage 3, up and down):**
  - **Level 19 · Attic Floorboards** (wiki, credited): stairwells on Level 0's top floor (3) climb into darkness and come out in the attic. Low wooden rooms (ceilings capped at 2.4 m), rafters, boxes and old furniture, no ceiling lights: the Orange Glow comes up through cracks between some floorboards. Weak floorboards (pits) drop you back into Level 0; broken doors lead to Level 0 or 1.
  - **Level -1 · The Data Center** (original, the user's idea; the wikidot wiki has no negative levels): an endless AI data center of server-rack halls, cold light and fan hum. Falling out of Level 0 drops you into a deep pool in a tall hall at its centre. The water starts chest-deep (you wade slowly, no sprinting) and drains a little every second (~15 min). Signs point to the pump control room in the very next hall (about 64 m from the pool, side set by the seed); spare control rooms stand in about one district in four further out, and any of their levers drains the pool in seconds. Control-room districts are server hall all round (no other place is built over the room); Level -1 has no warehouse districts; racks carry a cable tray over each row (2026-11-15). The door at the bottom of the basin (back up to Level 0) only opens once the pool is empty.
  - Code: DistrictSpecial.cs (server halls, pool hall, control room, attic glow and rafters), PoolDrain.cs, PoolSwitch.cs.
- **Done (stage 4, landmarks and traces):** code in DistrictLandmarks.cs, BoardedDoor.cs, ModelLibrary.cs.
  - **The Cut** (our own idea, not the film's scene): about 1 district in 70 on Level 0 (never near the start), a 24 m shaft through every floor. Each floor's rooms simply end at its edge, so you look down at the floors sliced open below; a railing runs along one side. Two floors up and down are loaded around it. Falling in takes you out of Level 0 (to Level -1).
  - **Boards:** planks across about half the pitfalls (narrow, walk carefully). Boarded-up doorways (0 to 3 per district): any whose blocking would cut something off can always be pried open (hold E about 1.6 s; the splintering is loud, so hunters nearby come to look). Doorways you can walk around may be nailed shut for good. Pried doorways stay open (saved).
  - **Signs of people** (rare, quiet; Level 0 and the attic): old camps (sleeping bag, lantern, cans, a box, sometimes almond water, a note from another wanderer for the logbook), a single chair facing a wall, tally marks, and "UP -->" arrows scratched on walls pointing toward the stairs.
  - **Models made in Blender** (source: Art/Stage4Props.blend, exported to Assets/_Game/Resources/Models): three plank variants with nail heads, sleeping bag, lantern, food can. Material names match the game's paints, so they merge into the generated meshes; anything without a model still falls back to code-built boxes.
  - Giant halls and hand-made rooms already existed (stages 1 and earlier).

## 16. Hand-made rooms (2026-11-14)

- Rooms written as data (JSON in `Assets/_Game/Resources/Forge/approved/`): zones (paper, carpet, height, lights), walls (wall, low, arcade, doorway, arch, wide), solids, props, wall writing, paintings with text on the back (E to turn over), and notes that go into the logbook (J).
- About 1 in 10 Level 0 districts gets one (never next to the origin). The self-check walks every room.
- Three so far: The Waiting Room, The Gallery of Nobody, Night Shift. Lore thread: "the Surveyor" (S.) measures how wrong the copies get and follows the hum back toward the photo rooms.
- An AI room generator was tried and dropped at the user's request.

## 15. Seeds and the origin rooms (2026-11-14)

- Every new game gets a random world seed. A custom seed works only in the Editor or a development build, so players can't pick one. (If multiplayer comes, each session still gets its own seed.)
- Every world starts in the **origin rooms** at Level 0's centre, rebuilt from the two real photos: you stand between a dotted wall (low rail) and a chevron wall (outlet) looking into a big hall; next door is the long room with the arcade of half walls and arches, a trash can and a bucket. They never drift and connect to ordinary Level 0 on all four sides. Code: `BuildOrigin()` in `DistrictDrift.cs`. Screenshots in `Screenshots/`.
- Controls: hold Ctrl to crouch, Shift to sprint. Dev keys: F4 resident, F5 hunt, F6 next level, F7 jump 16 chunks out (to see drift), F8 debug readout, F9 new game.
- Density pass: Level 0 is mostly tight mazes now (more walls, dead-end corridor caps, lone pillars, far fewer stores/atria, props 75% rarer).

## 14. Level 0 architecture (built 2026-11-14)

Level 0 and its variants use the **district generator** (`DistrictLayout.cs` + `DistrictExtract.cs`). Levels 1, 2 and 37 still use the maze generator.

**Your decisions:**
- **Look:** movie-bright (pale cream-yellow, even light, light haze).
- **Maze share:** about a third of districts are the partition maze.
- **Ceilings:** heights vary between rooms.
- **Furniture heaps:** rare, a few per km².
- **Wallpaper and carpet:** a subtle family.
- **Details:** baseboards, chair rails, cased door frames, arches, thick walls, pilasters.
- **Openings:** mostly cased, with no doors.
- **Scale:** up to 64 m spaces.
- **Real places:** offices, storage, hallways of doors, and four unbranded stores.
- **Extras:** hazards, curiosities, subtle wrongness.
- **Darkness:** occasional.
- **Visuals:** max quality.
- **Textures:** CC0 photo textures.
- **Adaptive layer:** fresh encounters now; the fear director comes in Phase 5.

**How it works:**
- **Districts.** The world is cut into 64 m districts (2 × 2 chunks). Each is planned whole from (seed, district) and each chunk extracts what it owns, so chunk seams inside a district are invisible. Plans are cached because each serves four chunks.
- **13 archetypes:** Maze, Open Expanse, Pillar Hall, Office Block, Corridors (incl. hallways of doors and hotel-room rows), Panel Rows, Nested Rooms, Atrium, Storage, Furniture Showroom, Supermarket, Clothing Store, Mall Concourse. A district never repeats the archetype of its west, south or south-west neighbour's first pick.
- **Rooms (zones).** Each room rolls:
  - **Wallpaper:** 6 patterns × 2 tints, some water-stained.
  - **Carpet:** 4 variants, plus rotation, or wood/tile/concrete in real places.
  - **Ceiling:** variant and height (2.4–6.2 m, with drywall steps where heights meet).
  - **Lights:** one of 7 layouts, snapped to the 0.5 m ceiling-tile grid.
  - **Trim:** baseboards, chair rails, pilasters.
  - **Openings:** doorways, arches or wide openings.
- **Reachability by construction.** All walls go through one union-find; a wall that would close a loop becomes a cased doorway, added corner-first so it lands mid-wall. A final pass removes large furniture from doorways. The self-check flood-fills every level and checks doorways are clear.
- **District borders** stay at 2.8 m and only carry "runs" (walls both districts can compute), so neighbours always meet cleanly.
- **Curiosities.** The layout proposes slots and the adaptive `EncounterDirector` decides what appears. It favours kinds you haven't seen, gets more generous after quiet stretches, and remembers choices in the save so places look the same when you return. Kinds: lone chair, chair circle, lone door, TV with static, ringing phone, tile pile, stairs into the ceiling, window onto a wall, outlet column, too-tall and tiny doors, almond-water stash, lone desk.
- **Lighting.** Panels are merged glowing mesh. A pool of 72 real-time lights (6 with soft shadows) and 8 hum sources follows the player.

**Measured (12 × 12-chunk sample):**
- All 13 archetypes appear.
- 3% of neighbouring districts share an archetype.
- 144/144 chunk layouts are distinct.
- 40 distinct floor/ceiling looks.
- Render with 3 × 3 chunks loaded: about 185k triangles and 1.2k draw calls.

**Tuned 2026-11-14 (feedback: "too common"):**
- **Plain areas dominate:** about 83% of districts are maze, open expanse, pillar hall or panel rows (maze ~38%). Real places are about 6%.
- **Rarer props and curiosities:** small props are down about 85%.
- **Mostly empty rooms:** 75% of offices and similar rooms are left empty.
- **Rare wrongness:** about 1 district in 5.
- **Fewer hazards:** heaps, pits, floods and dark rooms are roughly halved.
- **Dead ends:** 45% of corridors get a cap wall partway along. A cap that would seal anything off is skipped.
- **More pillars:** lone pillars and loose pillar grids also appear in open and maze areas, but only where every side is open, so you can always walk around them.

**Not done yet:**
- Light panels close up still read as glow more than as crisp troffers.
- No occlusion culling yet.
- Chunk objects aren't pooled.
- The fear director is Phase 5.

## 13. Open questions (decide later)

### Game modes and exploration features (decisions, 2026-11-17)

- New Game modes: **Exploration** (no survival drains), **Survival** (Easy / Normal / Hard / Extreme), **Hardcore** (tough, high-stakes, permadeath with a clear warning; available in co-op), **Endless** (open-ended exploration, no campaign missions or completion goal, choose a Survival difficulty), **Story** (missions in the procedural world, with a difficulty choice), and **Custom** (individual generation and gameplay settings).
- **Hardcore co-op death rule:** dead players stay dead through level transitions. If the surviving party beats three levels, dead party members respawn. (The exact definition of a level beaten and respawn location can be finalized during implementation.)
- Story: rated missions will be placed into the existing procedural world. Mission ratings, mission design and narrative are deliberately deferred: design them later; do not invent missions or lore as part of the mode-system work.
- Add an explored-area map across floors and visited levels, with a polished UI and player-created, named markers.
- Add an in-game photo album and track discoveries as photographed.
- Expand random events to include environmental changes such as lights changing, routes being blocked, and rooms being altered.
- Still to decide: what qualifies as a photographable discovery and how it is captured; whether the map and markers are shared in co-op; and whether event-driven room/route changes are temporary or persist (the existing layout stays fixed while you are in a level).

- Final title.
- Which wiki levels and entities come first (check each page's license box and author).
- First creature design.
- Simple puzzle types (fuses, keycards, codes on walls?).
- Whether sanity hallucinations can hurt you or only scare you.

## 20. Expansion Levels (Levels 3–9)

Reworked 2026-11-20 to completely replace generic district reuse with bespoke architectural generators and custom district blueprints true to the Backrooms Wiki:

### Dedicated Bespoke Generators
- **Level 7 · Thalassophobia (`OceanLayout.cs`)**:
  - `LevelGenerator.Ocean` replaces standard room blocks.
  - Chunk (0,0) builds the canon concrete **Entrance Bunker** with a dry raised floor, blast opening overlooking the void, and descending concrete stairs leading directly into the ocean.
  - Boundless ocean with dark water surface at y = 4.0m and an oppressive monolithic concrete sky at y = 28.0m.
  - Submerged architecture: sunken room ruins, massive concrete monolith pillars rising from seabed to ceiling, floating catwalk pontoons with ladders, and navigation beacon lamps.
  - Chunk (3,3) houses the **Deep Underwater Trench**: an aquatic chasm descending to -32m with an emergency marine marker glow and a submerged cave opening into Level 8.
- **Level 8 · Cave System (`CaveLayout.cs`)**:
  - `LevelGenerator.Caves` replaces 90-degree box rooms with organic subterranean karst cavern geometry.
  - 4x4 cellular grid per chunk with Perlin height variance generating rough limestone walls, low crawlways, and cathedral cavern roofs.
  - Inverted stalactite rock clusters suspended from roofs, stalagmite formations, echoing puddles, and abandoned explorer camps (lanterns, sleeping bags, supplies).
  - Pitch black (zero artificial fixtures; lit only by player flashlight).
  - Chunk (2,2) features a 6x6m fractured cavern **Sinkhole**: a deep pitfall dropping wanderers into the suburban streets of Level 9.
- **Level 9 · Darkened Suburbs (`SuburbsLayout.cs`)**:
  - `LevelGenerator.Suburbs` builds an authentic suburban neighborhood grid under a midnight sky (y = 24.0m).
  - Road grid: every 3 chunks forms an asphalt road with curbs, concrete sidewalks, and intersections.
  - Street infrastructure: tall sodium-vapor streetlight poles casting warm amber halos (16m range) through thick ground fog, trash cans, and cars (parked or tarped).
  - Residential lots: front lawns, concrete walkways, driveways, and full 3D houses with gabled pitched roofs, front porches, and warm porch lamps.
  - Chunk (0,0) arrival manhole from Level 8; chunk (2,1) contains an anomalous open front door leading out to Level 0 / 11.

### Bespoke District Architectures (`DistrictExpansion.cs`)
- **Level 3 · Electrical Station (`DistrictKind.ElectricalStation`)**:
  - 4.8m tall industrial hall with central runway and transformer bays.
  - Giant generator blocks, transformer units, ceiling conduit runs, hanging cable bundles, and high-voltage maintenance rooms.
- **Level 4 · Abandoned Office (`DistrictKind.OfficeCubicles`)**:
  - 2.8m corporate suites divided into 2x2 cubicle pods with low partition walls, desks, office chairs, and filing cabinets.
  - Central **Water Cooler Oasis** stocked with Almond Water clusters.
  - Executive conference suite with long tables, whiteboards, and hazardous exterior void windows (`WindowToWall`).
- **Level 5 · The Hotel (`DistrictKind.HotelSuites`)**:
  - Elegant 1920s hotel wing: central carpeted corridor with repeating guest suites on both sides, each with bed, nightstand, and vintage floor lamp.
  - Grand chandelier ballroom lounges with 5.2m ceilings, velvet sofas, armchairs, coffee tables, and warm vintage lighting.
  - 1 in 3 hotel districts forms a dark subterranean **Boiler Room** with rumbling iron boilers, steam pipes, and the iron descent hatch to Level 6.

| Level | Name | Generator | Key Architecture | Progression Links |
|---|---|---|---|---|
| **Level 3** | Electrical Station | Districts (`ElectricalStation`) | Transformer bays, generator blocks, conduit runs, heavy hum | In: Level 2 door. Out: Door to Level 4, Door to Level 2, Elevator to Level 5 |
| **Level 4** | Abandoned Office | Districts (`OfficeCubicles`) | Modular cubicle pods, conference suites, Almond Water coolers | In: Level 3 door. Out: Stairs to Level 5, Door to Level 3, Elevator to Level 1 |
| **Level 5** | The Hotel (Terror Hotel) | Districts (`HotelSuites`) | 1920s hotel halls, ballrooms, chandeliers, industrial boiler room | In: Level 4 stairs, Level 3 elevator. Out: Boiler room door to Level 6, Elevator to Level 4, Stairs to Level 3 |
| **Level 6** | Lights Out | Districts (`Corridors`) | Soundproof dark concrete, complete light absorption, smothered torch | In: Level 5 boiler room door. Out: Submerged stairs down to Level 7, Stairs to Level 5 |
| **Level 7** | Thalassophobia | Ocean (`OceanLayout`) | Entrance bunker, vast dark ocean, seabed ruins, underwater trench | In: Level 6 stairs. Out: Deep underwater cavern hatch to Level 8, Stairs up to Level 6 |
| **Level 8** | Cave System | Caves (`CaveLayout`) | Organic karst caverns, limestone stalactites, puddles, floor sinkhole | In: Level 7 underwater hatch. Out: Pitfall drop to Level 9, Crawlway to Level 7 |
| **Level 9** | Darkened Suburbs | Suburbs (`SuburbsLayout`) | Asphalt road grid, sodium streetlights, 3D houses, lawns, driveways | In: Level 8 floor drop. Out: Open house front door to Level 0 / 11, Storm drain hatch to Level 8 |

## Sources

- [Backrooms Wiki licensing guide](https://backrooms-wiki.wikidot.com/licensing-guide) · [Image use policy](https://backrooms-wiki.wikidot.com/image-use-policy)
- [A24 backs down from Backrooms copyright claim (TheWrap)](https://www.thewrap.com/creative-content/movies/a24-backrooms-copyright-claim-kane-parsons/) · [PC Gamer on Kane Parsons and A24](https://pcgamer.com/games/horror/kane-parsons-is-standing-up-for-an-artist-after-a24-attempted-to-copyright-strike-their-backrooms-art/)
- [Steam AI disclosure update (PC Gamer)](https://www.pcgamer.com/software/ai/steam-updates-ai-disclosure-form-to-specify-that-its-focused-on-ai-generated-content-that-is-consumed-by-players-not-efficiency-tools-used-behind-the-scenes/) · [GamingOnLinux](https://gamingonlinux.com/2026/01/valve-tweak-steam-ai-disclosure-form-for-developers-to-clarify-its-for-content-consumed-by-players)
- [PC Gamer: 150+ Backrooms games on Steam](https://pcgamer.com/gaming-industry/steam-week-in-review-4-backrooms-games-released-on-steam-last-week-joining-a-list-of-over-500-cash-ins-parodies-and-legitimate-contenders)
- [Roblox devforum: Backrooms generation determinism](https://devforum.roblox.com/t/world-generation-help/4662503) · [WFC in Unity (gamedev.net)](https://gamedev.net/blogs/entry/2275361-wave-function-collapse-for-procedural-generation-in-unity/)
- [LLM for Unity](https://dev.co/ai/frameworks/llmunity): considered for in-game AI text and rejected for now (2–5 GB, slow, Steam live-AI rules)
