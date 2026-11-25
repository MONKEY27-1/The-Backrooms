using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Backrooms.Game;
using Backrooms.Player;
using Backrooms.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.EditorTools
{
    /// Backrooms > Create Game Scene: (re)builds every level's textures, materials and settings, the level catalog,
    /// post-processing, render quality and the Game scene from code. Safe to run again; it overwrites what it made,
    /// including any hand edits to the level assets.
    public static class GameSceneSetup
    {
        const string Root = "Assets/_Game";
        const string ScenePath = Root + "/Scenes/Game.unity";
        const string Cc0 = Root + "/Art/Textures/CC0";
        const string Generated = Root + "/Art/Textures/Generated";
        const string License = "Licensed CC BY-SA 3.0. Adapted for this game.";

        [MenuItem("Backrooms/Create Game Scene")]
        public static void Create()
        {
            foreach (var dir in new[] { "Art/Textures/Generated", "Art/Materials", "Settings", "Scenes", "Data/Levels" })
                Directory.CreateDirectory(Path.Combine(Root, dir));
            SetupHdrp();
            var lit = Shader.Find("HDRP/Lit");
            var unlit = Shader.Find("HDRP/Unlit");

            // ---- CC0 photo sources (ambientCG) ----
            Texture2D Src(string id) => ProceduralTextures.Load($"{Cc0}/{id}/{id}_2K-JPG_Color.jpg");
            var paper = Src("Wallpaper001A");
            var stains = Src("Wallpaper002B");
            var carpet016 = Src("Carpet016");
            var carpet011 = Src("Carpet011");
            var carpet014 = Src("Carpet014");
            var ceilingPhoto = Src("OfficeCeiling001");
            var concretePhoto = Src("Concrete034");
            var nWall = Normal("Wallpaper001A");
            var n016 = Normal("Carpet016");
            var n011 = Normal("Carpet011");
            var n014 = Normal("Carpet014");
            var nCeiling = Normal("OfficeCeiling001");
            var nWood = Normal("WoodFloor051");
            var nTiles = Normal("Tiles107");
            var nConcrete = Normal("Concrete034");
            var woodColor = ColorMap("WoodFloor051");
            var mWall = Mask("Wallpaper001A");
            var m016 = Mask("Carpet016");
            var m011 = Mask("Carpet011");
            var m014 = Mask("Carpet014");
            var mCeil = Mask("OfficeCeiling001");
            var mConc = Mask("Concrete034");
            var mTile = Mask("Tiles107");
            var mWood = Mask("WoodFloor051");
            var tilesColor = ColorMap("Tiles107");

            // ---- Level 0: a subtle family of wallpapers, carpets and ceilings ----
            var cream = new Color(0.93f, 0.87f, 0.60f);
            var yellow = new Color(0.90f, 0.80f, 0.46f);
            // Hung wallpaper: strips, seams, uneven print, stains and drips, with its own relief and sheen (2 m per tile).
            Material Hung(string name, int pattern, Color tint, bool worn, float smooth, bool strips = true)
            {
                int seed = 17;
                foreach (char ch in name) seed = seed * 31 + ch;   // stable across runs (string.GetHashCode isn't promised to be)
                var maps = ProceduralTextures.WallpaperSet(pattern, tint, paper, worn, seed, strips);
                var mat = Lit(name, Save(name, maps.Albedo), SaveNormal(name + "_N", maps.Normal), Vector2.one * 0.5f, smooth, 1f, null, SaveLinear(name + "_M", maps.Mask));
                // Water damage on a much larger, odd-sized tile (6.3–7.9 m), so it never lines up with the 2 m pattern.
                float span = 6.3f + (seed & 0xFF) / 255f * 1.6f;
                mat.SetTexture("_DetailMap", SaveLinear(name + "_Damage", ProceduralTextures.WallDamage(worn, seed ^ 0x5EED)));
                mat.SetTextureScale("_DetailMap", Vector2.one / span);
                mat.SetFloat("_UVDetail", 0f);
                mat.SetFloat("_DetailAlbedoScale", 1f);
                mat.SetFloat("_DetailNormalScale", 0f);
                mat.SetFloat("_DetailSmoothnessScale", 1f);
                HDMaterial.ValidateMaterial(mat);
                EditorUtility.SetDirty(mat);
                return mat;
            }
            var wallpapers = new List<Material>();
            for (int pattern = 0; pattern < ProceduralTextures.PatternCount; pattern++)
                for (int tone = 0; tone < 2; tone++)
                    wallpapers.Add(Hung($"Wallpaper{pattern}{(tone == 0 ? "Cream" : "Yellow")}", pattern, tone == 0 ? cream : yellow, (pattern + tone) % 3 == 0, 0.22f));
            var carpets = new List<Material>
            {
                Lit("Carpet0", Save("Carpet0", ProceduralTextures.Tint(carpet016, new Color(0.80f, 0.73f, 0.50f), 1.1f)), n016, Vector2.one / 1.7f, 0.06f, 0.9f, null, m016),
                Lit("Carpet1", Save("Carpet1", ProceduralTextures.Tint(carpet016, new Color(0.78f, 0.66f, 0.40f), 1.2f, stains, 0.25f)), n016, Vector2.one / 1.7f, 0.06f, 0.9f, null, m016),
                Lit("Carpet2", Save("Carpet2", ProceduralTextures.Tint(carpet011, new Color(0.72f, 0.65f, 0.38f), 0.9f)), n011, Vector2.one / 1.5f, 0.05f, 1f, null, m011),
                Lit("Carpet3", Save("Carpet3", ProceduralTextures.Tint(carpet014, new Color(0.76f, 0.70f, 0.53f), 1.2f)), n014, Vector2.one / 1.2f, 0.06f, 0.8f, null, m014),
            };
            var ceilings = new List<Material>
            {
                Lit("Ceiling0", Save("Ceiling0", ProceduralTextures.Tint(ceilingPhoto, new Color(0.93f, 0.91f, 0.84f), 1f)), nCeiling, Vector2.one / 3f, 0.1f, 0.7f, null, mCeil),
                Lit("Ceiling1", Save("Ceiling1", ProceduralTextures.Tint(ceilingPhoto, new Color(0.88f, 0.85f, 0.74f), 1f, stains, 0.35f)), nCeiling, Vector2.one / 3f, 0.1f, 0.7f, null, mCeil),
            };

            // ---- Variants and other levels ----
            Material WallVariant(string name, int pattern, Color tint, bool worn, float smooth = 0.18f) =>
                Hung(name, pattern, tint, worn, smooth, name != "Plaster");   // plaster is painted, no paper seams
            Material CarpetVariant(string name, Color tint, float smooth) =>
                Lit(name, Save(name, ProceduralTextures.Tint(carpet016, tint, 1.1f)), n016, Vector2.one / 1.7f, smooth, 0.9f, null, m016);
            var mFrostWall = WallVariant("FrostWallpaper", 2, new Color(0.80f, 0.86f, 0.92f), false, 0.55f);
            var mFrostFloor = CarpetVariant("FrostCarpet", new Color(0.74f, 0.82f, 0.90f), 0.85f);
            var mRedWall = WallVariant("RedWallpaper", 2, new Color(0.64f, 0.12f, 0.10f), true);
            var mRedFloor = CarpetVariant("RedCarpet", new Color(0.50f, 0.08f, 0.06f), 0.3f);
            var mManila = WallVariant("ManilaWallpaper", 4, new Color(0.84f, 0.74f, 0.54f), false);
            var mPlaster = WallVariant("Plaster", 4, new Color(0.92f, 0.91f, 0.88f), false, 0.25f);
            var mDryRedCarpet = CarpetVariant("DryRedCarpet", new Color(0.62f, 0.20f, 0.14f), 0.05f);
            var mOfficeCarpet = CarpetVariant("OfficeCarpet", new Color(0.44f, 0.48f, 0.54f), 0.08f);
            var concreteTex = Save("Concrete", ProceduralTextures.Tint(concretePhoto, new Color(0.62f, 0.62f, 0.60f), 1.4f, stains, 0.3f));
            var concreteTiling = new Vector2(1f / 1.1f, 1f / 0.55f);
            var mConcreteWall = Lit("ConcreteWall", concreteTex, nConcrete, concreteTiling, 0.1f, 0.6f, new Color(0.95f, 0.95f, 0.93f), mConc);
            var mConcreteFloor = Lit("ConcreteFloor", concreteTex, nConcrete, concreteTiling, 0.35f, 0.6f, new Color(0.78f, 0.78f, 0.75f), mConc);
            var mConcreteCeiling = Lit("ConcreteCeiling", concreteTex, nConcrete, concreteTiling, 0.05f, 0.6f, new Color(0.85f, 0.85f, 0.83f), mConc);
            var mPipeWall = Lit("PipeWall", concreteTex, nConcrete, concreteTiling, 0.15f, 0.7f, new Color(0.72f, 0.66f, 0.56f), mConc);
            var mPipeFloor = Lit("PipeFloor", concreteTex, nConcrete, concreteTiling, 0.3f, 0.7f, new Color(0.55f, 0.50f, 0.44f), mConc);
            var mPipeCeiling = Lit("PipeCeiling", concreteTex, nConcrete, concreteTiling, 0.05f, 0.7f, new Color(0.6f, 0.56f, 0.5f), mConc);
            var mTileWall = Lit("TileWall", tilesColor, nTiles, Vector2.one, 0.75f, 1f, null, mTile);
            var mTileFloor = Lit("TileFloor", tilesColor, nTiles, Vector2.one, 0.9f, 1f, null, mTile);
            var mWoodFloor = Lit("WoodFloor", woodColor, nWood, Vector2.one / 1.8f, 0.4f, 1f, null, mWood);
            var mStoreTile = Lit("StoreTile", tilesColor, nTiles, Vector2.one / 1.5f, 0.5f, 1f, new Color(1f, 0.94f, 0.82f), mTile);
            var mStorageFloor = mConcreteFloor;
            Material Photo(string name, string id, Vector2 tiling, float smooth, Color? tint = null) =>
                Lit(name, ColorMap(id), Normal(id), tiling, smooth, 1f, tint, Mask(id));
            var mFreshPlaster = Photo("FreshPlaster", "PaintedPlaster017", Vector2.one / 2f, 0.35f);
            var mOldPlaster = Photo("OldPlaster", "Plaster007", Vector2.one / 2f, 0.3f, new Color(0.86f, 0.92f, 1f));
            var mHallConcrete = Photo("HallConcrete", "Concrete046", Vector2.one / 2.5f, 0.25f);
            var mHallFloor = Photo("HallFloor", "Concrete046", Vector2.one / 3f, 0.45f, new Color(0.8f, 0.8f, 0.78f));
            var mHallCeiling = Photo("HallCeiling", "Concrete046", Vector2.one / 3f, 0.15f, new Color(0.7f, 0.7f, 0.68f));
            var mGildBlue = Photo("GildBlue", "Concrete046", Vector2.one / 2.5f, 0.3f, new Color(0.55f, 0.68f, 0.85f));
            var mGildRed = Photo("GildRed", "Concrete046", Vector2.one / 2.5f, 0.3f, new Color(0.85f, 0.5f, 0.42f));
            var mGildGreen = Photo("GildGreen", "Concrete046", Vector2.one / 2.5f, 0.3f, new Color(0.55f, 0.8f, 0.55f));
            var mTunnelWall = Photo("TunnelWall", "Concrete042A", Vector2.one / 2f, 0.2f, new Color(0.55f, 0.53f, 0.5f));
            var mTunnelFloor = Photo("TunnelFloor", "Concrete042A", Vector2.one / 2f, 0.35f, new Color(0.42f, 0.4f, 0.38f));
            var mTunnelCeiling = Photo("TunnelCeiling", "Concrete042A", Vector2.one / 2f, 0.1f, new Color(0.58f, 0.55f, 0.52f));
            var mBrick = Photo("TunnelBrick", "Bricks105", Vector2.one / 1.2f, 0.15f, new Color(0.6f, 0.5f, 0.42f));
            var mPoolTile = Photo("PoolTile", "Tiles133A", Vector2.one / 1.2f, 0.85f);
            var mPoolTileFloor = Photo("PoolTileFloor", "Tiles133A", Vector2.one / 1.2f, 0.9f);
            var mPlanks = Photo("AtticPlanks", "Planks037A", Vector2.one / 1.6f, 0.25f, new Color(0.85f, 0.72f, 0.56f));
            var mStairWall = Photo("StaircaseStone", "Concrete042A", Vector2.one / 2.2f, 0.32f, new Color(0.50f, 0.48f, 0.45f));
            var mStairBrick = Photo("TarnishedBrick", "Bricks105", Vector2.one / 1.4f, 0.18f, new Color(0.42f, 0.36f, 0.31f));
            var mStairFloor = Photo("StairStoneFloor", "Concrete042A", Vector2.one / 2.5f, 0.42f, new Color(0.42f, 0.40f, 0.37f));
            // Natural rock has no UVs: the maps are projected from three sides, in metres.
            Material Rock(string name, float scale, float smooth, Color tint, string id = "Concrete042A")
            {
                var m = Photo(name, id, Vector2.one, smooth, tint);
                m.SetFloat("_UVBase", 5f);   // triplanar
                m.SetFloat("_TexWorldScale", scale);
                m.SetFloat("_NormalScale", 1.8f);
                HDMaterial.ValidateMaterial(m);
                return m;
            }
            var mCaveRock = Rock("CaveRock", 0.3f, 0.42f, new Color(0.95f, 0.87f, 0.77f));
            var mSeaRock = Rock("SeaRock", 0.22f, 0.3f, new Color(0.85f, 0.9f, 0.83f));
            var mSeaBone = Rock("SeaBone", 0.5f, 0.2f, new Color(0.8f, 0.76f, 0.66f), "Plaster007");
            // Level 9: painted boards, brick, a wet road, shingles, hedges.
            var mBoards = new[] { new Color(0.78f, 0.77f, 0.72f), new Color(0.42f, 0.5f, 0.58f), new Color(0.5f, 0.55f, 0.44f), new Color(0.74f, 0.66f, 0.44f) };
            var sidings = new Material[5];
            for (int i = 0; i < 4; i++) sidings[i] = Lit("Siding" + i, null, Normal("Planks037A"), Vector2.one / 1.8f, 0.25f, 1.4f, mBoards[i], Mask("Planks037A"));   // the boards' relief under paint
            sidings[4] = Photo("SuburbBrick", "Bricks105", Vector2.one / 1.2f, 0.2f, new Color(0.62f, 0.45f, 0.38f));
            var mAsphalt = Photo("WetAsphalt", "Concrete046", Vector2.one / 3.5f, 0.72f, new Color(0.13f, 0.13f, 0.14f));
            var mLawn = Photo("NightLawn", "Carpet016", Vector2.one / 2f, 0.12f, new Color(0.2f, 0.26f, 0.12f));
            var mSidewalk = Photo("Sidewalk", "Concrete034", Vector2.one / 2.2f, 0.4f, new Color(0.6f, 0.6f, 0.58f));
            var mBark = Rock("DeadBark", 0.9f, 0.15f, new Color(0.28f, 0.24f, 0.2f), "Planks037A");
            var mShingle = Rock("Shingles", 0.6f, 0.35f, new Color(0.22f, 0.21f, 0.22f), "Bricks105");
            var mHedge = Rock("HedgeLeaf", 1.2f, 0.2f, new Color(0.1f, 0.16f, 0.06f), "Carpet014");
            var mWheat = HdMat.DoubleSided(Flat("FieldGrass", Shader.Find("HDRP/Lit"), new Color(0.3f, 0.27f, 0.12f), 0.15f));
            var mWire = Flat("PowerCable", Shader.Find("HDRP/Lit"), new Color(0.02f, 0.02f, 0.02f), 0.4f);
            var mTubes = Flat("PlayTube", Shader.Find("HDRP/Lit"), new Color(0.82f, 0.84f, 0.84f), 0.55f);
            var mNight = Flat("NightSky", Shader.Find("HDRP/Lit"), new Color(0.004f, 0.005f, 0.008f), 0f);
            var mPane = HdMat.Emissive(new Color(0.9f, 0.95f, 1f), 60f, null, GetMaterial("TubePane", Shader.Find("HDRP/Lit")));
            foreach (var m in new[] { mWheat, mWire, mTubes, mNight, mPane }) EditorUtility.SetDirty(m);
            var mKelp = HdMat.DoubleSided(Flat("Kelp", Shader.Find("HDRP/Lit"), new Color(0.07f, 0.1f, 0.035f), 0.45f));
            var mFungus = HdMat.Emissive(new Color(0.25f, 1f, 0.7f), 5f, null, GetMaterial("CaveFungus", Shader.Find("HDRP/Lit")));
            EditorUtility.SetDirty(mKelp);
            EditorUtility.SetDirty(mFungus);
            var mStairCeiling = Photo("StairDarkCeiling", "Concrete042A", Vector2.one / 4f, 0.15f, new Color(0.12f, 0.115f, 0.11f));

            // ---- shared catalog materials ----
            var paints = new Material[Enum.GetValues(typeof(PropPaint)).Length];
            void Paint(PropPaint p, Color c, float smooth = 0.2f, float metal = 0f, bool glow = false) =>
                paints[(int)p] = Flat("Paint" + p, glow ? unlit : lit, c, smooth, metal);
            Paint(PropPaint.Metal, new Color(0.55f, 0.56f, 0.58f), 0.5f, 0.6f);
            Paint(PropPaint.Dark, new Color(0.07f, 0.07f, 0.07f), 0.3f);
            Paint(PropPaint.Wood, new Color(0.42f, 0.28f, 0.16f));
            Paint(PropPaint.Cardboard, new Color(0.62f, 0.47f, 0.30f), 0.05f);
            Paint(PropPaint.White, new Color(0.88f, 0.88f, 0.86f));
            Paint(PropPaint.Yellow, new Color(0.95f, 0.78f, 0.10f), 0.4f);
            Paint(PropPaint.Red, new Color(0.62f, 0.12f, 0.08f), 0.3f);
            Paint(PropPaint.Blue, new Color(0.15f, 0.30f, 0.62f), 0.3f);
            Paint(PropPaint.Green, new Color(0.10f, 0.55f, 0.20f), 0.4f);
            Paint(PropPaint.Glow, new Color(0.2f, 2.2f, 0.6f), glow: true);   // HDR so bloom picks it up
            Paint(PropPaint.Water, new Color(0.12f, 0.16f, 0.18f), 0.95f);
            Paint(PropPaint.Void, Color.black, glow: true);
            Paint(PropPaint.Fabric, new Color(0.55f, 0.52f, 0.38f), 0.1f);
            paints[(int)PropPaint.Glass] = MakeTransparent(Flat("PaintGlass", lit, new Color(0.75f, 0.85f, 0.9f, 0.22f), 0.95f));
            var staticTex = Save("Static", ProceduralTextures.Noise(), true);
            paints[(int)PropPaint.Static] = Flat("PaintStatic", unlit, new Color(1.4f, 1.4f, 1.4f), 0f, 0f, staticTex);
            paints[(int)PropPaint.Ice] = MakeTransparent(Flat("PaintIce", lit, new Color(0.78f, 0.9f, 0.98f, 0.55f), 0.95f));
            paints[(int)PropPaint.Rust] = Lit("PaintRust", ColorMap("Rust009"), Normal("Rust009"), Vector2.one, 0.35f, 1f, null, Mask("Rust009"));
            paints[(int)PropPaint.Oil] = Flat("PaintOil", lit, new Color(0.01f, 0.01f, 0.012f), 0.97f);
            Paint(PropPaint.Warm, new Color(2.6f, 1.7f, 0.8f), glow: true);       // lamps over exits, work lights
            Paint(PropPaint.NeonPink, new Color(3.2f, 0.35f, 1.7f), glow: true);
            Paint(PropPaint.NeonCyan, new Color(0.3f, 2.4f, 3.2f), glow: true);
            Paint(PropPaint.Leaf, new Color(0.15f, 0.3f, 0.09f), 0.25f);
            Paint(PropPaint.Tarp, new Color(0.2f, 0.25f, 0.31f), 0.2f);
            Paint(PropPaint.Concrete, new Color(0.5f, 0.5f, 0.48f), 0.1f);
            Paint(PropPaint.Orange, new Color(0.95f, 0.42f, 0.06f), 0.4f);   // rubber-duck beaks, pool floaties
            Paint(PropPaint.Brass, new Color(0.78f, 0.6f, 0.26f), 0.65f, 0.9f);   // Level 5: placards, elevator cages, chandeliers
            Paint(PropPaint.Mahogany, new Color(0.3f, 0.11f, 0.06f), 0.45f);      // Level 5: doors, beds, panelling

            var catalog = LoadOrCreate<LevelCatalog>(Root + "/Data/LevelCatalog.asset");
            catalog.propPaints = paints;
            catalog.panelLit = Flat("PanelLit", unlit, new Color(1f, 0.98f, 0.92f) * 2.8f);
            catalog.panelDead = Flat("PanelDead", unlit, new Color(0.36f, 0.35f, 0.31f));
            var waterNormals = SaveNormal("WaterRipples", ProceduralTextures.WaterRipples());
            var waterDetail = SaveLinear("WaterRippleDetail", ProceduralTextures.WaterRippleDetail());
            var caustics = SaveLinear("WaterCaustics", ProceduralTextures.WaterCaustics());
            // Two normal layers on different UV scales: a 15 m broad swell and fine 3 m ripples, each scrolling
            // at its own speed. HDRP takes the fine normals from _DetailMap.ag (R/B neutral), not _DetailNormalMap.
            // Crucially, HDRP samples every base-layer map (including _NormalMap) with layerTexCoord.base, which is
            // driven by _BaseColorMap_ST -- so the broad ripple scale and scroll must be set on _BaseColorMap, not
            // on _NormalMap. _LinkDetailsWithBase is off so the fine layer keeps its own independent transform.
            var waterMat = Flat("Water", lit, new Color(0.10f, 0.46f, 0.50f, 0.86f), 0.68f);
            waterMat.SetTexture("_NormalMap", waterNormals);
            waterMat.SetTextureScale("_BaseColorMap", new Vector2(0.065f, 0.065f));
            waterMat.SetTexture("_DetailMap", waterDetail);
            waterMat.SetTextureScale("_DetailMap", new Vector2(0.35f, 0.35f));
            waterMat.SetFloat("_LinkDetailsWithBase", 0f);
            waterMat.SetFloat("_NormalScale", 2.1f);
            waterMat.SetFloat("_DetailNormalScale", 1.15f);
            waterMat.SetFloat("_DetailAlbedoScale", 0f);
            waterMat.SetFloat("_DetailSmoothnessScale", 0f);
            if (waterMat.HasProperty("_Ior")) waterMat.SetFloat("_Ior", 1.33f);
            HDMaterial.ValidateMaterial(waterMat);
            catalog.water = MakeTransparent(waterMat);
            HdMat.DoubleSided(catalog.water);

            var deepMat = Flat("WaterDeep", lit, new Color(0.045f, 0.24f, 0.28f, 0.92f), 0.62f);
            deepMat.SetTexture("_NormalMap", waterNormals);
            deepMat.SetTextureScale("_BaseColorMap", new Vector2(0.05f, 0.05f));
            deepMat.SetTexture("_DetailMap", waterDetail);
            deepMat.SetTextureScale("_DetailMap", new Vector2(0.28f, 0.28f));
            deepMat.SetFloat("_LinkDetailsWithBase", 0f);
            deepMat.SetFloat("_NormalScale", 1.9f);
            deepMat.SetFloat("_DetailNormalScale", 1f);
            deepMat.SetFloat("_DetailAlbedoScale", 0f);
            deepMat.SetFloat("_DetailSmoothnessScale", 0f);
            if (deepMat.HasProperty("_Ior")) deepMat.SetFloat("_Ior", 1.33f);
            HDMaterial.ValidateMaterial(deepMat);
            catalog.waterDeep = MakeTransparent(deepMat);
            HdMat.DoubleSided(catalog.waterDeep);

            // The pool floor carries a slowly moving caustic layer. Deep basins get a dimmer copy so the water
            // reads as deeper instead of just darker glass.
            var causticMat = Flat("WaterCaustics", lit, new Color(0.42f, 0.82f, 0.95f, 0.55f), 0.25f, 0f, caustics);
            causticMat.SetTextureScale("_BaseColorMap", new Vector2(0.09f, 0.09f));
            causticMat.SetTexture("_EmissiveColorMap", caustics);
            causticMat.SetTextureScale("_EmissiveColorMap", new Vector2(0.09f, 0.09f));
            causticMat.SetColor("_EmissiveColor", new Color(0.12f, 0.45f, 0.55f));
            causticMat.SetColor("_EmissiveColorLDR", new Color(0.12f, 0.45f, 0.55f));
            causticMat.SetFloat("_UseEmissiveIntensity", 1f);
            causticMat.SetFloat("_EmissiveIntensity", 18f);
            catalog.waterCaustics = MakeTransparent(causticMat);
            HdMat.DoubleSided(catalog.waterCaustics);

            var deepCausticMat = Flat("WaterCausticsDeep", lit, new Color(0.18f, 0.58f, 0.72f, 0.32f), 0.25f, 0f, caustics);
            deepCausticMat.SetTextureScale("_BaseColorMap", new Vector2(0.09f, 0.09f));
            deepCausticMat.SetTexture("_EmissiveColorMap", caustics);
            deepCausticMat.SetTextureScale("_EmissiveColorMap", new Vector2(0.09f, 0.09f));
            deepCausticMat.SetColor("_EmissiveColor", new Color(0.05f, 0.22f, 0.30f));
            deepCausticMat.SetColor("_EmissiveColorLDR", new Color(0.05f, 0.22f, 0.30f));
            deepCausticMat.SetFloat("_UseEmissiveIntensity", 1f);
            deepCausticMat.SetFloat("_EmissiveIntensity", 6f);
            catalog.waterCausticsDeep = MakeTransparent(deepCausticMat);
            HdMat.DoubleSided(catalog.waterCausticsDeep);
            EditorUtility.SetDirty(catalog.water);
            catalog.redWall = mRedWall;
            catalog.redFloor = mRedFloor;
            catalog.manilaWall = mManila;
            catalog.trims = new[]
            {
                Flat("TrimWhite", lit, new Color(0.92f, 0.91f, 0.86f), 0.5f),
                Flat("TrimWood", lit, new Color(0.45f, 0.30f, 0.18f), 0.35f),
                Flat("TrimBeige", lit, new Color(0.82f, 0.76f, 0.60f), 0.4f),
            };
            catalog.woodFloor = mWoodFloor;
            catalog.tileFloor = mStoreTile;
            catalog.concreteFloor = mStorageFloor;

            // ---- levels ----
            var levels = new List<LevelTheme>();
            LevelTheme Level(string id, string name, string credit, Material wall, Material floor, Material ceiling, Action<LevelTheme> setup)
            {
                var t = LoadOrCreate<LevelTheme>($"{Root}/Data/Levels/Level_{id}.asset");
                t.id = id;
                t.displayName = name;
                t.credit = credit;
                t.generator = LevelGenerator.Maze;
                t.style = new MazeStyle();
                t.districts = Level0Districts();
                t.wall = wall;
                t.floor = floor;
                t.ceiling = ceiling;
                t.wallpapers = new[] { wall };
                t.carpets = new[] { floor };
                t.ceilings = new[] { ceiling };
                t.waterHeight = 0f;
                t.baseDrift = 0f;
                t.lightColor = new Color(1f, 0.96f, 0.86f);
                t.lightIntensity = 1.3f;
                t.lightRange = 6.5f;
                t.ambient = new Color(0.54f, 0.51f, 0.39f);
                t.fogColor = new Color(0.80f, 0.76f, 0.60f);
                t.fogDensity = 0.01f;
                t.humVolume = 0.12f;
                setup(t);
                EditorUtility.SetDirty(t);
                levels.Add(t);
                return t;
            }

            var level0Credit = "Based on \"Level 0\" (and its Pillars, Holes, Blackout Zones and Red Rooms) from the Backrooms Wiki, "
                + "https://backrooms-wiki.wikidot.com/level-0 . Concept: 4chan post; adaptation u/Deveyerr; written by DivineAtlas, "
                + "Robert Goerman, DrAkimoto. Manila Room: https://backrooms-wiki.wikidot.com/manila-room by Br Miller and neptunium153 "
                + "(concept Tocharian17). Almond Water: https://backrooms-wiki.wikidot.com/object-1 by 1000dumplings, Natedagreat563, Poliacci. " + License;
            var derived = "An original variation of Level 0 for this game. " + level0Credit;

            Level("0", "Level 0 · Threshold", level0Credit, wallpapers[0], carpets[0], ceilings[0], t =>
            {
                t.generator = LevelGenerator.Districts;
                t.wallpapers = wallpapers.ToArray();
                t.carpets = carpets.ToArray();
                t.ceilings = ceilings.ToArray();
                t.districts.wikiZones = true;   // damp carpet, Arches, Blackout Zones, Red Rooms, the Peripheral Shift, Holes
                t.districts.kinds = Level0Districts().kinds;
                t.districts.pitFieldChance = 0.055f;
                t.districts.manilaChance = 0.045f;
                t.districts.curiosities = 0.72f;
                t.districts.darkRoomChance = 0.055f;
                t.districts.furnitureHeapChance = 0.04f;
                t.districts.floodChance = 0.018f;
                // The source describes ordinary sightlines, not persistent yellow fog.
                t.fogColor = new Color(0.80f, 0.76f, 0.60f);
                t.fogDensity = 0.015f;
                t.ambient = new Color(0.50f, 0.47f, 0.34f);
                var s = t.style;
                s.deadLights = 0.05f;
                s.blackoutChance = 0.1f;
                s.redChance = 0.015f;
                s.exitChance = 0.18f;
                s.propDensity = 0.018f;
                s.glitchChance = 0.12f;
                s.walkers = new Vector2Int(36, 82);
                s.maxWallLength = 6;
                s.wiggle = 0.52f;
                s.roomChance = 0.3f;
                s.spiralChance = 0.14f;
                s.exits = new List<ExitLink>
                {
                    new ExitLink(ExitKind.FlickerWall, "1", 3f), new ExitLink(ExitKind.Door, "0.2", 1f), new ExitLink(ExitKind.Door, "fun", 0.3f),   // a balloon on the handle
                    new ExitLink(ExitKind.Door, "0.3", 0.8f), new ExitLink(ExitKind.Elevator, "9223372036854775807", 0.08f), // an unconfirmed, very rare route
                };
                s.props = OfficeProps();
            });
            Level("0.2", "Level 0.2 · Remodeled Mess",
                "Based on \"Level 0.2 - Remodeled Mess\", https://backrooms-wiki.wikidot.com/level-0-2 , by PandaShark71004 (cited: RowanLater). " + License,
                mFreshPlaster, mDryRedCarpet, ceilings[0], t =>
                {
                    t.baseDrift = 0.15f;   // copy drift: this copy is already further from the original
                    t.generator = LevelGenerator.Districts;
                    t.districts.kinds = new List<DistrictWeight>
                    {
                        new DistrictWeight(DistrictKind.OfficeBlock, 3f), new DistrictWeight(DistrictKind.Corridors, 1.5f),
                        new DistrictWeight(DistrictKind.NestedRooms, 1f), new DistrictWeight(DistrictKind.OpenExpanse, 0.6f),
                    };
                    var s = t.style;
                    s.deadLights = 0.15f;
                    s.exitChance = 0.25f;
                    s.propDensity = 0.1f;
                    s.glitchChance = 0.2f;
                    s.exits = new List<ExitLink> { new ExitLink(ExitKind.Door, "0", 1f) };   // any door leads back to Level 0
                    s.props = new List<PropWeight>
                    {
                        new PropWeight(PropKind.FallenTile, 5), new PropWeight(PropKind.Ladder, 2), new PropWeight(PropKind.CardboardBox, 3),
                        new PropWeight(PropKind.Bucket, 2), new PropWeight(PropKind.HangingWires, 3), new PropWeight(PropKind.Outlet, 3),
                        new PropWeight(PropKind.WetFloorSign, 1), new PropWeight(PropKind.TilePile, 1.5f),
                    };
                    t.lightColor = new Color(1f, 0.98f, 0.92f);
                    t.ambient = new Color(0.36f, 0.36f, 0.34f);
                    t.fogColor = new Color(0.6f, 0.58f, 0.54f);
                    t.fogDensity = 0.02f;
                });
            Level("0.3", "Level 0.3 · The Icy Rooms",
                "Based on \"Level 0.3 - The Icy Rooms\", https://backrooms-wiki.wikidot.com/level-0-3 , by CursedSliver (thanks JamiMyst, CutTheBirch). " + License,
                mOldPlaster, mFrostFloor, ceilings[1], t =>
                {
                    t.baseDrift = 0.15f;
                    t.generator = LevelGenerator.Districts;
                    t.districts.kinds = new List<DistrictWeight>
                    {
                        new DistrictWeight(DistrictKind.NestedRooms, 3f), new DistrictWeight(DistrictKind.OfficeBlock, 1.5f), new DistrictWeight(DistrictKind.Corridors, 1f),
                    };
                    t.wallpapers = new[] { mOldPlaster, mFrostWall };
                    t.style.deadLights = 0.12f;
                    t.style.exits = new List<ExitLink> { new ExitLink(ExitKind.Door, "0", 1f), new ExitLink(ExitKind.FlickerWall, "1", 1f) };
                    t.style.props = new List<PropWeight> { new PropWeight(PropKind.FallenTile, 2), new PropWeight(PropKind.Vent, 3), new PropWeight(PropKind.CardboardBox, 1) };
                    t.style.propDensity = 0.015f;
                    t.lightColor = new Color(0.85f, 0.93f, 1f);
                    t.ambient = new Color(0.32f, 0.37f, 0.42f);
                    t.fogColor = new Color(0.72f, 0.8f, 0.88f);   // the frosty mist from the ducts
                    t.fogDensity = 0.035f;
                });
            Level("1", "Level 1 · Habitable Zone",
                "Based on \"Level 1 - Habitable Zone\", https://backrooms-wiki.wikidot.com/level-1 , by Praetor3005 and DivineAtlas "
                + "(concept EnderMitten; original article u/ThePizzaEater1000). " + License,
                mHallConcrete, mHallFloor, mHallCeiling, t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.wallpapers = new[] { mHallConcrete, mGildBlue, mGildRed, mGildGreen, mBrick };   // 4: Ouroboros' bare block
                    t.districts.kinds = new List<DistrictWeight>
                    {
                        new DistrictWeight(DistrictKind.ParkingHall, 3.5f), new DistrictWeight(DistrictKind.GildHall, 1f),
                        new DistrictWeight(DistrictKind.GothicHall, 0.8f), new DistrictWeight(DistrictKind.Tunnels, 2f),
                        new DistrictWeight(DistrictKind.OuroborosHall, 0.6f), new DistrictWeight(DistrictKind.GardenHall, 0.5f),
                        new DistrictWeight(DistrictKind.FabledHall, 0.5f),
                    };
                    t.districts.markedExits = true;
                    var s = t.style;
                    s.deadLights = 0.06f;
                    s.exitChance = 0.2f;
                    s.propDensity = 0.03f;
                    s.exits = new List<ExitLink> { new ExitLink(ExitKind.Stairs, "2", 3f), new ExitLink(ExitKind.Door, "0", 1f), new ExitLink(ExitKind.Elevator, "37", 0.5f), new ExitLink(ExitKind.Door, "fun", 0.2f) };
                    s.props = new List<PropWeight>
                    {
                        new PropWeight(PropKind.Crate, 4), new PropWeight(PropKind.Puddle, 4), new PropWeight(PropKind.Pallet, 1.5f),
                        new PropWeight(PropKind.AlmondWater, 1.5f), new PropWeight(PropKind.Battery, 1), new PropWeight(PropKind.Vent, 1),
                        new PropWeight(PropKind.CardboardBox, 2), new PropWeight(PropKind.Bucket, 1),
                    };
                    t.lightColor = new Color(0.9f, 0.95f, 1f);
                    t.lightIntensity = 1.8f;
                    t.lightRange = 8f;
                    t.ambient = new Color(0.22f, 0.23f, 0.24f);
                    t.fogColor = new Color(0.4f, 0.41f, 0.42f);
                    t.fogDensity = 0.035f;   // stuffy, humid haze
                });
            Level("2", "Level 2 · Pipe Dreams",
                "Based on \"Level 2 - Pipe Dreams\" (archived), https://backrooms-wiki.wikidot.com/archived:level-2 , original by u/M654z, rewritten by 1000dumplings. " + License,
                mTunnelWall, mTunnelFloor, mTunnelCeiling, t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.wallpapers = new[] { mTunnelWall, mBrick };
                    t.districts.kinds = new List<DistrictWeight> { new DistrictWeight(DistrictKind.Tunnels, 1f) };
                    t.districts.darkRoomChance = 0.1f;
                    t.districts.markedExits = true;   // exits stand ajar under a lamp, chalk arrows lead to them
                    var s = t.style;
                    s.deadLights = 0.3f;
                    s.exitChance = 0.35f;
                    s.propDensity = 0.03f;
                    s.exits = new List<ExitLink>
                    {
                        new ExitLink(ExitKind.Door, "3", 3f),
                        new ExitLink(ExitKind.Door, "1", 2f),
                        new ExitLink(ExitKind.Door, "37", 1f),
                        new ExitLink(ExitKind.FlickerWall, "0", 0.4f),
                    };
                    s.props = new List<PropWeight>
                    {
                        new PropWeight(PropKind.HangingWires, 2), new PropWeight(PropKind.Bucket, 1), new PropWeight(PropKind.CardboardBox, 1),
                        new PropWeight(PropKind.AlmondWater, 1), new PropWeight(PropKind.Ladder, 0.5f),
                    };
                    t.lightColor = new Color(1f, 0.72f, 0.45f);
                    t.lightIntensity = 1.1f;
                    t.lightRange = 6f;
                    t.ambient = new Color(0.08f, 0.065f, 0.05f);
                    t.fogColor = new Color(0.12f, 0.09f, 0.06f);
                    t.fogDensity = 0.04f;
                    t.humVolume = 0.2f;   // machinery
                });
            Level("37", "Level 37 · Sublimity (Poolrooms)",
                "Based on \"Level 37 - Sublimity\", https://backrooms-wiki.wikidot.com/level-37 , by egglord (critique Sariastuff). " + License,
                mPoolTile, mPoolTileFloor, mPoolTile, t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.districts.kinds = new List<DistrictWeight> { new DistrictWeight(DistrictKind.Poolrooms, 1f) };
                    t.districts.darkRoomChance = 0.18f;
                    t.districts.wallHeight = 7.5f;
                    var s = t.style;
                    s.deadLights = 0.08f;
                    s.blackoutChance = 0.12f;
                    s.exitChance = 0.08f;
                    s.propDensity = 0f;
                    s.exits = new List<ExitLink> { new ExitLink(ExitKind.FlickerWall, "0", 1f), new ExitLink(ExitKind.Door, "2", 0.5f) };
                    s.props = new List<PropWeight>();
                    t.waterHeight = 0.95f;
                    t.zoneWater = true;
                    t.lightColor = new Color(0.82f, 0.96f, 1f);
                    t.lightIntensity = 1.45f;
                    t.ambient = new Color(0.44f, 0.54f, 0.57f);
                    t.fogColor = new Color(0.68f, 0.82f, 0.85f);
                    t.fogDensity = 0.022f;
                    t.humVolume = 0.02f;   // sound dies strangely here
                });

            // Level 3: Electrical Station (rebuilt 2026-11-21 from the wiki page; layout in Level3Layout.cs)
            Level("3", "Level 3 · Electrical Station",
                "Based on \"Level 3 - Electrical Station\", https://backrooms-wiki.wikidot.com/level-3 , original by u/M654z, rewritten twice by Natedagreat563. " + License,
                mBrick, mStorageFloor, mTunnelCeiling, t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.wallpapers = new[] { mBrick, mTunnelWall, mHallConcrete, mFreshPlaster };   // brick, bare concrete, hall concrete, Sanctum plaster
                    t.districts.kinds = new List<DistrictWeight>
                    {
                        new DistrictWeight(DistrictKind.ElectricalStation, 5f),   // the maintenance tunnels and squeezes
                        new DistrictWeight(DistrictKind.GeneratorHall, 2f),
                        new DistrictWeight(DistrictKind.AssemblyLine, 1.5f),
                        new DistrictWeight(DistrictKind.BoilerWorks, 1.2f),
                    };
                    t.districts.darkRoomChance = 0.15f;
                    t.districts.markedExits = true;
                    t.districts.wallHeight = 8f;   // the Sanctums are nearly eight metres tall (walls stay inside one 8 m floor band)
                    var s = t.style;
                    s.deadLights = 0.25f;
                    s.blackoutChance = 0.12f;
                    s.exitChance = 0.25f;
                    s.propDensity = 0.02f;
                    s.glitchChance = 0.1f;
                    s.exits = new List<ExitLink>
                    {
                        new ExitLink(ExitKind.Door, "4", 2f),
                        new ExitLink(ExitKind.Door, "2", 1.5f),
                        new ExitLink(ExitKind.Elevator, "5", 0.8f),
                    };
                    s.props = new List<PropWeight>
                    {
                        new PropWeight(PropKind.HangingWires, 3), new PropWeight(PropKind.Battery, 1.5f), new PropWeight(PropKind.AlmondWater, 1),
                        new PropWeight(PropKind.CardboardBox, 1.5f), new PropWeight(PropKind.Bucket, 1), new PropWeight(PropKind.OddOutlet, 1),
                        new PropWeight(PropKind.Outlet, 1),
                    };
                    t.lightColor = new Color(1f, 0.82f, 0.58f);
                    t.lightIntensity = 1.3f;
                    t.lightRange = 6.5f;
                    t.ambient = new Color(0.07f, 0.06f, 0.05f);
                    t.fogColor = new Color(0.13f, 0.105f, 0.08f);
                    t.fogDensity = 0.04f;   // hot, dusty haze
                    t.humVolume = 0.25f;    // the whine of the station under the generators' drone
                });

            // Level 4: Abandoned Office (rebuilt 2026-11-21 from the wiki page; layout in Level4Layout.cs)
            Level("4", "Level 4 · Abandoned Office",
                "Based on \"Level 4 - Abandoned Office\", https://backrooms-wiki.wikidot.com/level-4 , original by u/M654zy, with later community rewrites. " + License,
                mFreshPlaster, mOfficeCarpet, ceilings[0], t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.wallpapers = new[] { mFreshPlaster, mPlaster };
                    t.carpets = new[] { mOfficeCarpet, carpets[3] };
                    t.districts.kinds = new List<DistrictWeight>
                    {
                        new DistrictWeight(DistrictKind.OfficeCubicles, 5f),   // office floors: corridors, offices, conference and break rooms
                        new DistrictWeight(DistrictKind.OpenPlan, 2f),
                        new DistrictWeight(DistrictKind.Corridors, 1f),
                    };
                    t.districts.markedExits = true;
                    t.districts.darkRoomChance = 0.04f;
                    var s = t.style;
                    s.deadLights = 0.05f;
                    s.blackoutChance = 0.02f;
                    s.exitChance = 0.22f;
                    s.propDensity = 0.012f;   // nearly empty: the furniture is gone
                    s.exits = new List<ExitLink>
                    {
                        new ExitLink(ExitKind.Stairs, "5", 3f),
                        new ExitLink(ExitKind.Stairs, "6", 1.2f),
                        new ExitLink(ExitKind.Elevator, "3", 1f),
                    };
                    s.props = new List<PropWeight>
                    {
                        new PropWeight(PropKind.CardboardBox, 1), new PropWeight(PropKind.TrashCan, 1), new PropWeight(PropKind.FallenTile, 0.6f),
                        new PropWeight(PropKind.AlmondWater, 0.8f), new PropWeight(PropKind.Battery, 0.6f), new PropWeight(PropKind.Outlet, 1),
                        new PropWeight(PropKind.LightSwitch, 1),
                    };
                    t.lightColor = new Color(0.96f, 0.98f, 1f);
                    t.lightIntensity = 1.4f;
                    t.lightRange = 7.5f;
                    t.ambient = new Color(0.36f, 0.38f, 0.4f);
                    t.fogColor = new Color(0.62f, 0.65f, 0.68f);
                    t.fogDensity = 0.012f;
                    t.humVolume = 0.07f;   // a quiet office building's hum
                });

            var mDamask = WallVariant("HotelDamask", 6, new Color(0.42f, 0.07f, 0.06f), false, 0.25f);   // deep red paper, gold damask
            var mMahogany = Lit("MahoganyPanel", woodColor, nWood, Vector2.one / 1.4f, 0.45f, 1f, new Color(0.52f, 0.28f, 0.2f), mWood);
            var mHotelCream = WallVariant("HotelCream", 0, new Color(0.86f, 0.8f, 0.66f), false, 0.22f);
            // Level 5: The Hotel (rebuilt 2026-11-21 from the wiki page; layout in Level5Layout.cs)
            Level("5", "Level 5 · The Hotel",
                "Based on \"Level 5 - Terror Hotel\", https://backrooms-wiki.wikidot.com/level-5 , original by Discord13, rewritten by Stretchsterz (Entity 18 by ratscrapz). " + License,
                mDamask, mDryRedCarpet, mFreshPlaster, t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.wallpapers = new[] { mDamask, mMahogany, mHotelCream, mBrick };   // gold trellis on red, mahogany panelling, cream stripes, the boiler room's brick
                    t.carpets = new[] { mDryRedCarpet, carpets[1] };
                    t.districts.kinds = new List<DistrictWeight>
                    {
                        new DistrictWeight(DistrictKind.HotelSuites, 6f),   // the Main Hall
                        new DistrictWeight(DistrictKind.Ballroom, 0.6f),    // the Beverly Room
                        new DistrictWeight(DistrictKind.BoilerWorks, 1.2f), // the Boiler Room
                    };
                    t.districts.markedExits = true;
                    t.districts.darkRoomChance = 0.05f;
                    t.districts.wallHeight = 6.4f;
                    var s = t.style;
                    s.deadLights = 0.12f;
                    s.exitChance = 0.22f;
                    s.propDensity = 0.004f;
                    s.exits = new List<ExitLink>
                    {
                        new ExitLink(ExitKind.Stairs, "4", 1.5f),
                        new ExitLink(ExitKind.Elevator, "3", 1f),
                        new ExitLink(ExitKind.Door, "6", 0.5f),
                        new ExitLink(ExitKind.Door, "9", 0.4f),   // the dark wooden door
                    };
                    s.props = new List<PropWeight> { new PropWeight(PropKind.AlmondWater, 1), new PropWeight(PropKind.Battery, 0.6f) };
                    t.lightColor = new Color(1f, 0.8f, 0.55f);
                    t.lightIntensity = 1.0f;
                    t.lightRange = 6f;
                    t.ambient = new Color(0.15f, 0.11f, 0.08f);
                    t.fogColor = new Color(0.22f, 0.16f, 0.12f);
                    t.fogDensity = 0.022f;
                    t.humVolume = 0.03f;   // the buzz of old electric lamps
                });

            // Level 6: Lights Out (rebuilt 2026-11-21 from the wiki page; layout in Level6Layout.cs)
            Level("6", "Level 6 · Lights Out",
                "Based on \"Level 6 - Lights Out\", https://backrooms-wiki.wikidot.com/level-6 , by Bart0nius (originally with u/ThePizzaEater1000 and u/Mileisbored; earlier rewrites by Abe Venrick and 1000dumplings). " + License,
                mTunnelWall, mConcreteFloor, mStairCeiling, t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.wallpapers = new[] { mTunnelWall };
                    t.districts.kinds = new List<DistrictWeight> { new DistrictWeight(DistrictKind.DarkHalls, 1f) };
                    t.districts.darkRoomChance = 1f;
                    t.districts.wallHeight = 6.4f;
                    var s = t.style;
                    s.deadLights = 1f;
                    s.blackoutChance = 0f;
                    s.exitChance = 0.4f;   // in the dark, the waves must be within earshot more often than not
                    s.propDensity = 0.002f;
                    s.exits = new List<ExitLink>
                    {
                        new ExitLink(ExitKind.Stairs, "7", 3f),   // follow the waves
                        new ExitLink(ExitKind.Stairs, "5", 1f),
                    };
                    s.props = new List<PropWeight> { new PropWeight(PropKind.AlmondWater, 1), new PropWeight(PropKind.Battery, 1.5f) };
                    t.lightColor = new Color(0.01f, 0.01f, 0.01f);
                    t.lightIntensity = 0f;
                    t.lightRange = 1f;
                    t.ambient = new Color(0.004f, 0.004f, 0.005f);
                    t.fogColor = Color.black;
                    t.fogDensity = 0.12f;
                    t.humVolume = 0f;   // a silence you can hear
                });

            // Level 7: Thalassophobia (rebuilt 2026-11-22 from the wiki page; generator in OceanLayout.cs)
            Level("7", "Level 7 · Thalassophobia",
                "Based on \"Level 7 - Thalassophobia\", https://backrooms-wiki.wikidot.com/level-7 , originals by Bart0nius and M654z, rewritten twice by Bart0nius. " + License,
                wallpapers[0], mStairFloor, mHallCeiling, t =>
                {
                    t.generator = LevelGenerator.Ocean;
                    t.wallpapers = new[] { wallpapers[0] };            // the sideways room's paper
                    t.carpets = new[] { mSeaRock, carpets[0], mKelp, mSeaBone };   // the sea floor; the room's carpet (up the wall); weed; bones and coral
                    t.ceilings = new[] { mHallCeiling, ceilings[0] };   // the concrete sky; the room's tiles (the other wall)
                    t.waterHeight = 4.0f;
                    t.zoneWater = false;
                    t.style.exitChance = 0f;   // the ways out are placed: the cave (Level 8) and the trapdoor in the ring (Level 9)
                    t.lightColor = new Color(0.55f, 0.75f, 0.9f);
                    t.lightIntensity = 0.8f;
                    t.lightRange = 12f;
                    t.ambient = new Color(0.05f, 0.08f, 0.1f);   // a faint grey daylight from nowhere, near the surface
                    t.fogColor = new Color(0.05f, 0.07f, 0.09f);
                    t.fogDensity = 0.035f;   // a sea mist: the far water fades out before the world runs out
                    t.humVolume = 0f;
                });

            // Level 8: Cave System
            Level("8", "Level 8 · Cave System",
                "Based on \"Level 8 - Cave Systems\", https://backrooms-wiki.wikidot.com/level-8 , original by u/Bart0nius and u/avolded, rewritten by Stretchsterz. " + License,
                mCaveRock, mCaveRock, mCaveRock, t =>
                {
                    t.generator = LevelGenerator.Caves;
                    t.wallpapers = new[] { mCaveRock };
                    t.carpets = new[] { mCaveRock, mFungus };
                    t.style.ceiling = 4.5f;
                    t.style.deadLights = 1f;
                    t.lightColor = new Color(0.8f, 0.9f, 1f);
                    t.lightIntensity = 0.6f;   // markers, fungus, the odd lantern
                    t.lightRange = 6f;
                    t.style.exitChance = 0f;   // the ways out are placed: the pool (Level 7) and the road's end (Level 9)
                    t.ambient = new Color(0.01f, 0.01f, 0.015f);
                    t.fogColor = new Color(0.02f, 0.02f, 0.03f);
                    t.fogDensity = 0.05f;
                    t.humVolume = 0.02f;   // echoing drips and subterranean stillness
                });

            // Level 9: Darkened Suburbs
            Level("9", "Level 9 · Darkened Suburbs",
                "Based on \"Level 9 - The Suburbs\", https://backrooms-wiki.wikidot.com/level-9 , original by u/Bart0nius, rewritten by Stretchsterz. " + License,
                mFreshPlaster, mStorageFloor, mStairCeiling, t =>
                {
                    t.generator = LevelGenerator.Suburbs;
                    t.wallpapers = new[] { sidings[0], sidings[1], sidings[2], sidings[3], sidings[4], wallpapers[0], wallpapers[1], mFreshPlaster, mOldPlaster };
                    t.carpets = new[] { mLawn, mAsphalt, mSidewalk, carpets[0], mBark, mHedge, mShingle, mWheat, mWire, mTubes, mPane };
                    t.ceilings = new[] { mNight, mFreshPlaster };
                    t.style.exitChance = 0f;   // the way out is placed: the house the wires end at
                    t.style.ceiling = 24.0f;
                    t.lightColor = new Color(1f, 0.68f, 0.28f);
                    t.lightIntensity = 4.5f;
                    t.lightRange = 16f;
                    t.ambient = new Color(0.008f, 0.0095f, 0.014f);
                    t.fogColor = new Color(0.2f, 0.17f, 0.13f);
                    t.fogDensity = 0.045f;
                    t.humVolume = 0.03f;   // lonely midnight suburban wind
                });

            // Level -1: an original level for this game. Under Level 0: an endless AI data center.
            var mServerFloor = Lit("ServerFloor", tilesColor, nTiles, Vector2.one / 1.2f, 0.4f, 1f, new Color(0.62f, 0.64f, 0.68f));
            var mServerCeiling = Lit("ServerCeiling", concreteTex, nConcrete, concreteTiling, 0.05f, 0.6f, new Color(0.36f, 0.37f, 0.40f));
            Level("-1", "Level -1 · The Data Center",
                "An original level for this game (not from the Backrooms Wiki): an endless AI data center under Level 0. " + License,
                mPlaster, mServerFloor, mServerCeiling, t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.baseDrift = 0.1f;
                    t.districts.kinds = new List<DistrictWeight>
                    {
                        new DistrictWeight(DistrictKind.ServerHall, 6f), new DistrictWeight(DistrictKind.Corridors, 1.2f),
                        new DistrictWeight(DistrictKind.OfficeBlock, 0.6f),   // no warehouse shelving in a data center
                    };
                    t.districts.darkRoomChance = 0.05f;
                    var s = t.style;
                    s.deadLights = 0.08f;
                    s.exitChance = 0.06f;
                    s.propDensity = 0.01f;
                    s.exits = new List<ExitLink> { new ExitLink(ExitKind.Elevator, "0", 1f) };
                    s.props = new List<PropWeight> { new PropWeight(PropKind.CardboardBox, 2), new PropWeight(PropKind.HangingWires, 2), new PropWeight(PropKind.Ladder, 1), new PropWeight(PropKind.Vent, 1) };
                    t.lightColor = new Color(0.82f, 0.9f, 1f);
                    t.lightIntensity = 1.6f;
                    t.ambient = new Color(0.20f, 0.23f, 0.28f);
                    t.fogColor = new Color(0.18f, 0.21f, 0.26f);
                    t.fogDensity = 0.025f;
                    t.humVolume = 0.2f;   // the fans never stop
                });

            // Level 19: the attic you climb into from Level 0's top floor.
            var mAtticWood = Lit("AtticWood", woodColor, nWood, Vector2.one / 1.5f, 0.2f, 1f, new Color(0.78f, 0.64f, 0.48f));
            Level("19", "Level 19 · Attic Floorboards",
                "Based on \"Level 19 - Attic Floorboards\", https://backrooms-wiki.wikidot.com/level-19 , written by Boring Talking, "
                + "originally rewritten by 1000Dumplings, original author u/BendyBoi3393. " + License,
                mPlanks, mPlanks, mPlanks, t =>
                {
                    t.generator = LevelGenerator.Districts;
                    t.baseDrift = 0.15f;
                    t.districts.kinds = new List<DistrictWeight>
                    {
                        new DistrictWeight(DistrictKind.Maze, 3f), new DistrictWeight(DistrictKind.NestedRooms, 2f),
                        new DistrictWeight(DistrictKind.Storage, 0.4f), new DistrictWeight(DistrictKind.Corridors, 0.6f),
                    };
                    t.districts.ceilingCap = 2.4f;
                    t.districts.rafters = true;
                    t.districts.floorGlow = 0.05f;   // the Orange Glow, through some of the boards
                    t.districts.pitFieldChance = 0.08f;
                    var s = t.style;
                    s.deadLights = 0.7f;   // a few weak bulbs; most light comes up through the floor
                    s.exitChance = 0.3f;
                    s.propDensity = 0.08f;
                    s.exits = new List<ExitLink> { new ExitLink(ExitKind.Door, "0", 1f), new ExitLink(ExitKind.Door, "1", 0.4f) };   // broken doors lead elsewhere
                    s.props = new List<PropWeight>
                    {
                        new PropWeight(PropKind.CardboardBox, 6), new PropWeight(PropKind.StackedBoxes, 3), new PropWeight(PropKind.Crate, 2),
                        new PropWeight(PropKind.Armchair, 1), new PropWeight(PropKind.Bookshelf, 1), new PropWeight(PropKind.FloorLamp, 1),
                        new PropWeight(PropKind.Nightstand, 1), new PropWeight(PropKind.Sofa, 0.5f), new PropWeight(PropKind.WoodChair, 1),
                    };
                    t.lightColor = new Color(1f, 0.82f, 0.6f);
                    t.lightIntensity = 0.8f;
                    t.ambient = new Color(0.24f, 0.17f, 0.11f);
                    t.fogColor = new Color(0.22f, 0.15f, 0.09f);
                    t.fogDensity = 0.04f;
                    t.humVolume = 0.02f;
                });

            // Level 9223372036854775807: the True Final Level, with a concrete spiral stair and entrance elevator.
            Level("9223372036854775807", "Level 9223372036854775807 · An Endless Ending",
                "Based on Level 9223372036854775807, \"An Endless Ending\" (the True Final Level), from the Backrooms community wiki: https://ammbackrooms.fandom.com/wiki/Level_9223372036854775807 . " + License,
                mConcreteWall, mConcreteFloor, mConcreteCeiling, t =>
            {
                t.generator = LevelGenerator.Stairwell;
                t.baseDrift = 0f;
                t.style.ceiling = 3.6f;
                t.style.deadLights = 0f;
                t.style.blackoutChance = 0f;
                t.style.exitChance = 0f;
                t.wallpapers = new[] { mConcreteWall, mHallConcrete };
                t.carpets = new[] { mConcreteFloor, mHallFloor };
                t.ceilings = new[] { mConcreteCeiling, mHallCeiling };
                t.lightColor = new Color(1f, 1f, 1f, 1f);
                t.lightIntensity = 1.15f;
                t.lightRange = 12f;
                t.ambient = new Color(0.62f, 0.63f, 0.66f, 1f);
                t.fogColor = new Color(0.88f, 0.89f, 0.92f, 1f);
                t.fogDensity = 0.018f;
                t.humVolume = 0.02f;
            });

            // Level Fun =): the hand-made level model (made with an AI tool, Claude). Its meshes must be readable so the
            // game can find the lights, exit signs and food in them.
            const string FunModel = "Assets/Models/BackroomsLevelFun/LevelFun.fbx";
            if (AssetImporter.GetAtPath(FunModel) is ModelImporter funImporter && !funImporter.isReadable)
            {
                funImporter.isReadable = true;
                funImporter.SaveAndReimport();
            }
            Level("fun", "Level Fun =)",
                "Based on \"Level Fun =)\" on the Backrooms Wiki, original page by 1000dumplings (since rewritten as Level 26 \"The SS Fun =)\" by "
                + "Robert Goerman and MC_Crafter_24_7). Level model made for this game with an AI tool (Claude). " + License,
                mHallConcrete, mHallFloor, mHallCeiling, t =>
                {
                    t.generator = LevelGenerator.Static;
                    t.staticLevel = AssetDatabase.LoadAssetAtPath<GameObject>(FunModel);
                    t.lightColor = new Color(1f, 0.93f, 0.88f);
                    t.lightIntensity = 1.3f;
                    t.lightRange = 8f;
                    t.ambient = new Color(0.3f, 0.27f, 0.32f);
                    t.fogColor = new Color(0.32f, 0.28f, 0.36f);
                    t.fogDensity = 0.02f;
                    t.humVolume = 0.05f;
                });

            // Level 188 "The Courtyard of Windows": the hand-made atrium model (made with an AI tool, Claude).
            const string CourtyardModel = "Assets/Models/BackroomsLevel188/Level188.fbx";
            if (AssetImporter.GetAtPath(CourtyardModel) is ModelImporter courtyardImporter && !courtyardImporter.isReadable)
            {
                courtyardImporter.isReadable = true;
                courtyardImporter.SaveAndReimport();
            }
            Level("188", "Level 188 Â· The Courtyard of Windows",
                "Based on \"Level 188 - The Courtyard of Windows\" on the Backrooms Wiki, original by 1000dumplings, rewritten by Kai4C. "
                + "Level model made for this game with an AI tool (Claude). " + License,
                mHallConcrete, mHallFloor, mHallCeiling, t =>
                {
                    t.generator = LevelGenerator.Static;
                    t.staticLevel = AssetDatabase.LoadAssetAtPath<GameObject>(CourtyardModel);
                    t.lightColor = new Color(0.95f, 0.97f, 1f);
                    t.lightIntensity = 1.4f;
                    t.lightRange = 10f;
                    t.ambient = new Color(0.32f, 0.33f, 0.35f);
                    t.fogColor = new Color(0.55f, 0.56f, 0.58f);
                    t.fogDensity = 0.015f;
                    t.humVolume = 0.02f;   // heavy silence
                });

            catalog.levels = levels;
            EditorUtility.SetDirty(catalog);

            // ---- scene ----
            var profile = SaveVolumeProfile();
            var start = levels[0];
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null;
            RenderSettings.fog = false;   // HDRP: fog, fill light and reflections come from the volumes (see Atmosphere)

            var player = CreatePlayer(start.fogColor);
            var world = new GameObject("World").AddComponent<ChunkStreamer>();
            Assign(world, ("player", player.transform));
            var game = new GameObject("Game");
            var fx = game.AddComponent<TransitionFx>();
            var session = game.AddComponent<GameSession>();
            Assign(session, ("catalog", catalog), ("streamer", world), ("player", player), ("fx", fx));

            var volume = new GameObject("Post Processing").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            new GameObject("Atmosphere").AddComponent<Atmosphere>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath && File.Exists(s.path));
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
            AssetDatabase.SaveAssets();
            GrimeMaterials.Build();   // grime decals and colour variation (graphics revamp, Stage D)
            DistrictLayout.ClearCache();
            foreach (var t in new[] { paper, stains, carpet016, carpet011, carpet014, ceilingPhoto, concretePhoto }) UnityEngine.Object.DestroyImmediate(t);
            Debug.Log($"Game scene created at {ScenePath} with {levels.Count} levels. Press Play. Dev keys: F6 next level, F9 new game.");
        }

        /// Level 0's districts: mostly tight maze-like spaces with narrow rooms/corridors and occasional liminal variety.
        /// Open areas stay rare; giant halls come from rare region sites (DistrictRegions.cs), not from here.
        static DistrictStyle Level0Districts() => new DistrictStyle
        {
            kinds = new List<DistrictWeight>
            {
                new DistrictWeight(DistrictKind.Maze, 6.5f), new DistrictWeight(DistrictKind.OpenExpanse, 0.15f),
                new DistrictWeight(DistrictKind.PillarHall, 0.15f), new DistrictWeight(DistrictKind.PanelRows, 1.1f),
                new DistrictWeight(DistrictKind.OfficeBlock, 2.2f), new DistrictWeight(DistrictKind.Corridors, 2.3f),
                new DistrictWeight(DistrictKind.NestedRooms, 1.8f), new DistrictWeight(DistrictKind.Atrium, 0.04f),
                new DistrictWeight(DistrictKind.Storage, 0.06f), new DistrictWeight(DistrictKind.Showroom, 0.04f),
                new DistrictWeight(DistrictKind.Supermarket, 0.03f), new DistrictWeight(DistrictKind.ClothingStore, 0.03f),
                new DistrictWeight(DistrictKind.MallConcourse, 0.03f), new DistrictWeight(DistrictKind.Arches, 0.9f),
            },
        };

        static List<PropWeight> OfficeProps() => new List<PropWeight>
        {
            new PropWeight(PropKind.OfficeChair, 3f), new PropWeight(PropKind.CardboardBox, 3f), new PropWeight(PropKind.FilingCabinet, 1.4f),
            new PropWeight(PropKind.Desk, 1f), new PropWeight(PropKind.TrashCan, 1.6f), new PropWeight(PropKind.WetFloorSign, 1.2f),
            new PropWeight(PropKind.Bucket, 1.4f), new PropWeight(PropKind.FallenTile, 2.2f), new PropWeight(PropKind.HangingWires, 1.4f),
            new PropWeight(PropKind.Ladder, 0.45f), new PropWeight(PropKind.Outlet, 4f), new PropWeight(PropKind.LightSwitch, 1.5f),
            new PropWeight(PropKind.Vent, 1.5f), new PropWeight(PropKind.ExitSign, 0.7f), new PropWeight(PropKind.DoorToNowhere, 0.6f),
            new PropWeight(PropKind.AlmondWater, 1.4f), new PropWeight(PropKind.Battery, 1f),
            new PropWeight(PropKind.Puddle, 1.2f), new PropWeight(PropKind.TilePile, 0.8f), new PropWeight(PropKind.Phone, 0.35f),
            new PropWeight(PropKind.CrtTv, 0.2f), new PropWeight(PropKind.Can, 0.35f), new PropWeight(PropKind.Lantern, 0.15f),
        };

        /// HDRP: one pipeline asset for every quality level, with the features the look needs switched on
        /// (screen-space GI / AO / reflections, volumetric fog, custom passes, decals, optional ray tracing).
        static void SetupHdrp()
        {
            string path = $"{Root}/Settings/HDRP_Asset.asset";
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var so = new SerializedObject(asset);
            void Bool(string name, bool v) { var p = so.FindProperty("m_RenderPipelineSettings." + name); if (p != null) p.boolValue = v; else Debug.LogWarning("[HDRP] no setting " + name); }
            void Int(string name, int v) { var p = so.FindProperty("m_RenderPipelineSettings." + name); if (p != null) p.intValue = v; else Debug.LogWarning("[HDRP] no setting " + name); }
            Bool("supportSSAO", true);
            Bool("supportSSR", true);
            Bool("supportSSGI", true);
            Bool("supportVolumetrics", true);
            Bool("supportCustomPass", true);
            Bool("supportDecals", true);
            Bool("supportMotionVectors", true);
            Bool("supportRayTracing", SystemInfo.supportsRayTracing);
            Bool("supportContactShadows", true);
            Int("hdShadowInitParams.maxShadowRequests", 64);
            Int("hdShadowInitParams.punctualLightShadowAtlas.shadowAtlasResolution", 4096);
            Int("hdShadowInitParams.areaLightShadowAtlas.shadowAtlasResolution", 4096);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int q = 0; q < QualitySettings.names.Length; q++)
            {
                QualitySettings.SetQualityLevel(q, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
        }

        static FirstPersonController CreatePlayer(Color background)
        {
            var player = new GameObject("Player");
            player.transform.position = new Vector3(1f, 0.05f, 1f);   // a chunk's corner cell: always open floor
            var body = player.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.3f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.3f;

            var pivot = new GameObject("Camera") { tag = "MainCamera" };
            pivot.transform.SetParent(player.transform, false);
            pivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = pivot.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 150f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            var data = pivot.AddComponent<HDAdditionalCameraData>();
            data.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            data.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
            data.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            data.backgroundColorHDR = Color.black;
            data.volumeLayerMask = ~0;
            pivot.AddComponent<AudioListener>();

            var feet = player.AddComponent<AudioSource>();
            feet.playOnAwake = false;
            feet.spatialBlend = 0f;
            var controller = player.AddComponent<FirstPersonController>();
            player.AddComponent<PlayerStatus>();
            player.AddComponent<PlayerTools>();
            player.AddComponent<FirstPersonArms>();
            player.AddComponent<PlayerHud>();
            Assign(controller, ("cameraPivot", pivot.transform), ("footstepSource", feet));
            return controller;
        }

        static void Assign(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var prop = so.FindProperty(field) ?? throw new ArgumentException($"{target.GetType().Name} has no field '{field}'");
                prop.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// The CC0 normal map for a set, imported as a normal map.
        static Texture2D Normal(string id)
        {
            string path = $"{Cc0}/{id}/{id}_2K-JPG_NormalGL.jpg";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// HDRP mask map from a CC0 set: R metalness, G ambient occlusion, A smoothness (1 - roughness). Linear, cached.
        static Texture2D Mask(string id)
        {
            string outPath = $"{Generated}/{id}_Mask.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
            if (existing != null) return existing;
            string Find(string kind) { string f = $"{Cc0}/{id}/{id}_2K-JPG_{kind}.jpg"; return File.Exists(f) ? f : null; }
            var rough = Find("Roughness");
            if (rough == null) return null;
            var r = ProceduralTextures.Load(rough);
            var aoPath = Find("AmbientOcclusion");
            var metalPath = Find("Metalness");
            var ao = aoPath != null ? ProceduralTextures.Load(aoPath) : null;
            var metal = metalPath != null ? ProceduralTextures.Load(metalPath) : null;
            int w = r.width, h = r.height;
            var rp = r.GetPixels32();
            var ap = ao != null && ao.width == w ? ao.GetPixels32() : null;
            var mp = metal != null && metal.width == w ? metal.GetPixels32() : null;
            var outPx = new Color32[rp.Length];
            for (int i = 0; i < rp.Length; i++)
                outPx[i] = new Color32(mp != null ? mp[i].r : (byte)0, ap != null ? ap[i].r : (byte)255, 255, (byte)(255 - rp[i].r));   // blue on: detail maps work
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
            tex.SetPixels32(outPx);
            tex.Apply();
            File.WriteAllBytes(outPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(r);
            if (ao != null) UnityEngine.Object.DestroyImmediate(ao);
            if (metal != null) UnityEngine.Object.DestroyImmediate(metal);
            AssetDatabase.ImportAsset(outPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(outPath);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        static Texture2D ColorMap(string id)
        {
            string path = $"{Cc0}/{id}/{id}_2K-JPG_Color.jpg";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer != null && importer.anisoLevel != 8)
            {
                importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// Saves a generated tangent-space normal map (PNG) and imports it as a normal map.
        static Texture2D SaveNormal(string name, Texture2D tex)
        {
            var t = SaveLinear(name, tex);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(t));
        }

        /// Saves generated data (mask, normals) as a linear PNG, mipmapped, tiling.
        static Texture2D SaveLinear(string name, Texture2D tex)
        {
            string path = $"{Generated}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// Saves a generated texture as JPG (or PNG for point-filtered data) and imports it.
        static Texture2D Save(string name, Texture2D tex, bool point = false)
        {
            string path = $"{Generated}/{name}.{(point ? "png" : "jpg")}";
            File.WriteAllBytes(path, point ? tex.EncodeToPNG() : tex.EncodeToJPG(92));
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.anisoLevel = 8;   // keeps floors sharp at grazing angles
            importer.maxTextureSize = 2048;
            if (point) importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Lit(string name, Texture2D albedo, Texture2D normal, Vector2 tiling, float smoothness, float bump, Color? tint = null, Texture2D mask = null)
        {
            var mat = GetMaterial(name, Shader.Find("HDRP/Lit"));
            HdMat.Configure(mat, tint ?? Color.white, smoothness, 0f, albedo, normal, bump, mask, tiling);   // mesh UVs are in meters
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// A flat colour. With the unlit shader it becomes something that glows (brightness from the colour, HDR),
        /// or pure black for the void.
        static Material Flat(string name, Shader shader, Color color, float smoothness = 0.2f, float metallic = 0f, Texture2D tex = null)
        {
            bool glow = shader != null && shader.name.Contains("Unlit");
            if (glow && color.maxColorComponent < 0.01f)
            {
                var black = GetMaterial(name, shader);
                black.SetColor("_UnlitColor", Color.black);
                HDMaterial.ValidateMaterial(black);
                EditorUtility.SetDirty(black);
                return black;
            }
            var mat = GetMaterial(name, Shader.Find("HDRP/Lit"));
            if (glow) HdMat.Emissive(new Color(color.r, color.g, color.b) / Mathf.Max(1f, color.maxColorComponent), 1500f * Mathf.Max(0.3f, color.maxColorComponent), tex, mat);
            else HdMat.Configure(mat, color, smoothness, metallic, tex, null, 1f, null, null);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material GetMaterial(string name, Shader shader)
        {
            string path = $"{Root}/Art/Materials/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            return mat;
        }

        static Cubemap SaveReflection(Color color)
        {
            string path = $"{Root}/Settings/Reflection.cubemap";
            AssetDatabase.DeleteAsset(path);
            var cube = new Cubemap(16, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(color, 16 * 16).ToArray();
            foreach (CubemapFace face in new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ })
                cube.SetPixels(pixels, face);
            cube.Apply();
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        static Material MakeTransparent(Material mat)
        {
            HdMat.Transparent(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static VolumeProfile SaveVolumeProfile()
        {
            string path = $"{Root}/Settings/GameVolume.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            // The camera: exposure adapts like a cheap camcorder's (slowly, a little overexposed under fluorescents).
            var exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.AutomaticHistogram);
            exposure.limitMin.Override(4.5f);
            exposure.limitMax.Override(11f);
            exposure.compensation.Override(1.1f);
            exposure.adaptationSpeedDarkToLight.Override(1.2f);
            exposure.adaptationSpeedLightToDark.Override(0.8f);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.22f);
            bloom.scatter.Override(0.72f);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            profile.Add<Vignette>(true).intensity.Override(0.24f);
            var grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin2);
            grain.intensity.Override(0.32f);
            profile.Add<ChromaticAberration>(true).intensity.Override(0.07f);
            var color = profile.Add<ColorAdjustments>(true);
            color.contrast.Override(6f);
            color.saturation.Override(-8f);
            var balance = profile.Add<WhiteBalance>(true);   // fluorescent light on cheap sensors: a touch green-yellow
            balance.temperature.Override(0f);
            balance.tint.Override(4f);
            // Light that looks real: ambient occlusion, bounced light, reflections on wet carpet and tiles, contact shadows.
            var ao = profile.Add<ScreenSpaceAmbientOcclusion>(true);
            ao.intensity.Override(1.3f);
            ao.radius.Override(1.4f);
            profile.Add<GlobalIllumination>(true).enable.Override(true);
            profile.Add<ScreenSpaceReflection>(true).enabled.Override(true);
            var contact = profile.Add<ContactShadows>(true);
            contact.enable.Override(true);
            contact.length.Override(0.2f);
            profile.Add<HDShadowSettings>(true).maxShadowDistance.Override(45f);
            var blur = profile.Add<MotionBlur>(true);
            blur.intensity.Override(0.15f);

            // Volume components must live inside the profile asset or they're lost on reload.
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(profile);
            return profile;
        }
    }
}
