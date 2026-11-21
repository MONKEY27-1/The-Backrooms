using System.Collections.Generic;
using Backrooms.Creatures;
using Backrooms.Net;
using Backrooms.Player;
using Unity.Netcode;
using UnityEngine;

namespace Backrooms.Game
{
    /// Co-op: the fear director runs on the owner's game for everyone. Creatures are mirrored for the guests,
    /// a hunt's blackout reaches every screen, phantom footsteps go to the one friend picked to hear them,
    /// guests' loud noises draw hunters, and the owner keeps the level loaded around each friend.
    public sealed partial class GameSession
    {
        const string StepsMsg = "bk.steps", DarkMsg = "bk.dark", NoiseMsg = "bk.noise";
        float sentDark = -1f, nextDarkSend, nextChaseCheck;
        readonly System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit> noiseRates = new System.Collections.Generic.Dictionary<ulong, AntiCheat.RateLimit>();

        void WireDirector()
        {
            Creature.Spawned -= CreatureProxy.Mirror;
            Creature.Spawned += CreatureProxy.Mirror;
            director.RemoteSteps += who =>
            {
                if (who != null && who.TryGetComponent(out NetworkObject no)) coop.Send(StepsMsg, no.OwnerClientId, w => { });
            };
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

