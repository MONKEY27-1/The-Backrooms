using System;
using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    public enum PillarShape { Cross, Square, Mixed }
    public enum ExitKind { FlickerWall, Door, Stairs, Elevator, Hatch }   // Hatch: no door, just a trigger (stairs up into the dark)

    [Serializable]
    public struct PropWeight
    {
        public PropKind kind;
        public float weight;
        public PropWeight(PropKind kind, float weight) { this.kind = kind; this.weight = weight; }
    }

    [Serializable]
    public struct ExitLink
    {
        public ExitKind kind;
        public string target;   // level id
        public float weight;
        public ExitLink(ExitKind kind, string target, float weight) { this.kind = kind; this.target = target; this.weight = weight; }
    }

    /// Everything the maze generator needs to know about a level. Pure data, so generation stays deterministic.
    [Serializable]
    public sealed class MazeStyle
    {
        public float ceiling = 2.8f;
        [Tooltip("Chance a 2x2-chunk block is one giant pillar hall.")] public float hallChance = 0.12f;
        public PillarShape pillars = PillarShape.Mixed;
        [Tooltip("Wall walkers per chunk, from sparse to dense areas.")] public Vector2Int walkers = new Vector2Int(18, 50);
        public float roomChance = 0.25f, spiralChance = 0.08f, wiggle = 0.3f;
        public int maxWallLength = 10, solidBlocks = 2;
        [Tooltip("Chance of a block grid or pillar patch in a chunk.")] public float featureChance = 0.22f;
        [Tooltip("Chance of a field of black pits in a chunk.")] public float pitChance;
        [Tooltip("Chance of the Manila Room in a chunk.")] public float manilaChance;
        public float deadLights = 0.05f, blackoutChance = 0.08f, redChance;
        public float exitChance = 0.15f;
        [Tooltip("Props per open cell.")] public float propDensity = 0.06f;
        [Tooltip("Share of props placed wrong: sunk, floating, stretched, through walls, doubled.")] public float glitchChance = 0.12f;
        public List<PropWeight> props = new List<PropWeight>();
        public List<ExitLink> exits = new List<ExitLink>();
    }

    public enum LevelGenerator { Maze, Districts, Static, Stairwell, Ocean, Caves, Suburbs }

    /// The 64 m district archetypes the district generator can build.
    public enum DistrictKind
    {
        Maze, OpenExpanse, PillarHall, OfficeBlock, Corridors, PanelRows, NestedRooms, Atrium,
        Storage, Showroom, Supermarket, ClothingStore, MallConcourse,
        ServerHall,   // Level -1: rows of server racks
        Arches,       // Level 0: pale rooms opening through archways
        Tunnels,      // Level 2 / Level 1 corridors: two-metre tunnels
        ParkingHall,  // Level 1 Aquila sector
        GildHall,     // Level 1 Gild sector
        GothicHall,   // Level 1 Gothic sector
        Poolrooms,    // Level 37
        OuroborosHall, GardenHall, FabledHall,   // Level 1's other sectors (our own takes)
        ElectricalStation, // Level 3: generators, transformers, conduits, catwalks
        OfficeCubicles,    // Level 4: cubicles, conference rooms, break rooms, Almond water coolers
        HotelSuites,       // Level 5: 1920s hotel hallways, numbered suites, ballrooms, boiler room
        GeneratorHall, AssemblyLine, BoilerWorks,   // Level 3's other places (ElectricalStation = its maintenance tunnels)
        OpenPlan,          // Level 4: open-plan floors with the desks taken out (OfficeCubicles = its office floors)
        Ballroom,          // Level 5: the Beverly Room (HotelSuites = the Main Hall, BoilerWorks = its boiler room)
        DarkHalls,         // Level 6: tight, cold, lightless hallways
    }

    [Serializable]
    public struct DistrictWeight
    {
        public DistrictKind kind;
        public float weight;
        public DistrictWeight(DistrictKind kind, float weight) { this.kind = kind; this.weight = weight; }
    }

    /// Settings for the district generator (Level 0 and its variants).
    [Serializable]
    public sealed class DistrictStyle
    {
        public List<DistrictWeight> kinds = new List<DistrictWeight>();
        [Tooltip("Ceiling height range for ordinary rooms; corridors, atriums and stores override it.")]
        public Vector2 roomHeight = new Vector2(2.6f, 3.2f);
        [Tooltip("Walls are built this tall everywhere; anything above a room's ceiling is hidden.")]
        public float wallHeight = 6.4f;
        [Tooltip("Rooms whose lights are all dead.")] public float darkRoomChance = 0.02f;
        [Tooltip("Curiosity slots per district (the adaptive layer decides what, if anything, appears).")]
        public float curiosities = 0.4f;
        public float pitFieldChance = 0.02f, floodChance = 0.015f, furnitureHeapChance = 0.03f;
        public float manilaChance = 0.02f;
        [Tooltip("Ceilings never go higher than this (0 = no cap). The attic keeps everything low.")]
        public float ceilingCap;
        [Tooltip("Wooden rafters under every ceiling (the attic).")]
        public bool rafters;
        [Tooltip("Share of floor cells with light glowing up between the boards (the attic's Orange Glow).")]
        public float floorGlow;
        [Tooltip("Level 0's wiki areas: Blackout Zones and Red Rooms as regions, damp patches, the Peripheral Shift.")]
        public bool wikiZones;
        [Tooltip("Exit doors stand ajar with a lamp and the destination's number; chalk arrows lead to them.")] public bool markedExits;
    }

    /// One level: how it's generated, how it looks and sounds, and where its exits lead.
    [CreateAssetMenu(menuName = "Backrooms/Level Theme")]
    public sealed class LevelTheme : ScriptableObject
    {
        public string id = "0";
        public string displayName = "Level 0";
        [TextArea] public string credit;   // wiki page, authors, license (CC BY-SA 3.0)
        public LevelGenerator generator = LevelGenerator.Maze;
        [Tooltip("Static levels: the hand-made model to load instead of generating anything.")] public GameObject staticLevel;
        [Tooltip("Copy drift this level starts with (0 = faithful copy, 1 = barely a memory).")]
        [Range(0f, 1f)] public float baseDrift;
        public MazeStyle style = new MazeStyle();
        public DistrictStyle districts = new DistrictStyle();

        [Header("Materials")]
        public Material wall;
        public Material floor, ceiling;
        [Tooltip("District generator: rooms pick from these (falls back to wall / floor / ceiling).")]
        public Material[] wallpapers, carpets, ceilings;
        [Tooltip("Water surface height; when zoneWater is enabled, rooms opt into this water instead of every floor.")] public float waterHeight;
        [Tooltip("Only generate water in zones whose layout opts in, rather than covering every chunk.")] public bool zoneWater;

        [Header("Light and air")]
        public Color lightColor = new Color(1f, 0.94f, 0.78f);
        public float lightIntensity = 1.6f, lightRange = 6f;
        public Color ambient = new Color(0.30f, 0.27f, 0.17f);
        public Color fogColor = new Color(0.47f, 0.42f, 0.26f);
        public float fogDensity = 0.035f;
        public float humVolume = 0.12f;
    }
}
