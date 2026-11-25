using System.Collections.Generic;
using Backrooms.Meta;
using Backrooms.Net;
using Backrooms.Player;
using Unity.Netcode;
using UnityEngine;

namespace Backrooms.Game
{
    /// Co-op group rules, decided by the owner. Hardcore deaths persist across levels; after three successful
    /// level transitions the dead player respawns. Other difficulties retain the existing gather/exit behavior.
    public sealed partial class GameSession
    {
        const string DiedMsg = "bk.died", DieMsg = "bk.die", ThroughMsg = "bk.through", SayMsg = "bk.say";
        const string ReviveMsg = "bk.revive", HardcoreRosterMsg = "bk.hc.roster", RagdollMsg = "bk.ragdoll";
        const float GatherRadius = 6f;

        static bool OneDeathKillsAll => !Difficulty.Hardcore && Difficulty.Level >= 2;   // Survival Hard/Extreme
        bool HardcoreCoop => Difficulty.Hardcore && Coop.Active;
        Spectator spectator;
        readonly HashSet<ulong> deadClients = new HashSet<ulong>(), throughClients = new HashSet<ulong>();
        readonly Dictionary<ulong, string> hardcorePlayerIds = new Dictionary<ulong, string>();
        string pendingTarget;
        Vector2Int pendingArrival;
        Vector3 pendingAt;
        float nextGatherCheck, nextGatherSay, nextHardcoreSync;

        void WireGroup()
        {
            spectator = player.GetComponent<Spectator>() ?? player.gameObject.AddComponent<Spectator>();
            spectator.Friends = LivingFriends;
            coop.Handle(DiedMsg, (from, r) => { r.ReadValueSafe(out int cause); OnPlayerDied(from, cause); });
            coop.Handle(DieMsg, (from, r) => DieHere());
            coop.Handle(ThroughMsg, (from, r) => GoneThrough());
            coop.Handle(SayMsg, (from, r) => { r.ReadValueSafe(out string text); hud?.Flash(text, 4f); });
            coop.Handle(ReviveMsg, (from, r) => ReviveHere());
            coop.Handle(RagdollMsg, (from, r) =>
            {
                r.ReadValueSafe(out Vector3 position);
                r.ReadValueSafe(out Quaternion rotation);
                r.ReadValueSafe(out int cause);
                DeathRagdoll.Spawn(position, rotation, default, CauseName(cause));
            });
            coop.Handle(HardcoreRosterMsg, (from, r) =>
            {
                r.ReadValueSafe(out bool dead);
                r.ReadValueSafe(out int wait);
                if (!HardcoreCoop || Coop.IsOwner) return;
                EnsureHardcoreLists();
                string ownId = CoopSession.MyPlayerId;
                int ownIndex = save.hardcoreDead.IndexOf(ownId);
                if (dead && ownIndex < 0) { save.hardcoreDead.Add(ownId); save.hardcoreWait.Add(Mathf.Clamp(wait, 0, 2)); }
                else if (!dead && ownIndex >= 0) { save.hardcoreDead.RemoveAt(ownIndex); save.hardcoreWait.RemoveAt(ownIndex); }
                else if (dead) save.hardcoreWait[ownIndex] = Mathf.Clamp(wait, 0, 2);
                var playerInfo = Players.Local;
                if (playerInfo != null) playerInfo.Alive = !dead;
                if (dead && spectator != null && !spectator.Watching) DieHere();
                else if (!dead && spectator != null && spectator.Watching) ReviveHere();
                CoopAvatar.Mine?.SetDiedServer(dead);
                if (dead) hud?.Flash($"Dead Â· party progress {Mathf.Clamp(wait, 0, 3)}/3 levels", 5f);
            });
        }

        List<(Transform, string)> LivingFriends()
        {
            var list = new List<(Transform, string)>();
            foreach (var a in CoopAvatar.All.Values)
                if (a != null && !a.IsOwner && a.Alive && !deadClients.Contains(a.OwnerClientId)) list.Add((a.transform, CoopAvatar.SuitNames[Mathf.Clamp(a.Suit, 0, CoopAvatar.SuitNames.Length - 1)]));
            return list;
        }

