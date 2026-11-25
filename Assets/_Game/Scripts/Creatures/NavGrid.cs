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
        // Walls stand on the lines between cells, where the cell boxes never look: [i, j] is the line toward +x / +z.
        readonly bool[,] wallX = new bool[Size, Size], wallZ = new bool[Size, Size];
        readonly int[,] dist = new int[Size, Size];
        readonly Queue<Vector2Int> queue = new Queue<Vector2Int>();
        // Fields toward other goals than the grid's centre (a thrown light, a sound, someone else), made as they're asked for.
        readonly Dictionary<Vector2Int, int[,]> fields = new Dictionary<Vector2Int, int[,]>();
        const int MaxFields = 12;
        readonly Collider[] hits = new Collider[4];
        readonly Collider ignore;
        Vector2Int corner;   // world cell of grid index (0, 0)
        float baseY;         // floor height of the target (stacked floors)
        /// The floor slopes (Level 8's caves): each cell keeps its own height, walked out from the target.
        public bool Uneven;
        /// Feet follow the ground (also Level 9: pavements and floors a step above the road).
        public bool Stepped;
        readonly float[,] floor = new float[Size, Size];
        bool sloped;         // what the current field was built as

        public Vector2Int TargetCell { get; private set; } = new Vector2Int(int.MinValue, 0);

        /// `ignore`: the target's own collider (the player), which must not block the cells around it.
        public NavGrid(Collider ignore) => this.ignore = ignore;

        static Vector2Int CellOf(Vector3 p) => new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
        Vector3 CentreOf(Vector2Int c)
        {
            var i = c - corner;
            return new Vector3(c.x + 0.5f, sloped && Inside(i) ? floor[i.x, i.y] : baseY, c.y + 0.5f);
        }

        /// Rebuilds the field around `target`. ~2400 box queries; call only when the target changes cell.
        public void Build(Vector3 target)
        {
            TargetCell = CellOf(target);
            baseY = target.y;
            corner = TargetCell - new Vector2Int(Radius, Radius);
            sloped = Uneven;
            if (sloped) { WalkOut(); return; }
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
            for (int i = 0; i < Size; i++)
            for (int j = 0; j < Size; j++)
            {
                wallX[i, j] = i + 1 < Size && !blocked[i, j] && !blocked[i + 1, j] && WallBetween(i, j, i + 1, j);
                wallZ[i, j] = j + 1 < Size && !blocked[i, j] && !blocked[i, j + 1] && WallBetween(i, j, i, j + 1);
            }
            fields.Clear();
            Flood(new Vector2Int(Radius, Radius), dist);
        }

        /// Sloping ground: steps out cell by cell from the target, keeping to floor you can walk on from the cell before
        /// (tunnels cross over each other, so only the one you're in counts).
        void WalkOut()
        {
            for (int i = 0; i < Size; i++)
                for (int j = 0; j < Size; j++) { blocked[i, j] = true; dist[i, j] = Unreached; floor[i, j] = baseY; wallX[i, j] = wallZ[i, j] = false; }
            var mid = new Vector2Int(Radius, Radius);
            blocked[Radius, Radius] = false;
            queue.Clear();
            queue.Enqueue(mid);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    var n = c + Steps[k];
                    if (!Inside(n) || !blocked[n.x, n.y]) continue;
                    var w = corner + n;
                    var from = new Vector3(w.x + 0.5f, floor[c.x, c.y] + 1.3f, w.y + 0.5f);
                    var here = corner + c;
                    if (Physics.Linecast(new Vector3(here.x + 0.5f, from.y, here.y + 0.5f), from, out var rock, ~0, QueryTriggerInteraction.Ignore) && Players.Owns(rock.collider) == null) continue;   // rock is one-sided: from the air side only
                    if (!Physics.Raycast(from, Vector3.down, out var hit, 2.4f, ~0, QueryTriggerInteraction.Ignore) || hit.normal.y < 0.55f || Mathf.Abs(hit.point.y - floor[c.x, c.y]) > 1f) continue;
                    int found = Physics.OverlapSphereNonAlloc(hit.point + Vector3.up * 0.95f, 0.28f, hits, ~0, QueryTriggerInteraction.Ignore);
                    bool solid = false;
                    for (int h = 0; h < found; h++) if (Players.Owns(hits[h]) == null) solid = true;
                    if (solid) continue;
                    floor[n.x, n.y] = hit.point.y;
                    blocked[n.x, n.y] = false;
                    queue.Enqueue(n);
                }
            }
            for (int i = 0; i < Size; i++)
                for (int j = 0; j < Size; j++)
                {
                    if (blocked[i, j]) continue;
                    wallX[i, j] = i + 1 < Size && !blocked[i + 1, j] && (Mathf.Abs(floor[i, j] - floor[i + 1, j]) > 1f || WallBetween(i, j, i + 1, j));
                    wallZ[i, j] = j + 1 < Size && !blocked[i, j + 1] && (Mathf.Abs(floor[i, j] - floor[i, j + 1]) > 1f || WallBetween(i, j, i, j + 1));
                }
            fields.Clear();
            Flood(mid, dist);
        }

        bool WallBetween(int i, int j, int x, int y)
        {
            var a = CentreOf(corner + new Vector2Int(i, j)) + Vector3.up;
            var b = CentreOf(corner + new Vector2Int(x, y)) + Vector3.up;
            return Physics.Linecast(a, b, out var hit, ~0, QueryTriggerInteraction.Ignore) && hit.collider != ignore && Players.Owns(hit.collider) == null;
        }

        /// Can you step from this cell to its neighbour (no wall on the line, no corner cut)?
        bool Open(Vector2Int c, Vector2Int s)
        {
            if (s.x != 0 && s.y != 0)   // diagonally: both ways round the corner have to be open
                return Open(c, new Vector2Int(s.x, 0)) && Open(c, new Vector2Int(0, s.y))
                    && Open(new Vector2Int(c.x + s.x, c.y), new Vector2Int(0, s.y)) && Open(new Vector2Int(c.x, c.y + s.y), new Vector2Int(s.x, 0));
            var n = c + s;
            if (!Inside(n) || blocked[n.x, n.y] || blocked[c.x, c.y]) return false;
            return s.x > 0 ? !wallX[c.x, c.y] : s.x < 0 ? !wallX[n.x, n.y] : s.y > 0 ? !wallZ[c.x, c.y] : !wallZ[n.x, n.y];
        }

        void Flood(Vector2Int goal, int[,] dist)
        {
            dist[goal.x, goal.y] = 0;
            queue.Clear();
            queue.Enqueue(goal);
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var s in Steps)
                {
                    var n = c + s;
                    if (!Inside(n) || dist[n.x, n.y] != Unreached || !Open(c, s)) continue;
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

        int[,] FieldTo(Vector2Int goal)
        {
            if (goal == new Vector2Int(Radius, Radius)) return dist;
            if (fields.TryGetValue(goal, out var f)) return f;
            if (fields.Count >= MaxFields) fields.Clear();
            blocked[goal.x, goal.y] = false;
            f = new int[Size, Size];
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++) f[x, y] = Unreached;
            Flood(goal, f);
            return fields[goal] = f;
        }

        /// The point to walk toward next on the way to `target`. False: no known way (off the grid, or walled off),
        /// and `next` is just the straight line, so the caller must mind the walls itself.
        public bool NextWaypoint(Vector3 from, Vector3 target, out Vector3 next)
        {
            next = target;
            var i = CellOf(from) - corner;
            var g = CellOf(target) - corner;
            if (!Inside(i) || !Inside(g)) return false;
            if (i == g) return true;
            var dist = FieldTo(g);
            if (dist[i.x, i.y] == Unreached)
            {
                // Pressed into a cell that counts as solid (a doorway's edge): step to the best open neighbour.
                int low = Unreached;
                foreach (var s in Steps)
                {
                    var n = i + s;
                    if (Inside(n) && dist[n.x, n.y] < low) { low = dist[n.x, n.y]; next = CentreOf(corner + n); }
                }
                return low != Unreached;
            }
            if (dist[i.x, i.y] <= 1 && Open(i, g - i)) return true;
            var best = i;
            foreach (var s in Steps)
            {
                var n = i + s;
                if (!Inside(n) || dist[n.x, n.y] == Unreached || !Open(i, s)) continue;
                if (dist[n.x, n.y] < dist[best.x, best.y]) best = n;
            }
            next = CentreOf(corner + best);
            // Turning the end of a wall: round it by way of this cell's own middle.
            var eye = Vector3.up * (baseY + 1f);
            var a = sloped ? from + Vector3.up : new Vector3(from.x, 0f, from.z) + eye;
            var b = sloped ? next + Vector3.up : new Vector3(next.x, 0f, next.z) + eye;
            if (Physics.Linecast(a, b, out var hit, ~0, QueryTriggerInteraction.Ignore) && Players.Owns(hit.collider) == null)
                next = CentreOf(corner + i);
            return true;
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
