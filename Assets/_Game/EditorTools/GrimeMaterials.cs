using System.IO;
using Backrooms.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.EditorTools
{
    /// Graphics revamp Stage D: the grime decal material, and slow colour/sheen variation on the big surfaces so
    /// their tiling stops showing. Safe to re-run; GameSceneSetup calls it after making the level materials.
    public static class GrimeMaterials
    {
        const string Generated = "Assets/_Game/Art/Textures/Generated", Materials = "Assets/_Game/Art/Materials";

        [MenuItem("Backrooms/Build Grime Materials")]
        public static void Build()
        {
            var atlas = Import("GrimeAtlas", ProceduralTextures.GrimeAtlas(), true);
            const string path = "Assets/_Game/Resources/GrimeDecal.mat";
            var decal = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (decal == null) AssetDatabase.CreateAsset(decal = new Material(Shader.Find("HDRP/Decal")), path);
            decal.SetTexture("_BaseColorMap", atlas);
            decal.SetFloat("_DecalBlend", 1f);
            decal.SetFloat("_AffectAlbedo", 1f);
            foreach (var p in new[] { "_AffectNormal", "_AffectMetal", "_AffectAO", "_AffectSmoothness" }) decal.SetFloat(p, 0f);
            HDMaterial.ValidateMaterial(decal);
            EditorUtility.SetDirty(decal);

            const string bloodPath = "Assets/_Game/Resources/BloodDecal.mat";
            var blood = AssetDatabase.LoadAssetAtPath<Material>(bloodPath);
            if (blood == null) AssetDatabase.CreateAsset(blood = new Material(Shader.Find("HDRP/Decal")), bloodPath);
            blood.SetTexture("_BaseColorMap", Import("BloodAtlas", ProceduralTextures.BloodAtlas(), true));
            blood.SetFloat("_DecalBlend", 1f);
            blood.SetFloat("_AffectAlbedo", 1f);
            blood.SetFloat("_AffectSmoothness", 1f);   // wet
            blood.SetFloat("_Smoothness", 0.88f);
            blood.SetFloat("_MaskBlendSrc", 0f);       // only where there is blood (the base map's alpha), not the whole quad
            foreach (var p in new[] { "_AffectNormal", "_AffectMetal", "_AffectAO" }) blood.SetFloat(p, 0f);
            HDMaterial.ValidateMaterial(blood);
            EditorUtility.SetDirty(blood);

            var variation = Import("MacroVariation", ProceduralTextures.MacroVariation(), false);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Materials }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!m.HasProperty("_DetailMap") || !(GrimeDressing.Takes(m.name) || m.name.StartsWith("Ceiling"))) continue;
                var current = m.GetTexture("_DetailMap");
                if (current != null && current != variation) continue;   // hung wallpaper has its own water damage
                OpenDetail(m.GetTexture("_MaskMap") as Texture2D);
                m.SetTexture("_DetailMap", variation);
                m.SetFloat("_LinkDetailsWithBase", 0f);
                m.SetTextureScale("_DetailMap", Vector2.one / 9.7f);     // odd, so it never lines up with a base tile
                m.SetFloat("_DetailAlbedoScale", 1f);
                m.SetFloat("_DetailNormalScale", 0f);
                m.SetFloat("_DetailSmoothnessScale", 1f);
                HDMaterial.ValidateMaterial(m);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        /// A mask map's blue channel switches the detail map on; the generated ones used to be 0 there.
        static void OpenDetail(Texture2D mask)
        {
            string path = AssetDatabase.GetAssetPath(mask);
            if (mask == null || !path.StartsWith(Generated) || !path.EndsWith(".png")) return;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            t.LoadImage(File.ReadAllBytes(path));
            var px = t.GetPixels32();
            if (px[0].b != 255)
            {
                for (int i = 0; i < px.Length; i++) px[i].b = 255;
                t.SetPixels32(px);
                File.WriteAllBytes(path, t.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
            }
            Object.DestroyImmediate(t);
        }

        static Texture2D Import(string name, Texture2D tex, bool colour)
        {
            string path = $"{Generated}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var im = (TextureImporter)AssetImporter.GetAtPath(path);
            im.sRGBTexture = colour;
            im.wrapMode = colour ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            im.anisoLevel = 8;
            im.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// Every grime triangle faces the way its normal says, and none hangs off its surface.
        [MenuItem("Backrooms/Check Grime")]
        public static void Check()
        {
            var layout = new ChunkLayout();
            layout.Quads.Add(new SurfaceQuad { Area = new Rect(5, 5, 20, 20), FacingUp = true });
            layout.Boxes.Add(SurfaceBox.Uniform(new Bounds(new Vector3(15, 1.5f, 15), new Vector3(8, 3, 8)), default, BoxMeshBuilder.Faces.Sides));
            var m = new Material(Shader.Find("HDRP/Lit")) { name = "Carpet0" };
            int quads = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                var mesh = GrimeDressing.Build(layout, Vector3.zero, _ => m, seed);
                if (mesh == null) continue;
                var v = mesh.vertices; var n = mesh.normals; var t = mesh.triangles;
                for (int i = 0; i < t.Length; i += 3)
                {
                    var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).normalized;
                    if (Vector3.Dot(face, n[t[i]]) < 0.99f) throw new System.Exception($"grime triangle {i} faces backwards");
                }
                foreach (var p in v)
                    if (p.x < 4.99f || p.x > 25.01f || p.z < 4.99f || p.z > 25.01f || p.y > 3.01f || p.y < -0.01f) throw new System.Exception($"grime off its surface at {p}");
                quads += v.Length / 4;
                Object.DestroyImmediate(mesh);
            }
            Object.DestroyImmediate(m);
            if (quads == 0) throw new System.Exception("no grime was made");
            Debug.Log($"[Grime] ok, {quads} quads");
        }
    }
}
