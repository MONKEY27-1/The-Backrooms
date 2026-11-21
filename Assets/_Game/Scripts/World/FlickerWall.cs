using UnityEngine;

namespace Backrooms.World
{
    /// The wiki's way out of Level 0: a wall that flickers in and out. It has no collision; walk into it.
    public sealed class FlickerWall : MonoBehaviour
    {
        Renderer wall;
        float next;

        void Awake() => wall = GetComponent<Renderer>();

        void Update()
        {
            if (Time.time < next) return;
            wall.enabled = !wall.enabled;
            // Mostly there, gone for a blink: easy to miss unless you're looking at it.
            next = Time.time + (wall.enabled ? Random.Range(0.4f, 2.5f) : Random.Range(0.03f, 0.12f));
        }
    }
}
