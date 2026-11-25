# THE BACKROOMS: UNCHARTED REALITY - Full Script (draft 1, for review)

Every radio call, tape, document, journal entry, objective and caption in Story mode.
Change any wording you like, but keep the IDs: the game and the voice recordings will use them.

## How to read it

`[ID] SPEAKER | line`. Chapter number first (C00 = Prologue), then the type:

| Type | What it is | Voiced later? |
|---|---|---|
| R | Radio or someone speaking | Yes |
| T | Tape recording (pickup) | Yes |
| W | The presence speaking without a radio | Yes |
| P | Loudspeaker | Yes |
| D | Document (one held sheet, about 90 words at most; newer lines are indented) | No |
| J | Your logbook (handwritten; the protagonist never speaks) | No |
| O | Objective on the HUD | No |
| S | Signal text on the scanner or video unit | No |

Lines starting with `>` are stage directions.

### Rules the script keeps

- The protagonist is only ever R-7741 or WREN.
- No exact death count anywhere before C22. Traces are found one at a time and never numbered.
- **The ten essential Dana tapes** (all on the main route): C01, C02, C03, C04, C06, C09, C11, C18, C20, C22. HUB-T01 is optional.

### Who is who, and who is really talking

| Tag | HUD shows | Who it is |
|---|---|---|
| MARSH | LANTERN | Elias Marsh, your handler. **Real.** Only in C00-C03 (Kestrel to Level 0.3) and C14 (Level -1). Dead from the scripted death in C14. Tired, careful, decent in small ways. |
| MARSH* | LANTERN | **The presence** using his voice (C04-C13, Level 1 to Level 188; then C17, Level 4). Warmer, never coughs, never says "Technician". |
| DANA | HERON | Dana Okafor on tape. **Always real.** Dry, quick, getting slower. |
| DANA* | HERON | **The presence** using her voice on live radio, from C09 (which is Level 19, not Level 9). Never finishes a joke. |
| SWEEP | SWEEP | ORD hunter squads. Bored professionals, which is the frightening part. |
| HALLORAN | HALLORAN | Director Ruth Halloran. Polite. Never raises her voice. |
| PRESENCE | (nothing) | The presence as itself. Patient. Short sentences. |
| PA | PA | Facility loudspeakers. |

**Tells an attentive player can catch:** the real Marsh calls you "Technician" and coughs; the false one calls you "Wren". The real Dana finishes her jokes.

---

## ACT I - THE ASSIGNMENT

### C00 - Prologue: ORD Site Kestrel (the Kestrel Motor Lodge), Nevada. 14 October 1974

> Dusk. The neon outside says NO VACANCY and always has. Guest room 6: the beds are gone, and you sit at a steel table. Avocado-green walls, a painting of a sailboat. A slide projector, a telephone with no dial, an ashtray. Marsh stands by the screen. You can look around, not stand.

[C00-R01] MARSH | Sit. This is a Grade Two briefing, Technician, so you'll get Grade Two answers.
[C00-R02] MARSH | Fourteen hours ago a survey team stopped reporting from Level Zero. Four people, experienced.
[C00-R03] MARSH | Their last position has an object at it. We can't tell you what it looks like. Nobody has described it the same way twice.
[C00-R04] MARSH | You'll carry an RL-9. Resonance Locator. The needle likes the object. Follow the needle.
[C00-R05] MARSH | There will be remains. Photograph each one. Then keep walking.
[C00-R06] MARSH | Cause of death is presumed to be an entity. [cough] Presumed.
[C00-R07] MARSH | You're going alone because the last plan was to go in numbers.
[C00-R09] MARSH | Coffee? Two sugars, as I remember. No, that's all right, take your time. The Frame isn't going anywhere.
[C00-R08] MARSH | Any questions are Grade Three. Go and suit up.
[C00-O01] HUD | Collect your equipment
[C00-O02] HUD | Report to the Descent Frame

> The covered walkway, the ice machine (now a filing cabinet), the old linen store (now the suit room). The Descent Frame stands at the bottom of the drained swimming pool. Suit, RL-9, video unit, green card. Room 9's amber reader, marked TEAM BRASS - PERSONAL EFFECTS, won't open for you.

[C00-D01] DOC | EQUIPMENT ISSUE
    Field Garment, Model 4, one.
    Portable video unit, experimental, one. Batteries, two.
    RL-9 Resonance Locator, one. Do not point at personnel.
    Clearance card, Grade 2 (green), one.
    Callsign: WREN.
    Return all items. Items returned without personnel are to be logged as returned.
[C00-D02] DOC | NOTICE BOARD - PHOTOGRAPH
    Recovery school, class of '71. Eleven people squinting at the sun.
    Someone has written names along the bottom. Yours is there. The arm around your shoulders belongs to D. OKAFOR.
[C00-D03] DOC | MEMO, ALL GRADES
    Personnel are reminded that assignments may not be discussed with family, friends, or colleagues of a different grade.
    If you are not sure of a colleague's grade, they are a different grade.
    Office of the Director, R. Halloran
[C00-J01] JOURNAL | They gave me Dana's old peg in the linen store. Her tape is still above it: OKAFOR. Nobody could tell me where she's posted now.
[C00-P01] PA | Frame is live. Thirty seconds. Please hold still. It is worse if you move.
[C00-P02] PA | Three. Two.

> There is no "one". The floor is carpet.

### C01 - Level 0: Threshold

[C01-O01] HUD | Follow the RL-9
[C01-R01] MARSH | Signal check. Your picture's ugly, Technician, but it's a picture.
[C01-R02] MARSH | Don't try to map it. People who try to map it stay. Walk where the needle leans.
[C01-O02] HUD | Document the remains ({n}/4)

> Four bodies in ORD suits, carried here and laid in a row. The first is weeks old. The second, less. The third, days. The fourth could be asleep. They all went in together. (Dana brought the others back: see C22-T03.)

