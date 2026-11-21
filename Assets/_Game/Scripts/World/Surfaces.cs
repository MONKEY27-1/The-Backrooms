using System;
using UnityEngine;

namespace Backrooms.World
{
    /// Which material family a surface uses. The variant picks one of the theme's materials of that family.
    public enum Surface : byte { Wall, Floor, Ceiling, Trim, Manila, RedWall, RedFloor, Wood, Tile, Concrete, Void }

    public readonly struct SurfaceRef : IEquatable<SurfaceRef>
    {
        public readonly Surface Kind;
        public readonly byte Variant;

        public SurfaceRef(Surface kind, int variant = 0)
        {
            Kind = kind;
            Variant = (byte)variant;
        }

        public bool Equals(SurfaceRef o) => Kind == o.Kind && Variant == o.Variant;
        public override bool Equals(object o) => o is SurfaceRef s && Equals(s);
        public override int GetHashCode() => ((int)Kind << 8) | Variant;
    }

    /// An axis-aligned box with its own material per face. Only the faces in `Faces` are built.
    public struct SurfaceBox
    {
        public Bounds Box;
        public BoxMeshBuilder.Faces Faces;
        public SurfaceRef PosX, NegX, PosY, NegY, PosZ, NegZ;
        public bool Solid;   // collides and blocks reachability

        public static SurfaceBox Uniform(Bounds box, SurfaceRef s, BoxMeshBuilder.Faces faces, bool solid = true) =>
            new SurfaceBox { Box = box, Faces = faces, PosX = s, NegX = s, PosY = s, NegY = s, PosZ = s, NegZ = s, Solid = solid };
    }

    /// A box rotated around its vertical axis, used for individual spiral stair treads.
    public struct SurfaceOrientedBox
    {
        public Vector3 Center, Size;
        public float Yaw;
        public SurfaceRef Surface;
    }

    /// A sloped strip (a car ramp, the parapet along it): the top surface runs from A to B along its centre line,
    /// Width across. It is solid down to Bottom, or Thickness below the top when Thickness > 0 (a sloped wall).
    public struct SurfaceSlope
    {
        public Vector3 A, B;
        public float Width, Bottom, Thickness;
        public SurfaceRef Top, Side;
    }

    /// A horizontal rectangle (floor or ceiling) with optional 90-degree texture rotation.
    public struct SurfaceQuad
    {
        public Rect Area;       // x/z extents in world meters
        public float Height;
        public bool FacingUp;
        public bool Rotated;
        public SurfaceRef Surface;
        public bool Visual;     // drawn but not solid (the top side of a ceiling)
    }

    /// One ceiling light panel. Emitters also get a real-time light when the player is near.
    public struct PanelSpec
    {
        public Vector3 Position;
        public bool AlongX;     // long axis of the 0.5 x 1 m panel
        public FixtureState State;
        public bool Emitter;
        public float Range;
        public int Id;          // stable per world position; drives flicker timing
        public bool Red;        // Red Rooms tint
        public bool Sick;       // copy drift: greenish light
        public bool Floor;      // in the floor, shining up (the attic's glowing floorboards)
        public bool Bare;       // only a light: the fixture is drawn by something else (a lamp over an exit door)
        public Color Tint;      // light colour for bare lights (alpha 0 = the level's)
    }

    /// A spot where the adaptive encounter layer may place a curiosity. The layout only proposes the spot.
    public struct CuriositySlot
    {
        public int Id;              // stable per world position, so the layer can remember its choice
        public Matrix4x4 Pose;      // floor point; for wall slots local +z points away from the wall
        public bool OnWall;
        public bool BigRoom;        // enough space for large curiosities
        public float Ceiling;
    }
}
