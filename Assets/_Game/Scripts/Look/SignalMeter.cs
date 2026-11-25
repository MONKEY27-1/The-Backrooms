using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Look
{
    /// The camcorder's light meter: shrinks the lit scene (before exposure and colour grading, so its colour is the
    /// light's own) to a few hundred pixels that <see cref="Camcorder"/> reads back for auto exposure and white balance.
    /// Registered in HDRP Global Settings (Before Post Process).
    [Serializable, VolumeComponentMenu("Backrooms/Signal Meter")]
    public sealed class SignalMeter : CustomPostProcessVolumeComponent, IPostProcessComponent
    {
        public BoolParameter on = new BoolParameter(false);

        /// The last metered frame (about 30 x 17 pixels, pre-exposed scene colour), or null before the first.
        public static RenderTexture Result { get; private set; }

        const float LampCap = 6f;   // pre-exposed units; mid grey sits near 0.2
        Material material;
        RenderTexture a, b, c;
        MaterialPropertyBlock pa, pb, pc;

        public bool IsActive() => material != null && on.value;

        public override CustomPostProcessInjectionPoint injectionPoint => CustomPostProcessInjectionPoint.BeforePostProcess;

        public override void Setup()
        {
            var shader = Resources.Load<Shader>("Shaders/VhsSignal");
            if (shader != null) material = CoreUtils.CreateEngineMaterial(shader);
            pa = new MaterialPropertyBlock();
            pb = new MaterialPropertyBlock();
            pc = new MaterialPropertyBlock();
        }

        public override void Render(CommandBuffer cmd, HDCamera camera, RTHandle source, RTHandle destination)
        {
            int w = camera.actualWidth, h = camera.actualHeight;
            int aw = Mathf.Max(1, w / 4), ah = Mathf.Max(1, h / 4);
            int bw = Mathf.Max(1, aw / 4), bh = Mathf.Max(1, ah / 4);
            int cw = Mathf.Max(1, bw / 4), ch = Mathf.Max(1, bh / 4);
            Tape.Ensure(ref a, aw, ah, RenderTextureFormat.ARGBHalf, "Meter A");
            Tape.Ensure(ref b, bw, bh, RenderTextureFormat.ARGBHalf, "Meter B");
            Tape.Ensure(ref c, cw, ch, RenderTextureFormat.ARGBFloat, "Meter C");

            pa.SetTexture("_MainTex", source);
            pa.SetVector("_SrcSize", new Vector4(w, h, 1f / w, 1f / h));
            pa.SetFloat("_ClampMax", LampCap);
            CoreUtils.DrawFullScreen(cmd, material, a, pa, 0);
            pb.SetTexture("_SrcTex", a);
            pb.SetVector("_SrcSize", new Vector4(aw, ah, 1f / aw, 1f / ah));
            CoreUtils.DrawFullScreen(cmd, material, b, pb, 1);
            pc.SetTexture("_SrcTex", b);
            pc.SetVector("_SrcSize", new Vector4(bw, bh, 1f / bw, 1f / bh));
            CoreUtils.DrawFullScreen(cmd, material, c, pc, 1);
            Result = c;

            HDUtils.BlitCameraTexture(cmd, source, destination);
        }

        public override void Cleanup()
        {
            if (Result == c) Result = null;
            CoreUtils.Destroy(material);
            Tape.Release(ref a);
            Tape.Release(ref b);
            Tape.Release(ref c);
        }
    }
}
