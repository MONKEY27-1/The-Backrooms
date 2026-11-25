# Story Mode — Implementation Plan (draft for review)

Source: `STORY.md` (story bible v1). Every line of dialogue, radio, tape and document is in `STORY_SCRIPT.md`.
Drafted 2026-11-22 from your Q&A answers. Nothing is built yet: review this and the script first.

## 1. What you decided

| Topic | Decision |
|---|---|
| Where | **Story mode only** (New Game > Story). Exploration, Survival, Hardcore, Endless and Custom stay story-free. |
| Players | Single-player only. |
| Era | **October 1974.** Slides, reel and early cassette tape, telex, punch cards, avocado and wood panel. The camcorder is ORD's experimental portable video unit. |
| Originality | **A truly new take: nothing from Kane Pixels or the A24 film.** Rules in §8c. |
| Human form | The presence looks like **you, unmasked**. Story levels have no mirrors, so your face is a surprise. |
| Extras | Refusal ending, rank tapes, continuity errors, stolen ORD radio. (Photo habit: not chosen.) |
| Protagonist | Never named. Badge **R-7741**, callsign **WREN**. |
| Dana and Marsh | Marsh: approved. Dana carrying the artifact first: assumed yes (the Refusal ending rests on it). |
| New levels | Approved: 11, 36, !, 13, Kestrel, the Conduit, the Antechamber, the Original Prison. |
| Victims | Real-world people included (invented names). **No exact number before Level 9.** |
| Release ending | A knowing choice. This overrides STORY.md section 8's "unknowingly". |
| Refusal ending | Needs all ten essential Dana tapes, every one on the main route. No side trip needed. |
| Suit | A distinct Story-mode suit with ORD's own silhouette (§8d). |
| Bosses | Mixed. Unavoidable: Level !, Window entities (188), the Conduit tram, The Guest (5), the Antechamber chase. Avoidable with care: Dullers, Jerry's room, Partygoers, The Thing. |
| Telling | Documents and logs, radio/signal text, voice recordings. **Subtitles with static/garble for now**; real voices later. Every line lives in `STORY_SCRIPT.md` with an ID, so it doubles as the recording list. |
| Characters | **Marsh** (your handler, callsign LANTERN, on the radio) and **Dana Okafor** (a colleague you trained with, callsign HERON, one of the bodies in Level 0). The protagonist is silent; their thoughts are written in the logbook. |
| Death after the artifact | Respawn at the checkpoint in a **re-rolled, near-identical copy**: same level and main route; props, lights, notes, people and a few side rooms differ. |
| Victims | Each death kills someone else. Traces show up in the next copy (a body, a missing-person poster, an ORD casualty card). Near the end, a list names every one, matching your real death count. |
| ORD | Scripted early (radio, outposts, alarms); **roaming hunter squads from Level -1 on**; the finale capture is scripted. |
| Prologue | A short **playable ORD facility**. |
| Ending choice | An **in-world action** at the sealing mechanism. |
| Extra levels | Non-chapter levels are **optional side trips** off the main path. |
| Features | ORD clearance doors, a signal scanner, growing artifact effects, set-piece chases and bosses. |
| Length | 6–12 hours. The estimates below add up to **~9 h 50 m** for a first run. |

**Level -1 (your call → mine): split it.** Level -1 keeps its place: ORD learns you carry the artifact and the hunt begins, which gives the hunters a reason to chase you through Levels 3–9. The "the presence has been manipulating you" reveal moves to **Level 9**, right before the finale, where it hits hardest. STORY.md needs only that one change.

**Story ideas I added (please confirm, see §9):**
1. **Dana carried the artifact first.** The other three Level 0 bodies are her own team, Team BRASS: each died when she died, and she carried them back. She worked it out and refused to come back. Her tapes run through the whole campaign; her real last tape is the Level 9 reveal.
2. **Marsh is the guaranteed victim.** Level -1 has one scripted, unavoidable death (ORD corners and kills you). You come back; Marsh dies in your place. From then on the "LANTERN" voice on the radio is the presence wearing his voice. You find his casualty report, timed to the second of your death, in Level 9.
3. **The radio was false long before that.** The real Marsh loses you in Level 0.3. Every helpful "LANTERN" call from Level 1 on is the presence; a Level -1 comms log gives it away, and it lies its way back into your trust. So Level -1 keeps a partial reveal, as STORY.md asks.
4. **New name:** Director Ruth Halloran, who signs the orders and leads the capture.
5. **The Array's power is the seal's power:** ORD's lights run on the thing keeping the presence shut in (follows from Level 3's chapter).

