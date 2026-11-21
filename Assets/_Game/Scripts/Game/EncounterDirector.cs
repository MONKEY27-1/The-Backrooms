using System;
using System.Collections.Generic;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// The "fresh encounters" layer. The building's layout is fixed by the seed; this decides which curiosity,
    /// if any, fills each slot the layout proposes. It favours kinds you haven't seen, and the longer you go
    /// without one, the likelier the next. Choices are remembered per slot, so a place looks the same when you return.
    public sealed class EncounterDirector : ICuriosityResolver
    {
        const int MaxRemembered = 4000;
        static readonly CuriosityKind[] Kinds = (CuriosityKind[])Enum.GetValues(typeof(CuriosityKind));

        readonly SaveData save;
        readonly Dictionary<int, CuriosityKind> chosen = new Dictionary<int, CuriosityKind>();
        readonly System.Random random = new System.Random();
        float lastEncounter;

        public EncounterDirector(SaveData save)
        {
            this.save = save;
            for (int i = 0; i < save.curiositySlots.Count && i < save.curiosityKinds.Count; i++)
                chosen[save.curiositySlots[i]] = (CuriosityKind)save.curiosityKinds[i];
            while (save.curiositySeen.Count < Kinds.Length) save.curiositySeen.Add(0);
            lastEncounter = Time.time;
        }

        public CuriosityKind Resolve(CuriositySlot slot)
        {
            if (chosen.TryGetValue(slot.Id, out var known)) return known;

            float quietMinutes = (Time.time - lastEncounter) / 60f;
            float chance = 0.2f * Mathf.Clamp(1f + quietMinutes * 0.15f, 1f, 2f);   // about one slot in five, more after a quiet stretch
            var kind = CuriosityKind.None;
            if (random.NextDouble() < chance)
            {
                float total = 0f;
                foreach (var k in Kinds) total += Weight(k, slot);
                double roll = random.NextDouble() * total;
                foreach (var k in Kinds)
                {
                    roll -= Weight(k, slot);
                    if (roll < 0) { kind = k; break; }
                }
            }
            Remember(slot.Id, kind);
            if (kind != CuriosityKind.None)
            {
                save.curiositySeen[(int)kind]++;
                lastEncounter = Time.time;
            }
            return kind;
        }

        /// Rarer the more often you've met it, so new things keep turning up.
        float Weight(CuriosityKind k, CuriositySlot slot) =>
            k == CuriosityKind.None || !CuriosityLibrary.Fits(k, slot) ? 0f
                : CuriosityLibrary.BaseWeight(k) / (1f + 2f * save.curiositySeen[(int)k]);

        void Remember(int id, CuriosityKind kind)
        {
            chosen[id] = kind;
            save.curiositySlots.Add(id);
            save.curiosityKinds.Add((int)kind);
            if (save.curiositySlots.Count > MaxRemembered)   // forget the oldest; the world is too big to remember everything
            {
                chosen.Remove(save.curiositySlots[0]);
                save.curiositySlots.RemoveAt(0);
                save.curiosityKinds.RemoveAt(0);
            }
        }
    }
}
