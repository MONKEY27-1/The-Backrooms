using System.IO;
using Backrooms.Net;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.EditorTools
{
    /// Builds what co-op needs from the user's hazmat suit model (Assets/Models/PlayerHazmat):
    /// one recoloured base-colour texture per suit colour (only the yellow fabric changes), a material each,
    /// and the network avatar prefab in Resources.
    public static class CoopAssets
    {
        const string ModelDir = "Assets/Models/PlayerHazmat";
        const string Model = ModelDir + "/Player_Hazmat.fbx";
        const string Tex = ModelDir + "/Textures";
        const string SuitDir = "Assets/_Game/Resources/Suits";
        const string AvatarPath = "Assets/_Game/Resources/CoopAvatar.prefab";

        // Hue (0..1), saturation and brightness multipliers for each suit; index 0 is the original yellow.
        static readonly (float hue, float sat, float val)[] Suits = { (-1f, 1f, 1f), (0.6f, 0.95f, 0.85f), (0.0f, 1f, 0.85f), (0.33f, 0.8f, 0.8f) };

        [MenuItem("Backrooms/Build Co-op Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(SuitDir);
            PrepareImports();
            for (int i = 0; i < Suits.Length; i++) BuildSuit(i);
            BuildAvatar();
            BuildCreatureProxy();
            AssetDatabase.SaveAssets();
            Debug.Log("Co-op assets built: " + Suits.Length + " suits and " + AvatarPath);
        }

        static void PrepareImports()
        {
            if (AssetImporter.GetAtPath(Tex + "/T_Player_Normal.png") is TextureImporter n && n.textureType != TextureImporterType.NormalMap)
            {
                n.textureType = TextureImporterType.NormalMap;
                n.SaveAndReimport();
            }
            if (AssetImporter.GetAtPath(Tex + "/T_Player_MaskMap_HDRP.png") is TextureImporter m && m.sRGBTexture)
            {
                m.sRGBTexture = false;
                m.SaveAndReimport();
            }
            if (AssetImporter.GetAtPath(Model) is ModelImporter model && model.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                model.materialImportMode = ModelImporterMaterialImportMode.None;   // our own suit materials replace it
                model.SaveAndReimport();
            }
        }

        static void BuildSuit(int index)
        {
            string basePath = Tex + "/T_Player_BaseColor.png";
            string colourPath = index == 0 ? basePath : $"{Tex}/T_Player_BaseColor_{CoopAvatar.SuitNames[index]}.png";
            if (index > 0) Recolour(basePath, colourPath, Suits[index]);
            var mat = new Material(Shader.Find("HDRP/Lit")) { name = "Suit_" + index };
            mat.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colourPath));
            mat.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/T_Player_Normal.png"));
            mat.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Tex + "/T_Player_MaskMap_HDRP.png"));
            mat.SetColor("_BaseColor", Color.white);
            HDMaterial.ValidateMaterial(mat);
            string path = $"{SuitDir}/Suit_{index}.mat";
            var old = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (old != null) { EditorUtility.CopySerialized(mat, old); Object.DestroyImmediate(mat); }
            else AssetDatabase.CreateAsset(mat, path);
        }

        /// Swaps the suit's yellow for another colour. Only strongly yellow pixels change, eased in at the edges
        /// of the range, so grey rubber, black visor, olive filters and white cuffs stay as they are.
        static void Recolour(string from, string to, (float hue, float sat, float val) suit)
        {
            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            src.LoadImage(File.ReadAllBytes(from));
            var px = src.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                Color c = px[i];
                Color.RGBToHSV(c, out float h, out float s, out float v);
                float hueWeight = Mathf.Clamp01(1f - Mathf.Abs(h - 0.125f) / 0.06f);       // yellow-orange, ~23°–67°
                float satWeight = Mathf.Clamp01((s - 0.28f) / 0.15f);
                float valWeight = Mathf.Clamp01((v - 0.12f) / 0.1f);
                float w = Mathf.Clamp01(hueWeight * 1.6f) * satWeight * valWeight;
                if (w <= 0f) continue;
                var swapped = Color.HSVToRGB(suit.hue, Mathf.Clamp01(s * suit.sat), Mathf.Clamp01(v * suit.val));
                var o = Color.Lerp(c, swapped, w);
                o.a = c.a;
                px[i] = o;
            }
            var dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            dst.SetPixels32(px);
            File.WriteAllBytes(to, dst.EncodeToPNG());
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(dst);
            AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceSynchronousImport);
        }

        static void BuildAvatar()
        {
            var root = new GameObject("CoopAvatar");
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;   // each player moves their own body
            nt.SyncRotAngleX = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            var cap = root.AddComponent<CapsuleCollider>();
            cap.height = 1.8f;
            cap.radius = 0.3f;
            cap.center = new Vector3(0f, 0.9f, 0f);
            root.AddComponent<CoopAvatar>();

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            model.name = "Suit";
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0f, SuitYaw, 0f);
            model.AddComponent<SuitRig>();
            var suit0 = AssetDatabase.LoadAssetAtPath<Material>(SuitDir + "/Suit_0.mat");
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                r.sharedMaterial = suit0;
                r.updateWhenOffscreen = true;   // the rig moves bones in code; keep the bounds honest
            }

            var head = new GameObject("Head").transform;
            head.SetParent(root.transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);
            var torch = new GameObject("Torch");
            torch.transform.SetParent(head, false);
            torch.transform.localPosition = new Vector3(0.12f, -0.2f, 0.25f);
            torch.AddHDLight(LightType.Spot);
            var lamp = torch.GetComponent<Light>();
            lamp.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            lamp.range = 22f;
            lamp.spotAngle = 55f;
            lamp.innerSpotAngle = 25f;
            lamp.intensity = 900f;
            lamp.enabled = false;

            PrefabUtility.SaveAsPrefabAsset(root, AvatarPath);
            Object.DestroyImmediate(root);
        }

        /// What guests see of a creature the owner's game is running.
        static void BuildCreatureProxy()
        {
            var root = new GameObject("CoopCreature");
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            root.AddComponent<CreatureProxy>();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/_Game/Resources/CoopCreature.prefab");
            Object.DestroyImmediate(root);
        }

        /// Turns the model to face the avatar's +z (checked by eye in a render).
        const float SuitYaw = 0f;
    }
}
