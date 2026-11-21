using System.Collections.Generic;
using Backrooms.Creatures;
using Backrooms.Net;
using Backrooms.Player;
using Backrooms.World;
using Unity.Netcode;
using UnityEngine;

namespace Backrooms.Game
{
    /// Shared unpredictable changes in generated rooms. An event may fail safely when its chosen chunk is unloaded;
    /// it never mutates the deterministic layout, so the base level remains stable on every visit.
    public sealed class EnvironmentalEvents : MonoBehaviour
    {
        const string ChangeMsg = "environment.change";
        const float FirstEventDelay = 75f, MinInterval = 150f, MaxInterval = 300f;
        sealed class Change { public byte kind; public float until; public GameObject blocker; public Light light; }

        FirstPersonController player;
        ChunkStreamer streamer;
        PlayerHud hud;
        FearDirector director;
        readonly Dictionary<string, Change> active = new Dictionary<string, Change>();
        System.Action<FastBufferReader> receiveHandler;
        float nextEvent;

        public void Init(FirstPersonController body, ChunkStreamer world, FearDirector fears)
        {
            player = body;
            streamer = world;
            hud = body.GetComponent<PlayerHud>();
            director = fears;
            nextEvent = Time.time + FirstEventDelay + Random.Range(0f, 60f);
            receiveHandler = Receive;
            LevelSync.On(ChangeMsg, receiveHandler);
        }

        void OnDestroy()
        {
            LevelSync.Off(ChangeMsg, receiveHandler);
            foreach (var change in active.Values)
            {
                if (change.blocker != null) Destroy(change.blocker);
                if (change.light != null) Destroy(change.light.gameObject);
            }
        }

        void Update()
        {
            if (player == null || streamer == null) return;
            foreach (var item in active.Values)
            {
                if (item.kind == 0 && item.light != null)
                    item.light.enabled = Time.time >= item.until || Mathf.Repeat(Time.time * 5f, 1f) > 0.38f;
                if (item.until > 0f && Time.time >= item.until)
                {
                    if (item.blocker != null) Destroy(item.blocker);
                    if (item.light != null) Destroy(item.light.gameObject);
                    item.blocker = null;
                    item.light = null;
                    item.until = -1f;
                }
            }
            if (!LevelSync.Decides || Time.time < nextEvent) return;
            nextEvent = Time.time + Random.Range(MinInterval, MaxInterval) * Meta.Difficulty.Cooldowns;
            if (Meta.Difficulty.Mode == Meta.GameMode.Exploration) return;
            Vector3 at = FindSpot();
            if (at == Vector3.zero) return;
            byte kind = (byte)Random.Range(0, 3); // lights, temporary obstruction, altered room dressing
            string key = $"{Mathf.FloorToInt(at.x / 4f)}:{Mathf.FloorToInt(at.y / 4f)}:{Mathf.FloorToInt(at.z / 4f)}";
            float duration = kind == 0 ? Random.Range(14f, 28f) : Random.Range(45f, 90f);
            Apply(key, kind, at, duration);
            LevelSync.Send(ChangeMsg, w => { w.WriteValueSafe(key); w.WriteValueSafe(kind); w.WriteValueSafe(at); w.WriteValueSafe(duration); });
        }

        Vector3 FindSpot()
        {
            for (int i = 0; i < 20; i++)
            {
                var dir = Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward;
                var p = player.transform.position + dir * Random.Range(9f, 22f);
                var chunk = ChunkStreamer.ChunkOf(p);
                if (!streamer.Loaded(new Vector3Int(chunk.x, streamer.CurrentFloor, chunk.y))) continue;
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out var hit, 3f, ~0, QueryTriggerInteraction.Ignore)
                    && !Physics.CheckSphere(hit.point + Vector3.up * 0.7f, 0.55f, ~0, QueryTriggerInteraction.Ignore))
                    return hit.point;
            }
            return Vector3.zero;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void DeveloperTrigger()
        {
            if (!LevelSync.Decides) return;
            Vector3 at = FindSpot();
            if (at == Vector3.zero) { hud?.Flash("No nearby loaded spot for an event."); return; }
            byte kind = (byte)Random.Range(0, 3);
            string key = $"{Mathf.FloorToInt(at.x / 4f)}:{Mathf.FloorToInt(at.y / 4f)}:{Mathf.FloorToInt(at.z / 4f)}";
            float duration = kind == 0 ? 20f : 60f;
            Apply(key, kind, at, duration);
            LevelSync.Send(ChangeMsg, w => { w.WriteValueSafe(key); w.WriteValueSafe(kind); w.WriteValueSafe(at); w.WriteValueSafe(duration); });
        }
#endif

        void Receive(FastBufferReader r)
        {
            r.ReadValueSafe(out string key);
            r.ReadValueSafe(out byte kind);
            r.ReadValueSafe(out Vector3 at);
            r.ReadValueSafe(out float duration);
            Apply(key, kind, at, duration);
        }

        void Apply(string key, byte kind, Vector3 at, float duration)
        {
            if (active.TryGetValue(key, out var existing) && existing.until > Time.time) return;
            var change = new Change { kind = kind, until = Time.time + duration };
            active[key] = change;
            if (kind == 0)
            {
                var lightGo = new GameObject("Environmental Flicker");
                lightGo.transform.position = at + Vector3.up * 2.45f;
                change.light = lightGo.AddComponent<Light>();
                change.light.type = LightType.Point;
                change.light.range = 8f;
                change.light.intensity = 2.2f;
                change.light.color = new Color(0.95f, 0.82f, 0.56f);
                change.light.shadows = LightShadows.None;
                change.light.enabled = false;
                hud?.Flash("A light begins flickering in the next room.", 3f);
            }
            else
            {
                var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = kind == 1 ? "Temporary Route Block" : "Room Shift Debris";
                obstacle.transform.position = at + Vector3.up * 0.55f;
                obstacle.transform.localScale = kind == 1 ? new Vector3(1.4f, 1.1f, 0.55f) : new Vector3(1.2f, 1.1f, 1.1f);
                var renderer = obstacle.GetComponent<Renderer>();
                if (streamer.Theme != null && streamer.Theme.wall != null) renderer.sharedMaterial = streamer.Theme.wall;
                var collider = obstacle.GetComponent<Collider>();
                if (collider != null) collider.isTrigger = true; // temporary route dressing never physically traps a player
                change.blocker = obstacle;
                hud?.Flash(kind == 1 ? "A route is blocked by fallen debris." : "The room seems to have shifted.", 3f);
            }
            director?.Noise(at, 18f);
        }
    }
}
