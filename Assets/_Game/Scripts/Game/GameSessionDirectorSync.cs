using System.Collections.Generic;
using Backrooms.Creatures;
using Backrooms.Net;
using Backrooms.Player;
using Backrooms.World;
using Unity.Netcode;
using UnityEngine;

namespace Backrooms.Game
{
    /// Co-op: the fear director runs on the owner's game for everyone. Creatures are mirrored for the guests,
    /// a hunt's blackout reaches every screen, phantom footsteps go to the one friend picked to hear them,
    /// guests' loud noises draw hunters, and the owner keeps the level loaded around each friend.
    public sealed partial class GameSession
    {
        const string StepsMsg = "bk.steps", DarkMsg = "bk.dark", NoiseMsg = "bk.noise", ThrowMsg = "bk.throw", VoiceMsg = "bk.whisper";
        const string ExecuteMsg = "bk.execute", DeathPoseMsg = "bk.deathpose";
        float sentDark = -1f, nextDarkSend, nextChaseCheck;
        readonly System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit> noiseRates = new System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit>();

        void WireDirector()
        {
            coop.Handle(DeathPoseMsg, (from, r) =>
            {
                if (!Coop.IsOwner || !CoopAvatar.All.TryGetValue(from, out var avatar) || avatar.OwnerClientId != from || !avatar.Alive) return;
                r.ReadValueSafe(out byte rawKind); r.ReadValueSafe(out byte variant);
                r.ReadValueSafe(out Vector3 at); r.ReadValueSafe(out float yaw);
                if (!System.Enum.IsDefined(typeof(CreatureKind), (int)rawKind)) return;
                if (!FiniteDeathVector(at) || float.IsNaN(yaw) || float.IsInfinity(yaw)) return;
                avatar.PlayDeathPose(new DeathAttack { Kind = (CreatureKind)rawKind, Variant = variant & 1, KillerPosition = at, KillerYaw = yaw });
            });
            Creature.Spawned -= CreatureProxy.Mirror;
            Creature.Spawned += CreatureProxy.Mirror;
            Creature.RemoteExecution -= SendExecution;
            Creature.RemoteExecution += SendExecution;
            director.RemoteSteps += who =>
            {
                if (who != null && who.TryGetComponent(out NetworkObject no)) coop.Send(StepsMsg, no.OwnerClientId, w => { });
            };
            director.RemoteWhisper += (who, line, at) =>
            {
                if (who != null && who.TryGetComponent(out NetworkObject no)) coop.Send(VoiceMsg, no.OwnerClientId, w => { w.WriteValueSafe(line); w.WriteValueSafe(at); });
            };
            coop.Handle(VoiceMsg, (from, r) =>
            {
                if (!AntiCheat.SenderIsServer(from)) return;
                r.ReadValueSafe(out string line);
                r.ReadValueSafe(out Vector3 at);
                if (!string.IsNullOrEmpty(line) && line.Length < 120) director.WhisperHere(line, at);
            });
            coop.Handle(StepsMsg, (from, r) => { if (AntiCheat.SenderIsServer(from)) director.StartStepsHere(); });
            coop.Handle(DarkMsg, (from, r) =>
            {
                if (!AntiCheat.SenderIsServer(from)) return;
                r.ReadValueSafe(out float dark);
                if (!!float.IsNaN(dark) && !float.IsInfinity(dark)) return;
                dark = Mathf.Clamp01(dark);
                if (dark > 0.5f && streamer.Blackout < 0.5f) UI.Captions.Say("[the lights die]");
                streamer.Blackout = dark;
            });
            coop.Handle(ThrowMsg, (from, r) =>
            {
                r.ReadValueSafe(out byte kind);
                r.ReadValueSafe(out Vector3 at);
                r.ReadValueSafe(out Vector3 v);
                if (kind != (byte)PropKind.Glowstick && kind != (byte)PropKind.Exterminator && kind != (byte)PropKind.Repellent && kind != (byte)PropKind.Flare && kind != (byte)PropKind.AlmondWater) return;
                if (Coop.IsOwner)
                {
                    if (!noiseRates.TryGetValue(from, out var rl)) rl = new AntiCheat.RateLimit(1f, 8);
                    bool ok = rl.Allow(Time.unscaledTime);
                    noiseRates[from] = rl;
                    if (!ok || !AntiCheat.ReasonableNoise(at, 1f, from)) return;
                    v = Vector3.ClampMagnitude(v, 20f);
                    foreach (var id in coop.Net.ConnectedClientsIds)   // pass it on to the other guests
                        if (id != from && id != coop.Net.LocalClientId) coop.Send(ThrowMsg, id, w => { w.WriteValueSafe(kind); w.WriteValueSafe(at); w.WriteValueSafe(v); });
                }
                else if (!AntiCheat.SenderIsServer(from)) return;
                Thrown.Spawn((PropKind)kind, at, v, Coop.Authority);
            });
            coop.Handle(NoiseMsg, (from, r) =>
            {
                r.ReadValueSafe(out Vector3 at);
                r.ReadValueSafe(out float radius);
                if (!Coop.IsOwner) return;
                if (!noiseRates.TryGetValue(from, out var rl)) rl = new AntiCheat.RateLimit(1f, 8);
                bool ok = rl.Allow(Time.unscaledTime);
                noiseRates[from] = rl;
                if (!ok || !AntiCheat.ReasonableNoise(at, radius, from)) return;
                director.Noise(at, Mathf.Clamp(radius, 0.5f, 40f));
            });
            coop.Handle(ExecuteMsg, (from, r) =>
            {
                if (!AntiCheat.SenderIsServer(from) || Coop.IsOwner) return;
                r.ReadValueSafe(out byte rawKind); r.ReadValueSafe(out byte variant);
                r.ReadValueSafe(out Vector3 at); r.ReadValueSafe(out float yaw);
                if (!System.Enum.IsDefined(typeof(CreatureKind), (int)rawKind)) return;
                var me = Players.Local;
                if (me == null || !me.Alive || me.Dying) return;
                me.Dying = true;
                var attack = new DeathAttack { Kind = (CreatureKind)rawKind, Variant = variant & 1, KillerPosition = at, KillerYaw = yaw };
                me.ReceiveFatalAttack(attack);
                OnCaught(me);   // the sequence sends bk.died only after the victim has seen it
            });
        }