## 2. Campaign map (prologue + 24 chapters, ~10 h)

★ = new level to build · ◆ = original level only this game has · everything else already exists in the game.

| # | Act | Level | Story job | Threat / set piece | Est. |
|---|---|---|---|---|---|
| P | I | ◆★ **ORD Site Kestrel** (Nevada, 1974) | Briefing, suit-up, the Descent Frame. | None. Locked Team BRASS door teases clearance. | 15 m |
| 1 | I | Level 0 · Threshold | Bodies, Dana, the artifact, blackout, first "help". | Rare dark figures; first copy if you die. | 30 m |
| 2 | I | Level 0.2 · Remodeled Mess | ORD ran more experiments than you were told. | Remodel shifts (canon: no entities). | 20 m |
| 3 | I | Level 0.3 · The Icy Rooms | Cold survival; strange signals on the scanner. | Cold (canon: no entities). | 20 m |
| 4 | I | Level 1 · Habitable Zone | Explorers, escape attempts, ORD tracking them. | Hounds, Clump, Dullers (boss). | 30 m |
| 5 | II | ★ **Level 11 · The City That Never Sleeps** (wiki; older title "The Endless City") | A survivor community emptied by ORD "census teams". | Facelings, Skin-Stealer. | 25 m |
| 6 | II | ★ **Level 36 · Airport** (wiki) | Where escapees try to fly out; ORD's intercept desk. | Skin-Stealer passengers, scripted ORD sweep you watch. | 25 m |
| 7 | II | Level 2 · Pipe Dreams | Hidden ORD outpost Station Gauge: Program SPOOL experiments; Directive 9 (hide it from low grades). | Jerry's room (boss), alarm. | 35 m |
| 8 | II | ★ **Level ! · Run For Your Life!** (wiki) | Escape the outpost alarm; doors open ahead of you (the presence "helping"). | Full chase set piece. | 10 m |
| 9 | II | Level 19 · Attic Floorboards | First non-human architecture; boards rearrange into a path. First "Dana" radio call. | Canon: no entities. Pure dread. | 20 m |
| 10 | II | Level 37 · Sublimity | Tapes with voices that can't exist; the biomass seen under the water. | Canon: no entities. | 20 m |
| 11 | II | Level Fun =) | Too welcoming; not everything is human; ORD hid this level. | Partygoers (one-hit). | 25 m |
| 12 | III | ★ **Level 13 · The Infinite Apartments** (wiki) | Voices you know behind doors; a copy of your own Kestrel motel room (11). | Skin-Stealer mimic, the presence. | 20 m |
| 13 | III | Level 188 · Courtyard of Windows | Antecedent murals: the artifact is the origin of the Backrooms. Scanner now points one way. | Window entities (boss). | 25 m |
| 14 | III | Level -1 · The Data Center | ORD's biggest site: SPOOL files, your personnel file, the artifact file. ORD detects you; scripted death; Marsh dies. | **ORD hunters** begin. | 40 m |
| 15 | IV | ◆★ **The Conduit** | ORD's service tunnels and cargo tram from -1 to Level 3's power siphons. | Hunter set piece on the tram line. | 20 m |
| 16 | IV | Level 3 · Electrical Station | The grid draws power from the ancient seal. | Evolved Facelings, Smilers, hunters. | 25 m |
| 17 | IV | Level 4 · Abandoned Office | Evacuated forward base; window voids. "LANTERN" is back on the radio. | Dullers, lone Hound, hunters. | 20 m |
| 18 | IV | Level 5 · The Hotel | ORD's failed containment of an intelligent humanoid; spatial tears down. | **The Guest** (boss). | 25 m |
| 19 | IV | Level 6 · Lights Out | The presence speaks with no radio at all, in both voices. | Canon: no entities. Darkness. | 15 m |
| 20 | IV | Level 7 · Thalassophobia | Sunken dive stations; the water level lies. | The Thing (boss). | 25 m |
| 21 | IV | Level 8 · Cave System | ORD recon unit's gear and maps; the floor gives way. | Wretches, Deathmoths. | 20 m |
| 22 | IV | Level 9 · Darkened Suburbs | Final coordinates. **The reveal**: Marsh's casualty report, Dana's real last tape, the translation. | Searchlights, Smilers, hunters. | 30 m |
| 23 | V | ◆★ **The Antechamber** | The Antecedent city; the whole history; ORD's Lock Array. | The biomass hunts you (chase). | 25 m |
| 24 | V | ◆★ **The Original Prison** | Capture, the presence in human form, the choice, the ending. | Scripted capture, final sequence. | 25 m |

