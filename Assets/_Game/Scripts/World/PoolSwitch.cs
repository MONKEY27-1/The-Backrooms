using System;
using UnityEngine;

namespace Backrooms.World
{
    /// The pump lever in Level -1's control room. Pulling it drains the pool.
    public sealed class PoolSwitch : MonoBehaviour
    {
        public static event Action Pressed;
        public static bool Pulled { get; set; }

        public void Press() => PullAnywhere();

        /// Pulls the lever even if its room isn't loaded here (a friend pulled it, co-op).
        public static void PullAnywhere()
        {
            if (Pulled) return;
            Pulled = true;
            Pressed?.Invoke();
        }
    }
}
