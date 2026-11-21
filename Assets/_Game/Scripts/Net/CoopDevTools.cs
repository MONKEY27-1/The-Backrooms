#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Backrooms.Net
{
    /// Developer switches for testing co-op with a second copy of the game (development builds and the editor only):
    ///   -coopJoin CODE        join that code from the main menu
    ///   -coopShots DIR SECS   save a screenshot to DIR every SECS seconds (to see what the guest sees)
    ///   -coopProfile NAME     a separate sign-in identity (read by CoopSession)
    public sealed class CoopDevTools : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Arg("-coopJoin") == null && Arg("-coopShots") == null) return;
            Application.runInBackground = true;
            new GameObject("Co-op Dev Tools").AddComponent<CoopDevTools>();
        }

        static string Arg(string name, int offset = 1)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - offset; i++) if (args[i] == name) return args[i + offset];
            return null;
        }

        IEnumerator Start()
        {
            string code = Arg("-coopJoin");
            if (code != null)
            {
                yield return new WaitForSeconds(4f);
                var session = FindFirstObjectByType<Game.GameSession>();
                for (int tries = 0; tries < 5 && session != null; tries++)
                {
                    var join = session.JoinCoop(code);
                    while (!join.IsCompleted) yield return null;
                    if (join.Result) break;
                    yield return new WaitForSeconds(5f);
                }
            }
            string dir = Arg("-coopShots");
            if (dir != null && float.TryParse(Arg("-coopShots", 2), out float every))
            {
                Directory.CreateDirectory(dir);
                int n = 0;
                while (true)
                {
                    yield return new WaitForSeconds(every);
                    ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"guest_{n++:000}.png"));
                    File.WriteAllText(Path.Combine(dir, "guest_state.txt"), State());
                }
            }
        }

        static string State()
        {
            var me = Player.Players.Local;
            var s = FindFirstObjectByType<World.ChunkStreamer>();
            int voices = 0;
            try
            {
                if (Unity.Services.Vivox.VivoxService.Instance != null && Unity.Services.Vivox.VivoxService.Instance.IsLoggedIn)
                    foreach (var l in Unity.Services.Vivox.VivoxService.Instance.ActiveChannels.Values) voices += l.Count;
            }
            catch { }
            return $"time {Time.time:F0}\ncoop active {Coop.Active} guest {Coop.IsGuest} reconnecting {(Coop.Session != null && Coop.Session.Reconnecting)}\n"
                 + $"level {(s != null && s.Theme != null ? s.Theme.id : "-")} position {(me != null ? me.Position.ToString("F1") : "-")} alive {(me != null && me.Alive)}\n"
                 + $"players {Player.Players.All.Count} avatars {CoopAvatar.All.Count} pickups {FindObjectsByType<World.Pickup>(FindObjectsSortMode.None).Length} voice participants {voices}\n"
                 + $"blackout {(s != null ? s.Blackout : 0f):F2} {Shared()}\n";
        }

        /// The shared level events this game can see (balloons, debris, lures, the record).
        static string Shared()
        {
            var sb = new System.Text.StringBuilder();
            int tiles = 0, walls = 0, caves = 0;
            foreach (var go in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                switch (go.name)
                {
                    case "Ceiling Tile": tiles++; break;
                    case "Toppling Wall": walls++; break;
                    case "Cave-in": caves++; break;
                    case "Balloon": sb.Append($"balloon {go.position:F0} "); break;
                    case "A Balloon": sb.Append($"lure-balloon {go.position:F0} "); break;
                    case "A Window": sb.Append("lure-window "); break;
                    case "Gramophone": if (go.TryGetComponent(out AudioSource a) && a.isPlaying) sb.Append("record-playing "); break;
                }
            }
            return $"debris tiles {tiles} walls {walls} caves {caves} | " + sb;
        }
    }
}
#endif
