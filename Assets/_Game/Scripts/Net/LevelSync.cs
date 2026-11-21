using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Backrooms.Net
{
    /// Level events everyone in a co-op game should share (a wall coming down, the record starting, a lure
    /// appearing). The game in charge (single-player, or the co-op owner) decides and tells the others; guests only
    /// play what they're told. Personal scares (a shadow at the edge of your sight) stay on each player's own game.
    public static class LevelSync
    {
        const string Msg = "bk.lvl";
        static readonly Dictionary<string, Action<FastBufferReader>> handlers = new Dictionary<string, Action<FastBufferReader>>();

        /// True when this game decides shared level events.
        public static bool Decides => Coop.Authority;

        public static void On(string key, Action<FastBufferReader> handler) => handlers[key] = handler;
        public static void Off(string key) => handlers.Remove(key);
        public static void Off(string key, Action<FastBufferReader> handler)
        {
            if (handlers.TryGetValue(key, out var current) && current == handler) handlers.Remove(key);
        }

        /// The owner tells everyone else what just happened.
        public static void Send(string key, Action<FastBufferWriter> write = null, NetworkDelivery delivery = NetworkDelivery.ReliableSequenced)
        {
            if (!Coop.IsOwner) return;
            Coop.Session.SendAll(Msg, w => { w.WriteValueSafe(key); write?.Invoke(w); }, delivery);
        }

        internal static void Hook(CoopSession session) => session.Handle(Msg, (from, r) =>
        {
            if (Coop.IsOwner || !Backrooms.Game.AntiCheat.SenderIsServer(from)) return;
            r.ReadValueSafe(out string key);
            if (string.IsNullOrEmpty(key) || key.Length > 64) return;
            if (handlers.TryGetValue(key, out var h)) h(r);
        });

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => handlers.Clear();
    }
}

