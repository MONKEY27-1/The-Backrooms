using System;
using Backrooms.Net;
using Backrooms.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms.Game
{
    /// Co-op ownership: asking the owner before someone joins (if they chose that), becoming the owner when the
    /// old one hands the world on, and carrying on where you stood after a reconnect.
    public sealed partial class GameSession
    {
        Action<bool> joinAnswer;
        float joinAskedUntil;

        void WireOwnership()
        {
            coop.SaveJson = () => { Persist(); return JsonUtility.ToJson(save); };
            coop.BecameOwner += AdoptWorld;
            coop.JoinAsked += (name, answer) =>
            {
                joinAnswer = answer;
                joinAskedUntil = Time.unscaledTime + 50f;
                hud?.Flash($"{name} wants to join.   Y: let them in   ·   N: refuse", 50f);
            };
            coop.Status += text => { if (!InMenu) hud?.Flash(text, 4f); };
            var voice = gameObject.AddComponent<VoiceChat>();
            voice.Noise = (at, radius) => MakeNoise(at, radius);   // talking draws hunters
        }

        /// The old owner left you their world: it's in your saves now, and your game runs it.
        void AdoptWorld(string json)
        {
            SaveData theirs = null;
            try { theirs = JsonUtility.FromJson<SaveData>(json); } catch { }
            if (theirs == null || save == null) return;
            theirs.slot = SaveSystem.NewSlot();
            theirs.levelId = save.levelId;   // you're all still where you are
            theirs.arrivalX = save.arrivalX;
            theirs.arrivalZ = save.arrivalZ;
            foreach (var id in save.picked) if (!theirs.picked.Contains(id)) theirs.picked.Add(id);
            foreach (var g in save.goals) if (!theirs.goals.Contains(g)) theirs.goals.Add(g);
            save = theirs;
            director.StepsOnly = false;   // the scares are yours to run now
            Persist();
            hud?.Flash("You're the owner now. This world is in your saves.", 6f);
        }

        /// The owner's answer to a join request, from the keyboard (Y/N), or no after a while.
        void UpdateJoinRequest()
        {
            if (joinAnswer == null) return;
            var k = Keyboard.current;
            bool yes = k != null && k.yKey.wasPressedThisFrame, no = k != null && k.nKey.wasPressedThisFrame;
            if (!yes && !no && Time.unscaledTime < joinAskedUntil) return;
            var answer = joinAnswer;
            joinAnswer = null;
            answer(yes);
            hud?.Flash(yes ? "Letting them in." : "Refused.", 3f);
        }

        /// Back after a dropped connection: if we're still in the same place, carry on from where you stood.
        bool ResumeAfterReconnect(WorldInfo w)
        {
            if (InMenu || save == null || save.slot != null || w.Level != save.levelId) return false;
            foreach (var id in w.Picked ?? new int[0]) { picked.Add(id); if (!save.picked.Contains(id)) save.picked.Add(id); }
            foreach (var g in w.Goals ?? new string[0]) if (!save.goals.Contains(g)) save.goals.Add(g);
            if (w.PoolPulled) PoolSwitch.PullAnywhere();
            return true;
        }
    }
}
