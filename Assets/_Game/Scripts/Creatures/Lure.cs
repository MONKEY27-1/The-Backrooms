using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// A light on the floor that draws Smilers (a thrown glowstick). Lives until destroyed.
    public sealed class Lure : MonoBehaviour
    {
        static readonly List<Lure> all = new List<Lure>();

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        /// The nearest lure within range that has a clear line from `from` (roughly head height).
        public static Lure Nearest(Vector3 from, float range)
        {
            Lure best = null;
            float bestD = range;
            var eye = from + Vector3.up * 1.8f;
            foreach (var l in all)
            {
                if (l == null) continue;
                var p = l.transform.position + Vector3.up * 0.15f;
                float d = Vector3.Distance(from, p);
                if (d >= bestD) continue;
                if (Physics.Linecast(eye, p, out var hit, ~0, QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(l.transform)) continue;
                best = l; bestD = d;
            }
            return best;
        }
    }
}