Total ≈ 9 h 50 m on a first run (guesses until playtested) (more on Hard, with side trips, or when dying a lot). Pacing rule: at least one quiet chapter after every boss.

### Side trips (optional, each reached by one hidden or extra exit, with a way back onto the path)

| Side trip | From | What's there |
|---|---|---|
| ★ The Hub (wiki) | Level 1 | ORD checkpoint graffiti, explorer notice board, two Dana tapes. |
| ★ Level 10 · Field of Wheat (wiki) | Level 9 | A breather before the finale; Antecedent standing stones; a victim trace. |
| Level 9223372036854775807 · An Endless Ending (exists) | Level 6 | Secret: ORD's oldest log; clue for a secret ending. |
| ◆★ **Kestrel (Copy)** | Level -1 (rare no-clip) | A drifted copy of the Prologue facility, empty, your chair still warm. Secret ending clue. |

Every wiki ★ level needs its page, title, authors and license box checked before building (CREDITS.md rule). A quick search on 2026-11-22 confirmed only Level 11's page. Levels 13, 36, !, 10 and The Hub come from secondary sources and memory, and are unconfirmed.

## 3. New levels to build

Built like the Levels 3–9 rework: one at a time, its own layout + runtime + props scripts, Blender hero kit, playtest between.

| Level | Kind | Key build work |
|---|---|---|
| ORD Site Kestrel | Original, hand-made, small | 1974 motel, rebuilt room by room: briefing in guest room 6, wired doors between units, covered walkway, linen store, the Descent Frame at the bottom of the drained swimming pool. A converted motor lodge, linear, ~6 rooms. |
| Level 11 · The City That Never Sleeps | Wiki | Streets, apartment blocks, shop fronts; mostly closed buildings with a few enterable. |
| Level 36 · Airport | Wiki | Terminals, gates, baggage halls, a departures board that updates. |
| Level ! · Run For Your Life! | Wiki | A linear hospital-style corridor run with doors, exit signs, obstacles and a pursuer; scripted, short. |
| Level 13 · Infinite Apartments | Wiki | Endless apartment corridors; procedural flat interiors; one fixed "your dorm" room. |
| The Conduit | Original | ORD service tunnels, cargo tram line, maintenance bays. |
| The Antechamber | Original | The Antecedent city: vast stone-and-bone halls, murals, the dormant half of the seal. |
| The Original Prison | Original, hand-made | Impossible bio-architecture, ORD's Lock Array, the sealing mechanism, the ending stage. |
| Side trips: The Hub, Level 10, Kestrel (Copy) | Wiki / original | Smaller builds, after the main path works. |

## 4. Monsters and bosses

**Already done:** Smiler, Hound, Skin-Stealer, Clump, Facelings (all kinds).
**Already planned (monster rework):** Deathmoths, Wretch, Partygoer; bosses The Thing (7), Window entities (188), Jerry's room (2), Dullers (1/4).

**New for the story (my proposals):**

