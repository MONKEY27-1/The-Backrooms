using Backrooms.Creatures;
using Backrooms.Player;
using Backrooms.World;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Look
{
    /// The game is the tape. One object for the whole run that turns every camera into a cheap 90s camcorder:
    /// the VHS signal, an auto exposure and white balance that lag and overshoot like a real one, signal damage
    /// when danger is close, and the sway of a camera held in your hands.
    public sealed class Camcorder : MonoBehaviour
    {
        public static Camcorder Instance { get; private set; }

        /// A short knock to the signal (a level change, a scare). Fades over a couple of seconds.
        public static void Hit(float amount)
        {
            if (Instance != null) Instance.hit = Mathf.Clamp01(Instance.hit + amount);
        }

        /// 0 = clean tape, 1 = the signal is dying. Read by anything that wants to react to it.
        public static float Damage => Instance != null ? Instance.damage : 0f;

        // Exposure: the exposed scene's (log) average lands here. Higher reads brighter.
        const float MidGrey = 0.25f, EvMin = 3.4f, EvMax = 10f, EvDeadZone = 0.3f;
        // White balance: how much of the light's colour cast the camera takes out (the rest stays: Level 0 is yellow).
        const float WbCorrection = 0.1f, BaseTint = 4f;

        Volume volume;
        VhsSignal vhs;
        SignalMeter meter;
        Exposure exposure;
        WhiteBalance whiteBalance;

        float ev = 7f, evVel, evGoal = float.NaN, evAtRequest;
        float temp, tempVel, tempGoal, tint, tintVel, tintGoal;
        bool reading, metered;
        float hit, damage, band;

        FearDirector director;
        ChunkStreamer streamer;
        float nextLookup;

        Quaternion written = Quaternion.identity, offset = Quaternion.identity;
        Transform swayed;
        float swayTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("Camcorder");
            DontDestroyOnLoad(go);
            go.AddComponent<Camcorder>();
        }

        void Awake()
        {
            Instance = this;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vhs = profile.Add<VhsSignal>(true);
            meter = profile.Add<SignalMeter>(true);
            meter.on.Override(true);
            exposure = profile.Add<Exposure>(true);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(ev);
            whiteBalance = profile.Add<WhiteBalance>(true);
            whiteBalance.tint.Override(BaseTint);
            // A video sensor's response, not film's: a short toe and a highlight shoulder that clips fairly hard
            // (lamps and lit ceilings burn out white), with the bloom of a cheap lens smearing round them.
            var curve = profile.Add<Tonemapping>(true);
            curve.mode.Override(TonemappingMode.Custom);
            curve.toeStrength.Override(0.3f);
            curve.toeLength.Override(0.4f);
            curve.shoulderStrength.Override(0.7f);
            curve.shoulderLength.Override(2.5f);
            curve.shoulderAngle.Override(0.1f);
            curve.gamma.Override(1f);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.12f);
            bloom.scatter.Override(0.8f);
            bloom.threshold.Override(1f);
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 3f;   // over the level's own volumes; under madness and night vision
            volume.sharedProfile = profile;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            var s = Meta.GameSettings.Current;
            var cam = Camera.main;

            Meter();
            AutoExposure(dt, s.effects);
            Damaged(dt, cam);

            vhs.intensity.Override(Mathf.Clamp01(s.effects));
            vhs.damage.Override(damage * Mathf.Lerp(0.5f, 1f, s.effects));
            vhs.reduceFlashing.Override(s.reduceFlashing);
            band = Mathf.Repeat(band + dt * (0.04f + 0.35f * damage), 1f);
            vhs.trackingPosition.Override(band);
            vhs.visibleWidth.Override(cam != null && s.aspect == 1 ? Mathf.Min(1f, (4f / 3f) / cam.aspect) : 1f);

            Handheld(dt, cam, s);
            if (cam != null && !cam.allowDynamicResolution)
            {
                cam.allowDynamicResolution = true;   // HDRP wants both flags; the HDRP asset forces 75% with STP
                if (cam.TryGetComponent(out HDAdditionalCameraData hd)) hd.allowDynamicResolution = true;
            }
        }

        /// The share of the screen width the picture fills (for the viewfinder overlay).
        public float VisibleWidth => vhs != null ? vhs.visibleWidth.value : 1f;

        // ---------- the light meter ----------

        void Meter()
        {
            var rt = SignalMeter.Result;
            if (reading || rt == null) return;
            reading = true;
            evAtRequest = ev;
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBAFloat, OnMeter);
        }

        void OnMeter(AsyncGPUReadbackRequest request)
        {
            reading = false;
            if (this == null || request.hasError) return;
            NativeArray<Vector4> data = request.GetData<Vector4>();
            int w = request.width, h = request.height;
            double logSum = 0, weightSum = 0, r = 0, g = 0, b = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = data[y * w + x];
                    if (float.IsNaN(c.x) || float.IsNaN(c.y) || float.IsNaN(c.z)) continue;
                    // Centre-weighted, like a camcorder's meter.
                    float dx = (x + 0.5f) / w - 0.5f, dy = (y + 0.5f) / h - 0.5f;
                    float wgt = 0.25f + Mathf.Exp(-(dx * dx + dy * dy) / 0.06f);
                    float lum = Mathf.Max(1e-4f, 0.2126f * c.x + 0.7152f * c.y + 0.0722f * c.z);
                    logSum += wgt * Mathf.Log(lum, 2f);
                    r += wgt * c.x; g += wgt * c.y; b += wgt * c.z;
                    weightSum += wgt;
                }
            if (weightSum <= 0) return;
            // What the scene measures before any exposure, then the exposure that brings it to mid grey.
            float exposed = (float)(logSum / weightSum);
            float scene = exposed + Mathf.Log(1.2f, 2f) + evAtRequest;        // HDRP: exposure = 1 / (1.2 * 2^EV100)
            float goal = Mathf.Clamp(scene - Mathf.Log(1.2f * MidGrey, 2f), EvMin, EvMax);
            if (!metered || float.IsNaN(evGoal) || Mathf.Abs(goal - evGoal) > EvDeadZone) evGoal = goal;   // it ignores small changes
            if (!metered) { ev = goal; evVel = 0f; }                            // first frame: start where it should be
            float rr = (float)(r / weightSum), gg = (float)(g / weightSum), bb = (float)(b / weightSum);
            if (rr > 1e-4f && gg > 1e-4f && bb > 1e-4f)
            {
                tempGoal = Mathf.Clamp(-WbCorrection * 60f * Mathf.Log(rr / bb, 2f), -15f, 15f);
                tintGoal = Mathf.Clamp(WbCorrection * 60f * Mathf.Log(gg / Mathf.Sqrt(rr * bb), 2f), -10f, 10f);
                if (!metered) { temp = tempGoal; tint = tintGoal; }
            }
            metered = true;
        }

        /// Springs, so the camera overshoots and settles; with the effects slider down they're critically damped.
        void AutoExposure(float dt, float effects)
        {
            if (!metered) return;
            float hunt = Mathf.Clamp01(effects);
            Spring(ref ev, ref evVel, evGoal, 2.6f, Mathf.Lerp(1f, 0.45f, hunt), dt);
            ev = Mathf.Clamp(ev, EvMin - 0.5f, EvMax + 0.5f);
            Spring(ref temp, ref tempVel, tempGoal, 1.1f, Mathf.Lerp(1f, 0.4f, hunt), dt);
            Spring(ref tint, ref tintVel, tintGoal, 1.1f, Mathf.Lerp(1f, 0.4f, hunt), dt);
            exposure.fixedExposure.Override(ev);
            whiteBalance.temperature.Override(temp);
            whiteBalance.tint.Override(BaseTint + tint);
        }

        static void Spring(ref float x, ref float v, float goal, float omega, float zeta, float dt)
        {
            v += (omega * omega * (goal - x) - 2f * zeta * omega * v) * dt;
            x += v * dt;
        }

        // ---------- signal damage ----------

        void Damaged(float dt, Camera cam)
        {
            if (Time.unscaledTime > nextLookup)
            {
                nextLookup = Time.unscaledTime + 1f;
                if (director == null) director = FindFirstObjectByType<FearDirector>();
                if (streamer == null) streamer = FindFirstObjectByType<ChunkStreamer>();
            }
            float threat = 0f;
            if (director != null && cam != null)
                foreach (var c in director.Creatures)
                {
                    if (c == null || c.State == CreatureState.Leave || c.State == CreatureState.Numb || (c.Kind == CreatureKind.Wretch && c.State == CreatureState.Lurk)) continue;   // a Wretch playing dead gives nothing away
                    float near = Mathf.InverseLerp(25f, 4f, Vector3.Distance(c.transform.position, cam.transform.position));
                    threat = Mathf.Max(threat, near * (c.State == CreatureState.Chase ? 1f : 0.6f));
                }
            if (FirstPersonController.Chased) threat = Mathf.Max(threat, 0.5f);
            var status = cam != null ? cam.GetComponentInParent<PlayerStatus>() : null;
            float mind = status != null ? Mathf.InverseLerp(0.5f, 0.05f, status.Sanity) * 0.7f : 0f;
            float drift = streamer != null ? Mathf.InverseLerp(0.3f, 1f, streamer.CurrentDrift) * 0.4f : 0f;
            float dark = streamer != null ? streamer.Blackout * 0.15f : 0f;
            float strongest = Mathf.Max(threat, Mathf.Max(mind, drift));
            // Danger alone tops out at 0.75 (tracking band, wobble, dropouts: still playable); only hits reach the snow.
            float danger = Mathf.Min(0.75f, strongest + 0.35f * (threat + mind + drift - strongest) + dark);
            float goal = Mathf.Clamp01(danger + hit);
            damage = Mathf.MoveTowards(damage, goal, dt * (goal > damage ? 3f : 0.6f));
            hit = Mathf.MoveTowards(hit, 0f, dt * 0.6f);
        }

        // ---------- handheld ----------

        /// Small rotations on top of wherever the game points the camera. If nobody rewrote the camera since our
        /// last frame we take our own offset back out first, so it never builds up.
        void Handheld(float dt, Camera cam, Meta.SettingsData s)
        {
            var t = cam != null ? cam.transform : null;
            if (t != swayed) { swayed = t; written = t != null ? t.localRotation : Quaternion.identity; offset = Quaternion.identity; }
            if (t == null) return;
            var baseRot = t.localRotation == written ? written * Quaternion.Inverse(offset) : t.localRotation;

            swayTime += dt;
            var body = t.GetComponentInParent<CharacterController>();
            var status = t.GetComponentInParent<PlayerStatus>();
            float speed = body != null ? new Vector2(body.velocity.x, body.velocity.z).magnitude : 0f;
            float winded = status != null ? 1f - status.Stamina : 0f;
            float amount = Mathf.Clamp01(s.handheld);

            float T = swayTime;
            float slow = 1f + speed * 0.25f;
            float pitch = (Mathf.PerlinNoise(T * 0.31f, 1.7f) - 0.5f) * 0.7f * slow
                        + Mathf.Sin(T * Mathf.PI * 2f * Mathf.Lerp(0.22f, 0.55f, winded)) * (0.12f + 0.5f * winded);   // breathing
            float yaw = (Mathf.PerlinNoise(T * 0.27f, 5.3f) - 0.5f) * 0.9f * slow;
            float roll = (Mathf.PerlinNoise(T * 0.23f, 9.1f) - 0.5f) * 1.2f * slow;
            // Fear: the hands shake.
            float shake = damage * damage * 0.5f;
            pitch += (Mathf.PerlinNoise(T * 9f, 2.2f) - 0.5f) * shake;
            yaw += (Mathf.PerlinNoise(T * 8f, 7.7f) - 0.5f) * shake;

            offset = Quaternion.Euler(pitch * amount, yaw * amount, roll * amount);
            written = baseRot * offset;
            t.localRotation = written;
        }
    }
}
