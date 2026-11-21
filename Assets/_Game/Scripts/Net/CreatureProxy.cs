using Backrooms.Creatures;
using Unity.Netcode;
using UnityEngine;

namespace Backrooms.Net
{
    /// A creature as guests see it. The owner's game runs the real creature (its brain, pathing, catching);
    /// this follows it and tells the guests what it is, what it's doing and who it's after, so they see the
    /// grin in the dark and hear the static coming.
    public sealed class CreatureProxy : NetworkBehaviour
    {
        readonly NetworkVariable<byte> kind = new NetworkVariable<byte>();
        readonly NetworkVariable<byte> state = new NetworkVariable<byte>();
        readonly NetworkVariable<ulong> prey = new NetworkVariable<ulong>(ulong.MaxValue);

        public Creature Source;
        Transform glow;
        AudioSource voice;
        float stateTime;
        CreatureState shown;

        public static bool ChasingMe(ulong me)
        {
            foreach (var p in FindObjectsByType<CreatureProxy>(FindObjectsSortMode.None))
                if (p.IsSpawned && (CreatureState)p.state.Value == CreatureState.Chase && p.prey.Value == me) return true;
            return false;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) return;
            var k = (CreatureKind)kind.Value;
            CreatureBodies.Build(k, transform, out glow);
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.maxDistance = 22f;
            voice.loop = true;
            voice.dopplerLevel = 0f;
            if (k == CreatureKind.Smiler)
            {
                voice.clip = Audio.ProceduralAudio.Static();
                voice.pitch = 0.45f;
                voice.volume = 0.25f * Meta.AudioMix.Creatures;
                voice.Play();
                UI.Captions.Say("[crackling static]");
            }
            // Guests used to see a rigid statue gliding along the floor; give the proxy the same
            // subtle body motion the owner's real creature gets.
            var motion = gameObject.AddComponent<CreatureBodyMotion>();
            motion.Initialize(k);
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                if (Source == null) { NetworkObject.Despawn(true); return; }
                transform.SetPositionAndRotation(Source.transform.position, Source.transform.rotation);
                kind.Value = (byte)Source.Kind;
                state.Value = (byte)Source.State;
                prey.Value = Source.Target != null && Source.Target.TryGetComponent(out NetworkObject no) ? no.OwnerClientId
                           : Source.Target != null && Source.Target.IsLocal ? NetworkManager.LocalClientId : ulong.MaxValue;
                return;
            }
            var s = (CreatureState)state.Value;
            if (s != shown) { shown = s; stateTime = 0f; }
            CreatureBodies.SetSkinStealerRevealed(transform, s != CreatureState.Disguise);
            stateTime += Time.deltaTime;
            if (glow != null) glow.gameObject.SetActive(s != CreatureState.Leave || Mathf.Repeat(stateTime * 7f, 1f) < 0.5f);
            if (voice != null && voice.clip != null)
            {
                if (s == CreatureState.Chase) { voice.pitch = 0.75f; voice.volume = Mathf.MoveTowards(voice.volume, 0.6f * Meta.AudioMix.Creatures, Time.deltaTime); }
                else if (s == CreatureState.Leave) voice.volume = Mathf.MoveTowards(voice.volume, 0f, Time.deltaTime * 0.3f);
                else { voice.pitch = 0.45f; voice.volume = 0.25f * Meta.AudioMix.Creatures; }
            }
        }

        /// The owner's game: mirror a new creature for the guests.
        public static void Mirror(Creature c)
        {
            if (!Coop.IsOwner || c == null) return;
            var prefab = Resources.Load<GameObject>("CoopCreature");
            if (prefab == null) return;
            var go = Instantiate(prefab, c.transform.position, c.transform.rotation);
            var proxy = go.GetComponent<CreatureProxy>();
            proxy.Source = c;
            proxy.kind.Value = (byte)c.Kind;
            go.GetComponent<NetworkObject>().Spawn(true);
        }
    }
}