| Creature | Where | Idea |
|---|---|---|
| **ORD Sweepers** (hunter squads) | -1, Conduit, 3–9, Prison | Human enemy. 2–4 in black hazmat with shoulder lamps and radios you can overhear (their chatter gives away their plan). Search patterns, flashlight sweeps, flank. Hide and run only. Caught = shot/tasered (death → copy). Scripted early as distant radio and footprints. |
| **The Pursuer** | Level ! | The chase entity: never seen clearly, a sound and a shape behind you. Our own design. |
| **The Guest** | Level 5 | Intelligent humanoid in a 1920s suit who walks the halls, knocks, speaks politely in subtitles. Answer the wrong door and it's in the room. The wiki's own Level 5 entity is Entity 18 (the Beast: sapient, shifts the hallways), so we either adapt it with credit or keep The Guest as ours. |
| **Searchlights** (name TBD) | Level 9 | The airborne patrols STORY.md asks for, as our own design: the wiki's Level 9 page doesn't seem to list the Neighborhood Watch (found only on other sites). Hide from the beams under porches and in houses. |
| **The Presence (biomass)** | Glimpses from 37 on; chase in Antechamber | A shifting, multicoloured mass that pours through gaps. It never kills before the Antechamber; there it hunts. |
| **The Presence (human form)** | Prison | Not a fight. A walking, talking finale (see §9 question 4). |
| **Echoes** (artifact effect) | Anywhere after 3 deaths | VHS ghosts of your earlier copies replaying how you died. Harmless, then (late) the presence starts using them to mislead you. |

Wretches get a story tie-in: ORD's Program SPOOL subjects who weren't killed became Wretches (fits the wiki idea that wretches are people the Backrooms wore down).

## 5. Story features

