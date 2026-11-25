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
        readonly NetworkVariable<int> wounds = new NetworkVariable<int>();
        readonly NetworkVariable<ulong> prey = new NetworkVariable<ulong>(ulong.MaxValue);
        readonly NetworkVariable<bool> hasPocket = new NetworkVariable<bool>();
        readonly NetworkVariable<int> suit = new NetworkVariable<int>(-1);   // a Skin-Stealer wearing a friend's suit
        readonly NetworkVariable<Vector3> pocketAt = new NetworkVariable<Vector3>();
        World.ChunkStreamer.DarkPocket pocket;   // a lurking Smiler's dead lights, mirrored here

        public Creature Source;
        public CreatureKind Kind => Source != null ? Source.Kind : (CreatureKind)kind.Value;
        SmilerLook smiler;
        HoundLook hound;
        SkinStealerLook stealer;
        ClumpLook clump;
        FacelingLook faceling;
        MothFlock flock;
        MothWalker walker;
        PartygoerLook guest;
        WretchLook wretch;
        AudioSource voice;
        CreatureWounds woundView;
        float stateTime;
        CreatureState shown;

        public static bool ChasingMe(ulong me)
        {
            foreach (var p in FindObjectsByType<CreatureProxy>(FindObjectsSortMode.None))
                if (p.IsSpawned && (CreatureState)p.state.Value == CreatureState.Chase && p.prey.Value == me) return true;
            return false;
        }

        public override void OnNetworkDespawn()
        {
            World.ChunkStreamer.Instance?.RemoveDarkPocket(pocket);
            pocket = null;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer) return;
            woundView = gameObject.AddComponent<CreatureWounds>();
            var k = (CreatureKind)kind.Value;
            if (k == CreatureKind.SkinStealer) stealer = SkinStealerLook.Build(transform, suit.Value);
            else if (Creature.IsFaceling(k)) faceling = FacelingLook.Build(transform, k, suit.Value);
            else if (k == CreatureKind.Wretch || k == CreatureKind.WretchMass) wretch = WretchLook.Build(transform, k, suit.Value);
            else CreatureBodies.Build(k, transform);
            smiler = GetComponent<SmilerLook>();
            hound = GetComponent<HoundLook>();
            clump = GetComponent<ClumpLook>();
            flock = GetComponent<MothFlock>();
            walker = GetComponent<MothWalker>();
            guest = GetComponent<PartygoerLook>();
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 1f;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.maxDistance = 22f;
            voice.loop = true;
            voice.dopplerLevel = 0f;
            if (wretch != null)   // its voice is how you judge it
            {
                voice.clip = Audio.CreatureSounds.Growl();
                voice.pitch = WretchLook.VoicePitch(k, suit.Value);
                voice.maxDistance = 24f;
                voice.volume = 0f;
                voice.Play();
            }
            if (k == CreatureKind.Smiler)
            {
                voice.clip = Audio.ProceduralAudio.Static();
                voice.pitch = 0.45f;
                voice.volume = 0.25f * Meta.AudioMix.Creatures;
                voice.Play();
                UI.Captions.Say("[crackling static]");
            }
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
                wounds.Value = Source.Wounds != null ? Source.Wounds.Mask : 0;
                var dead = Source is Smiler sm ? sm.Pocket : Source is MothHive mh ? mh.Pocket : null;
                if (dead != null && !hasPocket.Value) { pocketAt.Value = dead.At; hasPocket.Value = true; }
                prey.Value = Source.Target != null && Source.Target.TryGetComponent(out NetworkObject no) ? no.OwnerClientId
                           : Source.Target != null && Source.Target.IsLocal ? NetworkManager.LocalClientId : ulong.MaxValue;
                return;
            }
            if (hasPocket.Value && pocket == null && World.ChunkStreamer.Instance != null) pocket = World.ChunkStreamer.Instance.AddDarkPocket(pocketAt.Value, Smiler.PocketRadius);
            woundView?.Apply(wounds.Value);
            var s = (CreatureState)state.Value;
            if (s != shown) { shown = s; stateTime = 0f; }
            stateTime += Time.deltaTime;
            if (smiler != null) smiler.State = s;
            if (hound != null) hound.State = s;
            if (stealer != null) stealer.State = s;
            if (clump != null) clump.State = s;
            if (walker != null) walker.State = s;
            if (guest != null) guest.State = s;
            if (wretch != null)
            {
                wretch.State = s;
                voice.volume = Mathf.MoveTowards(voice.volume, (s == CreatureState.Lurk ? 0f : s == CreatureState.Chase || s == CreatureState.Lunge ? 0.6f : 0.28f) * Meta.AudioMix.Creatures, Time.deltaTime * 0.5f);
            }
            if (flock != null)
            {
                flock.State = s;
                bool mine = s == CreatureState.Grab && prey.Value == NetworkManager.LocalClientId;
                flock.Prey = mine && Player.Players.Local != null ? Player.Players.Local.Head : null;
                if (mine)
                {
                    // The guest owns their breath and the smother's fatal bleed, not the authority.
                    var attack = new Player.DeathAttack { Kind = CreatureKind.MothSwarm, Variant = -1, KillerPosition = transform.position, KillerYaw = transform.eulerAngles.y };
                    MothSmother.Hold(Player.Players.Local, null, attack);
                }
            }
            if (faceling != null) faceling.State = s;   // shortcut: guests don't see waving/working routines, just walking   // shortcut: a guest sees the long limb curled, not aimed, while it holds someone
            if (voice != null && voice.clip != null && wretch == null)
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
            if (c is SkinStealer ss) proxy.suit.Value = ss.Suit;
            if (c is Faceling fl) proxy.suit.Value = fl.Variant;
            if (c is Wretch wr) proxy.suit.Value = wr.Variant;
            go.GetComponent<NetworkObject>().Spawn(true);
        }
    }
}