        /// A level transition or checkpoint reload. Hardcore counts only successful transitions to another level.
        void ReviveForLevel(bool changedLevel)
        {
            throughClients.Clear();
            pendingTarget = null;
            Players.Local?.ResetDeath();
            if (HardcoreCoop)
            {
                EnsureHardcoreLists();
                if (!Coop.IsOwner)
                {
                    string meId = CoopSession.MyPlayerId;
                    int meIndex = save.hardcoreDead.IndexOf(meId);
                    var localPlayer = Players.Local;
                    if (localPlayer != null) localPlayer.Alive = meIndex < 0;
                    if (meIndex >= 0 && spectator != null && !spectator.Watching) DieHere();
                    else if (meIndex < 0 && spectator != null && spectator.Watching) ReviveHere();
                    if (meIndex >= 0) hud?.Flash($"Dead Â· party progress {Mathf.Clamp(save.hardcoreWait[meIndex], 0, 3)}/3 levels", 5f);
                    return;
                }
                SyncDeadFromSave();
                if (changedLevel)
                {
                    for (int i = save.hardcoreWait.Count - 1; i >= 0; i--)
                    {
                        save.hardcoreWait[i]++;
                        if (save.hardcoreWait[i] < 3) continue;
                        string id = save.hardcoreDead[i];
                        save.hardcoreDead.RemoveAt(i);
                        save.hardcoreWait.RemoveAt(i);
                        ulong client = ClientFor(id);
                        if (client != ulong.MaxValue) deadClients.Remove(client);
                        if (CoopAvatar.All.TryGetValue(client, out var revivedAvatar) && revivedAvatar != null) revivedAvatar.SetDiedServer(false);
                        if (client == MyId) ReviveHere();
                        else if (client != ulong.MaxValue) coop.Send(ReviveMsg, client, w => { });
                    }
                }
                SyncDeadFromSave();
                SyncHardcoreRoster();
                Persist();
                return;
            }
            deadClients.Clear();
            if (Coop.IsOwner) foreach (var avatar in CoopAvatar.All.Values) avatar?.SetDiedServer(false);
            if (spectator != null && spectator.Watching) spectator.End();
            var me = Players.Local;
            if (me != null) me.Alive = true;
        }

        ulong MyId => coop != null && coop.Net != null ? coop.Net.LocalClientId : 0;

        // ---------- deaths ----------

        /// You died here (caught, or fell). In co-op the owner decides what that means for everyone.
        void DieInCoop()
        {
            int cause = Players.Local != null && Players.Local.DeathCause.HasValue ? (int)Players.Local.DeathCause.Value : -1;
            if (Coop.IsOwner) { DieHere(); OnPlayerDied(MyId, cause); }
            else { DieHere(); coop.Send(DiedMsg, NetworkManager.ServerClientId, w => w.WriteValueSafe(cause)); }
        }

        void ReviveHere()
        {
            StartCoroutine(ReviveAfterEntry());
        }

        System.Collections.IEnumerator ReviveAfterEntry()
        {
            while (busy) yield return null;
            if (InMenu) yield break;
            if (spectator != null && spectator.Watching) spectator.End();
            if (!InMenu && menus != null && !menus.Paused) SetPlayerActive(true);
            hud?.Flash("Your party brought you back.", 5f);
        }

        string PlayerIdOf(ulong client) => client == MyId ? CoopSession.MyPlayerId
            : CoopAvatar.All.TryGetValue(client, out var a) && a != null ? a.PlayerId : "";

        ulong ClientFor(string id)
        {
            if (string.IsNullOrEmpty(id)) return ulong.MaxValue;
            if (id == CoopSession.MyPlayerId) return MyId;
            foreach (var kv in CoopAvatar.All) if (kv.Value != null && kv.Value.PlayerId == id) return kv.Key;
            return ulong.MaxValue;
        }

        void EnsureHardcoreLists()
        {
            save.hardcoreDead ??= new List<string>();
            save.hardcoreWait ??= new List<int>();
            while (save.hardcoreWait.Count < save.hardcoreDead.Count) save.hardcoreWait.Add(0);
            while (save.hardcoreWait.Count > save.hardcoreDead.Count) save.hardcoreWait.RemoveAt(save.hardcoreWait.Count - 1);
        }

