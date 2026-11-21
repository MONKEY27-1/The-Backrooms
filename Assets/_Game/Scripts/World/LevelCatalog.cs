using System.Collections.Generic;
using UnityEngine;

namespace Backrooms.World
{
    /// All levels plus the materials they share (props, panels, water, trims, area variants).
    [CreateAssetMenu(menuName = "Backrooms/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        public List<LevelTheme> levels = new List<LevelTheme>();

        [Tooltip("One per PropPaint, in enum order.")] public Material[] propPaints;
        public Material panelLit, panelDead, water, waterDeep, waterCaustics, waterCausticsDeep;
        [Tooltip("Level 0 areas: Red Rooms and the Manila Room.")] public Material redWall, redFloor, manilaWall;
        [Tooltip("Baseboards, chair rails, door casings: white paint, wood, beige.")] public Material[] trims;
        [Tooltip("Special floors for real places: wood (showrooms), tile (stores), concrete (storage).")]
        public Material woodFloor, tileFloor, concreteFloor;

        public LevelTheme Find(string id)
        {
            foreach (var level in levels)
                if (level != null && level.id == id) return level;
            return null;
        }

        /// The material for a surface reference in a given level.
        public Material Resolve(LevelTheme theme, SurfaceRef s)
        {
            Material Pick(Material[] list, Material fallback) =>
                list != null && list.Length > 0 ? list[s.Variant % list.Length] ?? fallback : fallback;
            switch (s.Kind)
            {
                case Surface.Wall: return Pick(theme.wallpapers, theme.wall);
                case Surface.Floor: return Pick(theme.carpets, theme.floor);
                case Surface.Ceiling: return Pick(theme.ceilings, theme.ceiling);
                case Surface.Trim: return Pick(trims, theme.wall);
                case Surface.Manila: return manilaWall;
                case Surface.RedWall: return redWall;
                case Surface.RedFloor: return redFloor;
                case Surface.Wood: return woodFloor != null ? woodFloor : theme.floor;
                case Surface.Tile: return tileFloor != null ? tileFloor : theme.floor;
                case Surface.Concrete: return concreteFloor != null ? concreteFloor : theme.floor;
                case Surface.Void: return propPaints != null && propPaints.Length > (int)PropPaint.Void && propPaints[(int)PropPaint.Void] != null ? propPaints[(int)PropPaint.Void] : theme.wall;
                default: return theme.wall;
            }
        }
    }
}
