using System.Collections.Generic;
using Backrooms.Net;
using Backrooms.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace Backrooms.Creatures
{
    /// Builds creature bodies from generated models, with distinct procedural silhouettes as a safe fallback.
    public static class CreatureBodies
    {
        static Material black, grin, skin;
        static readonly Dictionary<string, Material> creatureMaterials = new Dictionary<string, Material>();
        static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int EmissiveColorMap = Shader.PropertyToID("_EmissiveColorMap");

        public static void Build(CreatureKind kind, Transform root, out Transform glow)
        {
            glow = null;
            if (kind == CreatureKind.SkinStealer)
            {
                var disguise = AddHazmatDisguise(root);
                var revealed = AddModel(root, "SkinStealer", "Skin Stealer Revealed", 3.5f);
                if (disguise != null && revealed != null)
                {
                    SetSkinStealerRevealed(root, false);
                    return;
                }
                if (disguise != null) Object.Destroy(disguise);
                if (revealed != null) Object.Destroy(revealed);
            }
            else if (kind == CreatureKind.Faceling && AddModel(root, "Faceling", "Faceling Model", 3.0f) != null) return;
            else if (kind == CreatureKind.PartyGoer && AddModel(root, "PartyGoer", "Partygoer Model", 3.5f) != null) return;

            if (kind == CreatureKind.PartyGoer)
            {
                skin = skin != null ? skin : Lit(new Color(0.92f, 0.66f, 0.12f));
                Part(root, PrimitiveType.Capsule, skin, new Vector3(0f, 0.98f, 0f), new Vector3(0.34f, 0.62f, 0.3f));
                Part(root, PrimitiveType.Sphere, skin, new Vector3(0f, 1.76f, 0f), new Vector3(0.3f, 0.32f, 0.28f));
                var grinMat = Unlit(new Color(4f, 3.5f, 0.5f));
                for (int i = 0; i < 8; i++)
                {
                    float x = (i - 3.5f) * 0.065f;
                    Part(root, PrimitiveType.Cube, grinMat, new Vector3(x, 1.73f - Mathf.Abs(x) * 0.18f, 0.264f), new Vector3(0.035f, 0.06f, 0.025f));
                }
                Part(root, PrimitiveType.Sphere, grinMat, new Vector3(-0.09f, 1.9f, 0.23f), Vector3.one * 0.05f);
                Part(root, PrimitiveType.Sphere, grinMat, new Vector3(0.09f, 1.9f, 0.23f), Vector3.one * 0.05f);
                Part(root, PrimitiveType.Capsule, skin, new Vector3(-0.34f, 0.98f, 0f), new Vector3(0.115f, 0.63f, 0.12f));
                Part(root, PrimitiveType.Capsule, skin, new Vector3(0.34f, 0.98f, 0f), new Vector3(0.115f, 0.63f, 0.12f));
                glow = root;
                return;
            }

            if (kind == CreatureKind.Smiler)
            {
                // Almost nothing: a tall shape you can't quite see, and a grin that floats in the dark.
                black = black != null ? black : Lit(new Color(0.035f, 0.035f, 0.04f));
                grin = grin != null ? grin : Unlit(new Color(4f, 3.8f, 3.2f));
                Part(root, PrimitiveType.Capsule, black, new Vector3(0f, 1.08f, 0f), new Vector3(0.42f, 1.08f, 0.36f));
                Part(root, PrimitiveType.Sphere, black, new Vector3(0f, 1.58f, 0f), new Vector3(0.72f, 0.34f, 0.38f));
                Part(root, PrimitiveType.Capsule, black, new Vector3(-0.43f, 1.02f, 0f), new Vector3(0.15f, 0.72f, 0.15f));
                Part(root, PrimitiveType.Capsule, black, new Vector3(0.43f, 1.02f, 0f), new Vector3(0.15f, 0.72f, 0.15f));
                var face = new GameObject("Grin").transform;
                face.SetParent(root, false);
                face.localPosition = new Vector3(0f, 1.84f, 0.31f);
                const int teeth = 15;
                for (int i = 0; i < teeth; i++)
                {
                    float t = (i / (float)(teeth - 1)) * 2f - 1f;
                    var tooth = Part(face, PrimitiveType.Cube, grin, new Vector3(t * 0.29f, -0.09f * (1f - t * t), 0f), new Vector3(0.038f, 0.065f, 0.018f));
                    tooth.localRotation = Quaternion.Euler(0f, 0f, t * 28f);
                }
                Part(face, PrimitiveType.Sphere, grin, new Vector3(-0.15f, 0.19f, 0f), Vector3.one * 0.065f);
                Part(face, PrimitiveType.Sphere, grin, new Vector3(0.15f, 0.19f, 0f), Vector3.one * 0.065f);
                glow = face;
                return;
            }

            // Faceling: pallid, long-limbed watcher with subtle dark eye sockets.
            skin = skin != null ? skin : Lit(new Color(0.78f, 0.72f, 0.64f));
            Part(root, PrimitiveType.Capsule, skin, new Vector3(0f, 0.98f, 0f), new Vector3(0.33f, 0.62f, 0.27f));
            Part(root, PrimitiveType.Sphere, skin, new Vector3(0f, 1.76f, 0f), new Vector3(0.22f, 0.28f, 0.2f));
            Part(root, PrimitiveType.Capsule, skin, new Vector3(-0.32f, 0.9f, 0f), new Vector3(0.105f, 0.72f, 0.11f));
            Part(root, PrimitiveType.Capsule, skin, new Vector3(0.32f, 0.9f, 0f), new Vector3(0.105f, 0.72f, 0.11f));
            Part(root, PrimitiveType.Capsule, skin, new Vector3(-0.105f, 0.24f, 0f), new Vector3(0.12f, 0.54f, 0.12f));
            Part(root, PrimitiveType.Capsule, skin, new Vector3(0.105f, 0.24f, 0f), new Vector3(0.12f, 0.54f, 0.12f));
            var voidMat = Lit(new Color(0.035f, 0.026f, 0.025f));
            Part(root, PrimitiveType.Sphere, voidMat, new Vector3(-0.085f, 1.78f, 0.17f), new Vector3(0.035f, 0.07f, 0.022f));
            Part(root, PrimitiveType.Sphere, voidMat, new Vector3(0.085f, 1.78f, 0.17f), new Vector3(0.035f, 0.07f, 0.022f));
        }

        static GameObject AddModel(Transform root, string folder, string name, float height)
        {
            var prefab = Resources.Load<GameObject>("Creatures/" + folder + "/" + folder);
            if (prefab == null) return null;
            var model = Object.Instantiate(prefab, root, false);
            model.name = name;
            model.transform.localPosition = Vector3.zero;
            // The generated FBXs declare +Z as forward, matching the creature brain's forward direction.
            model.transform.localRotation = Quaternion.identity;
            ApplyAppearance(model, folder);
            return FitModel(root, model, height);
        }

        static void ApplyAppearance(GameObject model, string folder)
        {
            if (!creatureMaterials.TryGetValue(folder, out var material) || material == null)
            {
                material = MakeCreatureMaterial(folder);
                if (material == null) return;
                creatureMaterials[folder] = material;
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                if (materials.Length == 0) materials = new Material[1];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        /// One shared material per creature kind: albedo, the roughness/metallic maps packed into an HDRP
        /// mask map, and the emissive tell (cracks, eyes, grin) at a brightness you can actually see.
        static Material MakeCreatureMaterial(string folder)
        {
            var albedo = Resources.Load<Texture2D>($"Creatures/{folder}/texture/albedo");
            var emission = Resources.Load<Texture2D>($"Creatures/{folder}/texture/emission");
            var shader = Shader.Find("HDRP/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) return null;
            bool hdrp = shader.name.StartsWith("HDRP/");
            var material = new Material(shader) { enableInstancing = true };
            if (material.HasProperty(BaseColorMap)) material.SetTexture(BaseColorMap, albedo);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty(BaseColor)) material.SetColor(BaseColor, Color.white);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (hdrp)
            {
                // Without a mask map the whole mesh renders at one flat smoothness and reads as plastic.
                var mask = MaskMap(folder);
                if (mask != null)
                {
                    material.SetTexture("_MaskMap", mask);
                    material.SetFloat("_SmoothnessRemapMin", 0f);
                    material.SetFloat("_SmoothnessRemapMax", 0.7f);
                    material.SetFloat("_AORemapMin", 0f);
                    material.SetFloat("_AORemapMax", 1f);
                }
                else material.SetFloat("_Smoothness", 0.35f);
            }
            else material.SetFloat("_Glossiness", 0.35f);
            if (emission != null && material.HasProperty(EmissiveColorMap))
            {
                material.SetTexture(EmissiveColorMap, emission);
                if (hdrp)
                {
                    // The old 0.2 nits was invisible in HDRP's unit-aware path; the glowing parts are the tell.
                    HDMaterial.SetUseEmissiveIntensity(material, true);
                    HDMaterial.SetEmissiveColor(material, Color.white);
                    HDMaterial.SetEmissiveIntensity(material, 30f, EmissiveIntensityUnit.Nits);
                }
                else
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.white * 1.5f);
                }
            }
            if (hdrp) HDMaterial.ValidateMaterial(material);
            return material;
        }

        static readonly Dictionary<string, Texture2D> maskMaps = new Dictionary<string, Texture2D>();

        /// Pack the generator's roughness + metallic JPGs into an HDRP mask map (R metal, G AO, B detail, A smoothness).
        static Texture2D MaskMap(string folder)
        {
            if (maskMaps.TryGetValue(folder, out var cached)) return cached;
            var rough = Resources.Load<Texture2D>($"Creatures/{folder}/texture/roughness");
            var metal = Resources.Load<Texture2D>($"Creatures/{folder}/texture/metallic");
            if (rough == null && metal == null) { maskMaps[folder] = null; return null; }
            const int size = 1024;   // plenty for a creature, and built once per kind
            var roughness = Downsample(rough, size);
            var metallic = Downsample(metal, size);
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte metalR = metallic != null ? metallic[i].r : (byte)0;
                byte smoothA = roughness != null ? (byte)(255 - roughness[i].r) : (byte)90;
                pixels[i] = new Color32(metalR, 255, 0, smoothA);
            }
            var mask = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { name = folder + " MaskMap" };
            mask.SetPixels32(pixels);
            mask.Apply(true, true);
            maskMaps[folder] = mask;
            return mask;
        }

        /// GPU-downsample any texture (readable or not) to raw pixels.
        static Color32[] Downsample(Texture2D source, int size)
        {
            if (source == null) return null;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(source, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            var pixels = copy.GetPixels32();
            Object.Destroy(copy);
            return pixels;
        }

        static GameObject AddHazmatDisguise(Transform root)
        {
            var prefab = Resources.Load<GameObject>("CoopAvatar");
            if (prefab == null) return null;
            var model = Object.Instantiate(prefab, root, false);
            model.name = "Wanderer Disguise";
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            // Keep the suit helmet as part of the disguise, but hide its attached flashlight.
            var torch = model.transform.Find("Head/Torch");
            if (torch != null) torch.gameObject.SetActive(false);
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var light in model.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (var networkObject in model.GetComponentsInChildren<NetworkObject>(true)) networkObject.enabled = false;
            var rig = model.GetComponentInChildren<SuitRig>(true);
            if (rig != null)
            {
                rig.Animate(0f, false, false, 0f, 1f);
                rig.enabled = false;
            }
            foreach (var avatar in model.GetComponentsInChildren<CoopAvatar>(true)) avatar.enabled = false;
            return FitModel(root, model, 1.8f);
        }

        static GameObject FitModel(Transform root, GameObject model, float height)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Object.Destroy(model); return null; }
            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y < 0.001f) { Object.Destroy(model); return null; }
            // Keep the scale authored into the imported FBX root. Replacing localScale outright
            // discards that import scale and can shrink the visible mesh drastically.
            model.transform.localScale *= height / bounds.size.y;
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || !renderers[i].enabled) continue;
                if (!hasBounds) { bounds = renderers[i].bounds; hasBounds = true; }
                else bounds.Encapsulate(renderers[i].bounds);
            }
            if (!hasBounds || bounds.size.y < 0.001f) { Object.Destroy(model); return null; }
            model.transform.position += Vector3.up * (root.position.y - bounds.min.y);
            return model;
        }

        public static void SetSkinStealerRevealed(Transform root, bool revealed)
        {
            // CreatureBodyMotion rewraps the models under its pivot, so a shallow Find misses them and
            // the Skin Stealer used to keep its disguise while it chased you. Search the whole subtree.
            var disguise = FindDeep(root, "Wanderer Disguise");
            var monster = FindDeep(root, "Skin Stealer Revealed");
            if (disguise != null) disguise.gameObject.SetActive(!revealed);
            if (monster != null) monster.gameObject.SetActive(revealed);
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        static Transform Part(Transform parent, PrimitiveType type, Material m, Vector3 pos, Vector3 scale)
        {
            var p = GameObject.CreatePrimitive(type);
            Object.Destroy(p.GetComponent<Collider>());   // creatures don't block anything: the flow field steers them
            p.transform.SetParent(parent, false);
            p.transform.localPosition = pos;
            p.transform.localScale = scale;
            var r = p.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return p.transform;
        }

        static Material Unlit(Color c)
        {
            // Brighter than 1 means it glows (the Smiler's grin); otherwise a flat, unlit-looking black.
            return c.maxColorComponent > 1f ? HdMat.Emissive(c / c.maxColorComponent, 600f) : HdMat.Make(c, 0.05f);
        }

        static Material Lit(Color c)
        {
            return HdMat.Make(c, 0.35f);
        }
    }
}
