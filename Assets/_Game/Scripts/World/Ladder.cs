using Backrooms.Player;
using UnityEngine;

namespace Backrooms.World
{
    /// A climbable ladder: a trigger in front of the rungs. While you're inside it, forward/back climbs.
    public sealed class Ladder : MonoBehaviour
    {
        public float Top;   // height of the floor at the top, where you step off

        void OnTriggerStay(Collider other)
        {
            if (other.TryGetComponent(out FirstPersonController player)) player.OnLadder(transform.forward, Top);
        }
    }
}
