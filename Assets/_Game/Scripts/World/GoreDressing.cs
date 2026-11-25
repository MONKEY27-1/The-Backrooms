using System.Collections.Generic;
using Backrooms.Creatures;
using UnityEngine;

namespace Backrooms.World
{
    /// Procedural monster nests, drag marks, and found victims (GAME_PLAN §24, Stage C).
    /// Adults only: child Facelings are strictly excluded.
    public static class GoreDressing
    {
        public static void Decorate(Transform parent, ChunkLayout layout, Vector3 origin, LevelTheme theme, uint seed, Vector3Int key)
        {
            if (parent == null || layout == null || theme == null) return;
            string id = theme.id ?? "";
            uint h = WorldHash.Hash(seed, key.x, key.z, (int)WorldHash.Hash((uint)key.y, 0x608E));
            float roll = (h & 0xFFFF) / 65535f;

            // Frequency per chunk based on level danger / creature presence
            float chance = id == "0" ? 0.05f
                : id == "1" ? 0.09f
                : id == "2" ? 0.12f
                : id == "3" ? 0.14f
                : id == "5" ? 0.12f
                : id == "8" ? 0.16f
                : id == "fun" ? 0.35f : 0.07f;

            if (roll >= chance) return;

            // Pick nest / scene type according to the level's dominant entity
            string nestType = "Hound";
            if (id == "5") nestType = "Deathmoth";
            else if (id == "fun") nestType = "Partygoer";
            else if (id == "1" && roll < chance * 0.5f) nestType = "SkinStealer";
            else if (id == "2" || id == "8") nestType = "Wretch";
            else if (id == "0") nestType = roll < chance * 0.4f ? "Wanderer" : "Hound";

            if (nestType == "Deathmoth")
            {
                // Cocoon stuck to a wall with trapped human arm
                if (TryFindWall(layout, origin, out var wallPos, out var wallNormal))
                {
                    Quaternion rot = Quaternion.LookRotation(wallNormal, Vector3.up);
                    var cocoon = GoreKit.CreatePiece("GoreCocoon", wallPos + Vector3.up * 0.1f, rot, parent, 1f);
                    Blood.Burst(wallPos);
                    // Acid & silk drips
                    AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Drip(), wallPos, 0.4f * Meta.AudioMix.Effects);
                }
            }
            else if (nestType == "SkinStealer")
            {
                // Flayed skin hung on wall, discarded clothes, skull on floor
                if (TryFindWall(layout, origin, out var wallPos, out var wallNormal))
                {
                    Quaternion rot = Quaternion.LookRotation(wallNormal, Vector3.up);
                    GoreKit.CreatePiece("GoreHide", wallPos + Vector3.up * 1.6f, rot, parent, 1f);
                    Vector3 floorPos = wallPos + wallNormal * 0.5f;
                    floorPos.y = wallPos.y;
                    GoreKit.CreatePiece("GoreSkull", floorPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent, 1f);
                    Blood.Burst(floorPos);
                    Blood.Drag(floorPos + wallNormal * 2.5f, floorPos, 0.5f);
                }
            }
            else if (nestType == "Partygoer")
            {
                // Slumped victim against wall, smiling carved face, open belly, confetti/cake gore
                if (TryFindWall(layout, origin, out var wallPos, out var wallNormal))
                {
                    Vector3 floorPos = wallPos + wallNormal * 0.45f;
                    Quaternion rot = Quaternion.LookRotation(-wallNormal, Vector3.up);
                    GoreKit.SpawnVictim(floorPos, rot, 0, "Partygoer");
                    Blood.Burst(floorPos);
                }
            }
            else if (nestType == "Hound")
            {
                // Hound feeding den: picked ribs, gnawed arm, drag mark pulling victim into dark
                if (TryFindFloor(layout, origin, out var floorPos))
                {
                    Vector3 denCorner = floorPos;
                    Vector3 dragStart = floorPos + Random.onUnitSphere * 4f;
                    dragStart.y = floorPos.y;

                    GoreKit.CreatePiece("GoreRibs", denCorner, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), parent, 1f);
                    GoreKit.CreatePiece("GoreArm", denCorner + Random.insideUnitSphere * 0.4f, Quaternion.Euler(0f, Random.Range(0f, 360f), 90f), parent, 1f);
                    GoreKit.DropMeatChunks(denCorner + Vector3.up * 0.2f, 3, 1.5f);
                    Blood.Burst(denCorner);
                    Blood.Drag(dragStart, denCorner, 0.55f);
                }
            }
            else
            {
                // Wretch / Wanderer gore splatter: opened belly, meat chunks, spine
                if (TryFindFloor(layout, origin, out var floorPos))
                {
                    GoreKit.SpawnVictim(floorPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Random.Range(0, 4), "Wretch");
                    GoreKit.DropMeatChunks(floorPos + Vector3.up * 0.2f, 4, 2f);
                    Blood.Burst(floorPos);
                }
            }
        }

        static bool TryFindWall(ChunkLayout layout, Vector3 origin, out Vector3 at, out Vector3 normal)
        {
            at = Vector3.zero;
            normal = Vector3.forward;
            foreach (var b in layout.Boxes)
            {
                if (!b.Solid || b.Box.size.y < 1.8f) continue;
                Vector3 center = b.Box.center;
                Vector3 extents = b.Box.extents;
                // Pick a wall face
                normal = extents.x < extents.z ? Vector3.right : Vector3.forward;
                at = center + normal * (Mathf.Abs(Vector3.Dot(normal, extents)) + 0.02f);
                at.y = b.Box.min.y;
                return true;
            }
            return false;
        }

        static bool TryFindFloor(ChunkLayout layout, Vector3 origin, out Vector3 at)
        {
            at = Vector3.zero;
            foreach (var q in layout.Quads)
            {
                if (!q.FacingUp || q.Visual) continue;
                at = new Vector3(q.Area.center.x, q.Height + 0.02f, q.Area.center.y);
                return true;
            }
            return false;
        }
    }
}
