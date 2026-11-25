using System;
using UnityEngine;

namespace Backrooms.World
{
    /// An exit (Target = level id) or a pit (Target = null). Reports the player walking in; the game session reacts.
    public sealed class LevelTrigger : MonoBehaviour
    {
        public static event Action<string, Vector3> ExitEntered;
        public static event Action PitEntered;

        public string Target;

        /// Something other than a trigger volume sends you somewhere (Level 4's windows).
        public static void Fire(string target, Vector3 at) => ExitEntered?.Invoke(target, at);

        /// Like falling into a pit: back to your checkpoint (Level 5's thirteenth floor).
        public static void FirePit() => PitEntered?.Invoke();

        void OnTriggerEnter(Collider other)
        {
            if (!other.TryGetComponent<CharacterController>(out _)) return;
            if (string.IsNullOrEmpty(Target)) PitEntered?.Invoke();
            else ExitEntered?.Invoke(Target, transform.position);
        }
    }
}