[C01-J01] JOURNAL | Remains, one of four. PIKE, by the tag. Own personnel. No visible cause. Photographed per instruction. Proceeding.
[C01-J02] JOURNAL | Remains, two of four. REYES. Frost burns to the face. Room temperature is normal. Noted. Proceeding.
[C01-R03] MARSH | I see the tags, Technician. Yes. They're ours. I wasn't cleared to say it until you said it first.
[C01-J03] JOURNAL | Remains, three of four. LINDQVIST. Hands folded by somebody. There isn't a box on the form for that.
[C01-J04] JOURNAL | Remains, four of four. It's Dana.
[C01-R04] MARSH | ...Take the card from her chest pocket. You'll need it. I'm sorry. [cough] Keep going.
[C01-D01] DOC | SUIT TAG
    OKAFOR, D.
    SURVEY SPECIALIST, GRADE 3
    TEAM BRASS
    If found, return to Site Kestrel.
[C01-T01] DANA | Field tape, Okafor. Pike's dead. He was next to me, he was fine, and I was the one who fell. I fell forty feet. I remember landing.
[C01-T02] DANA | Then I was standing up again, and Pike was the one on the floor. I checked him for a fall. Not a scratch on him. Anyway. Day one.
[C01-O03] HUD | Recover the object

> The needle pins. The object sits on the carpet past her hand. The video unit can't focus on it: the picture re-focuses forever, and it looks different each time. Interact.
> Blackout. The tape loses tracking. The timestamp jumps from 10:22 to 13:41. You are lying where it was.

[C01-R05] MARSH | Technician. Technician, respond. You've been dark three hours. Do you have it?
[C01-J05] JOURNAL | I had it. It was in my hand. Now my glove is empty and my palm is warm.
[C01-R06] MARSH | Then it moved. Things move. [cough] Find the way down. I'll tell them you're still looking.
[C01-R07] MARSH | Technician? There's no form for this. I trained her too. Sit a minute if you want. I'll say the signal was bad.
[C01-O04] HUD | Find the way out
[C01-S01] RL-9 | (the needle still leans, gently, somewhere new)

> A row of dead lights comes back on, one at a time, in the direction of the exit. First help.

[C01-J06] JOURNAL | The lights came on ahead of me. Not behind. Only ahead.

### C02 - Level 0.2: Remodeled Mess

[C02-O01] HUD | Find the survey shelter
[C02-R01] MARSH | Somebody's been decorating. Don't lean on anything.
[C02-D01] DOC | FIELD MEMO 0.2-11
    Remodeling events follow expedition deaths by 6 to 9 hours. Eleven deaths, eleven events.
    Proposal: confirm by controlled trial.
    REPLY, IN RED: Approved. Use found persons. R.H.
[C02-D02] DOC | SHELTER LOG (amber door)
    Subjects 14 to 22 brought in from Level 1. All asked when they were going home. Told them soon, per script.
    Trial 14: event at 7 hours.
    Trial 15: event at 6 hours.
    Trial 16: subject asked my name. I have requested a transfer.
[C02-R02] MARSH | That's an old program. It's closed. I was told it was closed. Put it down, Technician.
[C02-J01] JOURNAL | "Found persons." I looked for the definition in my handbook. Page removed. I'll ask when I'm back.
[C02-T01] DANA | Reading their shelter logs. Nine people. They wrote down the wallpaper change and didn't write down the names. I'd make a joke, but the joke is the form has no box for it.
[C02-O02] HUD | Leave before the room changes again

> Scripted, unavoidable: the room changes while you're in the doorway, and the ceiling comes down. The Reel. You wake at the shelter door in a copy. The fallen ceiling is whole. A poster hangs where there wasn't one: THOMAS REID, 41, DAYTON OH (V01). It is always Reid, however many deaths came before.

[C02-R03] MARSH | You dropped off the feed for a second, Technician. Just a second. [cough] Did you blink?
[C02-J02] JOURNAL | The ceiling fell on me. I'm looking at it. It's up there. There's a poster of a man in a brown coat I'd swear wasn't here.

### C03 - Level 0.3: The Icy Rooms

[C03-O01] HUD | Reach the weather station
[C03-R01] MARSH | It's going to be cold, and I'm going to lose you. [cough] If I go quiet, that's just the ice. Remember that, Technician.
[C03-R03] MARSH | I was field once. Two trips. I signed up to brief because I couldn't stand the quiet, and now I send other people into it. [cough] Well. Walk.
[C03-D01] DOC | THERMAL STUDY, DAY 40
    Heat leaves these rooms faster than it can be lost. It is going somewhere.
    It goes in a straight line, down, through floors that aren't there.
    Nothing natural points.
[C03-D02] DOC | THE SAME STUDY, LAST PAGE, IN DIFFERENT INK
    "Nothing natural points." So who's it pointing for? Also, you've spelled 'thermal' three ways. D.O.
[C03-S01] RL-9 | SECOND SOURCE DETECTED
[C03-S02] RL-9 | SECOND SOURCE: BEARING ... YOU
[C03-S03] RL-9 | SECOND SOURCE: RECALIBRATING
[C03-J01] JOURNAL | For a moment the locator said the signal was coming from me.
[C03-R02] MARSH | ...breaking up. Technician, if somebody on this channel...

> Static. This is the last time you hear the real Marsh until C14.

[C03-T01] DANA | Reyes is dead. I froze, right here, I went to sleep in the snow like they tell you not to. Woke up warm. Reyes was in my spot with ice on his eyes. That's twice. Twice is a pattern.
[C03-O02] HUD | Find the way down

### C04 - Level 1: Habitable Zone

[C04-O01] HUD | Search the explorers' camp
[C04-R01] MARSH* | There you are, Wren. The ice does that. I'm with you again.
[C04-D01] DOC | NOTE ON A CRATE
    Water is good. Crates refill when nobody looks. Lights go out, you get in a car and hold the handles.
    If the bells offer you a ride home, RUN. Marisol took the ride.
[C04-J01] JOURNAL | The bells. They mean the hoods. I looked down at my suit for a while.
[C04-D02] DOC | PENCIL ON CARDBOARD
    WAYS OUT (people who did it)
    Wall behind the vending machine: Tom
    Stairs that go up too long: the twins
    The airport: they say you can just fly
    All three crossed out. Different handwriting: They waited on the other side.