| Feature | How it works | Builds on |
|---|---|---|
| **Story state** | Per save slot: chapter, flags, documents found, tapes heard, death count, victims list, ending reached. | `SaveSystem.cs`, `GameSession.cs` |
| **Chapters and story exits** | Story mode replaces the hashed exit destinations with the chapter table: one "story exit" to the next chapter, plus side-trip exits where listed. Normal procedural levels, fixed story seed per chapter. | Exit system, `LevelCatalog` |
| **Objectives** | Chapter objectives on the VHS HUD ("Document remains 2/4"). Replaces the level goal in Story mode. | `Objectives.cs` (`Goal`, `GoalDoor`) |
| **Story placements** | Hand-placed beats (bodies, artifact, outposts, documents) put at deterministic spots in the procedural layout near the route, like landmarks. | Landmarks/boards system |
| **Documents** | Memos, files, letters, maps in the held-paper view; kept in the logbook's new "Files" tab. | `PaperView.cs`, logbook |
| **Logbook thoughts** | The silent protagonist's own handwritten entries, added at story beats. | Logbook |
| **Radio and tapes** | Lines play as subtitles with garble/static and a speaker tag (LANTERN, HERON, SWEEP, UNKNOWN). Tapes are pickups played on a portable recorder. Lines are read from the script file by ID. | `Captions.cs` |
| **Clearance doors** | ORD doors with grade readers. Grades 1 white, 2 green (you), 3 amber (Dana's card, Ch1), 4 red (Station Gauge, Ch7), 5 black (Level -1). Locked rooms hold the secrets and side trips. | Doors, bag items |
| **RL-9 Resonance Locator** | Handheld 1970s instrument (needle + click rate). Finds the artifact in Ch1. Afterwards it keeps pointing: to routes, doors, the story exit. In Level 9 you learn it was pointing at the prison all along. | Tools (like the camcorder) |
| **The copy (death loop)** | Before the artifact: a normal checkpoint. After: static, a short walk through **the Reel** (5–10 s of tape-noise corridor that changes each time), then the checkpoint in a re-rolled copy (same chapter seed for the layout, death count salts props/lights/notes/side rooms). | Checkpoints, level seeds |
| **Victim traces** | Death N places victim N's trace in the next copy near your route. Victims list in `STORY_SCRIPT.md` §V. | Story placements |
| **Growing artifact effects** | More deaths and later chapters = more wrongness: echoes, lights turning on ahead of you, doors appearing, the scanner speaking. | `EnvironmentalEvents.cs`, Director |
| **Set pieces** | Scripted sequences: blackout (Ch1), alarm escape (Ch7–8), scripted death (Ch14), tram chase (Ch15), biomass chase (Ch23), capture (Ch24). | Director, creatures |
| **Endings** | Two in-world ending sequences + credits; ending reached saved to the profile; achievements. | Achievements |
| **Chapter select** | After finishing once: replay any chapter (keeps documents found). | Menus |

## 6. Death, copies and victims, in detail

- **Before touching the artifact** (Prologue and early Ch1): a normal checkpoint, no victim.
- **After:** every death → +1 death count → the Reel → checkpoint in copy N.
- **What changes in a copy:** prop placement and choice, light colours and dead fixtures, which documents are on which desk (never which documents exist), people and creatures, 1–3 side rooms. **Never** the main route, story placements or exits (so the player isn't punished by re-learning).
- **Victim traces:** victim N's trace (poster, body, casualty card or news clipping) is placed in copy N within ~40 m of the checkpoint. Scripted victims (Marsh) are always included.
- **The list:** in Level 9 (ORD's casualty correlation sheet) and in the Prison, every victim is named, in order, with your real count. More than 30 deaths → the rest are "UNIDENTIFIED, N".
- **Difficulty:** it changes survival and monsters only, never the death rule. Permadeath isn't offered in Story.

## 7. Build phases

Each phase ends playable; you playtest before the next.

| Phase | What | Done when |
|---|---|---|
| **S0** | Review this plan and `STORY_SCRIPT.md`; answer §9. | You sign off on the script. |
| **S1** | Story framework: story state, chapter table, story exits, objectives, story placements, documents tab, radio/tape subtitles reading the script file, logbook thoughts. | A test chapter runs start to end with radio, a document, an objective and the story exit. |
| **S2** | Prologue (Kestrel) + Chapters 1 and 2 (slice 1, §8j): bodies, Dana, RL-9, artifact, blackout, the Reel, copies, victim traces, clearance doors. | Prologue → Level 0 → Level 0.2 playable; the scripted first death makes a copy with Reid's poster. |
| **S3** | Act I: chapters 3–4 on the existing levels; Dullers boss. Then the skeleton (§8j). | Prologue to end of Level 1. |
| **S4** | Act II: build Levels 11, 36, ! one at a time; chapters 7, 9–11 beats; Jerry's room, Partygoers; first Dana call. | To end of Level Fun. |
| **S5** | Act III: build Level 13; Level 188 beats + Window entities; Level -1 beats; **ORD Sweepers**; scripted death; Marsh. | To end of Level -1. |
| **S6** | Act IV: build The Conduit; chapters 16–22 beats; The Guest, The Thing, Wretches, Deathmoths, Searchlights; the Level 9 reveal. | To end of Level 9. |
| **S7** | Act V: build The Antechamber + The Original Prison; biomass + human form models (own Blender models); all three endings; credits. | Full campaign start to finish. |
| **S8** | Side trips, secret clues, chapter select, achievements, recorded voices when you have them, full-length playtest and pacing pass. | A 6–12 h run feels right. |

## 8. What I'd add beyond the bible (you choose)

- **(Chosen) Secret third ending, "Refusal"** (Dana's way): with the ten essential Dana tapes found, at the mechanism you can put the artifact down and walk away without carrying it to either side; you die for real, nobody else does, and because you put it down on the seal rather than on a carpet, the lock can finally close on it with nobody inside. The side trips hint at it but aren't needed.
- **(Chosen) ORD rank-up**: each clearance card also unlocks a short ORD orientation tape for that grade, so the player literally hears what each rank is told (great for "ranks hide things").
- **(Not chosen) Photograph everything**: the Level 0 "document the bodies" mechanic becomes a light campaign-wide habit: photographing story things adds them to the logbook (ties into the planned photo album).
- **(Chosen) Copies you can notice**: on purpose, small "continuity errors" between copies (a poster's text, a clock's time) that sharp players can spot; achievements for spotting them.
- **(Chosen) Hunter radio**: you can pick up a dropped ORD radio in -1 and listen to the Sweepers' channel for the rest of Act IV (warning of their routes, and a way to hear ORD lie about you).

## 8b. Where this collides with the game as built

| Problem | Plan |
|---|---|
| **Sequence breakers.** Levels link freely today: Level 4's lure windows (to 0-3), Level 5's elevators, Level 7's trapdoor straight to 9, no-clips, Level 9's front door to 0, the levels' own goal doors. | In Story mode every level change goes through one check: the next chapter, a listed side trip, or back to the checkpoint. |
| **Death counters.** The Stats page shows "Times caught", counted by `ProgressTracker.cs` in every mode. | Story deaths don't add to it, and no Story screen or save slot shows a count, until the Level 9 reveal. |
| **Missable tapes.** A missed essential tape would lock the Refusal ending. | Each sits in a space the chapter's objective makes you enter, never behind a clearance door. The RL-9 clicks differently nearby, and chapter select recovers a missed one. |
| **Level goals** (2 keycards on 4, the room key on 5, power on 3...) already open a guaranteed exit. | Story reuses them as the chapter's last step where they fit, instead of adding a second fetch quest. |
| **Wandering off.** Endless levels can separate you from story placements. | RL-9 always points at the next story placement; placements sit within ~150 m of arrival. |
| **Captions** can be switched off, and fade after 3.5 s. | Story subtitles get their own always-on lines, timed to their length. |
| **Papers** are one handwritten sheet. | Documents are kept to one sheet, plus a typed font for ORD memos. The victims sheet needs pages, like the logbook. |
| **Existing lore** (M.E.G. base and log on Level 4, notes on 7). | They stay: independent explorers, not ORD. |
| **"Layout never changes while you're in a level."** | Still true: rooms change only between deaths. |
| **GAME_PLAN says** no lore, no voice acting, restart at checkpoint. | Overridden for Story mode only. |

## 8c. Staying a new take

Not legal advice; these are working rules so nothing is copied, on purpose or by drift.

- **Never use** from Kane Pixels' series or the A24 film: its organisation, names, logo, people, dates, the machine or its name, its creature, footage layouts, reports, shots, music or cause of the Backrooms.
- **Shared by everyone, so fine:** yellow rooms, the hum, no-clipping (the 2019 post), and wiki levels and entities (credited, CC BY-SA).
- **Still similar in outline:** a secret organisation, protective suits, found video, and recovering bodies. STORY.md is built on these, so we stand apart in how they look and what they mean:
  - **1974, not the late 1980s-90s**; ORD is a small, secretive outfit that grew out of one property, the Kestrel Motor Lodge (approved): neon, guest-room labs, cables through the bathroom walls, everything improvised. Its Backrooms stations reuse motel furniture, key tags and the sailboat painting.
  - **Suits:** ORD's own design, §8d.
  - **The Descent Frame:** a brass-and-canvas harness over a trapdoor. No portal, magnets, or glowing gate.
  - **What's ours:** the folded presence, the Antecedents, copies paid for with strangers, a guide that wants to be carried.
- Each Story asset gets a check against this list before it goes in.

## 8d. The ORD Field Garment, Model 4 (Story suit)

Still clearly a protective suit, but nobody should mistake it for a yellow hazmat suit.

| Part | Design |
|---|---|
| **Silhouette** | A tall, soft **bell hood** flowing into wide quilted shoulders; a split, knee-length **apron** over straight trousers. Explorers call ORD staff "the bells". |
| **Hood and face** | Bone waxed canvas on a leather collar. One wide amber visor, mirrored outside, so nobody's face is seen (it pays off in the Prison). |
| **Breathing** | A brown Bakelite filter box on the chest, with one short cloth-covered hose on the left. |
| **Body** | Bone canvas with an **oxblood** quilted yoke; brass snaps; rubberised seams gone yellow with age. |
| **Hands and feet** | Oxblood rubber gauntlets with five fingers and canvas cuffs (what you see in first person); cork-soled overshoes, which give a soft, recognisable step. |
| **Rank** | A woven sleeve band: green, amber, red or black, matching the clearance cards. |
| **ORD mark** | A lopsided black flake of obsidian, one facet left blank. Stencilled, never glossy. |
| **Colours** | Bone, oxblood, Bakelite brown, amber, black. No yellow suits, no biohazard symbols. |

- **Variants:** WREN (clean, green band); Team BRASS (amber bands, worn at four stages of age); Sweepers (charcoal, longer apron, lamp on each shoulder, hood tied down); Halloran (no suit: a camel coat, the only person dressed for outside).
- **Build:** our own Blender model skinned to the same bones as the current suit, plus a matching arms FBX, so the hands, poses and ragdoll code keep working. Your `Player_Hazmat.fbx` and arms aren't touched, and other modes keep them.
- **Needed in S2:** arms, WREN for the Prologue, and the BRASS bodies. Sweepers come in S5.
- **First step:** a turntable render for your approval before rigging.

## 8e. What the player knows, and when

Players may suspect the truth before Level 9. Nothing confirms the full rules until then. Every line before C22 is held to this.

| Point | What they learn | Carried by |
|---|---|---|
| Level 0 to 1 | Something is wrong with death and returning. | Dana's tapes C01-C04, the bodies' ages, the first copies. |
| Level 11 to 36 | Deaths have consequences beyond the one who dies. | Dana's C06 tape, victim traces, ORD intercepts. |
| Station Gauge (2) | A strong hint: losses up, names cut out, cause blank. No mechanism. | C07-D03. |
| Level 13 to 188 | Enough to be suspicious. | Copies, traces, the murals, "it must be carried". |
| Level -1 | ORD knows more than it admits. | Object file with sections removed; "nobody shoots". |
| Level 9 | The complete rule and the exact personal cost. | Marsh's report, the correlation sheet, Dana's last tape. |

**Removed to keep to it:** the heartbeat matching at Gauge; "what the return is paid with" in Level -1; hunters saying Marsh died the same minute (it also ruined his "I got out" lie on Level 4); "if you drop it, somebody drops" on the stolen radio; a victim card for Marsh's replacement; "then nobody pays" in the Kestrel copy; and "nobody left to pay for it" in Dana's C09 tape.

## 8f. The rules underneath (for us; mostly unsaid)

| Question | Answer |
|---|---|
| Why does it make the Backrooms? | It loves making rooms and was once asked to. Nobody ever said stop. It now builds from what visitors remember, which is why the levels are human places remembered badly (the game's copy drift). |
| Why must it be carried to a door? | Folded, it has no place of its own: somebody has to *go* through, and it goes with them. |
| Why a human carrier? | Anything that wants to come back would do. Humans are who turned up. |
| Why do deaths cost a stranger? | It can make anything but a person, so it restores a carrier with someone else's life. Whether it understands what that means stays open. |
| Why can Dana put it down? | Its grip on a carrier is the carrier's wish to return. She stopped wishing, so it had nothing to hold. |
| Why didn't that end it? | She left it on a carpet, where WREN picked it up. At the seal, unheld and with the Array dead, the lock can close. |
| Containment or Refusal? | Containment closes the seal around a living carrier: it holds, but a way in remains (Copper arrives). Refusal closes it empty and for good, and costs WREN's life. |
| Why can the Array touch the seal but not remove the artifact? | It only taps the current of the lock. The artifact answers to wanting, not current. |
| Why does the Array fail then? | It has worn the lock thin. With the contents beside it, the draw reverses down every connected line; those on headsets die. This isn't a return and adds no victims. |
| Who were the Antecedents? | Deliberately unexplained: tall, seven-fingered or not, frightened too late. |
| Dana's cycle | One carrier, five deaths: Pike (0), Reyes (0.3), Lindqvist (1), a civilian in Ohio (19), then her own, with no return. |
| Marsh | An ordinary victim: he counts in the total and is on the list. |
| Time | Over a month inside for a day outside. Dana lived more than three weeks in fourteen hours; WREN leaves at dusk and comes out at dawn, having lived about as long. Marsh dies at 04:17 between the two. |

**Its personality, kept in every line:** a complicated combination. The loneliness is real and so is the manipulation. As itself it tells the truth about facts and feelings, and misleads about what happens afterwards ('go home'). It only lies outright in a borrowed voice. It is gentle, literal, patient, hurt by fear, and never gloats or threatens. No raised voice, no 'mortal' talk.

**Is the presence evil?** Left open. It lies and spends strangers, and it's lonely and may not understand death. STORY.md section 5 says it wants revenge on humanity: the script never states or denies that. Release still ends the world, so ambiguity about its motive isn't ambiguity about the danger.

## 8g. Act identities and WREN's arc

No chapters added. Each act gets its own kind of play:

| Act | Play | WREN |
|---|---|---|
| I (0 to 1) | Exploration and discovery. RL-9, photographs, almost no pursuit. | Professional. Writes reports. |
| II (11 to Fun) | Investigation. Clearance doors, intercepted radio, watching ORD unseen. | Suspicious, then ashamed. Stops writing reports (Gauge). |
| III (13 to -1) | Psychological horror. False voices, rooms that know you, documents you can't trust. | Frightened for the victims, worn down. |
| IV (Conduit to 9) | Survival. Hunters, stolen radio; fewer documents. | Exhausted; obeys because it's easier, and knows it. |
| V (Antechamber, Prison) | Ancient mystery. No documents after the walls; two things to touch. | Decides alone. |

**Dana's footsteps:** besides the tapes, her corrections ("D.O.") on ORD papers, chalk, rope, breaker tag and pencilled translation run from Level 0.3 to Level 8. Her jokes thin out, and the last tape says what they were for.

## 8h. One mystery at a time

The player's question is always **"What happens when I die?"** Every other mystery is a piece of it or supports it, and no more than two are at the front at once.

| Mystery | Opens | In front during | Closed |
|---|---|---|---|
| **What happens when I die?** (the spine) | Level 0.2, scripted first death | whole campaign | Level 9 |
| What happened to Dana's team? | Level 0 | Act I | Level 9 (part of the spine) |
| What is ORD doing to people? | Level 0.2 | Levels 1 to 2 | Level -1 |
| Who's on the radio? | Level 1 (tells) | Levels 19 to -1 | Level -1, proved in Level 9 |
| What is the artifact, and the presence? | Level 19 | Levels 188 to 6 | Antechamber |
| How do the Prison and the Backrooms connect? | Level 3 | Act V | Antechamber |

**What was it all for?** If somebody remembers one sentence, it's Dana's: *"Every time you trip it spends a stranger to pick you up."* It's spoken once, in house three of Level 9, and nothing earlier paraphrases it. **Don't reword it.**

## 8i. The Level 9 clue ledger

Level 9 explains nothing new. It's split over three houses with hiding in between, and each makes the player recognise something they already have.

| Clue | Already planted | Where it clicks |
|---|---|---|
| Victim traces | Reid's poster (0.2, guaranteed), one per death after | House two: the same posters, names underlined in your hand |
| Altered copies | The whole ceiling (0.2), the standing chair and 04:17 clocks (-1) | House one: 04:17 |
| Marsh | Two sugars and the cough (Kestrel), the radio tells, his sudden silence (-1), "Lantern's off the desk" (Conduit) | House one: the cup, still warm |
| Dana's warnings | Ten tapes, D.O. notes, the wrong knot | House three: knot, jokes, last tape |
| The helpful presence | Lights ahead (0), doors (!), boards (19), tram (Conduit), needle | Outside: the street lamps come on ahead of you |

**Why the scripted death in 0.2 exists:** without it a careful player would meet no copy and no victim until Level -1, and Level 9 would have nothing to connect. It isn't in STORY.md. Playtest check for S3 and S6: ask testers before Level 9 what they think death costs. Aim for "some guessed, none sure".

## 8j. Building it in pieces

| Gate | Prove this before going on |
|---|---|
| **Slice 1 (S1 + S2 + Level 0.2)** | Kestrel, Level 0, Level 0.2 including the first death, copy and Reid's poster. If this isn't frightening and clear, fix it before building any new level. |
| **Slice 2 (rest of S3)** | Act I start to finish. First real pacing numbers; redo the time estimates from them. |
| **Skeleton** | Existing levels in order with plain greybox stand-ins for the new ones, placeholder text only, so the whole story plays end to end, Level 9 included, before any of the remaining new levels get art. |
| **Then S4 to S8** | One level or monster at a time, art last, each playtested. |

**Cut order if it's too big** (story survives each cut): side trips, then Level ! merged into Gauge's escape, then the Conduit merged into Level 3, then Levels 11 and 36 merged (keep the C06 tape, the orange and Door B).
**Never cut:** Kestrel, 0, 0.2, 1, Gauge, 19, 188, -1, 9, Antechamber, Prison.

## 9. Still open

1. **Script sign-off**, then S1 can start.
2. **STORY.md section 5 (revenge):** leave the bible as it is, or change it to match the ambiguous presence?
