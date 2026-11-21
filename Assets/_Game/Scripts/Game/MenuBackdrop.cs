using System.Collections;
using System.Collections.Generic;
using Backrooms.Creatures;
using Backrooms.Meta;
using Backrooms.UI;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// Behind the main menu: the camera drifts slowly through the level as if someone left a camcorder running
    /// while walking. Things happen now and then: the lights stutter or die, footsteps come from behind, a ceiling
    /// tile drops, a faceless figure crosses far away, a grin shows in the dark. The further you've got in the game
    /// (Profile.Progress), the further the camera wanders and the more often something happens.
    public sealed class MenuBackdrop : MonoBehaviour
    {
        ChunkStreamer streamer;
        Transform body, cam;
        Material ceiling;
        Vector3 home;
        float yaw, targetYaw, nextSteer, nextEvent, progress;
        AudioSource sfx;
        Coroutine running;
        readonly List<GameObject> spawned = new List<GameObject>();

        public void Init(Transform player, Camera camera, ChunkStreamer world, LevelCatalog catalog, LevelTheme theme)
        {
            body = player;
            cam = camera.transform;
            streamer = world;
            ceiling = catalog.Resolve(theme, new SurfaceRef(Surface.Ceiling));
            home = body.position;
            yaw = targetYaw = body.eulerAngles.y;
            progress = Profile.Progress;
            sfx = new GameObject("Menu Sounds").AddComponent<AudioSource>();
            sfx.spatialBlend = 1f;
            sfx.rolloffMode = AudioRolloffMode.Linear;
            sfx.maxDistance = 30f;
            spawned.Add(sfx.gameObject);
            cam.localPosition = Vector3.up * 1.6f;
            nextEvent = Time.time + Random.Range(6f, 14f);
        }

        void OnDestroy()
        {
            foreach (var g in spawned) if (g != null) Destroy(g);
            if (streamer != null) streamer.Blackout = 0f;
        }

        void Update()
        {
            Drift(Time.deltaTime);
            if (Time.time < nextEvent || running != null) return;
            nextEvent = Time.time + Mathf.Lerp(40f, 12f, progress) * Random.Range(0.7f, 1.3f);
            running = StartCoroutine(Event());
        }

        // ---------- the drift ----------

        Vector3 Eye => body.position + Vector3.up * 1.6f;

        /// How far you could move in `dir` before hitting a wall or reaching a drop.
        float Free(Vector3 dir)
        {
            float d = Physics.SphereCast(Eye, 0.3f, dir, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore) ? hit.distance : 10f;
            if (!Physics.Raycast(Eye + dir * Mathf.Min(d, 1.5f), Vector3.down, 2.5f, ~0, QueryTriggerInteraction.Ignore)) d = Mathf.Min(d, 0.5f);   // a pit
            return d;
        }

        void Drift(float dt)
        {
            float speed = 0.12f + 0.35f * progress;
            float leash = Mathf.Lerp(5f, 60f, progress);
            var toHome = home - body.position;
            toHome.y = 0f;
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (Time.time > nextSteer)
            {
                nextSteer = Time.time + 0.5f;
                bool homesick = toHome.magnitude > leash;
                if (Free(fwd) < 3f || homesick)
                {
                    float best = float.MinValue;
                    for (int a = -150; a <= 150; a += 30)
                    {
                        var dir = Quaternion.Euler(0f, yaw + a, 0f) * Vector3.forward;
                        float score = Free(dir) + Random.value * 0.5f - Mathf.Abs(a) * 0.004f
                                    + (homesick ? 4f * Vector3.Dot(dir, toHome.normalized) : 0f);
                        if (score > best) { best = score; targetYaw = yaw + a; }
                    }
                }
            }
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, dt * 18f);
            if (Free(fwd) > 1.2f) body.position += fwd * speed * dt;
            body.rotation = Quaternion.Euler(0f, yaw, 0f);
            // A handheld sway, as if someone is carrying the camera.
            float t = Time.time * 0.12f;
            cam.localRotation = Quaternion.Euler((Mathf.PerlinNoise(t, 1.3f) - 0.5f) * 6f, (Mathf.PerlinNoise(2.1f, t) - 0.5f) * 10f, 0f);
        }

        // ---------- the events ----------

        IEnumerator Event()
        {
            var options = new List<System.Func<IEnumerator>> { Flicker, Footsteps };
            if (progress > 0.1f) options.Add(FallingTile);
            if (progress > 0.15f) options.Add(LightsDie);
            if (progress > 0.25f) options.Add(Faceling);
            if (progress > 0.6f) options.Add(Phone);
            yield return options[Random.Range(0, options.Count)]();
            running = null;
        }

        IEnumerator Flicker()
        {
            Captions.Say("[the lights buzz]");
            if (GameSettings.ReduceFlashing)
            {
                streamer.Blackout = 0.6f;
                yield return new WaitForSeconds(1.5f);
            }
            else
                for (int i = Random.Range(4, 8); i > 0; i--)
                {
                    streamer.Blackout = 1f;
                    yield return new WaitForSeconds(Random.Range(0.06f, 0.15f));
                    streamer.Blackout = 0f;
                    yield return new WaitForSeconds(Random.Range(0.05f, 0.3f));
                }
            streamer.Blackout = 0f;
        }

        IEnumerator Footsteps()
        {
            Captions.Say("[footsteps behind you]");
            var clips = Audio.ProceduralAudio.Footsteps();
            for (int i = Random.Range(4, 7); i > 0; i--)
            {
                sfx.transform.position = body.position - body.forward * Random.Range(6f, 9f) + Vector3.up * 0.1f;
                sfx.pitch = Random.Range(0.85f, 0.95f);
                sfx.PlayOneShot(clips[Random.Range(0, clips.Length)], 0.8f * AudioMix.Creatures);
                yield return new WaitForSeconds(0.55f);
            }
        }

        IEnumerator FallingTile()
        {
            var at = Eye + body.forward * Random.Range(2.5f, 5f) + body.right * Random.Range(-1.5f, 1.5f);
            if (!Physics.Raycast(at, Vector3.up, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) yield break;
            Captions.Say("[something falls]");
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "Fallen Tile";
            tile.transform.position = hit.point + Vector3.down * 0.05f;
            tile.transform.localScale = new Vector3(0.6f, 0.025f, 0.6f);
            tile.GetComponent<MeshRenderer>().sharedMaterial = ceiling;
            var rb = tile.AddComponent<Rigidbody>();
            rb.angularVelocity = Random.insideUnitSphere * 3f;
            spawned.Add(tile);
        }

        IEnumerator LightsDie()
        {
            Captions.Say("[the lights die]");
            streamer.Blackout = 1f;
            GameObject grin = null;
            if (progress > 0.4f && Random.value < 0.5f)
            {
                var spot = Spot(8f, 12f);
                if (spot.HasValue)
                {
                    yield return new WaitForSeconds(1.2f);
                    grin = Figure(CreatureKind.Smiler, spot.Value);
                    Captions.Say("[crackling static]");
                    sfx.transform.position = spot.Value;
                    sfx.pitch = 0.45f;
                    sfx.PlayOneShot(Audio.ProceduralAudio.Static(), 0.2f * AudioMix.Creatures);
                }
            }
            yield return new WaitForSeconds(Random.Range(3f, 5f));
            if (grin != null) Destroy(grin);
            streamer.Blackout = 0f;
        }

        IEnumerator Faceling()
        {
            var spot = Spot(14f, 20f);
            if (!spot.HasValue) yield break;
            var f = Figure(CreatureKind.Faceling, spot.Value);
            var side = body.right * (Random.value < 0.5f ? 1f : -1f);
            f.transform.rotation = Quaternion.LookRotation(side);
            for (float t = 0f; t < 6f && f != null; t += Time.deltaTime)
            {
                var next = f.transform.position + side * 0.9f * Time.deltaTime;
                if (Physics.Raycast(next + Vector3.up, side, 0.5f, ~0, QueryTriggerInteraction.Ignore)) break;   // reached a wall
                f.transform.position = next;
                yield return null;
            }
            if (f != null) Destroy(f);
        }

        IEnumerator Phone()
        {
            Captions.Say("[a phone rings somewhere]");
            sfx.transform.position = body.position + body.right * Random.Range(-12f, 12f) + body.forward * Random.Range(4f, 12f);
            sfx.pitch = 1f;
            sfx.clip = Audio.ProceduralAudio.Ring();
            sfx.volume = 0.5f * AudioMix.Effects;
            sfx.Play();
            yield return new WaitForSeconds(Random.Range(3f, 6f));
            sfx.Stop();
            sfx.volume = 1f;
        }

        /// A spot on the floor in front of the camera, between min and max metres away, in plain view.
        Vector3? Spot(float min, float max)
        {
            for (int i = 0; i < 10; i++)
            {
                var dir = Quaternion.Euler(0f, yaw + Random.Range(-25f, 25f), 0f) * Vector3.forward;
                float d = Random.Range(min, max);
                if (Physics.SphereCast(Eye, 0.4f, dir, out _, d, ~0, QueryTriggerInteraction.Ignore)) continue;
                var p = Eye + dir * d;
                if (Physics.Raycast(p, Vector3.down, out var floor, 2.5f, ~0, QueryTriggerInteraction.Ignore)) return floor.point;
            }
            return null;
        }

        /// Just the body of a creature, no brain: it can't hurt anyone in the menu.
        GameObject Figure(CreatureKind kind, Vector3 at)
        {
            var go = new GameObject(kind + " (menu)");
            go.transform.position = at;
            CreatureBodies.Build(kind, go.transform, out _);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
            var look = Eye - at;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) go.transform.rotation = Quaternion.LookRotation(look);
            spawned.Add(go);
            return go;
        }
    }
}
