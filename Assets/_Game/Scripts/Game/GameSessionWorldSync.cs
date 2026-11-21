using Backrooms.Net;
using Backrooms.World;
using Unity.Netcode;
using UnityEngine;

namespace Backrooms.Game
{
    /// Co-op: one shared world. Whatever anyone takes, reads, pries open or pulls is gone or done for everyone:
    /// the change goes to the owner, who applies it and passes it on. Friends can also hand each other supplies.
    public sealed partial class GameSession
    {
        enum Act : byte { Pickup = 1, Note = 2, Goal = 3, Boards = 4, Crate = 5, ForcedDoor = 6, Pump = 7, DevObjective = 8 }
        const string ActMsg = "bk.act", GiveMsg = "bk.give";
        const string DevPickupMsg = "bk.dev.pickup";
        bool applyingRemote;
        readonly System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit> actRates = new System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit>();
        readonly System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit> giveRates = new System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit>();

        void WireWorldSync()
        {
            coop.Handle(ActMsg, OnAct);
            coop.Handle(GiveMsg, OnGive);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            coop.Handle(DevPickupMsg, OnDeveloperPickup);
#endif
            if (tools != null)
            {
                tools.PickedUp += p => Share(Act.Pickup, p.Id);
                tools.Read += r => Share(Act.Note, r.Id);
                tools.GoalTaken += i => Share(Act.Goal, i);
                tools.Pried += b => Share(b.Crate ? Act.Crate : b.LockedDoor ? Act.ForcedDoor : Act.Boards, b.Id);
                tools.Gave += (friend, kind) => Give(friend, kind);
            }
            PoolSwitch.Pressed -= SharePump;
            PoolSwitch.Pressed += SharePump;
        }

        void OnDestroy() => PoolSwitch.Pressed -= SharePump;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ShareDeveloperPickup(Pickup pickup)
        {
            if (!Coop.IsOwner || pickup == null) return;
            coop.SendAll(DevPickupMsg, w =>
            {
                w.WriteValueSafe(pickup.Id);
                w.WriteValueSafe(pickup.transform.position);
                w.WriteValueSafe((byte)pickup.Kind);
            });
        }

        public void ShareDeveloperObjective()
        {
            if (!Coop.Active) return;
            if (Coop.IsOwner) coop.SendAll(ActMsg, w => { w.WriteValueSafe((byte)Act.DevObjective); w.WriteValueSafe(0); });
        }

        void OnDeveloperPickup(ulong from, FastBufferReader r)
        {
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out Vector3 at);
            r.ReadValueSafe(out byte kind);
            if (Coop.IsOwner || !AntiCheat.SenderIsServer(from)) return;
            if (float.IsNaN(at.x) || float.IsInfinity(at.x) || at.sqrMagnitude > 100000000f) return;
            SpawnDeveloperPickup((PropKind)kind, at, id);
        }