        void SyncHardcoreRoster()
        {
            if (!HardcoreCoop || !Coop.IsOwner || coop?.Net == null) return;
            EnsureHardcoreLists();
            foreach (ulong client in coop.Net.ConnectedClientsIds)
            {
                string id = PlayerIdOf(client);
                if (string.IsNullOrEmpty(id)) continue;
                int dead = save.hardcoreDead.IndexOf(id);
                int wait = dead >= 0 ? save.hardcoreWait[dead] : -1;
                coop.Send(HardcoreRosterMsg, client, w => { w.WriteValueSafe(dead >= 0); w.WriteValueSafe(wait); });
            }
        }

        void SyncDeadFromSave()
        {
            if (!HardcoreCoop || !Coop.IsOwner) return;
            deadClients.Clear();
            hardcorePlayerIds.Clear();
            foreach (var a in CoopAvatar.All.Values)
                if (a != null && a.IsSpawned)
                {
                    int i = save.hardcoreDead.IndexOf(a.PlayerId);
                    if (i >= 0)
                    {
                        deadClients.Add(a.OwnerClientId);
                        hardcorePlayerIds[a.OwnerClientId] = a.PlayerId;
                        if (a.Agent != null) a.Agent.Alive = false;
                        a.SetDiedServer(true);
                    }
                    else
                    {
                        if (a.Agent != null) a.Agent.Alive = true;
                        a.SetDiedServer(false);
                    }
                }
            int ownerIndex = save.hardcoreDead.IndexOf(CoopSession.MyPlayerId);
            if (ownerIndex >= 0) { deadClients.Add(MyId); hardcorePlayerIds[MyId] = CoopSession.MyPlayerId; }
        }

        void FailHardcoreRun()
        {
            SayAll("Everyone died. This Hardcore run is over.");
            if (!string.IsNullOrEmpty(save.slot)) SaveSystem.Delete(save.slot);
            if (Coop.Active) _ = coop.Leave();
            StartCoroutine(Transition(ShowMenu));
        }

        /// Become a spectator (Easy/Normal) until the group moves on â€” the owner sends a checkpoint if needed.
        void DieHere()
        {
            if (spectator == null || spectator.Watching) return;
            if (OneDeathKillsAll) { hud?.Flash("Everything goes to static.", 3f); return; }   // the owner is sending everyone back
            spectator.Begin();
            hud?.Flash("You're dead. You'll come back when someone finds the way out. Click to watch another friend.", 6f);
        }

        static string CauseName(int cause) => cause >= 0 && System.Enum.IsDefined(typeof(Backrooms.Creatures.CreatureKind), cause) ? ((Backrooms.Creatures.CreatureKind)cause).ToString() : null;

        void OnPlayerDied(ulong who, int cause = -1)
        {
            if (!Coop.IsOwner || deadClients.Contains(who)) return;
            deadClients.Add(who);
            string causeName = CauseName(cause);
            Vector3 deathPosition = who == MyId ? player.transform.position
                : CoopAvatar.All.TryGetValue(who, out var fallen) && fallen != null ? fallen.transform.position : player.transform.position;
            Quaternion deathRotation = who == MyId ? player.transform.rotation
                : CoopAvatar.All.TryGetValue(who, out var turned) && turned != null ? turned.transform.rotation : player.transform.rotation;
            DeathRagdoll.Spawn(deathPosition, deathRotation, default, causeName);
            foreach (ulong client in coop.Net.ConnectedClientsIds)
                if (client != MyId) coop.Send(RagdollMsg, client, w => { w.WriteValueSafe(deathPosition); w.WriteValueSafe(deathRotation); w.WriteValueSafe(cause); });
            throughClients.Remove(who);
            if (CoopAvatar.All.TryGetValue(who, out var deadAvatar) && deadAvatar != null)
            {
                if (deadAvatar.Agent != null) deadAvatar.Agent.Alive = false;
                deadAvatar.SetDiedServer(true);
            }
            if (HardcoreCoop)
            {
                EnsureHardcoreLists();
                string id = PlayerIdOf(who);
                if (string.IsNullOrEmpty(id)) id = "client:" + who;
                int i = save.hardcoreDead.IndexOf(id);
                if (i < 0) { save.hardcoreDead.Add(id); save.hardcoreWait.Add(0); }
                hardcorePlayerIds[who] = id;
                SyncHardcoreRoster();
                Persist();
                SayAll($"{NameOf(who)} died. They'll return after the party beats three levels.");
                if (AliveCount() == 0) FailHardcoreRun();
                return;
            }
            if (OneDeathKillsAll) { SayAll($"{NameOf(who)} died. Everyone wakes at the checkpoint."); BackToCheckpoint(); return; }
            if (AliveCount() == 0) { SayAll("Nobody is left. Everyone wakes at the checkpoint."); BackToCheckpoint(); return; }
            SayAll($"{NameOf(who)} is dead. Find the way out to bring them back.");
            CheckHardExit();   // the dead don't need to step through
        }

