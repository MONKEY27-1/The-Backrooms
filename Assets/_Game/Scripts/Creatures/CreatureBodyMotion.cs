using UnityEngine;

namespace Backrooms.Creatures
{
    /// Subtle procedural motion keeps static generated creature meshes from reading as lifeless props.
    public sealed class CreatureBodyMotion : MonoBehaviour
    {
        CreatureKind kind;
        Transform pivot;
        Vector3 previous;
        float phase;

        public void Initialize(CreatureKind creatureKind)
        {
            kind = creatureKind;
            int childCount = transform.childCount;
            var visuals = new Transform[childCount];
            for (int i = 0; i < childCount; i++) visuals[i] = transform.GetChild(i);

            // Pivot around the torso rather than rotating every primitive/body part independently.
            var pivotObject = new GameObject("Body Motion Pivot");
            pivot = pivotObject.transform;
            pivot.SetParent(transform, false);
            pivot.localPosition = Vector3.up * 0.9f;
            var content = new GameObject("Body Visuals").transform;
            content.SetParent(pivot, false);
            content.localPosition = Vector3.down * 0.9f;
            // Preserve authored/generated mesh transforms when moving them beneath the motion pivot.
            // false reset every child to local zero/identity/unit scale, collapsing creature models.
            foreach (var visual in visuals) visual.SetParent(content, true);
            previous = transform.position;
        }

        void LateUpdate()
        {
            if (pivot == null) return;
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            Vector3 delta = transform.position - previous;
            previous = transform.position;
            float speed = new Vector2(delta.x, delta.z).magnitude / dt;
            phase += dt * (2.8f + Mathf.Min(speed, 5f) * 1.4f);

            float bob = kind == CreatureKind.Faceling
                ? Mathf.Sin(Time.time * 1.1f) * 0.012f
                : Mathf.Abs(Mathf.Sin(phase)) * (kind == CreatureKind.PartyGoer ? 0.07f : 0.045f);
            float stride = Mathf.Clamp01(speed / 2.5f);
            float lean = stride * (kind == CreatureKind.PartyGoer ? 12f : kind == CreatureKind.SkinStealer ? 8f : 5f);
            float twitch = kind == CreatureKind.SkinStealer ? Mathf.Sin(Time.time * 11f) * 2f : 0f;
            float sway = kind == CreatureKind.Faceling ? Mathf.Sin(Time.time * 0.65f) * 2.2f
                       : Mathf.Sin(phase * 0.5f) * (kind == CreatureKind.PartyGoer ? 7f : 2.5f);

            pivot.localPosition = Vector3.up * (0.9f + bob);
            pivot.localRotation = Quaternion.Euler(-lean + twitch, sway, kind == CreatureKind.PartyGoer ? Mathf.Sin(phase) * 3f : 0f);
        }
    }
}
