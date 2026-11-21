using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Vivox;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms.Net
{
    /// Proximity voice for co-op (Vivox). Everyone in the game is in one voice channel, but each friend's voice
    /// plays from their own suit through a normal 3D sound, so you only hear people near you and they sound
    /// muffled through walls. The dead can only talk to the dead (the living never hear them); the dead still hear
    /// the living around whoever they're watching. Talking loudly is noise: creatures come to look.
    public sealed class VoiceChat : MonoBehaviour
    {
        public enum Mode { OpenMic, PushToTalk, Off }

        public static Mode MicMode
        {
            get => (Mode)Mathf.Clamp(PlayerPrefs.GetInt("voice_mode", 0), 0, 2);
            set { PlayerPrefs.SetInt("voice_mode", (int)value); PlayerPrefs.Save(); }
        }

        public static float Volume
        {
            get => PlayerPrefs.GetFloat("voice_volume", 1f);
            set { PlayerPrefs.SetFloat("voice_volume", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        public static VoiceChat Instance { get; private set; }
        public bool Talking { get; private set; }
        public bool Muted { get; private set; } = true;
        public string InputDeviceLabel
        {
            get
            {
                var service = VivoxService.Instance;
                string id = PlayerPrefs.GetString("voice_input_device", "");
                if (string.IsNullOrEmpty(id)) return "System Default";
                if (service != null)
                    foreach (var device in service.AvailableInputDevices)
                        if (device.DeviceID == id) return device.DeviceName;
                return "System Default";
            }
        }

        Unity.Services.Vivox.VivoxInputDevice systemDefaultInput;

        public async Task CycleInputDeviceAsync()
        {
            var service = VivoxService.Instance;
            if (service == null) return;
            try
            {
                if (service.InitializationState != VivoxInitializationState.Initialized)
                    await service.InitializeAsync();
                if (systemDefaultInput == null && service.ActiveInputDevice != null
                    && service.ActiveInputDevice.DeviceName.IndexOf("Default System Device", StringComparison.OrdinalIgnoreCase) >= 0)
                    systemDefaultInput = service.ActiveInputDevice;
                var devices = new List<Unity.Services.Vivox.VivoxInputDevice>();
                foreach (var device in service.AvailableInputDevices)
                    if (device.DeviceName.IndexOf("Default System Device", StringComparison.OrdinalIgnoreCase) < 0)
                        devices.Add(device);
                string id = PlayerPrefs.GetString("voice_input_device", "");
                int current = 0; // System Default is always the first option.
                for (int i = 0; i < devices.Count; i++) if (devices[i].DeviceID == id) current = i + 1;
                int next = (current + 1) % (devices.Count + 1);
                Unity.Services.Vivox.VivoxInputDevice selected;
                if (next == 0)
                {
                    if (systemDefaultInput == null) { Debug.LogWarning("[Voice] System Default input is unavailable."); return; }
                    selected = systemDefaultInput;
                    PlayerPrefs.SetString("voice_input_device", "");
                }
                else
                {
                    selected = devices[next - 1];
                    PlayerPrefs.SetString("voice_input_device", selected.DeviceID);
                }
                PlayerPrefs.Save();
                await service.SetActiveInputDeviceAsync(selected);
            }
            catch (Exception e) { Debug.LogWarning("[Voice] Couldn't change input device: " + e.Message); }
        }

        /// A loud noise you made by talking: (position, radius). The session sends it to the owner's game.
        public Action<Vector3, float> Noise;

        const float Hearing = 24f;   // metres a normal voice carries
        string channel;
        bool busy, ready;
        float nextCheck, nextNoise;
        readonly Dictionary<string, AudioSource> taps = new Dictionary<string, AudioSource>();

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        async void Update()
        {
            if (busy) return;
            if (Time.unscaledTime >= nextCheck)
            {
                nextCheck = Time.unscaledTime + 1f;
                // In the game's channel whenever co-op is on (even with your mic off, to hear the others).
                string want = Coop.Active && Coop.Session != null && Coop.Session.Session != null ? "bk" + Clean(Coop.Session.Session.Id) : null;
                if (want != channel)
                {
                    busy = true;
                    try { await Switch(want); }
                    finally { busy = false; }
                    return;
                }
            }
            if (!ready || channel == null)
            {
                Talking = false;
                Muted = MicMode == Mode.Off || (MicMode == Mode.PushToTalk && (Keyboard.current == null || !Keyboard.current.vKey.isPressed));
                return;
            }
            Microphone();
            Place();
            UpdateTalking();
            TalkNoise();
        }

        async Task Switch(string to)
        {
            try
            {
                if (channel != null && VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
                {
                    await VivoxService.Instance.LeaveAllChannelsAsync();
                    ClearTaps();
                }
                channel = null;
                ready = false;
                if (to == null) return;
                if (!vivoxStarted)
                {
                    await VivoxService.Instance.InitializeAsync();
                    VivoxService.Instance.ParticipantAddedToChannel += OnAdded;
                    VivoxService.Instance.ParticipantRemovedFromChannel += OnRemoved;
                    vivoxStarted = true;
                }
                if (systemDefaultInput == null && VivoxService.Instance.ActiveInputDevice != null
                    && VivoxService.Instance.ActiveInputDevice.DeviceName.IndexOf("Default System Device", StringComparison.OrdinalIgnoreCase) >= 0)
                    systemDefaultInput = VivoxService.Instance.ActiveInputDevice;
                if (!VivoxService.Instance.IsLoggedIn)
                    await VivoxService.Instance.LoginAsync(new LoginOptions { DisplayName = Coop.PlayerName, EnableTTS = false });
                var inputService = VivoxService.Instance;
                if (systemDefaultInput == null && inputService.ActiveInputDevice != null
                    && inputService.ActiveInputDevice.DeviceName.IndexOf("Default System Device", StringComparison.OrdinalIgnoreCase) >= 0)
                    systemDefaultInput = inputService.ActiveInputDevice;
                ApplySavedInputDevice();
                await VivoxService.Instance.JoinGroupChannelAsync(to, ChatCapability.AudioOnly);
                channel = to;
                ready = true;
                foreach (var list in VivoxService.Instance.ActiveChannels.Values)
                    foreach (var p in list) OnAdded(p);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Voice] " + e.Message);
                nextCheck = Time.unscaledTime + 15f;   // try again later (voice is a nice-to-have, never a reason to drop)
            }
        }

        static bool vivoxStarted;

        void OnAdded(VivoxParticipant p)
        {
            if (p == null || p.IsSelf || taps.ContainsKey(p.PlayerId)) return;
            var go = p.CreateVivoxParticipantTap("Voice " + p.DisplayName, true);
            var src = go != null ? go.GetComponent<AudioSource>() : null;
            if (src == null) return;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 2f;
            src.maxDistance = Hearing;
            src.dopplerLevel = 0f;
            go.AddComponent<AudioLowPassFilter>().cutoffFrequency = 22000f;
            taps[p.PlayerId] = src;
        }

        void OnRemoved(VivoxParticipant p)
        {
            if (p == null || !taps.TryGetValue(p.PlayerId, out var src)) return;
            taps.Remove(p.PlayerId);
            if (src != null) Destroy(src.gameObject);
        }

        void ClearTaps()
        {
            foreach (var src in taps.Values) if (src != null) Destroy(src.gameObject);
            taps.Clear();
        }

        void ApplySavedInputDevice()
        {
            var service = VivoxService.Instance;
            if (service == null) return;
            string id = PlayerPrefs.GetString("voice_input_device", "");
            foreach (var device in service.AvailableInputDevices)
                if (!string.IsNullOrEmpty(id) && device.DeviceID == id)
                { _ = service.SetActiveInputDeviceAsync(device); return; }
            if (string.IsNullOrEmpty(id) && systemDefaultInput != null)
                _ = service.SetActiveInputDeviceAsync(systemDefaultInput);
        }

        /// Open mic, push-to-talk (V) or off. Talking while dead only reaches the dead (decided on the listening side).
        void Microphone()
        {
            bool talk = MicMode == Mode.OpenMic || (MicMode == Mode.PushToTalk && Keyboard.current != null && Keyboard.current.vKey.isPressed);
            if (talk && VivoxService.Instance.IsInputDeviceMuted) VivoxService.Instance.UnmuteInputDevice();
            else if (!talk && !VivoxService.Instance.IsInputDeviceMuted) VivoxService.Instance.MuteInputDevice();
            Muted = VivoxService.Instance.IsInputDeviceMuted;
        }

        void UpdateTalking()
        {
            Talking = false;
            if (Muted) return;
            foreach (var list in VivoxService.Instance.ActiveChannels.Values)
                foreach (var participant in list)
                    if (participant.IsSelf && participant.SpeechDetected && participant.AudioEnergy > 0.04)
                    { Talking = true; return; }
        }

        /// Each voice comes from its speaker's suit, muffled through walls; the dead are heard only by the dead.
        void Place()
        {
            var me = Player.Players.Local;
            bool iAmDead = me != null && !me.Alive;
            var listener = Camera.main != null ? Camera.main.transform.position : me != null ? me.Eyes : Vector3.zero;
            foreach (var kv in taps)
            {
                var src = kv.Value;
                if (src == null) continue;
                CoopAvatar speaker = null;
                foreach (var a in CoopAvatar.All.Values) if (a != null && a.PlayerId == kv.Key) { speaker = a; break; }
                if (speaker == null || speaker.Away) { src.volume = 0f; continue; }
                if (!speaker.Alive)
                {
                    // The dead: a voice in your head, but only if you're dead too.
                    src.volume = iAmDead ? Volume : 0f;
                    src.spatialBlend = 0f;
                    continue;
                }
                var mouth = speaker.transform.position + Vector3.up * 1.6f;
                src.transform.position = mouth;
                src.spatialBlend = 1f;
                src.volume = Volume;
                bool wall = Physics.Linecast(listener, mouth, out var hit, ~0, QueryTriggerInteraction.Ignore) && Player.Players.Owns(hit.collider) == null;
                var lp = src.GetComponent<AudioLowPassFilter>();
                if (lp != null) lp.cutoffFrequency = Mathf.Lerp(lp.cutoffFrequency, wall ? 900f : 22000f, 0.2f);
                if (wall) src.volume *= 0.6f;
            }
        }

        /// Your own voice: loud enough and you're alive, and the hunters hear it.
        void TalkNoise()
        {
            var me = Player.Players.Local;
            if (me == null || !me.Alive || Time.unscaledTime < nextNoise || VivoxService.Instance.IsInputDeviceMuted) return;
            foreach (var list in VivoxService.Instance.ActiveChannels.Values)
                foreach (var p in list)
                {
                    if (!p.IsSelf || !p.SpeechDetected) continue;
                    float energy = (float)p.AudioEnergy;   // 0..1
                    if (energy < 0.35f) continue;
                    nextNoise = Time.unscaledTime + 1.5f;
                    Noise?.Invoke(me.Position, Mathf.Lerp(10f, 30f, (energy - 0.35f) / 0.65f));
                    return;
                }
        }

        static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s) if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        async void OnApplicationQuit()
        {
            if (VivoxService.Instance != null && VivoxService.Instance.IsLoggedIn)
            {
                try { await VivoxService.Instance.LogoutAsync(); } catch { }
            }
        }
    }
}
