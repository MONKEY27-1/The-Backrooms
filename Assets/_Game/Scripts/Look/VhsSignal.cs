using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Look
{
    /// The tape: every frame goes through a 90s camcorder's VHS signal (soft luma, smeared late chroma, edge halo,
    /// line wobble, head-switch tear, dropouts; a tracking band and snow when the signal is damaged).
    /// Registered in HDRP Global Settings (After Post Process). Driven by <see cref="Camcorder"/>.
    [Serializable, VolumeComponentMenu("Backrooms/VHS Signal")]
    public sealed class VhsSignal : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter damage = new ClampedFloatParameter(0f, 0f, 1f);
        /// Share of the screen width that shows picture: 1 = full, 0.75 = 4:3 on a 16:9 screen.
        public ClampedFloatParameter visibleWidth = new ClampedFloatParameter(1f, 0.3f, 1f);
        public ClampedFloatParameter trackingPosition = new ClampedFloatParameter(0f, 0f, 1f);
        public BoolParameter reduceFlashing = new BoolParameter(false);

        const int TapeLines = 576;   // PAL's line count: 480 read too blurry on a 1080p screen
        Material material;
        RenderTexture tape;
        MaterialPropertyBlock down, composite;

        public bool IsActive() => material != null && (intensity.value > 0f || damage.value > 0f || visibleWidth.value < 1f);

        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.AfterPostProcess;

        public override void Setup()
        {
            var shader = Resources.Load<Shader>("Shaders/VhsSignal");
            if (shader != null) material = CoreUtils.CreateEngineMaterial(shader);
            down = new MaterialPropertyBlock();
            composite = new MaterialPropertyBlock();
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            int w = camera.actualWidth, h = camera.actualHeight;
            int tw = Mathf.Max(1, Mathf.RoundToInt(TapeLines * (float)w / h));
            Tape.Ensure(ref tape, tw, TapeLines, RenderTextureFormat.ARGBHalf, "VHS Tape");

            down.SetTexture("_MainTex", source);
            down.SetVector("_SrcSize", new Vector4(w, h, 1f / w, 1f / h));
            down.SetFloat("_ClampMax", 65000f);
            CoreUtils.DrawFullScreen(cmd, material, tape, down, 0);

            composite.SetTexture("_LowTex", tape);
            composite.SetVector("_LowSize", new Vector4(tw, TapeLines, 1f / tw, 1f / TapeLines));
            float t = Time.unscaledTime;
            composite.SetVector("_Vhs", new Vector4(intensity.value, damage.value, Mathf.Floor(t * 59.94f) % 10000f, visibleWidth.value));
            composite.SetVector("_Vhs2", new Vector4(trackingPosition.value, t % 1000f, reduceFlashing.value ? 1f : 0f, 0f));
            HDUtils.DrawFullScreen(cmd, material, destination, composite, 2);
        }

        public override void Cleanup()
        {
            CoreUtils.Destroy(material);
            Tape.Release(ref tape);
        }
    }

    /// Small render-texture helpers shared by the tape and the light meter.
    static class Tape
    {
        public static void Ensure(ref RenderTexture rt, int w, int h, RenderTextureFormat format, string name)
        {
            if (rt != null && rt.width == w && rt.height == h) return;
            Release(ref rt);
            rt = new RenderTexture(w, h, 0, format) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            rt.Create();
        }

        public static void Release(ref RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            CoreUtils.Destroy(rt);
            rt = null;
        }
    }
}
