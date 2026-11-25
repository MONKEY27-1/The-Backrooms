using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Backrooms.Creatures
{
    /// Counter damage is persistent for this encounter. These bits also cross co-op in CreatureProxy,
    /// so a guest sees the same burns, chemical holes and smashed limbs as the authority.
    public enum CreatureWoundType { Burn = 1, Chemical = 2, Door = 4, Repellent = 8 }

    public sealed class CreatureWounds : MonoBehaviour
    {
        public int Mask { get; private set; }
        int applied = -1;
        int attachmentsApplied;
        int visualMask;
        readonly List<Material> owned = new List<Material>();
        readonly List<GameObject> attachments = new List<GameObject>();

        void OnEnable()
        {
            if (applied >= 0) Apply(Mask);
        }

        public void Add(CreatureWoundType wound)
        {
            int next = Mask | (int)wound;
            if (next == Mask) return;
            Mask = next;
            Apply(Mask);
        }

        public void Apply(int mask)
        {
            Mask = mask;
            if (applied == mask || !isActiveAndEnabled) return;
            int previous = applied;
            int added = previous < 0 ? mask : mask & ~previous;
            applied = mask;
            int toDamage = mask & ~visualMask;
            if (toDamage != 0)
            {
                visualMask |= toDamage;
                DamageMaterials(toDamage);
            }
            if ((added & (int)CreatureWoundType.Burn) != 0 && (attachmentsApplied & (int)CreatureWoundType.Burn) == 0)
            {
                attachmentsApplied |= (int)CreatureWoundType.Burn;
                var flames = PartygoerLook.Flames(transform, 1.1f, 120f, 850f);
                foreach (var particle in flames.GetComponentsInChildren<ParticleSystem>())
                {
                    var emission = particle.emission;
                    emission.rateOverTime = 120f;
                }
                attachments.Add(flames);
            }
            if ((added & (int)CreatureWoundType.Chemical) != 0) WoundPiece("GoreBelly", 1.35f, 0.72f);
            if ((added & (int)CreatureWoundType.Door) != 0) WoundPiece("GoreStump", 1.0f, 0.8f);
            if ((added & (int)CreatureWoundType.Repellent) != 0) WoundPiece("GoreChunkB", 1.05f, 0.45f);
        }

        /// A visible wound belongs to this creature, not to a global material shared by its whole species.
        void DamageMaterials(int mask)
        {
            bool burned = (mask & (int)CreatureWoundType.Burn) != 0;
            float darken = burned ? 0.27f : 0.72f;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer) continue;
                var source = renderer.sharedMaterials;
                if (source == null || source.Length == 0) continue;
                var changed = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                {
                    if (source[i] == null) continue;
                    var mat = new Material(source[i]) { name = source[i].name + " (counter wound)" };
                    if (mat.HasProperty("_BaseColor"))
                    {
                        var c = mat.GetColor("_BaseColor");
                        c.r *= darken; c.g *= burned ? darken * 0.7f : darken; c.b *= burned ? darken * 0.55f : darken;
                        mat.SetColor("_BaseColor", c);
                    }
                    if (burned && mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.13f);
                    changed[i] = mat;
                    owned.Add(mat);
                }
                renderer.sharedMaterials = changed;
            }
        }

        void WoundPiece(string piece, float height, float scale)
        {
            // Don't place the same attachment twice if more than one counter lands.
            int bit = piece == "GoreBelly" ? (int)CreatureWoundType.Chemical : piece == "GoreStump" ? (int)CreatureWoundType.Door : (int)CreatureWoundType.Repellent;
            if ((attachmentsApplied & bit) != 0) return;
            attachmentsApplied |= bit;
            var side = (Mask & 1) == 0 ? -1f : 1f;
            var at = transform.TransformPoint(new Vector3(0.2f * side, height, 0.18f));
            var wound = GoreKit.CreatePiece(piece, at, transform.rotation * Quaternion.Euler(15f, 0f, 70f), transform, scale);
            if (wound != null) attachments.Add(wound);
        }

        void OnDestroy()
        {
            foreach (var mat in owned) if (mat != null) Destroy(mat);
            owned.Clear();
        }
    }
}
