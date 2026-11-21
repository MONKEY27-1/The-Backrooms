#if UNITY_EDITOR || DEVELOPMENT_BUILD
using Backrooms.Creatures;
using Backrooms.Net;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    public sealed partial class GameSession
    {
        bool DevAllowed => Debug.isDebugBuild;

        public void DevGoTo(string level, Vector2Int arrival)
        {
            if (!DevAllowed || !Coop.Authority || catalog.Find(level) == null || busy) return;
            GoTo(level, arrival);
        }

        public void DevSetBlackout(float value)
        {
            if (DevAllowed && Coop.Authority && streamer != null) streamer.Blackout = Mathf.Clamp01(value);
        }

        public void DevResetCheckpoint()
        {
            if (!DevAllowed || !Coop.Authority || save == null || busy) return;
            StartCoroutine(Transition(() => Enter(save.levelId, new Vector2Int(save.arrivalX, save.arrivalZ))));
        }

        public void DevSpawnCreature(CreatureKind kind)
        {
            if (!DevAllowed || !Coop.Authority || director == null) return;
            director.DeveloperSpawn(kind);
        }

        public void DevStartHunt()
        {
            if (DevAllowed && Coop.Authority && director != null) director.DeveloperStartHunt();
        }

        public void DevClearCreatures()
        {
            if (DevAllowed && Coop.Authority && director != null) director.DeveloperClearCreatures();
        }

        public void DevTriggerEvent()
        {
            if (!DevAllowed || !Coop.Authority || environmentalEvents == null) return;
            environmentalEvents.DeveloperTrigger();
        }

        public void DevCompleteObjective()
        {
            if (!DevAllowed || !Coop.Authority || objectives == null) return;
            objectives.DeveloperComplete();
            ShareDeveloperObjective();
        }

        public void DevSharePickup(Pickup pickup)
        {
            if (DevAllowed && Coop.IsOwner) ShareDeveloperPickup(pickup);
        }

        public void DevSave()
        {
            if (!DevAllowed) return;
            if (Coop.IsGuest) PersistExploration();
            else Persist();
        }

        public void DevDeleteSave()
        {
            if (!DevAllowed || !Coop.Authority || save == null || busy) return;
            if (!string.IsNullOrEmpty(save.slot)) SaveSystem.Delete(save.slot);
            if (Coop.Active) LeaveCoop();
            save = null;
            StartCoroutine(Transition(ShowMenu));
        }
    }
}
#endif