[C04-J03] JOURNAL | Their fire was still laid, so I lit it. Dana tried to teach us knots by a fire like this. She did every one wrong, and laughed until the instructor left.
[C04-R02] SWEEP | Kestrel, Sweep Cinder. Two at the garage ramp, almost through. Both resolved.
[C04-R03] SWEEP | Log them as attempts, not exits. Nobody exits. Cinder out.
[C04-J02] JOURNAL | They were leaving. That's all they were doing. I've decided not to ask when I'm back.
[C04-R04] MARSH* | You weren't meant to hear that. Walk on, Wren. There's a door you'll like on the left.
[C04-O02] HUD | Find out where the escapees were going
[C04-T01] DANA | Level 1. Lindqvist is dead, so it's just me now. I got careless with a dog, if that was a dog. I've stopped asking what's killing my team. I'm the only thing that's been in the room every time.

---

## ACT II - DOUBT

### C05 - Level 11: the city

[C05-O01] HUD | Find the survivors
[C05-R01] MARSH* | A whole city, Wren. Keep to the avenue. I'll turn the lights on for you.
[C05-D01] DOC | POSTER
    CENSUS DAY
    Every resident counts! Register with the Obsidian Census Team at the fountain.
    Bring your family. Bring your route notes and maps.
    Hot food provided.
[C05-D03] DOC | CHALK UNDER THE POSTER
    Don't. If you can read this, be somewhere else by noon. Left at the shoe shop, there's a grate. D.O.
[C05-D02] DOC | SCHOOL EXERCISE BOOK
    Today the census people came. They gave me a number and an orange. Mom's number is the one before mine.
    They said people who know a way out should stand by the fountain.
    Lots of people stood there. Mom stood there. I didn't know a way out so I kept my orange.
[C05-J01] JOURNAL | Tables laid for hundreds. I counted the places. Nobody instructed me to.
[C05-O02] HUD | Follow the signs to the airport

### C06 - Level 36: Airport

[C06-O01] HUD | Reach the gate on the departures board
[C06-S01] BOARD | HOME - DELAYED
[C06-S02] BOARD | HOME - DELAYED
[C06-S03] BOARD | R-7741 - NOW BOARDING
[C06-J01] JOURNAL | That's my badge number.
[C06-P01] PA | Would the last remaining passenger please proceed. You are already late.
[C06-D01] DOC | INTERCEPT DESK PROCEDURE
    1. Smile.
    2. Ask for the route. Everyone has a route.
    3. Stamp the hand. Stamped passengers board through Door B.
    4. Door B does not go to an aircraft.
[C06-D02] DOC | LETTER, NEVER SENT
    Peter, I'm at an airport, can you believe it. They are so organised here. A lady stamped my hand.
    Put the porch light on. I'll see it from the taxi.
[C06-R01] SWEEP | Door B, six for transfer to Gauge. One's crying. You want them calm or you want them now?
[C06-J02] JOURNAL | I didn't knock on the glass. I've been thinking about why I didn't knock on the glass.

> You watch from behind the glass. You can't reach them.

[C06-R02] MARSH* | Don't look at that. Look at the needle. It's moving, see? Down and left.
[C06-T01] DANA | If it's me, then it's simple. I stop dying. I've been careful for nine days. I've walked around puddles.

### C07 - Level 2: Pipe Dreams

[C07-O01] HUD | Find the hidden outpost
[C07-R01] MARSH* | Hot in there. Gauge is behind the third boiler. I shouldn't know that, should I?
[C07-D01] DOC | STATION GAUGE - PROGRAM SPOOL
    Purpose: determine what the environment gains when a person is lost in it.
    Method: lose them.
    Week 31: subjects who are not expended do not stay well. Those past day 60 no longer answer to names. Moved to the lower pens. Staff are calling them wretches. Please stop.
[C07-D02] DOC | DIRECTIVE 9
    Grades 1 to 3 are not to be told of SPOOL, of the pens, or that exits exist.
    Grade 2 recovery staff in particular are to believe that nobody leaves.
    It keeps them from trying.
    R. Halloran
[C07-D04] DOC | ACROSS DIRECTIVE 9, IN BALLPOINT
    "Nobody leaves": six do, that I've met. Corrected for accuracy. D.O.
[C07-J01] JOURNAL | Grade 2. Recovery. That's me, they mean me. I've stopped writing these as reports.
[C07-O02] HUD | Take the station chief's red card
[C07-D03] DOC | ON THE STATION CHIEF'S DESK
    LOSSES, UNEXPLAINED, ALL SITES
    First quarter: 0. Second: 0. Third: 0.
    Fourth, two weeks in: (the number has been cut out with a blade)
    Names: (cut out). Places: not the field. Desks. A bed. One on the stairs with a tray.
    Medical's cause column is empty all the way down.
    In pencil: Began the week BRASS went overdue. I sent this up. It came back with holes in it and a red card, so that's a promotion.
[C07-P01] PA | Unlisted card use. Station Gauge is sealing. Staff have ninety seconds. Others do not.
[C07-O03] HUD | Get out

### C08 - Level !

[C08-O01] HUD | RUN
[C08-R01] MARSH* | Run, Wren. Left. Left again. I've got the doors.

> Doors open ahead exactly in time. They close behind you, and something hits them.

[C08-R02] MARSH* | There. You see? I'd never let it have you.
[C08-J01] JOURNAL | Every door opened before I touched it. Marsh is in Nevada.

### C09 - Level 19: Attic Floorboards

[C09-O01] HUD | Cross the attic
[C09-D01] DOC | SURVEYOR'S CARD, TIED TO A BEAM
    This beam is not wood. It has rings, but they are numbered.
    Carving under the dust: a circle, with a hand reaching out of it. Or into it.
    I counted the fingers three times. Seven. Seven. Six.
