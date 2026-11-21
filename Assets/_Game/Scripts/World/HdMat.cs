using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.World
{
    /// HDRP materials made from code (editor setup and runtime alike), so every system builds them the same way.
    public static class HdMat
    {
        static Shader lit;
        static Shader Lit => lit != null ? lit : lit = Shader.Find("HDRP/Lit");

        /// A plain lit surface: colour, optional albedo / normal / mask maps (mask: R metal, G AO, A smoothness).
        public static Material Make(Color color, float smoothness = 0.3f, float metallic = 0f, Texture albedo = null, Texture normal = null,
                                    float normalScale = 1f, Texture mask = null, Vector2? tiling = null, Material reuse = null)
        {
            var m = reuse != null ? reuse : new Material(Lit);
            m.shader = Lit;
            Configure(m, color, smoothness, metallic, albedo, normal, normalScale, mask, tiling);
            return m;
        }

        public static void Configure(Material m, Color color, float smoothness, float metallic, Texture albedo, Texture normal,
                                     float normalScale, Texture mask, Vector2? tiling)
        {
            m.SetColor("_BaseColor", color);
            m.SetTexture("_BaseColorMap", albedo);
            m.SetTextureScale("_BaseColorMap", tiling ?? Vector2.one);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetTexture("_NormalMap", normal);
            m.SetFloat("_NormalScale", normalScale);
            m.SetTexture("_MaskMap", mask);
            if (mask != null)
            {
                // With a mask map, smoothness comes from its alpha, remapped into this range.
                m.SetFloat("_SmoothnessRemapMin", 0f);
                m.SetFloat("_SmoothnessRemapMax", Mathf.Clamp01(smoothness * 1.6f));
                m.SetFloat("_AORemapMin", 0f);
                m.SetFloat("_AORemapMax", 1f);
            }
            HDMaterial.ValidateMaterial(m);
        }

        /// Something that glows (light panels, LEDs, a lantern globe, TV static): `nits` is its brightness.
        public static Material Emissive(Color color, float nits, Texture map = null, Material reuse = null)
        {
            var m = reuse != null ? reuse : new Material(Lit);
            m.shader = Lit;
            m.SetColor("_BaseColor", Color.black);
            m.SetTexture("_BaseColorMap", null);
            m.SetFloat("_Smoothness", 0.4f);
            m.SetTexture("_EmissiveColorMap", map);
            HDMaterial.SetUseEmissiveIntensity(m, true);
            HDMaterial.SetEmissiveColor(m, new Color(color.r, color.g, color.b, 1f));
            HDMaterial.SetEmissiveIntensity(m, nits, UnityEditor_EmissiveUnit);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        const EmissiveIntensityUnit UnityEditor_EmissiveUnit = EmissiveIntensityUnit.Nits;

        /// See-through (water, glass): alpha from the colour.
        /// Renders both faces (thin walls, panes and blinds look right from either side).
        public static Material DoubleSided(Material m)
        {
            if (m == null || !m.HasProperty("_DoubleSidedEnable")) return m;
            m.SetFloat("_DoubleSidedEnable", 1f);
            if (m.HasProperty("_CullMode")) m.SetFloat("_CullMode", 0f);
            if (m.HasProperty("_CullModeForward")) m.SetFloat("_CullModeForward", 0f);
            m.doubleSidedGI = true;
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        public static Material Transparent(Material m)
        {
            HDMaterial.SetSurfaceType(m, true);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// The current glow of an emissive material (the property HDRP renders from), so it can be dimmed and restored.
        public const string EmissiveProperty = "_EmissiveColor";
    }
}
