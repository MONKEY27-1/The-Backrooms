using UnityEngine;

namespace Backrooms.World
{
    /// Level 5 "The Hotel" props, Blender-made (Art/Level5Props.blend -> Resources/Models/L5*), each with a blocky stand-in.
    public static partial class PropLibrary
    {
        static bool BuildLevel5(PropSpec prop, BoxMeshBuilder mesh, float ceiling)
        {
            var p = prop.Pose;
            void Box(Matrix4x4 m, PropPaint paint, float cx, float cy, float cz, float sx, float sy, float sz) =>
                mesh.AddOrientedBox(m, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz), (int)paint);
            switch (prop.Kind)
            {
                case PropKind.Chandelier:   // hangs from the ceiling above its floor point
                {
                    var at = p * Matrix4x4.Translate(new Vector3(0f, ceiling, 0f));
                    if (!ModelLibrary.Add(mesh, "L5Chandelier", at)) Box(at, PropPaint.Brass, 0, -0.8f, 0, 1.2f, 0.3f, 1.2f);
                    return true;
                }
                case PropKind.CageElevator:
                    if (!ModelLibrary.Add(mesh, "L5CageElevator", p)) Box(p, PropPaint.Brass, 0, 1.25f, 0.8f, 1.7f, 2.5f, 0.06f);
                    return true;
                case PropKind.Sconce:
                    if (!ModelLibrary.Add(mesh, "L5Sconce", p)) Box(p, PropPaint.Brass, 0, 1.85f, 0.1f, 0.3f, 0.2f, 0.2f);
                    return true;
                case PropKind.Gramophone:
                    if (!ModelLibrary.Add(mesh, "L5Gramophone", p)) Box(p, PropPaint.Mahogany, 0, 0.4f, 0, 0.5f, 0.8f, 0.45f);
                    return true;
                case PropKind.MahjongTable:
                    if (!ModelLibrary.Add(mesh, "L5MahjongTable", p)) Box(p, PropPaint.Green, 0, 0.76f, 0, 0.9f, 0.05f, 0.9f);
                    return true;
                case PropKind.HotelBed:   // headboard toward local -z (against the wall), foot toward +z
                    if (!ModelLibrary.Add(mesh, "L5Bed", p)) Box(p, PropPaint.White, 0, 0.4f, 0, 1.6f, 0.6f, 2f);
                    return true;
            }
            return false;
        }

        /// Small dark digits (an elevator button's floor number), centred at the pose; `scale` 1 = exit-plate size.
        public static void Label(BoxMeshBuilder mesh, Matrix4x4 p, string text, float scale) =>
            Digits(p * Matrix4x4.Scale(new Vector3(scale, scale, 1f)), text, Vector3.zero, mesh, PropPaint.Dark);

        /// A hotel room door's brass number plate (three digits), on the leaf at eye height.
        public static void RoomNumber(BoxMeshBuilder mesh, Matrix4x4 p, int number, float z)
        {
            string text = number.ToString();
            mesh.AddOrientedBox(p, new Vector3(0f, 1.62f, z), new Vector3(0.1f + 0.06f * text.Length, 0.12f, 0.006f), (int)PropPaint.Brass);
            Digits(p * Matrix4x4.Scale(new Vector3(0.45f, 0.45f, 1f)), text, new Vector3(0f, 1.62f / 0.45f, z + 0.005f), mesh, PropPaint.Dark);
        }
    }
}
