using Backrooms.Player;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.World
{
    /// A house's front door. Odd ids are locked.
    public sealed class HouseDoor : MonoBehaviour, IInteractable
    {
        Transform hinge;
        Mesh leafMesh;
        AudioSource click;
        float angle, target;
        bool locked;

        public string Prompt => target != 0f ? "Close the door" : "Open the door";

        public void Build(SpecialSpec s, Material[] paints)
        {
            locked = (s.Id & 1) == 1;
            hinge = new GameObject("Hinge").transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = new Vector3(0.5f, 0f, 0f);   // +z is the street; the leaf swings in
            var paint = (int)new[] { PropPaint.Red, PropPaint.Green, PropPaint.Blue, PropPaint.White, PropPaint.Mahogany }[(s.Id >> 1) % 5];
            var b = new BoxMeshBuilder(Vector3.zero, paints.Length);
            b.AddOrientedBox(Matrix4x4.identity, new Vector3(-0.5f, 1.05f, 0f), new Vector3(0.98f, 2.1f, 0.05f), paint);
            foreach (float x in new[] { -0.72f, -0.28f })
            foreach (float y in new[] { 0.5f, 1.3f })
                b.AddOrientedBox(Matrix4x4.identity, new Vector3(x, y, 0f), new Vector3(0.32f, 0.62f, 0.075f), paint);
            foreach (float z in new[] { -0.06f, 0.06f }) b.AddSphere(new Vector3(-0.9f, 1f, z), Vector3.one * 0.035f, (int)PropPaint.Brass);
            leafMesh = b.ToMesh("House door");
            var leaf = new GameObject("Leaf");
            leaf.transform.SetParent(hinge, false);
            leaf.AddComponent<MeshFilter>().sharedMesh = leafMesh;
            leaf.AddComponent<MeshRenderer>().sharedMaterials = paints;
            var col = leaf.AddComponent<BoxCollider>();
            col.center = new Vector3(-0.5f, 1.05f, 0f);
            col.size = new Vector3(0.98f, 2.1f, 0.07f);
            if (!locked && s.Id % 5 == 0) target = angle = -78f;   // someone left it open
            hinge.localRotation = Quaternion.Euler(0f, -angle, 0f);
            click = Level5Sound.Source(gameObject, 14f);
        }

        void OnDestroy() { if (leafMesh != null) Destroy(leafMesh); }

        public void Interact()
        {
            click.PlayOneShot(Audio.ProceduralAudio.DoorClick(), 0.7f * Meta.AudioMix.Effects);
            if (locked) { UI.Captions.Say("[locked]"); return; }
            target = target != 0f ? 0f : -95f;
        }

        void Update()
        {
            if (angle == target) return;
            angle = Mathf.MoveTowards(angle, target, 150f * Time.deltaTime);
            hinge.localRotation = Quaternion.Euler(0f, -angle, 0f);
        }
    }

    /// Something above the fog that nobody has seen. Only its light comes down: it works its way along the streets and
    /// stops on whoever it finds. Roofs, porches, hedges, cars and walls stop the beam.
    /// shortcut: each player's game runs its own beams (no co-op sync), fine until somebody compares notes.
    public sealed class Searchlight : MonoBehaviour
    {
        const float Height = 23f, HalfAngle = 10f;
        Light lamp;
        Vector3 spot;          // where the beam meets the ground
        Vector3 over;          // where the thing itself hangs
        Vector2Int from, to;   // street crossings, in blocks
        float along, sweep;
        AudioSource drone;

        public bool Sees { get; private set; }

        public static Searchlight Create(Transform parent, int i)
        {
            var go = new GameObject("Searchlight " + i);
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<Searchlight>();
            var lightGo = new GameObject("Beam", typeof(Light), typeof(HDAdditionalLightData));
            lightGo.transform.SetParent(go.transform, false);
            s.lamp = lightGo.GetComponent<Light>();
            s.lamp.type = LightType.Spot;
            s.lamp.spotAngle = HalfAngle * 2f;
            s.lamp.innerSpotAngle = HalfAngle * 1.5f;
            s.lamp.range = 45f;
            s.lamp.lightUnit = UnityEngine.Rendering.LightUnit.Candela;
            s.lamp.intensity = 700000f;
            s.lamp.color = new Color(0.85f, 0.93f, 1f);
            s.lamp.shadows = LightShadows.Hard;
            lightGo.GetComponent<HDAdditionalLightData>().volumetricDimmer = 5f;
            s.drone = Level5Sound.Source(lightGo, 45f);
            s.drone.clip = Audio.ProceduralAudio.Machine();
            s.drone.loop = true;
            s.drone.pitch = 0.4f;
            s.drone.volume = 0.35f * Meta.AudioMix.Effects;
            s.drone.Play();
            s.sweep = i * 2.1f;
            return s;
        }

        static Vector3 Crossing(Vector2Int b) => new Vector3(b.x * 96f + 16f, 0f, b.y * 96f + 16f);

        void Restart(Vector3 near, float far)
        {
            float a = Random.value * 6.283f;
            var p = near + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * far;
            from = new Vector2Int(Mathf.RoundToInt((p.x - 16f) / 96f), Mathf.RoundToInt((p.z - 16f) / 96f));
            if ((Crossing(from) - near).magnitude < 45f) from.x += 1;
            Turn(near);
            along = 0f;
            over = Crossing(from);
            spot = over;
        }

        /// The next street, more often than not the one that leads nearer you.
        void Turn(Vector3 player)
        {
            var best = from + Vector2Int.up;
            float score = float.MaxValue;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
            {
                float s = (Crossing(from + d) - player).magnitude + Random.value * 120f;
                if (s < score) { score = s; best = from + d; }
            }
            to = best;
        }

        public void Tick(PlayerAgent me, bool hunting)
        {
            float dt = Time.deltaTime;
            if (over == Vector3.zero || (over - me.Position).magnitude > 190f) Restart(me.Position, 130f);
            if (hunting && Sees)
            {
                over = Vector3.MoveTowards(over, new Vector3(me.Position.x, 0f, me.Position.z), dt * 3f);
                spot = Vector3.MoveTowards(spot, me.Position, dt * 3.4f);   // a run outruns it
            }
            else
            {
                along += dt * 5f / 96f;
                if (along >= 1f) { along -= 1f; from = to; Turn(me.Position); }
                var target = Vector3.Lerp(Crossing(from), Crossing(to), along);
                over = Vector3.MoveTowards(over, target, dt * 9f);
                sweep += dt * 0.55f;
                spot = Vector3.MoveTowards(spot, over + new Vector3(Mathf.Sin(sweep) * 11f, 0f, Mathf.Sin(sweep * 1.37f) * 11f), dt * 9f);
            }
            var eye = over + Vector3.up * Height;
            lamp.transform.position = eye;
            lamp.transform.rotation = Quaternion.LookRotation(spot - eye);
            Sees = false;
            if (!me.Alive) return;
            foreach (var p in new[] { me.Eyes, me.Position + Vector3.up * 0.6f })
            {
                var dir = p - eye;
                if (Vector3.Angle(lamp.transform.forward, dir) > HalfAngle * 0.9f) continue;
                if (!Physics.Raycast(eye, dir.normalized, out var hit, dir.magnitude - 0.1f, ~0, QueryTriggerInteraction.Ignore) || Players.Owns(hit.collider) == me) { Sees = true; break; }
            }
            lamp.color = Sees ? new Color(1f, 0.95f, 0.85f) : new Color(0.85f, 0.93f, 1f);
        }
    }

    /// Level 9 while you're in it: the searchlights, the fog that rolls down the streets, and what it makes you think.
    public sealed class Level9Events : MonoBehaviour
    {
        static readonly string[] Thoughts =
        {
            "[static, behind your eyes]", "[you could lie down here]", "[nobody is looking for you]", "[you have been on this street before]",
            "[it would not hurt for long]", "[somebody is standing in that window. no. nobody]",
        };

        FirstPersonController player;
        Searchlight[] lights;
        float exposure, bank, nextThought, nextHurt;
        AudioSource head;
        float quietUntil;
        bool found, warned;

        public void Init(FirstPersonController body)
        {
            player = body;
            lights = new Searchlight[2];
            for (int i = 0; i < lights.Length; i++) lights[i] = Searchlight.Create(transform, i);
            var go = new GameObject("Static");
            go.transform.SetParent(body.transform, false);
            head = go.AddComponent<AudioSource>();
            head.clip = Audio.ProceduralAudio.Static();
            head.loop = true;
            head.volume = 0f;
            head.Play();
            nextThought = Time.time + 25f;
            quietUntil = Time.time + 15f;
        }

        void OnDestroy()
        {
            if (lights != null) foreach (var l in lights) if (l != null) Destroy(l.gameObject);
            if (head != null) Destroy(head.gameObject);
            if (Atmosphere.Instance != null) Atmosphere.Instance.SetBank(0f);
        }

        void Update()
        {
            var me = Players.Local;
            if (player == null || me == null) return;
            float dt = Time.deltaTime;
            var pos = player.transform.position;
            bool street = SuburbsLayout.OnStreet(pos);

            // Fog banks roll along the streets; yards and houses only get the edge of them.
            float roll = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.75f, Mathf.PerlinNoise(pos.x / 70f + Time.time * 0.02f, pos.z / 70f + 31f)));
            bank = Mathf.MoveTowards(bank, roll * (street ? 1f : 0.35f), dt * 0.12f);
            if (Atmosphere.Instance != null) Atmosphere.Instance.SetBank(Mathf.Round(bank * 40f) / 40f);
            float bad = street ? 0.35f + bank : bank * 0.4f;
            head.volume = Mathf.MoveTowards(head.volume, street ? bank * 0.16f * Meta.AudioMix.Effects : 0f, dt * 0.05f);
            if (Meta.Difficulty.SurvivalEnabled && me.Alive)
            {
                me.LoseSanity(dt * bad / 300f * Meta.Difficulty.Sanity);
                if (street && bank > 0.4f && Time.time > nextThought) { nextThought = Time.time + Random.Range(14f, 30f); UI.Captions.Say(Thoughts[Random.Range(0, Thoughts.Length)]); }
            }

            // The beams.
            if (Time.time < quietUntil) return;   // a moment to find your feet
            bool seen = false;
            foreach (var l in lights) { l.Tick(me, exposure > 0.1f); seen |= l.Sees; }
            exposure = Mathf.Clamp01(exposure + dt * (seen ? 0.33f : -0.25f));   // three seconds
            if (seen && !warned) { warned = true; UI.Captions.Say("[a light has stopped on you]"); player.GetComponent<PlayerHud>()?.Flash("Get under something.", 2f); }
            if (exposure <= 0f) { warned = false; found = false; }
            if (exposure >= 1f && !found)
            {
                found = true;
                UI.Captions.Say("[something overhead lets out a long, low note]");
                AudioSource.PlayClipAtPoint(Audio.ProceduralAudio.Clang(), pos + Vector3.up * 8f, Meta.AudioMix.Effects);
                FindFirstObjectByType<Game.GameSession>()?.MakeNoise(pos, 60f);   // everything on the street turns to look
            }
            if (found && seen && Meta.Difficulty.SurvivalEnabled && Time.time > nextHurt)
            {
                nextHurt = Time.time + 0.7f;
                me.Hurt(0.08f, false);
                me.LoseSanity(0.03f);
            }
        }
    }
}