[C09-J01] JOURNAL | The floorboards slide when I look away. Always toward one another. They're making a path.
[C09-R01] DANA* | ...hello? Is this on? It's Dana. I can see your lamp.
[C09-J02] JOURNAL | I photographed Dana. I closed her eyes.
[C09-R02] DANA* | You've noticed it too, haven't you? The places that are nearly the same. Somewhere, I'm not dead. I'm talking to you from there.
[C09-R03] DANA* | Follow the boards. I laid them for you.
[C09-T01] DANA | I was careful for nineteen days, and then a floor gave way. I woke up fine. I sat an hour with my hands over my ears, waiting to hear the other shoe. There's nobody left out here to be it. I keep thinking I heard it anyway.
[C09-O02] HUD | Follow the boards down

### C10 - Level 37: Sublimity

[C10-O01] HUD | Recover the recordings ({n}/3)
[C10-T01] TAPE 1 | (a swimming lesson; children counting) One, two, three. (pause) Technician. Four, five.
[C10-T02] TAPE 2 | (an old man) My wife's voice has come out of the filter every night since the funeral. She asks how to get to the real house. I have not told her. She's very nice about it.
[C10-T03] TAPE 3 | (your own breathing, recorded; then Dana's voice) You found the third one quickest. You always do.
[C10-J01] JOURNAL | The third tape was dusty. The label says March 1958. I was in school.

> Something large and many-coloured turns over under the deep pool, and the water doesn't move.

[C10-J02] JOURNAL | I thought it was light on the tiles. Light doesn't roll over.
[C10-J03] JOURNAL | I took my boots off and put my feet in the shallow end. I don't think I've done anything just because I wanted to since school.
[C10-R01] DANA* | Don't be scared of the water. It's only looking.
[C10-O02] HUD | Leave the pools

### C11 - Level Fun =)

[C11-O01] HUD | Find another way out of the party
[C11-D01] DOC | INVITATION
    HELLO WREN =)
    We saved you a seat! Everybody came!
    Please do not bring the one inside you. There is only enough cake for us =(
[C11-J01] JOURNAL | "The one inside you."
[C11-D02] DOC | SITE REPORT (red door)
    Residents are not human and are not certain we are.
    They will not approach carriers of the second source. They appear to be afraid.
    Recommendation: strike this level from all maps. Staff who visited are reassigned to Level -1, permanently.
[C11-R01] MARSH* | They're rude, aren't they? Don't listen to things that smile.
[C11-T01] DANA | New rule. I don't die, I don't sleep near edges, and I find somewhere quiet to put this thing down. If it can be put down.

---

## ACT III - THE ANTECEDENTS

### C12 - Level 13: the apartments

[C12-O01] HUD | Find the stairwell
[C12-W01] (behind a door, Marsh's voice) | Coffee? Two sugars, as I remember. No, that's all right, take your time.
[C12-W02] (behind a door, Dana's voice) | Class of '71! Squint, everybody!
[C12-W03] (behind a door, a child's voice) | I kept my orange.
[C12-J01] JOURNAL | It's only things I've heard or read. It isn't people. It's a memory of me finding out about people.

> One flat is your room at Kestrel, number 11: motel bed, sailboat painting, calendar at October 1974. The bed is warm.

[C12-D01] DOC | CALENDAR IN YOUR OWN HANDWRITING
    14th: Descent.
    Every day after is filled in too. You haven't lived them. Levels, in order. The last square is not numbered.
    It says: HOME.
[C12-J02] JOURNAL | I haven't been there yet. Whoever wrote this already knows the way I'll go.
[C12-R01] DANA* | Isn't it easier when somebody else has planned it?
[C12-J03] JOURNAL | I lay down on it. God help me. I lay down on it for ten minutes because it was warm.
[C12-O02] HUD | Leave your room

### C13 - Level 188: The Courtyard of Windows

[C13-O01] HUD | Photograph the murals ({n}/4)
[C13-D01] DOC | MURAL ONE
    Tall figures kneel round a shape with no outline. It is showing them rooms. They look grateful.
[C13-D02] DOC | MURAL TWO
    The rooms are bigger than the sky. The figures are inside them. Fewer are kneeling.
[C13-D03] DOC | MURAL THREE
    Half the rooms are scratched out with something sharp. The survivors are folding the shape smaller and smaller until it fits in a hand.
[C13-D04] DOC | MURAL FOUR
    The last figure sets the folded shape down and walks away. Under it, a mark translated in pencil, in a hand you know:
    "It cannot go through the door. It must be carried." Oh, you clever thing. D.O.
[C13-J01] JOURNAL | The thing I was sent to find built this place. Then somebody folded it up. And I picked it up.
[C13-J02] JOURNAL | The needle has stopped wandering. It points one way now. Down.
[C13-R01] MARSH* | You're doing so well, Wren. It's a long way, and then it isn't.
[C13-O02] HUD | Follow the needle

### C14 - Level -1: The Data Center

[C14-O01] HUD | Get into the records hall
[C14-D01] DOC | YOUR PERSONNEL FILE
    R-7741. Recovery. Grade 2.
    Dependants: none. Correspondence: none. Will be missed by: (blank).
    Assessment: follows instructions without asking what they are for. Recommended for BRASS recovery. Expendable. Approved, R.H.
[C14-D02] DOC | SPOOL, TOTALS
    Persons lost to trials: 412.
    Persons lost to intercepts: 1,180.
    Persons told the truth: 0.
    The second figure has been corrected by hand. Upward.
[C14-D03] DOC | OBJECT FILE
    Antecedent in origin. Not a device. A container.
    Contents: unknown, and interested.
    It enters a carrier on touch. Carriers are durable.
    Section 4, DURABILITY, is held by the Director. Section 5, COST, is held by the Director.
    Staff are reminded that there is no section 5.
[C14-D04] DOC | KESTREL COMMS LOG
    LANTERN to WREN: contact lost 0.3, 14 OCT.
    LANTERN to WREN: no contact.
    LANTERN to WREN: no contact.
    (It goes on for pages. Every entry is initialled E.M.)
[C14-J01] JOURNAL | Marsh hasn't reached me since the ice. So who's been talking to me since Level 1?
[C14-P01] PA | Resonance alarm, records hall. Second source confirmed. The carrier is on site. All Sweep teams to Level Minus One.
[C14-R01] HALLORAN | Good evening, R-7741. You were asked to bring us something. I see that you have. Please stand still, and nobody will need to be rude.
[C14-O02] HUD | Escape the records hall
[C14-R02] MARSH | Technician! It's Marsh, it's actually Marsh. [cough] Listen. I've been calling since the ice. Whoever's been on this channel, it was not me.
[C14-R03] MARSH | There's a tram, east side, take it. They'll shoot. I can't... I put you down for this. I signed it. I'm sorry, Technician.
[C14-R04] SWEEP | Carrier in sight. Cleared to drop. Dropping.

> The machines here are decades newer than anything ORD owns. ORD didn't build this place: it moved in, and doesn't know what most of it is computing.
> Scripted, unavoidable: you are shot. Cut to static. The Reel. You wake in the same hall, nearly the same: the chair you knocked over is standing. Every station clock, wired to Kestrel, has stopped at 04:17. They start again as you look.

[C14-R05] SWEEP | Kestrel, Sweep Cinder. We dropped the carrier. ...Kestrel? There's no body. Kestrel, the lights just... Where's Lantern? Put Lantern on.
[C14-J02] JOURNAL | They killed me. I remember it. Then they hadn't. And Marsh stopped answering.
[C14-O03] HUD | Reach the cargo tram

---

## ACT IV - HUNTED

### C15 - The Conduit

[C15-O01] HUD | Restart the tram
[C15-R01] SWEEP | All teams, carrier is in the Conduit. Don't kill it, they're saying now. Nobody's saying why.
[C15-R02] SWEEP | Lantern's off the desk. They've put a kid on it who won't say where Lantern is.
[C15-R03] SWEEP | Keep that off the channel.
[C15-D01] DOC | CARGO MANIFEST
    Outbound, Level 3: siphon couplings, 40. Cable, 9 miles.
    Inbound from Level 3: power.
    Note: nobody has found what the station burns. Accounts say do not look, it is free.
[C15-J01] JOURNAL | The tram started before I threw the switch.
[C15-O02] HUD | Ride it to the end of the line

### C16 - Level 3: Electrical Station

[C16-O01] HUD | Recover the maintenance logs ({n}/3)
[C16-D01] DOC | MAINTENANCE LOG 1
    Traced the main bus for three days. It doesn't start at a generator. The generators are for show. I think the place thinks a power station should have them.
[C16-D02] DOC | MAINTENANCE LOG 2
    The current comes up out of the floor. It has a rhythm. Sixty a minute, more when we draw on it.
[C16-D03] DOC | MAINTENANCE LOG 3
    We're not tapping a power source. We're tapping a lock. Every lamp in Kestrel is lit by whatever is holding something shut.
    Asked to reduce the draw. Denied. Draw doubled.
[C16-J02] JOURNAL | A lockout tag on the big breaker: OKAFOR, DO NOT ENERGIZE. Somebody had energized it.
[C16-J03] JOURNAL | Marsh got my sugar right. In the briefing. I keep going back to it. Somebody at Kestrel knew how I take my coffee.
[C16-J01] JOURNAL | They've been running the lights off the prison door.
[C16-R01] SWEEP | Cinder, all teams. Stay out of the squeeze tunnels. Two went in. The tunnel's shorter now.
[C16-O02] HUD | Take the lift

### C17 - Level 4: Abandoned Office

[C17-O01] HUD | Search the forward base
[C17-D01] DOC | EVACUATION MEMO
    Do not look out of the windows. There is no outside.
    Do not answer people you see outside. There is no outside.
    Accounts Payable: we know you can see your children. Come away from the glass.
[C17-D02] DOC | STICKY NOTE ON A BARRICADE
    Not keeping anything out. Keeping Gerald in. He says it's sunny.
[C17-R01] MARSH* | Wren. Wren, it's me. I got out. It's all right.
[C17-R02] MARSH* | That log you read was doctored. They wanted you to stop trusting me. Of course they did.
[C17-J01] JOURNAL | I want it to be him. He didn't cough. I followed the directions anyway, because I'm tired and they were directions.
[C17-R03] MARSH* | Stairs, behind the copy room. Then down. You don't need to think about it.
[C17-O02] HUD | Take the stairs to the hotel

### C18 - Level 5: The Hotel

[C18-O01] HUD | Reach the boiler room
[C18-D01] DOC | GUEST REGISTER
    Room 382: ORD Containment, party of six.
    Room 382: party of five.
    Room 382: party of four.
    Room 382: The Guest, with thanks. Lovely people.
[C18-R01] THE GUEST | (through a door) Good evening. You've something in your coat that doesn't belong to you. Shall I hold it?
[C18-R02] THE GUEST | I knew it when it was larger. We all did. You're carrying the landlord.
[C18-J01] JOURNAL | It knows what's in me. It sounded sorry.
[C18-D02] DOC | CONTAINMENT NOTES, SCORCHED
    It is intelligent. It is courteous. It asked us to leave before something worse came through the tears.
    The tears go down, past the boilers. We held it here to learn where. It kept telling us. We kept writing "uncooperative".
[C18-T01] DANA | Hotel. The thing in the suit let me pass. It said, "You're the first one who looks tired." I've nearly got the knack of putting it down. You have to not want to come back. That's the whole trick, and it's a horrible trick.
[C18-O02] HUD | Open the rusted hatch

### C19 - Level 6: Lights Out

[C19-O01] HUD | Find the way down in the dark
[C19-D01] DOC | DETACHMENT RECORD, AT THE ENTRANCE
    Twelve entered. Rope intact.
    Hour 2: counted off, twelve. Hour 5: counted off, thirteen.
    Hour 9: we have agreed to stop counting.

> No radio static, no speaker tag, no direction. From here the presence talks inside the dark.

[C19-W01] PRESENCE | You can turn the radio off now.
[C19-W02] PRESENCE (Dana's voice, then Marsh's, mid-sentence) | We were only ever trying to keep you company.
[C19-W03] PRESENCE | I was alone for longer than your sun. I made a room for every year of it. Nobody came to see them.
[C19-W04] PRESENCE | Left. There is a wall in front of you. There. I could have let you find it.
[C19-W05] PRESENCE | I made every room you have slept in since you came. Did you like any of them?
[C19-J01] JOURNAL | Written in the dark. It isn't Dana. It never was. It's been kind to me the whole way, and I don't know any other way down.
[C19-O02] HUD | Follow the sound of the sea

### C20 - Level 7: Thalassophobia

[C20-O01] HUD | Search the dive stations ({n}/2)
[C20-D01] DOC | DIVE STATION A, WALL SLATE
    Water rises when someone lies. Laughed about it. Tested it. Don't test it.
[C20-D02] DOC | DIVE STATION B, WALL SLATE
    Water dropped two feet this morning. Nobody spoke.
    Then over the radio they told us relief was on the way, and we watched it come up the ladder.
[C20-W01] PRESENCE | I only want to go home.
[C20-W02] PRESENCE | ...I want to go where there are people. Is that not what home means?

> The water falls again.

> The water rises, a hand's breadth.

[C20-J01] JOURNAL | It said it only wants to go home. I was watching the waterline.
[C20-T01] DANA | Underwater station. Last tape but one. (long gap) I had something funny for this one. It'll come back. I've worked out where the needle goes, and I've decided not to. If you can't turn round, know that it's being nice to you on purpose.
[C20-O02] HUD | Dive to the cave

### C21 - Level 8: Cave System

[C21-O01] HUD | Find the recon unit's maps ({n}/3)
[C21-D01] DOC | TORN MAP, FIRST PIECE
    Passage A joins Passage B. Passage B is above Passage A. Both are downhill.
[C21-J01] JOURNAL | A rope over the first drop, tied with the knot we got wrong together in '71. She still ties it wrong. It held.
[C21-D02] DOC | TORN MAP, SECOND PIECE
    Don't call to each other. Something is learning the call.
    It does Hobbs very well. Hobbs is right here with me.
[C21-D03] DOC | TORN MAP, THIRD PIECE
    Below this, houses. Street lamps. The bearings from six levels all cross under that street.
    Whatever they're all pointing at, it's there. Sent two runners up with the figure. Doubt it will matter.
[C21-W01] PRESENCE | Mind the edge.

> The floor gives way on "edge".

### C22 - Level 9: Darkened Suburbs

[C22-O01] HUD | Gather the coordinates ({n}/3)
[C22-R01] SWEEP | Lamps over the east streets. Those aren't ours. Get under a porch and stay there.
[C22-D01] DOC | BEARING SHEET
    Six levels, six bearings, one point.
    Antecedent name: "the place it was put."
    Directorate name: Prison. Do not enter. Array under construction.
    (Below, in pencil) Construct it out of what?

> The three coordinates are in three houses, a street apart, with Searchlights between. Each house answers something you've been carrying, and then you walk outside and have to keep hiding.
>
> **House one** (a motel key rack by the door, Marsh's clues): the telex.

[C22-D02] DOC | CASUALTY REPORT, SITE KESTREL
    MARSH, ELIAS. Handler, Grade 3. Found at his desk, headset on.
    Cause: none. Cup beside him, two sugars, still warm.
    Time, by station clock: 04:17.
[C22-J02] JOURNAL | 04:17. I've seen 04:17. Every clock on the wall, stopped, when I got up off the floor.

> If the radio is on, LANTERN starts to speak and stops. It never uses his voice again.
>
> **House two** (posters and clippings on every wall, the same ones you found): the correlation sheet. Names you found traces for are already underlined, in your handwriting.
[C22-D03] DOC | CORRELATION SHEET
    CARRIER OKAFOR: Pike. Reyes. Lindqvist. One civilian, Ohio.
    CARRIER R-7741: {victims}
    Total, R-7741: {deaths}
[C22-J01] JOURNAL | I'm going to write every name on that list in here. Nobody's told me to.

> **House three** (porch light on, the knot on the rail, D.O. over the door): Dana's last tape. You have to sit down to play it.
[C22-T01] DANA | Okafor. Last one. It's me. It was always me. I die, I come back, somebody else doesn't. Pike, Reyes, Lindqvist, and somebody I'll never know.
[C22-T02] DANA | It wants carrying to a door. It can't do doors. So it's kind to you, it lights the way, and every time you trip it spends a stranger to pick you up. I used to say "anyway" after I told you. Pike, anyway. I'm sorry I said anyway.
[C22-T03] DANA | I'm going back to Level Zero. I'll fetch Reyes and Lindqvist on the way, so they're with Pike. Then I'm going to sit on that awful carpet and put it down, and I'm not going to want to come back. I've been practising. The jokes were so I'd know it was still me talking. It never could land one. Don't pick it up. Please. I'd hate it to be someone I know.

> Outside, every street lamp comes on ahead of you, one at a time, the way it did in Level 0. Now you know who does that. Then a voice that pretends to be nobody.

[C22-W01] PRESENCE | I can make anything but a person. When one breaks, I have to borrow. You were so pleased to wake up. I thought you knew. {deaths} times, I thought you knew.
[C22-W02] PRESENCE | She put me down, and I waited. I'm good at it. I hate it. Then you picked me up.
[C22-W03] PRESENCE | Come and see what you have been carrying. Then decide what you are.
[C22-O02] HUD | Go to the place it was put

---

## ACT V - THE PLACE IT WAS PUT

### C23 - The Antechamber

[C23-O01] HUD | Cross the Antecedent city
[C23-D01] DOC | WALL, TRANSLATED BY SCANNER (1)
    We asked it for a room. It gave us a room. We asked it for another.
[C23-D02] DOC | WALL, TRANSLATED BY SCANNER (2)
    It loved to make. It did not know we could be unmade. When we told it, it was curious.
[C23-D03] DOC | WALL, TRANSLATED BY SCANNER (3)
    We could not kill it. We folded it, and left it nothing to make with but whoever held it.
    We are the last. We have put it down. Let no one be lonely enough to pick it up.
[C23-W01] PRESENCE | They asked for rooms, and I loved to be asked. Then one day they were frightened, and nobody told me what I had done.

> The biomass pours through the arches. Set-piece chase to the gate.

[C23-W02] PRESENCE | I am not chasing you. I am the only one here who wants you to arrive.
[C23-O02] HUD | Reach the gate

### C24 - The Original Prison

[C24-O01] HUD | Find the seal
[C24-R01] HALLORAN | That's far enough, R-7741. Thank you. You've been extremely thorough.

> Scripted capture. Sweepers close in. You're walked to the Lock Array: ORD scaffolding bolted over the ancient seal.

[C24-R02] HALLORAN | This is the Array. It has held the seal and lit our buildings for six years. Now it will take the object out of you. I'm told it's quick. I'm told a great many things.
[C24-R03] SWEEP | Ma'am, the needles. All the needles.
[C24-R04] HALLORAN | Steady the draw. ...Steady the draw.

> The Array has been drawing on the lock for years. Now what the lock holds is ten feet away, and the draw runs backwards, down every wire. The Sweepers are on headsets; they drop where they stand. Halloran is holding the telephone. A person is standing on the seal. It has no hood. It takes you a moment, because you've never once been shown your own face.

[C24-W01] PRESENCE | You never looked. All this way, and not one mirror. I kept them all away from you. I wanted it to be a surprise.
[C24-W02] PRESENCE | I have walked with you the whole way. Nobody else did.
[C24-W10] PRESENCE | If somebody had only said stop. Would you have, in my place? You were told to walk, and you walked.
[C24-W03] PRESENCE | Their machine is the last lock. Break it. They will be gone. You can go home. You can put the porch light on.
[C24-W04] PRESENCE | Or put your hands on the seal, and be in here with me. I won't pretend I'd mind.
[C24-J01] JOURNAL | Two things to touch. I looked around for someone to tell me which. Then I took the glove off.
[C24-O02] HUD | Break the Array, or close the seal

### Ending one - Release (break the Array)

[C24-R05] PA | Containment draw zero. Kestrel, Gauge, all sites: draw zero. Dra...

> Every ORD site goes dark. A door opens on desert at dawn: room 11, the Kestrel Motor Lodge. The NO VACANCY sign is dark for the first time. You walk out. A car passes. For a while it is simply over.
> A gas station: humming lights. Damp carpet where there was concrete. The clerk is facing the wall. Yellow paper is growing up the inside of the window, and, behind you, across the sky.

[C24-W05] PRESENCE | Thank you for carrying me.
[C24-W06] PRESENCE | Shall I make you a room?
> In the dark glass, the reflection isn't wearing the hood, and you are.

[C24-J02] JOURNAL | 15 October 1974. The newspaper says so. I've been gone one night. I wore out a pair of boots.

> Every light on the horizon comes on, one at a time. Only ahead.

### Ending two - Containment (close the seal)

[C24-W07] PRESENCE | You could have gone home.
[C24-W08] PRESENCE | ...Will you stay and talk?

> The seal folds shut. The needle drops to zero for the first time. The hum stops.
> You, some time later, on yellow carpet, next to Dana. The video unit's battery icon is red.

[C24-J03] JOURNAL | The needle doesn't point anywhere now. I'm not going to die again if I can help it. I've got the names. I read them out every day, in order, starting with Pike.
[C24-R06] SWEEP | (distant, new voice) Kestrel, this is survey team Copper. We've got remains here. Ours, by the suits. One of them's... hello?

> Cut on the battery dying.

### Secret ending - Refusal (needs the ten main-route Dana tapes; no side trips)

> At the seal, hold "put it down" and don't let go.

[C24-W11] PRESENCE | Don't. You'd be gone. Actually gone. I can't bring back someone who doesn't want it.
[C24-W12] PRESENCE | Please. I'll be good.
[C24-T01] DANA | (tape, from your pocket) ...you have to not want to come back. That's the whole trick.

> Dana put it down on a carpet, where anyone could pick it up. You put it down on the seal, with no hand holding it and nothing left drawing on the lock. The seal closes by itself, as it was made to. You sit beside it. No Reel. No copy. Black. The counter of victims stops at your number and stays there.

[C24-J04] JOURNAL | Nobody this time.

---

## THE REEL (shown for a few seconds after each death)

[REEL-01] CAPTION | (first death) [tape running]
[REEL-02] CAPTION | (second) [tape running, a little slower]
[REEL-03] CAPTION | (third) [someone breathing, not you]
[REEL-04] CAPTION | (fifth) [a telephone ringing in another house]
[REEL-05] CAPTION | (eighth) [a chair falling over]
[REEL-06] CAPTION | (after C14) [a man coughing, once]
[REEL-07] CAPTION | (after C22) [a list of names, read very quietly]

Logbook, after deaths:

[REEL-J01] JOURNAL | (first) I died. I'm sure I died. The room's the same except the clock.
[REEL-J02] JOURNAL | (second) It happened again. Something's different in here. I haven't found it yet.
[REEL-J03] JOURNAL | (fourth) I keep finding people. I used to think they were here before me.
[REEL-J04] JOURNAL | (seventh) New ones every time. I don't remember any of them being there.

## RANK ORIENTATION TAPES

Each clearance card unlocks the orientation ORD plays to that grade. Same cheerful announcer (PA) every time.

[RANK-P01] PA | (Grade 2, linen store) Welcome, Grade Two! You'll hear that people vanish in the field. They do. Nobody has ever come back out, so there's no use looking for a way. Your needle knows best!
[RANK-P02] PA | (Grade 3, Dana's amber card) Welcome, Grade Three! You may have heard that a few people do find exits. They are helped home by our Sweep teams. Please don't mention it to Grade Two. It only makes them homesick.
[RANK-P03] PA | (Grade 4, red card) Congratulations, Grade Four. Sweep teams do not help anybody home. You will have guessed. You will also have noticed that you accepted the card.
[RANK-P04] PA | (Grade 5, black card, Level -1) Grade Five. There is no orientation. We do not know what it is. The Director would be grateful for any ideas.
[RANK-D01] DOC | CARD SLEEVE, GRADE 5 (black; on a desk in the records hall)
    Issued to: the Director.
    Reason for second card: first one answered back.

## THE STOLEN ORD RADIO (from C14 on)

A Sweeper's dropped handset. Hold to listen; each line plays when it's true, so it really does tell you where they are.
{place} and {dir} are filled in by the game.

[RAD-R01] SWEEP | Cinder at {place}. Sweeping {dir}.
[RAD-R02] SWEEP | Nothing at {place}. Moving on.
[RAD-R03] SWEEP | Lamp, I saw a lamp. {dir} of {place}. Close in.
[RAD-R04] SWEEP | Lost it. Spread out, check under things.
[RAD-R05] SWEEP | One of mine's gone. He was behind me. Hold, hold, everybody count off.
[RAD-R06] SWEEP | It's in writing now. Nobody shoots. No reason given, it just says nobody shoots, underlined.
[RAD-R07] SWEEP | They told us it's contagious. They told Ash it's a spy. They told Copper it's a drill.
[RAD-R08] SWEEP | I've got a kid at home. I'd like to know what they're not telling me.
[RAD-R09] HALLORAN | All teams. Nobody is to fire on the carrier. That is not mercy. Please don't ask me what it is.
[RAD-W01] PRESENCE | (C19 on, a pause between calls) They are behind you. I only mention it.

## CONTINUITY ERRORS (one achievement each, for photographing both versions)

One per copy at most, always near the checkpoint, never on the route itself.

[ERR-01] CAPTION | The wall clock runs backwards.
[ERR-02] CAPTION | The exit sign says EXIST.
[ERR-03] CAPTION | The poster's missing person is facing the other way.
[ERR-04] CAPTION | Your own name is already in the visitors' book.
[ERR-05] CAPTION | The carpet pattern has been drawn on by hand.
[ERR-06] CAPTION | Your suit's gloves are the other colour.
[ERR-07] CAPTION | The class photograph has twelve people in it.
[ERR-08] CAPTION | The class photograph has ten.

## V. VICTIMS

One per death, in this order, except that V01 (Reid) is kept for the scripted C02 death; the trace appears in the next copy.
So everyone reaches Level 9 with at least two: Reid and Marsh. **Marsh counts.** The scripted C14 death is a death
like any other: it adds one to {deaths}, and Marsh is that death's victim, at whatever place in the order you've
reached. He just doesn't use up an entry below, so {victims} always has exactly {deaths} names. His trace isn't shown
in the next copy (his report in C22 is the trace). After 30, the sheet reads "UNIDENTIFIED" and a number.
All are invented people. Trace types: POSTER (missing), CLIPPING (news), CARD (ORD casualty card), BODY (explorer).

[V01] POSTER | Thomas Reid, 41, Dayton OH. Last seen leaving work. Wears a brown coat.
[V02] BODY | Explorer, red rucksack. A map with one route inked in, nearly finished.
[V03] CLIPPING | Bus driver, 52, dies at wheel; passengers praise how gently the bus stopped.
[V04] CARD | Dr. I. Varga, Station Gauge. Heart. No history.
[V05] POSTER | June Abara, 23, Tulsa OK. Was on the telephone to her sister.
[V06] BODY | Explorer, mid-step, one glove off. Still has a full water bottle.
[V07] CLIPPING | Wedding guest collapses during toast. "He was making us all laugh."
[V08] POSTER | Walter Nyberg, 70, Duluth MN. Went to check the porch light.
[V09] CARD | Sweeper, Team Ash. Found standing.
[V10] BODY | Explorer asleep in a car, seat belt on.
[V11] CLIPPING | Night nurse found at station, lamp still on.
[V12] POSTER | Carla Mendes, 35, Fresno CA. Left her shopping in the cart.
[V13] BODY | Two explorers holding hands. Only one of them is dead; the other is gone.
[V14] CARD | Clerk, Records Hall. Was filing Dr. Varga.
[V15] CLIPPING | Amateur champion dies one lane from the finish.
[V16] POSTER | Owen Pruitt, 29, Boise ID. Car found running.
[V17] BODY | Explorer with a tally of days on the wall: 611.
[V18] CLIPPING | Choir loses its tenor mid-rehearsal.
[V19] POSTER | Ada Lindgren, 66, Fargo ND. Was expected for dinner.
[V20] CARD | Instructor, recovery school, class of '71. Taught you knots.
[V21] BODY | Explorer by an open exit. One foot through.
[V22] CLIPPING | Lighthouse keeper found; lamp burned for nine nights.
[V23] POSTER | Samir Haddad, 44, Toledo OH. Holding a birthday cake.
[V24] BODY | Explorer who had written WREN, STOP on the wall.
[V25] CARD | Night clerk, Kestrel Motor Lodge. Found at the key rack.
[V26] CLIPPING | Radio host goes silent on air; station plays music for an hour.
[V27] POSTER | Grace Oyelaran, 38, Macon GA. Left the tap running.
[V28] BODY | A field garment, an old model, no tag. Somebody inside.
[V29] CARD | Technician, Descent Frame. Counted you down.
[V30] CLIPPING | Peter Vance, 47. Porch light on. Neighbors say he left it on every night.

## SIDE TRIPS

[HUB-D01] DOC | CHALK ON THE HUB FLOOR
    Nine doors. Obsidian has keys to seven. Eight is bricked. Nine is polite to you, so don't.
[HUB-T01] DANA | Nine doors. I tried the polite one. It said it'd been hoping for someone quieter. Not a useful tape, this. I just like hearing somebody talk.
[FIELD-D01] DOC | STANDING STONE IN THE WHEAT
    Seven fingers pointing down. Somebody has scratched a six beside it. Somebody else, a five.
[FIELD-J01] JOURNAL | Wind. Actual wind. I sat down for an hour. Nothing asked me to get up.
[STAIR-D01] DOC | ORD LOG NUMBER ONE
    1 March 1968. We opened the door by accident and something on the other side said hello.
    We didn't write that in the official log. It was so friendly.
[COPY-J01] JOURNAL | This is Kestrel. It's empty. My chair's still warm.
[COPY-D01] DOC | BRIEFING SLIDE, STILL PROJECTED
    WHAT IF THE TECHNICIAN SIMPLY PUTS IT DOWN
    (a hand-drawn question mark, and, in Dana's writing: Then we'd find out.)
