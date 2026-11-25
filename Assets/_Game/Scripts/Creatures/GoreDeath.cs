using System;
using System.Collections;
using System.Collections.Generic;
using Backrooms.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Backrooms.Creatures
{
    /// Stage D: two complete, first-person kills for every creature that can actually kill you.
    /// The killer is posed by its ordinary Look script while this moves the victim's camera, times the
    /// impacts and paints the room. GameSession waits for `done` before static/spectating/checkpoint.
    public static class GoreDeath
    {
        public struct KillSpec
        {
            public string Name, Grip, Tear;
            public float Duration, Height, Distance, Pitch, Roll;
            public bool Smother, Caustic, Static, DoubleHit, SelfFocus;

            public KillSpec(string name, float duration, float height, float distance, string grip, string tear,
                float pitch = 0f, float roll = 75f, bool smother = false, bool caustic = false,
                bool stat = false, bool doubleHit = false, bool selfFocus = false)
            {
                Name = name; Duration = duration; Height = height; Distance = distance;
                Grip = grip; Tear = tear; Pitch = pitch; Roll = roll; Smother = smother;
                Caustic = caustic; Static = stat; DoubleHit = doubleHit; SelfFocus = selfFocus;
            }
        }

        static readonly HashSet<string> seen = new HashSet<string>();
        static readonly CreatureKind[] killers =
        {
            CreatureKind.Smiler, CreatureKind.Hound, CreatureKind.SkinStealer, CreatureKind.Clump,
            CreatureKind.Faceling, CreatureKind.EvolvedFaceling, CreatureKind.ShadowFaceling,
            CreatureKind.MothSwarm, CreatureKind.Praetorian, CreatureKind.MothEmpress,
            CreatureKind.Wretch, CreatureKind.WretchMass, CreatureKind.PartyGoer,
        };

        public static IReadOnlyList<CreatureKind> TestKinds => killers;
        public static bool HasSequence(CreatureKind kind) => Array.IndexOf(killers, kind) >= 0;

        public static KillSpec Spec(CreatureKind kind, int variant)
        {
            int v = variant & 1;
            switch (kind)
            {
                case CreatureKind.Smiler:
                    return v == 0 ? new KillSpec("Smiler · the last light", 4.8f, 1.85f, 0.5f, "[cold fingers close around your head]", "[your teeth crack; static fills your eyes]", -8f, 95f, stat: true)
                                  : new KillSpec("Smiler · torn through the signal", 5.2f, 1.75f, 0.65f, "[the grin opens wider than its face]", "[your body folds the wrong way]", 20f, -135f, stat: true, doubleHit: true);
                case CreatureKind.Hound:
                    return v == 0 ? new KillSpec("Hound · throat", 4.5f, 0.8f, 0.45f, "[it pins you; teeth find your throat]", "[it shakes its head with your neck in its jaws]", 15f, 105f, doubleHit: true)
                                  : new KillSpec("Hound · feeding", 5.4f, 0.65f, 0.55f, "[claws hold your shoulders to the floor]", "[your belly opens under its muzzle]", 65f, 55f, selfFocus: true);
                case CreatureKind.SkinStealer:
                    return v == 0 ? new KillSpec("Skin-Stealer · two pieces", 4.7f, 1.55f, 0.7f, "[wet hands take your waist and shoulders]", "[your top half comes away]", -12f, 115f, stat: false, doubleHit: true)
                                  : new KillSpec("Skin-Stealer · a new face", 5.5f, 1.6f, 0.48f, "[thumbs press under the edge of your face]", "[your skin stretches, then lets go]", 8f, -85f, smother: true);
                case CreatureKind.Clump:
                    return v == 0 ? new KillSpec("Clump · swallowed", 4.9f, 0.55f, 0.35f, "[the long arm drags you under the limbs]", "[teeth close through your chest]", 18f, 140f, doubleHit: true)
                                  : new KillSpec("Clump · folded back", 5.1f, 0.85f, 0.5f, "[an arm catches your ankle; another takes your neck]", "[your spine bends until it gives]", -70f, -90f);
                case CreatureKind.Faceling:
                    return v == 0 ? new KillSpec("Facelings · the crowd", 5.6f, 1.6f, 0.62f, "[too many hands cover your mouth and eyes]", "[nails dig in; the hands pull you apart]", 5f, 65f, smother: true)
                                  : new KillSpec("Faceling · beaten down", 4.4f, 1.55f, 0.7f, "[a fist turns your head to the carpet]", "[they keep hitting after you stop moving]", 55f, 100f, doubleHit: true);
                case CreatureKind.EvolvedFaceling:
                    return v == 0 ? new KillSpec("Evolved Faceling · pipe", 4.3f, 1.7f, 0.65f, "[the pipe comes round into your temple]", "[bone gives with a wet crack]", -15f, 120f, doubleHit: true)
                                  : new KillSpec("Evolved Faceling · held for the blow", 5f, 1.6f, 0.55f, "[one holds your arms; the other lifts its pipe]", "[the second swing goes through your skull]", 25f, -105f, doubleHit: true);
                case CreatureKind.ShadowFaceling:
                    return v == 0 ? new KillSpec("Shadow Faceling · cold inside", 5.3f, 1.55f, 0.38f, "[cold fingers pass through your eyes]", "[something pulls your mind out through them]", -10f, 75f, smother: true, stat: true)
                                  : new KillSpec("Shadow Faceling · outline", 4.6f, 1.6f, 0.45f, "[your shadow stands up without you]", "[it peels you away from yourself]", 35f, -145f, stat: true);
                case CreatureKind.MothSwarm:
                    return v == 0 ? new KillSpec("Deathmoths · no air", 5.7f, 1.25f, 0.4f, "[wings force your mouth open]", "[moths crawl down your throat]", 10f, 80f, smother: true)
                                  : new KillSpec("Deathmoths · caustic", 4.8f, 1.35f, 0.45f, "[hot spit floods your eyes]", "[your face runs between your fingers]", 35f, -90f, caustic: true, doubleHit: true);
                case CreatureKind.Praetorian:
                    return v == 0 ? new KillSpec("Praetorian · impaled", 4.6f, 0.85f, 0.45f, "[a spined leg punches through your chest]", "[it lifts you on the leg, then bites]", -25f, 100f)
                                  : new KillSpec("Praetorian · paralysed", 5.4f, 0.75f, 0.42f, "[your legs won't answer; the long legs hold you down]", "[it tears pieces away while you watch]", 55f, 70f, selfFocus: true, doubleHit: true);
                case CreatureKind.MothEmpress:
                    return v == 0 ? new KillSpec("Empress · wingbeat", 4.4f, 0.9f, 0.6f, "[the wing throws you onto your back]", "[the vast body comes down over you]", 50f, 135f, doubleHit: true)
                                  : new KillSpec("Empress · given to the hive", 5.8f, 1.0f, 0.35f, "[hooked legs drag you beneath her wings]", "[the whole hive feeds at once]", 20f, -65f, smother: true, doubleHit: true);
                case CreatureKind.Wretch:
                    return v == 0 ? new KillSpec("Wretch · loose skin", 5.2f, 1.45f, 0.5f, "[its claws hook beneath your skin]", "[your skin tears away in long strips]", 20f, 95f, doubleHit: true)
                                  : new KillSpec("Wretch · the Cycle", 5.6f, 1.4f, 0.55f, "[your own hands clutch at its mouth]", "[you become something else before you die]", -35f, -125f, selfFocus: true);
                case CreatureKind.WretchMass:
                    return v == 0 ? new KillSpec("Wretch Mass · underneath", 5.5f, 0.9f, 0.55f, "[a hundred wet hands pull you under the mass]", "[mouths close over every part of you]", 55f, 80f, smother: true, doubleHit: true)
                                  : new KillSpec("Wretch Mass · sludge", 4.9f, 0.85f, 0.6f, "[hot sludge sprays into your mouth]", "[your body comes apart under the weight]", 35f, -95f, caustic: true);
                case CreatureKind.PartyGoer:
                    return v == 0 ? new KillSpec("Partygoer · guest of honour", 5.4f, 1.45f, 0.72f, "[boneless arms lift you to its opening chest]", "[the little arms inside pull you in]", 10f, 105f, smother: true, doubleHit: true)
                                  : new KillSpec("Partygoer · a smile like theirs", 5f, 1.75f, 0.48f, "[a toothed cup clamps around your head]", "[it carves the corners of your smile wider]", -5f, -80f, doubleHit: true);
                default: return default;
            }
        }

        public static string Describe(CreatureKind kind, int variant) => HasSequence(kind) ? Spec(kind, variant).Name : kind + " has no lethal attack";

        /// Editor self-check: complete coverage, full-length first viewings, distinct staging and text.
        public static bool CheckCatalog(out string error)
        {
            foreach (var kind in killers)
            {
                var a = Spec(kind, 0); var b = Spec(kind, 1);
                if (a.Duration < 4f || a.Duration > 6f || b.Duration < 4f || b.Duration > 6f) { error = kind + ": kill length outside 4-6 s"; return false; }
                if (string.IsNullOrWhiteSpace(a.Grip) || string.IsNullOrWhiteSpace(a.Tear) || a.Name == b.Name || a.Grip == b.Grip || a.Tear == b.Tear) { error = kind + ": missing/distinct kill copy"; return false; }
                if (Mathf.Abs(a.Height - b.Height) + Mathf.Abs(a.Distance - b.Distance) + Mathf.Abs(a.Pitch - b.Pitch) + Mathf.Abs(a.Roll - b.Roll) < 10f) { error = kind + ": variants share the same staging"; return false; }
            }
            error = null;
            return true;
        }

        public static void Play(PlayerAgent victim, DeathAttack attack, Action done)
        {
            var host = new GameObject("Kill · " + Describe(attack.Kind, attack.Variant));
            host.AddComponent<Runner>().StartCoroutine(Run(victim, attack, Spec(attack.Kind, attack.Variant), done, host));
        }

        sealed class Runner : MonoBehaviour { }

        static IEnumerator Run(PlayerAgent victim, DeathAttack attack, KillSpec spec, Action done, GameObject host)
        {
            if (victim == null) { done?.Invoke(); UnityEngine.Object.Destroy(host); yield break; }
            victim.Dying = true;
            var body = victim.Body;
            var controller = victim.GetComponent<CharacterController>();
            var tools = victim.GetComponent<PlayerTools>();
            bool restoreBody = body != null && body.enabled, restoreTools = tools != null && tools.enabled;
            bool restoreController = controller != null && controller.enabled;
            var cam = victim.Head;
            Vector3 restAt = cam != null ? cam.localPosition : default;
            Quaternion restTurn = cam != null ? cam.localRotation : default;
            void RestorePlayer()
            {
                if (body != null) body.enabled = restoreBody;
                if (controller != null) controller.enabled = restoreController;
                if (tools != null) tools.enabled = restoreTools;
                if (cam != null) { cam.localPosition = restAt; cam.localRotation = restTurn; }
            }
            if (body != null) { body.KnockDown(spec.Duration + 3f, 0.001f); body.enabled = false; }
            if (controller != null) controller.enabled = false;
            if (tools != null) tools.enabled = false;
            Vector3 startPosition = cam != null ? cam.position : victim.Eyes;
            Transform killer = attack.Source != null ? attack.Source.transform : FindProxy(attack);
            GameObject anchor = null;
            if (killer == null)
            {
                anchor = new GameObject("Kill pose");
                anchor.transform.SetPositionAndRotation(attack.KillerPosition, Quaternion.Euler(0f, attack.KillerYaw, 0f));
                killer = anchor.transform;
            }

            var canvas = new GameObject("Death overlay", typeof(Canvas), typeof(CanvasScaler)).GetComponent<Canvas>();
            canvas.transform.SetParent(host.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            var black = Full(canvas.transform, null, Color.clear);
            var red = Full(canvas.transform, null, Color.clear);
            var shutter = Full(canvas.transform, null, Color.clear);
            string key = attack.Kind + ":" + (attack.Variant & 1);
            bool repeat = seen.Contains(key);
            seen.Add(key);
            Text skip = null;
            if (repeat)
            {
                skip = UI.Ui.Label(canvas.transform, "[E]  SKIP DEATH", 26, TextAnchor.LowerRight, UI.Ui.Dim);
                var rt = skip.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f); rt.pivot = new Vector2(1f, 0f);
                rt.anchoredPosition = new Vector2(-55f, 42f);
            }

            UI.Captions.Say(spec.Grip, true);
            Sound(host.transform, attack.Kind, false);
            Look.Camcorder.Hit(spec.Static ? 0.65f : 0.25f);
            if (body != null) { body.KnockDown(spec.Duration + 3f, spec.Roll); body.enabled = false; }
            if (controller != null) controller.enabled = false;
            if (tools != null) tools.enabled = false;
            Vector3 direction = (startPosition - killer.transform.position);
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.back;
            direction.Normalize();
            bool torn = false, finished = false, limb = false;
            float elapsed = 0f;

            void Finish()
            {
                if (finished) return;
                finished = true;
                Blood.Burst(victim.Position);
                Blood.DrownLens();
                if (!Net.Coop.Active)
                    GoreKit.SpawnVictim(victim.Position, victim.transform.rotation, 0, attack.Kind == CreatureKind.FacelingChild ? null : attack.Kind.ToString());   // child deaths keep the body intact
                RestorePlayer();
                if (anchor != null) UnityEngine.Object.Destroy(anchor);
                done?.Invoke();
                UnityEngine.Object.Destroy(host, 3f);   // blood and black remain beneath the static
            }

            while (elapsed < spec.Duration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                if (repeat && elapsed > 0.65f && Controls.Interact.WasPressedThisFrame())
                {
                    Finish();
                    Blood.DrownLens();
                    UnityEngine.Object.Destroy(host);
                    yield break;
                }
                float t = Mathf.Clamp01(elapsed / spec.Duration);
                Vector3 focus = killer.TransformPoint(new Vector3(0f, spec.Height, attack.Kind == CreatureKind.Hound ? 0.45f : 0.18f));
                Vector3 close = focus + direction * spec.Distance;
                float grip = Smooth(Mathf.Clamp01(t / 0.27f));
                float hit = Smooth(Mathf.Clamp01((t - 0.28f) / 0.37f));
                float collapse = Smooth(Mathf.Clamp01((t - 0.68f) / 0.32f));
                float tremor = (attack.Variant & 1) == 0 ? Mathf.Sin(elapsed * 37f) : Mathf.Sin(elapsed * 51f);
                float beat = spec.DoubleHit ? Mathf.Sin(elapsed * 18f) : Mathf.Sin(elapsed * 11f);

                if (cam != null)
                {
                    Vector3 wanted = Vector3.Lerp(startPosition, close, grip);
                    wanted += Vector3.up * (tremor * 0.055f * hit);
                    wanted += Vector3.right * (beat * 0.06f * hit);
                    if (attack.Kind == CreatureKind.Clump || attack.Kind == CreatureKind.Smiler)
                        wanted = Vector3.Lerp(wanted, focus, hit * 0.55f);
                    Vector3 floor = victim.Position + Vector3.up * 0.22f + victim.transform.right * (((attack.Variant & 1) == 0 ? 1f : -1f) * 0.22f);
                    wanted = Vector3.Lerp(wanted, floor, collapse);
                    if (Physics.Raycast(wanted + Vector3.up * 0.15f, Vector3.down, out var floorHit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
                        wanted.y = Mathf.Max(wanted.y, floorHit.point.y + 0.12f);
                    cam.position = wanted;

                    Vector3 look = spec.SelfFocus ? (victim.Position + Vector3.up * 0.72f + victim.transform.forward * 0.25f - cam.position).normalized : (focus - cam.position).normalized;
                    if (look.sqrMagnitude < 0.001f) look = cam.forward;
                    Quaternion turn = Quaternion.LookRotation(look);
                    turn = Quaternion.Euler(spec.Pitch * hit, 0f, spec.Roll * hit + tremor * 3f * hit) * turn;
                    if (collapse > 0f)
                    {
                        Quaternion fallen = Quaternion.Euler(12f, victim.transform.eulerAngles.y, spec.Roll);
                        turn = Quaternion.Slerp(turn, fallen, collapse);
                    }
                    cam.rotation = turn;
                }

                if (!torn && t >= 0.43f)
                {
                    torn = true;
                    UI.Captions.Say(spec.Tear, true);
                    Sound(host.transform, attack.Kind, true);
                    Blood.DrownLens();
                    Look.Camcorder.Hit(spec.Static ? 0.85f : 0.35f);
                }
                if (!limb && t >= 0.58f && (attack.Kind == CreatureKind.Hound || attack.Kind == CreatureKind.SkinStealer || attack.Kind == CreatureKind.Clump || attack.Kind == CreatureKind.PartyGoer))
                {
                    limb = true;
                    GoreKit.CreatePiece("GoreArm", victim.Position + victim.transform.right * 0.45f + Vector3.up * 0.08f,
                        Quaternion.Euler(0f, UnityEngine.Random.value * 360f, 85f));
                }
                if (spec.Smother) MothSmother.Hold(victim);   // visible insects for every suffocating kill; existing effect is non-graphic

                float pulse = spec.DoubleHit ? Mathf.Max(Pulse(t, 0.43f), Pulse(t, 0.62f)) : Pulse(t, 0.43f);
                red.color = new Color(0.55f, 0f, 0f, Meta.GameSettings.ReduceFlashing ? pulse * 0.18f : pulse * 0.5f);
                shutter.color = spec.Caustic ? new Color(0.28f, 0.34f, 0.04f, Mathf.Clamp01((t - 0.3f) * 1.1f))
                    : new Color(0f, 0f, 0f, spec.Smother || spec.Static ? Mathf.Clamp01((t - 0.35f) * 1.35f) : 0f);
                black.color = new Color(0f, 0f, 0f, Mathf.Clamp01((t - 0.76f) * 4.2f));
                if (skip != null) skip.color = new Color(0.9f, 0.88f, 0.8f, Mathf.Clamp01((1f - t) * 0.65f));
                yield return null;
            }
            Finish();
        }

        static float Pulse(float t, float at) => Mathf.Clamp01(1f - Mathf.Abs(t - at) / 0.055f);
        static float Smooth(float t) => t * t * (3f - 2f * t);

        static Transform FindProxy(DeathAttack attack)
        {
            Transform best = null;
            float distance = 3.5f;
            foreach (var proxy in UnityEngine.Object.FindObjectsByType<Net.CreatureProxy>(FindObjectsSortMode.None))
            {
                if (proxy.Source == null || proxy.Source.Kind != attack.Kind) continue;
                float d = Vector3.Distance(proxy.transform.position, attack.KillerPosition);
                if (d < distance) { distance = d; best = proxy.transform; }
            }
            return best;
        }

        static void Sound(Transform at, CreatureKind kind, bool tear)
        {
            AudioClip clip = kind == CreatureKind.Smiler || kind == CreatureKind.ShadowFaceling ? Audio.ProceduralAudio.Static()
                : kind == CreatureKind.PartyGoer ? Audio.ProceduralAudio.Laughter()
                : kind == CreatureKind.MothSwarm || kind == CreatureKind.Praetorian || kind == CreatureKind.MothEmpress ? Audio.CreatureSounds.Flutter()
                : kind == CreatureKind.Wretch || kind == CreatureKind.WretchMass ? Audio.CreatureSounds.Growl()
                : Audio.CreatureSounds.Maul();
            AudioSource.PlayClipAtPoint(clip, at.position, (tear ? 1f : 0.7f) * Meta.AudioMix.Creatures);
            if (tear) AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Squelch(), at.position, Meta.AudioMix.Creatures);
        }

        internal static RawImage Full(Transform parent, Texture tex, Color c)
        {
            var img = new GameObject("Layer", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(parent, false);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.texture = tex;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSeen() => seen.Clear();
    }
}