        static bool FiniteDeathVector(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z)
            && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);

        /// Only the victim reports an actual killing blow; the authority never guesses a guest's health.
        void SyncDeathPose(DeathAttack attack)
        {
            if (!Coop.Active) return;
            byte kind = (byte)attack.Kind, variant = (byte)(attack.Variant & 1);
            Vector3 at = attack.KillerPosition;
            float yaw = attack.KillerYaw;
            if (Coop.IsOwner && CoopAvatar.All.TryGetValue(MyId, out var avatar)) avatar.PlayDeathPose(attack);
            else if (Coop.IsGuest) coop.Send(DeathPoseMsg, NetworkManager.ServerClientId, w =>
            {
                w.WriteValueSafe(kind); w.WriteValueSafe(variant); w.WriteValueSafe(at); w.WriteValueSafe(yaw);
            });
        }

        /// One-touch kills don't apply health damage, so they travel separately from bk.hurt.
        void SendExecution(DeathAttack attack, PlayerAgent victim)
        {
            if (!Coop.IsOwner || victim == null || victim.IsLocal) return;
            ulong client = ClientOf(victim);
            byte kind = (byte)attack.Kind, variant = (byte)attack.Variant;
            Vector3 at = attack.KillerPosition;
            float yaw = attack.KillerYaw;
            coop.Send(ExecuteMsg, client, w => { w.WriteValueSafe(kind); w.WriteValueSafe(variant); w.WriteValueSafe(at); w.WriteValueSafe(yaw); });
        }

        /// You threw something: it lands in your game now and in everyone else's (the owner's copy is the one that counts).
        void ThrowShared(PropKind kind, Vector3 at, Vector3 v)
        {
            Thrown.Spawn(kind, at, v, Coop.Authority);
            if (!Coop.Active) return;
            byte k = (byte)kind;
            if (Coop.IsOwner) coop.SendAll(ThrowMsg, w => { w.WriteValueSafe(k); w.WriteValueSafe(at); w.WriteValueSafe(v); });
            else coop.Send(ThrowMsg, NetworkManager.ServerClientId, w => { w.WriteValueSafe(k); w.WriteValueSafe(at); w.WriteValueSafe(v); });
        }

        /// A loud noise you made (prying boards, shouting): hunters on the owner's game come to look.
        public void MakeNoise(Vector3 at, float radius)
        {
            if (Coop.Authority) director.Noise(at, radius);
            else coop.Send(NoiseMsg, NetworkManager.ServerClientId, w => { w.WriteValueSafe(at); w.WriteValueSafe(radius); }, NetworkDelivery.Unreliable);
        }

        void UpdateDirectorSync()
        {
            if (!Coop.Active)
            {
                if (streamer.ExtraFocus.Count > 0) streamer.ExtraFocus.Clear();
                return;
            }
            if (Coop.IsOwner)
            {
                streamer.ExtraFocus.Clear();
                foreach (var a in Players.All)
                    if (a != null && !a.IsLocal && a.Alive) streamer.ExtraFocus.Add(a.Position);
                // Big changes at once (a hunt, lights-out); small wobbles (the record in Level 188) only now and then.
                float change = Mathf.Abs(sentDark - streamer.Blackout);
                if (change > 0.3f || (change > 0.01f && Time.time >= nextDarkSend) || Time.time >= nextDarkSend + 5f)
                {
                    sentDark = streamer.Blackout;
                    nextDarkSend = Time.time + 5f;   // now and then anyway, for anyone who just joined
                    float d = sentDark;
                    coop.SendAll(DarkMsg, w => w.WriteValueSafe(d));
                }
            }
            else if (Time.time >= nextChaseCheck)
            {
                nextChaseCheck = Time.time + 0.25f;
                FirstPersonController.Chased = CreatureProxy.ChasingMe(MyId);
            }
        }
    }
}