        void SpawnDeveloperPickup(PropKind kind, Vector3 at, int id)
        {
            if (streamer == null || streamer.Paints == null) return;
            foreach (var existing in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                if (existing.Id == id && existing.Goal == null) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = kind == PropKind.AlmondWater ? "Developer Almond Water" : "Developer Battery";
            go.transform.position = at;
            go.transform.localScale = Vector3.one * 0.22f;
            go.transform.SetParent(streamer.transform, true);
            go.GetComponent<Renderer>().sharedMaterial = kind == PropKind.AlmondWater ? streamer.Paints[(int)PropPaint.Water] : streamer.Paints[(int)PropPaint.Yellow];
            go.GetComponent<SphereCollider>().isTrigger = true;
            var pickup = go.AddComponent<Pickup>();
            pickup.Kind = kind;
            pickup.Id = id;
        }
#endif

        void SharePump()
        {
            if (!applyingRemote) Share(Act.Pump, 0);
        }

        /// Something you did to the shared world: tell the owner (or, as the owner, everyone).
        void Share(Act act, int id)
        {
            if (!Coop.Active) return;
            if (Coop.IsOwner) coop.SendAll(ActMsg, w => { w.WriteValueSafe((byte)act); w.WriteValueSafe(id); });
            else coop.Send(ActMsg, NetworkManager.ServerClientId, w => { w.WriteValueSafe((byte)act); w.WriteValueSafe(id); });
        }

        void OnAct(ulong from, FastBufferReader r)
        {
            r.ReadValueSafe(out byte raw);
            r.ReadValueSafe(out int id);
            var act = (Act)raw;
            if (!System.Enum.IsDefined(typeof(Act), act)) return;

            if (Coop.IsOwner)
            {
                // As owner we apply our own local changes directly via gameplay; incoming remote acts must be validated.
                if (from != coop.Net.LocalClientId)
                {
                    if (!KnownClient(from) || !AllowRate(actRates, from, 1f, 20))
                    {
                        Reject(from, "Ignored suspicious world action.");
                        return;
                    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (act == Act.DevObjective)
                    {
                        Reject(from, "Rejected unauthorized developer action from guest.");
                        return;
                    }
#endif
                    if (!CanApply(act, id, from))
                    {
                        Reject(from, "Ignored invalid world action.");
                        return;
                    }
                    Apply(act, id);
                    foreach (var other in coop.Net.ConnectedClientsIds)
                        if (other != from && other != coop.Net.LocalClientId)
                            coop.Send(ActMsg, other, w => { w.WriteValueSafe(raw); w.WriteValueSafe(id); });
                }
                else
                {
                    Apply(act, id);
                }
                return;
            }

            // Guest: only accept shared world changes from the owner.
            if (!AntiCheat.SenderIsServer(from)) return;
            Apply(act, id);
        }

        bool KnownClient(ulong id)
        {
            if (coop?.Net == null) return false;
            foreach (var c in coop.Net.ConnectedClientsIds) if (c == id) return true;
            return false;
        }

        static bool AllowRate(System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit> rates, ulong from, float window, int max)
        {
            if (!rates.TryGetValue(from, out var rl)) rl = new AntiCheat.RateLimit(window, max);
            bool ok = rl.Allow(Time.unscaledTime);
            rates[from] = rl;
            return ok;
        }

        void Reject(ulong from, string reason)
        {
            Debug.LogWarning($"[AntiCheat] {reason} (client {from})");
            if (hud != null && Time.frameCount % 30 == 0) hud.Flash(reason, 2f);
        }

        bool CanApply(Act act, int id, ulong from)
        {
            if (save == null) return false;
            if (picked.Contains(id) && act != Act.Goal && act != Act.Pump) return false;
            switch (act)
            {
                case Act.Pickup:
                    foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                        if (p.Id == id && p.Goal == null) return NearSender(from, p.transform.position, 8f);
                    return false;
                case Act.Note:
                    foreach (var n in FindObjectsByType<Readable>(FindObjectsSortMode.None))
                        if (n.Id == id && n.Kind == ReadableKind.Note) return NearSender(from, n.transform.position, 8f);
                    return false;
                case Act.Goal:
                    return objectives != null;
                case Act.Boards:
                    foreach (var b in FindObjectsByType<BoardedDoor>(FindObjectsSortMode.None))
                        if (b.Id == id) return NearSender(from, b.transform.position, 10f);
                    return false;
                case Act.Crate:
                case Act.ForcedDoor:
                    foreach (var b in FindObjectsByType<BoardedDoor>(FindObjectsSortMode.None))
                        if (b.Id == id && b.Pryable) return NearSender(from, b.transform.position, 10f);
                    return false;
                case Act.Pump:
                    if (pool == null) return false;
                    foreach (var s in FindObjectsByType<PoolSwitch>(FindObjectsSortMode.None))
                        return NearSender(from, s.transform.position, 12f);
                    return NearSender(from, player != null ? player.transform.position : Vector3.zero, 80f);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                case Act.DevObjective:
                    return false;
#endif
                default:
                    return false;
            }
        }

        bool NearSender(ulong client, Vector3 at, float radius)
        {
            var avatar = AntiCheat.FindAvatar(client);
            if (avatar == null) return true; // if unknown, don't hard-fail here; rate limiting still applies
            return (avatar.transform.position - at).sqrMagnitude <= radius * radius;
        }

        /// A friend's change, made here too.
        void Apply(Act act, int id)
        {
            if (save == null) return;
            applyingRemote = true;
            try
            {
                bool changed = false;
                switch (act)
                {
                    case Act.Pickup:
                        foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                            if (p.Id == id && p.Goal == null) { Destroy(p.gameObject); changed = true; }
                        break;
                    case Act.Note:
                        foreach (var n in FindObjectsByType<Readable>(FindObjectsSortMode.None))
                            if (n.Id == id && n.Kind == ReadableKind.Note) { Destroy(n.gameObject); changed = true; }
                        break;
                    case Act.Goal:
                        if (objectives != null) { objectives.TookRemotely(id); changed = true; }
                        break;
                    case Act.Boards:
                        foreach (var b in FindObjectsByType<BoardedDoor>(FindObjectsSortMode.None))
                            if (b.Id == id) { Destroy(b.gameObject); changed = true; }
                        break;
                    case Act.Crate:
                    case Act.ForcedDoor:
                        foreach (var b in FindObjectsByType<BoardedDoor>(FindObjectsSortMode.None))
                            if (b.Id == id && b.Pryable) { b.ForceOpen(); changed = true; }
                        break;
                    case Act.Pump:
                        PoolSwitch.PullAnywhere();
                        changed = true;
                        break;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    case Act.DevObjective:
                        objectives?.DeveloperComplete();
                        changed = true;
                        break;
#endif
                }
                if (changed && act != Act.Goal && act != Act.Pump)
                {
                    picked.Add(id);
                    if (!save.picked.Contains(id)) save.picked.Add(id);
                }
            }
            finally { applyingRemote = false; }
        }

        // ---------- handing supplies to a friend ----------

        void Give(CoopAvatar friend, PropKind kind)
        {
            if (!Coop.Active || friend == null) return;
            ulong to = friend.OwnerClientId, me = coop.Net.LocalClientId;
            hud?.Flash(kind == PropKind.AlmondWater ? "You handed over a bottle of almond water." : "You handed over a battery.");
            if (Coop.IsOwner) coop.Send(GiveMsg, to, w => { w.WriteValueSafe(to); w.WriteValueSafe(me); w.WriteValueSafe((byte)kind); });
            else coop.Send(GiveMsg, NetworkManager.ServerClientId, w => { w.WriteValueSafe(to); w.WriteValueSafe(me); w.WriteValueSafe((byte)kind); });
        }

        void OnGive(ulong from, FastBufferReader r)
        {
            r.ReadValueSafe(out ulong to);
            r.ReadValueSafe(out ulong giver);
            r.ReadValueSafe(out byte kind);
            if (kind != (byte)PropKind.AlmondWater && kind != (byte)PropKind.Battery) return;

            if (Coop.IsOwner)
            {
                if (!KnownClient(from) || !AllowRate(giveRates, from, 2f, 12)) { Reject(from, "Ignored suspicious item transfer."); return; }
                if (giver != from) { Reject(from, "Rejected spoofed item transfer."); return; }
                if (!KnownClient(to) || to == from) return;
                if (to != coop.Net.LocalClientId)
                {
                    coop.Send(GiveMsg, to, w => { w.WriteValueSafe(to); w.WriteValueSafe(giver); w.WriteValueSafe(kind); });
                    return;
                }
            }
            else
            {
                if (!AntiCheat.SenderIsServer(from)) return;
            }

            if (to != coop.Net.LocalClientId || status == null) return;
            if ((PropKind)kind == PropKind.AlmondWater) status.AlmondWater++;
            else status.Batteries++;
            string who = CoopAvatar.All.TryGetValue(giver, out var a) && a != null ? CoopAvatar.SuitNames[Mathf.Clamp(a.Suit, 0, CoopAvatar.SuitNames.Length - 1)] : "A friend";
            hud?.Flash($"{who} gave you {((PropKind)kind == PropKind.AlmondWater ? "a bottle of almond water" : "a battery")}.");
        }
    }
}

