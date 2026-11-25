namespace Backrooms.UI
{
    /// The level log's entries, written for this game in a field-guide voice (our own words, after the wiki's ideas).
    public static class LevelLog
    {
        public static (string danger, string text) Entry(string id)
        {
            switch (id)
            {
                case "0": return ("Low", "A sprawling, yellowed maze of inconsistent office backrooms. Sickly paper hangs over seamless damp, brown-beige carpet; "
                    + "buzzing fluorescent panels repeat overhead. Rooms can change after you leave them. The rare unlit Blackout Zones, red rooms, and holes "
                    + "are anomalous pockets rather than the whole level. Stairwells lead to floors above and below; an unlisted elevator is rumored to go farther.");
                case "0.2": return ("Low, then high", "It looks like the Lobby after a fresh renovation: new plaster, clean red carpet, everything straight. "
                    + "It doesn't stay that way. The longer you're inside, the faster it comes apart: ceiling tiles drop, walls fall, the floor gives way. "
                    + "Get back to the door you came in through.");
                case "0.3": return ("Moderate", "A frozen copy of the Lobby. Frost on bare walls, rusted ducts breathing cold air, and huge blocks of ice "
                    + "crowding the small rooms so there's barely space to squeeze past. Keep moving and keep warm.");
                case "1": return ("Moderate", "A huge concrete warehouse level of long halls, corridors and sectors, with crates, shelving and the odd furnished room. "
                    + "Every so often there's an electrical pulse and all the lights die at once, and things get bolder in the dark. "
                    + "The green exit signs keep glowing.");
                case "2": return ("Moderate", "Narrow maintenance tunnels of brick and concrete, every wall lined with pipes. Doors that won't open, "
                    + "boilers ticking away in dead ends, puddles of oil, and the air hot and close. Following the pipes is as good a plan as any.");
                case "3": return ("Survival difficulty 4", "Brick and dusty concrete, hot and loud. Tunnels shrink to crawlspaces and open into generator halls "
                    + "the size of a food court. The assembly lines are where the supplies are; the boiler rooms leak a scalding black sludge. "
                    + "Some rooms sit behind bars with no gate. If you hear someone crying, keep walking. If you find a statue of an angel, rest: nothing follows you in.");
                case "4": return ("Survival difficulty 1", "An empty office building. The furniture is gone; you can still see where it stood. Water coolers, vending machines "
                    + "and fountains all run almond water. Most windows are blacked out. If you find one that isn't, don't look through it for long, "
                    + "and don't touch it. People lived here once: M.E.G., a quiet cult, some collectors. Their rooms are still here.");
                case "5": return ("Survival difficulty 2", "An endless 1930s hotel: mahogany, gold, red carpet, rooms behind numbered doors that follow no order. Some doors open "
                    + "and close on their own. The copper-cage elevators run to floors 1-12 somewhere else in the hotel; never press 13. Find the Beverly Room's ballroom "
                    + "and its doors will be different every time. The boiler room is unbearably hot, and somewhere at its bottom are the stairs to Level 6.");
                case "6": return ("Survival difficulty unknown", "Tight hallways of something smooth and cold, and a darkness your light can't push back more than an arm's length. "
                    + "Silence, except for what you make, and sometimes what makes it back. Mark the walls with chalk (G). "
                    + "Listen for the sea: the stairs down to Level 7 are where the waves are loudest. Corridors behind you don't always stay open.");
                case "7": return ("Survival difficulty 4", "An endless, cold sea under a concrete sky. You arrive in a furnished room built sideways into that sky; "
                    + "its door drops you into the water. Below: the Daylight zone, the Twilight (bones), the Midnight (darkness, pressure), the Abyss (tar). "
                    + "Rest on the pillars. Fort Surrender's diving helmet doubles your breath. The way on is a lit cave on a drowned mountain, 88 m down.");
                case "8": return ("Extreme", "An endless network of natural caves: tunnels that climb, dip and cross over each other, huge chambers, crawls. Lamps reach half as far here and batteries drain twice as fast. "
                    + "Yellow M.E.G. posts mark the 9th Road, the safest way through. Off it are pits that flood, boiling tar with hands in it, and crawls that come out where you have already been. "
                    + "The road ends at a shaft; the fall comes out in Level 9.");
                case "9": return ("High", "An infinite suburb at midnight, wet and foggy. The houses are furnished, nobody lives in them and nothing in them has power. Stay off the streets: fog rolls down them and gets into your head, and lights in the sky search them. Cover stops the beams. One power line is live; follow it. The houses "
                    + "stand under the dim yellow haze of flickering sodium-vapor streetlights. A heavy ground fog clings to the lawns, concealing things that watch from the roads.");
                case "37": return ("Low", "An immense, empty complex of pristine white ceramic tile beneath lukewarm, gently rippling water. "
                    + "Rooms and corridors join without any clear purpose; pillars interrupt open pools, some expanses have no ledges, and the light falls "
                    + "at impossible angles. A few staircases descend straight into deep water. The level is believed to be devoid of life.");
                case "-1": return ("Moderate", "An endless data centre: row after row of humming server racks under cold light. You arrive in a vast flooded pool "
                    + "that drains a little every second. Find the pump control room to empty it and reach the door at the bottom.");
                case "19": return ("High", "The dark space above the Lobby, reached by stairs from its top floor. Old floorboards and rafters with a faint orange "
                    + "glow coming up between them, and in places nothing underfoot at all. Watch where you step.");
                case "188": return ("Deadly", "An enclosed courtyard thirty metres tall, white concrete walls lined with windows, most of them behind blinds. "
                    + "A paved path through living moss leads to a circle of pale birch in the middle. Shapes stand behind the windows at the edge of your sight "
                    + "and are gone when you look. Locked doors, and something beating on them. Leave the odd objects alone. If a record starts playing, leave. "
                    + "The way out is the window with light behind it.");
                case "9223372036854775807": return ("Low", "A cold concrete room contains an elevator and a spiral staircase that winds both upward and downward beyond sight. "
                    + "At the signed 64-bit limit, it is often called the True Final Level. The concrete is bare and brutalist; the central void is silent. "
                    + "No entities are known to exist here. The elevator may be your only reliable way back; stories of reaching the staircase's end remain unconfirmed.");
                case "fun": return ("Deadly", "A party that never ends: balloons, streamers, cake on every table, music always one room away. "
                    + "Nobody is here, and yet the balloons are closer every time you turn round, and there are smiles on walls that were bare. "
                    + "Don't eat the cake. Don't drink anything. Some of the exit signs are real; most are not.");
                default: return ("Unknown", "No notes yet.");
            }
        }

        public static string Credit(string id) => id switch
        {
            "-1" => "This level is original to this game.",
            "9223372036854775807" => "Based on the Backrooms community's \"Level 9223372036854775807 - An Endless Ending\" (CC BY-SA 3.0).",
            "188" => "Based on the Backrooms Wiki's Level 188 \"The Courtyard of Windows\" (original by 1000dumplings, rewritten by Kai4C), CC BY-SA 3.0. Level model made with an AI tool (Claude).",
            "fun" => "Based on the Backrooms Wiki's Level Fun =) (original page by 1000dumplings; now rewritten as Level 26 by Robert Goerman and MC_Crafter_24_7), CC BY-SA 3.0. Level model made with an AI tool (Claude).",
            "2" => "Based on the archived \"Old Pipe Dreams\" version of Level 2 on the Backrooms Wiki (CC BY-SA 3.0).",
            "0.3" => "Based on Level 0.3 \"The Icy Rooms\" by CursedSliver et al. on the Backrooms Wiki (CC BY-SA 3.0).",
            _ => $"Based on the Backrooms Wiki's page for Level {id} (CC BY-SA 3.0).",
        };
    }
}
