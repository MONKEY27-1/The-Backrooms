using System;
using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// Models made in Blender (Resources/Models/*.fbx, source in Art/Stage4Props.blend). Each Blender material is
    /// named after a PropPaint (Wood, Metal, Fabric, Blue, Dark, Glow, Red...), so a model drops straight into the
    /// generated prop meshes with the game's own materials.
    public static class ModelLibrary
    {
        sealed class Model { public Mesh Mesh; public int[] Paint; public string[] Names; }
        static readonly Dictionary<string, Model> cache = new Dictionary<string, Model>();

        static Model Get(string name)
        {
            if (cache.TryGetValue(name, out var m)) return m;
            var go = Resources.Load<GameObject>("Models/" + name);
            var filter = go != null ? go.GetComponentInChildren<MeshFilter>() : null;
            if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
            {
#if !UNITY_EDITOR
                cache[name] = null;   // in the editor a model can still be mid-import; try again next time
#endif
                return null;
            }
            var mats = go.GetComponentInChildren<MeshRenderer>().sharedMaterials;
            var paint = new int[filter.sharedMesh.subMeshCount];
            var names = new string[paint.Length];
            for (int i = 0; i < paint.Length; i++)
            {
                string matName = i < mats.Length && mats[i] != null ? mats[i].name : "Wood";
                int dot = matName.IndexOf('.');   // Blender duplicates: "Metal.001"
                if (dot > 0) matName = matName.Substring(0, dot);
                names[i] = matName;
                if (matName == "Trim") matName = "White";
                if (matName == "PanelGlow") matName = "Glow";
                paint[i] = Enum.TryParse(matName, true, out PropPaint p) ? (int)p : (int)PropPaint.Wood;
            }
            m = new Model { Mesh = filter.sharedMesh, Paint = paint, Names = names };
            cache[name] = m;
            return m;
        }

        public static bool Has(string name) => Get(name) != null;

        /// Adds model `name` at `pose` (its own origin on the floor) into `mesh`. False if the model isn't there.
        public static bool Add(BoxMeshBuilder mesh, string name, Matrix4x4 pose)
        {
            var m = Get(name);
            if (m == null) return false;
            mesh.AddMesh(m.Mesh, pose, m.Paint);
            return true;
        }

        /// Adds a model with your own submesh mapping by Blender material name (-1 leaves that part out).
        public static bool AddMapped(BoxMeshBuilder mesh, string name, Matrix4x4 pose, Func<string, int> map)
        {
            var m = Get(name);
            if (m == null) return false;
            var sub = new int[m.Names.Length];
            for (int i = 0; i < sub.Length; i++) sub[i] = map(m.Names[i]);
            mesh.AddMesh(m.Mesh, pose, sub);
            return true;
        }

        public static PropPaint CapPaint(int variant) =>
            (variant & 3) == 0 ? PropPaint.Metal : (variant & 3) == 1 ? PropPaint.Green : (variant & 3) == 2 ? PropPaint.Red : PropPaint.Blue;

        static int PaintOf(string material) => Enum.TryParse(material, true, out PropPaint p) ? (int)p : (int)PropPaint.Wood;

        /// A model as its own mesh (built once, cached), with submeshes in the model's material order; Names gives them.
        public static Mesh Standalone(string name, out string[] materialNames)
        {
            var m = Get(name);
            materialNames = m?.Names;
            if (m == null) return null;
            if (standalone.TryGetValue(name, out var mesh) && mesh != null) return mesh;   // meshes made in Play die when it stops
            var b = new BoxMeshBuilder(Vector3.zero, m.Names.Length);
            var identity = new int[m.Names.Length];
            for (int i = 0; i < identity.Length; i++) identity[i] = i;
            b.AddMesh(m.Mesh, Matrix4x4.identity, identity);
            return standalone[name] = b.ToMesh(name);
        }
        static readonly Dictionary<string, Mesh> standalone = new Dictionary<string, Mesh>();

        /// The Blender-made version of a prop, if there is one.
        public static bool TryBuild(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            switch (prop.Kind)
            {
                case PropKind.Outlet: return Add(mesh, "OutletPlate", prop.Pose * Matrix4x4.Translate(new Vector3(0f, 0.3f, 0f)));
                case PropKind.LightSwitch: return Add(mesh, "SwitchPlate", prop.Pose * Matrix4x4.Translate(new Vector3(0f, 1.2f, 0f)));
                case PropKind.Vent: return Add(mesh, "WallVent", prop.Pose * Matrix4x4.Translate(new Vector3(0f, prop.Variant % 2 == 0 ? ceiling - 0.35f : 0.25f, 0f)));
                case PropKind.DoorToNowhere: return Add(mesh, "PanelDoor", prop.Pose);
                case PropKind.SleepingBag: return Add(mesh, "SleepingBag", prop.Pose);
                case PropKind.Lantern: return Add(mesh, "Lantern", prop.Pose);
                case PropKind.AlmondWater:   // one to three dented thermoses; caps grey, green, red or blue
                    if (!Has("AlmondWater")) return false;
                    for (int i = 0; i <= prop.Variant % 3; i++)
                    {
                        int cap = (int)CapPaint(prop.Variant + i);
                        var pose = prop.Pose * Matrix4x4.TRS(new Vector3(0.13f * i, 0f, 0.06f * (i % 2)), Quaternion.Euler(0f, 25f * i, 0f), Vector3.one);
                        AddMapped(mesh, "AlmondWater", pose, n => n == "Green" ? cap : PaintOf(n));
                    }
                    return true;
                case PropKind.Battery:
                    if (!Has("Battery")) return false;
                    for (int i = 0; i <= prop.Variant % 2; i++) Add(mesh, "Battery", prop.Pose * Matrix4x4.Translate(new Vector3(0.05f * i, 0f, 0f)));
                    return true;
                case PropKind.Can: return Add(mesh, "Can", prop.Pose * (prop.Variant % 2 == 1 ? Matrix4x4.TRS(new Vector3(0f, 0.037f, 0f), Quaternion.Euler(0f, 0f, 90f), Vector3.one) : Matrix4x4.Translate(new Vector3(0f, 0.055f, 0f))));   // the model's origin is its middle
                default: return false;
            }
        }
    }
}
