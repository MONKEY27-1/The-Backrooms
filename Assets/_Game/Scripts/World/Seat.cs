using UnityEngine;

namespace Backrooms.World
{
    /// A seat interaction on a generated chair, sofa, or bench.
    public sealed class Seat : MonoBehaviour
    {
        public float EyeHeight = 1.15f;
        public Vector3 SitPosition => transform.position + transform.up * 0.46f;
        public Quaternion SitRotation => transform.rotation;
        public Vector3 StandPosition => transform.position + transform.forward * 0.8f;
    }
}
