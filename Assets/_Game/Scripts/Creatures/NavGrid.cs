using Backrooms.Player;
using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.Creatures
{
    /// A flow field toward one target (the player), on a 1 m grid around it, built from physics queries.
    /// Works on any streamed geometry with no baking: every creature walks downhill on the distance field.
    public sealed class NavGrid
    {
        public const int Radius = 24;
        const int Size = Radius * 2 + 1;
        const int Unreached = int.MaxValue;
        static readonly Vector2Int[] Steps =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
            new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
        };

        readonly bool[,] blocked = new bool[Size, Size];
        readonly int[,] dist = new int[Size, Size];
        readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();
        readonly Collider[] hits = new Collider[4];
        readonly Collider ignore;
        Vector2Int corner;   // world cell of grid index (0, 0)
        float baseY;         // floor height of the target (stacked floors)

        public Vector2Int TargetCell { get; private set; } = new Vector2Int(int.MinValue, 0);

        /// `ignore`: the target's own collider (the player), which must not block the cells around it.
        public NavGrid(Collider ignore) => this.ignore = ignore;

        static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
        Vector3 CentreOf(Vector2Int c) => new Vector3(c.x + 0.5f, baseY, c.y + 0.5f);

        /// Rebuilds the field around `target`. ~2400 box queries; call only when the target changes cell.
        public void Build(Vector3 target)
        {
            TargetCell = CellOf(target);
            baseY = target.y;
            corner = TargetCell - new Vector2Int(Radius, Radius);
            var half = new Vector3(0.3f, 0.6f, 0.3f);
            for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
            {
                var c = CentreOf(corner + new Vector2Int(i, j));
                int n = Physics.OverlapBoxNonAlloc(c + Vector3.up * 0.9f, half, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                bool solid = false;
                for (int k = 0; k < n; k++) if (hits[k] != ignore && Players.Owns(hits[k]) == null) solid = true;   // players never block
                blocked[i, j] = solid;
                dist[i, j] = Unreached;
            }
            blocked[Radius, Radius] = false;   // the target's own cell (its collider is in it)
            dist[Radius, Radius] = 0;
            queue.Clear();
            queue.Enqueue(new Vector2Int(Radius, Radius));
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var s in Steps)
                {
                    var n = c + s;
                    if (!Inside(n) || blocked[n.x, n.y] || dist[n.x, n.y] != Unreached) continue;
                    if (s.x != 0 && s.y != 0 && (blocked[c.x + s.x, c.y] || blocked[c.x, c.y + s.y])) continue;   // no corner cutting
                    dist[n.x, n.y] = dist[c.x, c.y] + 1;
                    queue.Enqueue(n);
                }
            }
        }

        static bool Inside(Vector2Int i) => i.x >= 0 && i.y >= 0 && i.x < Size && i.y < Size;

        /// Whether there's a walkable path from `from` to the target within the grid.
        public bool Reachable(Vector3 from)
        {
            var i = CellOf(from) - corner;
            return Inside(i) && dist[i.x, i.y] != Unreached;
        }

        /// The point to walk toward next. Falls back to a straight line when off the field.
        public Vector3 NextWaypoint(Vector3 from, Vector3 target)
        {
            var i = CellOf(from) - corner;
            if (!Inside(i) || dist[i.x, i.y] == Unreached) return target;
            if (dist[i.x, i.y] <= 1) return target;
            var best = i;
            foreach (var s in Steps)
            {
                var n = i + s;
                if (!Inside(n) || dist[n.x, n.y] == Unreached) continue;
                if (s.x != 0 && s.y != 0 && (blocked[i.x + s.x, i.y] || blocked[i.x, i.y + s.y])) continue;
                if (dist[n.x, n.y] < dist[best.x, best.y]) best = n;
            }
            return CentreOf(corner + best);
        }

        /// A random open cell `minDist`..`maxDist` steps from the target, or null.
        public Vector3? RandomSpot(int minSteps, int maxSteps, System.Func<Vector3, bool> accept)
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                int i = Random.Range(0, Size), j = Random.Range(0, Size);
                int d = dist[i, j];
                if (d == Unreached || d < minSteps || d > maxSteps) continue;
                var p = CentreOf(corner + new Vector2Int(i, j));
                if (accept == null || accept(p)) return p;
            }
            return null;
        }
    }
}
