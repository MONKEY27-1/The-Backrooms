using System.Collections.Generic;
using Backrooms.Net;
using Backrooms.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

namespace Backrooms.Creatures
{
    /// Builds each kind's body.
    public static class CreatureBodies
    {
        public static void Build(CreatureKind kind, Transform root)
        {
            if (kind == CreatureKind.Smiler) { SmilerLook.Build(root); return; }
            if (kind == CreatureKind.Hound) { HoundLook.Build(root); return; }
            if (kind == CreatureKind.Clump) { ClumpLook.Build(root); return; }
            if (kind == CreatureKind.MothSwarm || kind == CreatureKind.MothMales) { MothFlock.Build(root, kind == CreatureKind.MothMales, kind == CreatureKind.MothMales ? 4 : 14); return; }
            if (kind == CreatureKind.Praetorian) { MothWalker.Build(root, "MothPraetorian", 0.59f); return; }
            if (kind == CreatureKind.MothEmpress) { MothWalker.Build(root, "MothEmpress", 0.55f); return; }
            if (kind == CreatureKind.MothHive) { root.gameObject.AddComponent<MothHiveCopy>(); return; }
            if (Creature.IsFaceling(kind)) { FacelingLook.Build(root, kind, Random.Range(1, 999999)); return; }
            if (kind == CreatureKind.SkinStealer) { SkinStealerLook.Build(root, -1); return; }
            if (kind == CreatureKind.Wretch || kind == CreatureKind.WretchMass) { WretchLook.Build(root, kind, 0); return; }
            if (kind == CreatureKind.PartyGoer) PartygoerLook.Build(root);
        }

        /// The player's hazmat suit as a disguise (the Skin-Stealer copying a co-op friend).
        public static GameObject HazmatDisguise(Transform root)
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
    }
}