        void BackToCheckpoint() => GoTo(save.levelId, new Vector2Int(save.arrivalX, save.arrivalZ));

        int AliveCount()
        {
            int n = 0;
            foreach (var id in coop.Net.ConnectedClientsIds) if (!deadClients.Contains(id)) n++;
            return n;
        }

        // ---------- exits ----------

        /// The owner: someone (`who`) walked into an exit. Returns true if the group leaves now.
        bool GroupExit(string target, Vector2Int arrival, Vector3 at, ulong who)
        {
            if (OneDeathKillsAll || HardcoreCoop)
            {
                if (pendingTarget == null) { pendingTarget = target; pendingArrival = arrival; pendingAt = at; }
                if (throughClients.Add(who))
                {
                    if (who == MyId) GoneThrough();
                    else coop.Send(ThroughMsg, who, w => { });
                }
                return CheckHardExit();
            }
            pendingTarget = target;
            pendingArrival = arrival;
            pendingAt = at;
            nextGatherCheck = 0f;
            return CheckGathered(true);
        }

        /// Hard/Extreme: you're through the exit; wait for the others.
        void GoneThrough()
        {
            if (spectator == null || spectator.Watching) return;
            spectator.Begin();
            hud?.Flash("You're through. Waiting in the static for the others...", 6f);
        }

        bool CheckHardExit()
        {
            if (pendingTarget == null) return false;
            int alive = AliveCount(), through = 0;
            foreach (var id in throughClients) if (!deadClients.Contains(id)) through++;
            if (alive > 0 && through >= alive) { LeaveTogether(); return true; }
            if (through > 0) SayAll($"{through} of {alive} through. The rest still have to find it.");
            return false;
        }

        /// Easy/Normal: everyone alive within a few metres of the exit?
        bool CheckGathered(bool justArrived)
        {
            if (pendingTarget == null) return false;
            int alive = 0, near = 0;
            foreach (var a in Players.All)
            {
                if (a == null || !a.Alive) continue;
                alive++;
                if (Vector3.Distance(a.Position, pendingAt) < GatherRadius) near++;
            }
            if (alive > 0 && near >= alive) { LeaveTogether(); return true; }
            if (near == 0) { pendingTarget = null; return false; }   // they walked away
            if (justArrived || Time.time >= nextGatherSay)
            {
                nextGatherSay = Time.time + 6f;
                SayAll($"A way out. {near} of {alive} here â€” everyone alive has to gather at it.");
            }
            return false;
        }

        void LeaveTogether()
        {
            var t = pendingTarget;
            var arrival = pendingArrival;
            pendingTarget = null;
            GoTo(t, arrival);
        }

        void UpdateGroup()
        {
            if (HardcoreCoop && Coop.IsOwner && Time.time >= nextHardcoreSync)
            {
                nextHardcoreSync = Time.time + 1f;
                SyncDeadFromSave();
                SyncHardcoreRoster();
            }
            if (!Coop.IsOwner || busy || pendingTarget == null || OneDeathKillsAll || HardcoreCoop) return;
            if (Time.time < nextGatherCheck) return;
            nextGatherCheck = Time.time + 0.5f;
            CheckGathered(false);
        }

        // ---------- helpers ----------

        void SayAll(string text)
        {
            hud?.Flash(text, 4f);
            coop.SendAll(SayMsg, w => w.WriteValueSafe(text));
        }

        string NameOf(ulong id) =>
            CoopAvatar.All.TryGetValue(id, out var a) && a != null ? CoopAvatar.SuitNames[Mathf.Clamp(a.Suit, 0, CoopAvatar.SuitNames.Length - 1)] : "Someone";

        ulong ClientOf(PlayerAgent agent)
        {
            if (agent == null || agent.IsLocal) return MyId;
            return agent.TryGetComponent(out NetworkObject no) ? no.OwnerClientId : MyId;
        }
    }
}
