using Backrooms.Creatures;
using Backrooms.Meta;
using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// Watches play for the profile: lifetime stats, the level log, and achievements. Enabled only while playing.
    public sealed class ProgressTracker : MonoBehaviour
    {
        FirstPersonController player;
        PlayerTools tools;
        ChunkStreamer streamer;
        Vector3 last;
        float sessionTime, nextCheck, nextSave;
        LevelVisit visit;

        static ProfileData P => Profile.Data;

        public void Init(FirstPersonController player, PlayerTools tools, PlayerStatus status, FearDirector director, ChunkStreamer streamer)
        {
            this.player = player;
            this.tools = tools;
            this.streamer = streamer;
            if (status != null) status.Drank += () => Count(ref P.almondWater, 10, "hydrated");
            if (tools != null)
            {
                tools.Read += r =>
                {
                    if (r.Kind == ReadableKind.Painting) Count(ref P.paintingsTurned, 5, "art_critic");
                    else Count(ref P.notesRead, 10, "reader");
                };
                tools.Pried += b => Count(ref P.boardsPried, 5, "carpenter");
            }
            player.Hopped += n =>
            {
                P.bestHopChain = Mathf.Max(P.bestHopChain, n);
                if (n >= 5) Achievements.Unlock("bunny");
            };
            director.CaughtPlayer += who => { if (who == null || !who.IsLocal) return; P.timesCaught++; Achievements.Unlock("caught"); };
            director.HuntSurvived += () => Count(ref P.huntsSurvived, 1, "survived_hunt");
            director.FacelingSeen += () => Achievements.Unlock("faceling");
        }

        static void Count(ref int stat, int goal, string achievement)
        {
            stat++;
            if (stat >= goal) Achievements.Unlock(achievement);
        }

        public void StartSession()
        {
            sessionTime = 0f;
            last = player.transform.position;
        }

        /// A new level (not a respawn in the same one).
        public void Arrive(string id, string from)
        {
            Profile.Arrive(id, from);
            visit = Profile.Visit(id);
            last = player.transform.position;
            if (id == "0") Achievements.Unlock("first_steps");
            if (Achievements.Find("lvl_" + id) != null) Achievements.Unlock("lvl_" + id);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || player == null) return;   // paused
            var p = player.transform.position;
            float step = new Vector2(p.x - last.x, p.z - last.z).magnitude;
            last = p;
            if (step < 3f) P.distance += step;   // teleports and level changes don't count
            sessionTime += dt;
            P.playSeconds += dt;
            P.longestSession = Mathf.Max(P.longestSession, sessionTime);
            if (visit != null) visit.seconds += dt;
            if (tools != null && tools.CamcorderUp) P.camcorderSeconds += dt;

            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 1f;
            if (P.distance >= 500f) Achievements.Unlock("threshold");
            if (P.distance >= 5000f) Achievements.Unlock("lost");
            if (sessionTime >= 1800f) Achievements.Unlock("marathon");
            if (P.camcorderSeconds >= 120f) Achievements.Unlock("archivist");
            if (PoolSwitch.Pulled) Achievements.Unlock("drained");
            var theme = streamer.Theme;
            if (theme != null && theme.id == "0" && DistrictLayout.HasFloors(theme))
            {
                int floor = streamer.CurrentFloor;
                P.highestFloor = Mathf.Max(P.highestFloor, floor);
                P.lowestFloor = Mathf.Min(P.lowestFloor, floor);
                if (floor >= DistrictLayout.TopFloor) Achievements.Unlock("floor_3");
                if (floor <= DistrictLayout.BottomFloor) Achievements.Unlock("floor_-3");
                if (DistrictLayout.NearCut(streamer.Seed, p, theme)) Achievements.Unlock("the_cut");
            }
            if (Time.time > nextSave) { nextSave = Time.time + 30f; Profile.Save(); }
        }
    }
}
