using UnityEngine;
using UnityEngine.InputSystem;

namespace Backrooms.Player
{
    /// Co-op: while you're dead (Easy/Normal) or already through the exit (Hard/Extreme), your body is gone and the
    /// camera follows a living friend in third person. Move the mouse to look around them; click to switch friends.
    public sealed class Spectator : MonoBehaviour
    {
        public bool Watching { get; private set; }
        public string Watched { get; private set; } = "";
        /// The friend being watched right now (so streaming and effects can follow what you see).
        public Transform Target { get; private set; }

        FirstPersonController body;
        CharacterController controller;
        PlayerTools tools;
        PlayerStatus status;
        PlayerAgent agent;
        Camera cam;
        Transform camParent;
        Vector3 camLocalPos;
        Quaternion camLocalRot;
        int index;
        float yaw, pitch = 15f;

        /// Who can be watched: the living friends (filled in by co-op).
        public System.Func<System.Collections.Generic.List<(Transform body, string name)>> Friends;

        void Awake()
        {
            body = GetComponent<FirstPersonController>();
            controller = GetComponent<CharacterController>();
            tools = GetComponent<PlayerTools>();
            status = GetComponent<PlayerStatus>();
            agent = GetComponent<PlayerAgent>();
            cam = GetComponentInChildren<Camera>();
        }

        public void Begin()
        {
            if (Watching) return;
            Watching = true;
            Target = null;
            if (agent != null) agent.Alive = false;
            if (tools != null) { tools.Stow(); tools.enabled = false; }
            if (status != null) status.enabled = false;   // a ghost doesn't get thirsty
            body.enabled = false;
            controller.enabled = false;
            camParent = cam.transform.parent;
            camLocalPos = cam.transform.localPosition;
            camLocalRot = cam.transform.localRotation;
            cam.transform.SetParent(null, true);
            yaw = transform.eulerAngles.y;
        }

        public void End()
        {
            if (!Watching) return;
            Watching = false;
            Watched = "";
            Target = null;
            cam.transform.SetParent(camParent, false);
            cam.transform.localPosition = camLocalPos;
            cam.transform.localRotation = camLocalRot;
            controller.enabled = true;
            body.enabled = true;
            if (tools != null) tools.enabled = true;
            if (status != null) status.enabled = true;
            if (agent != null) agent.Alive = true;
        }

        void LateUpdate()
        {
            if (!Watching) return;
            var list = Friends?.Invoke();
            if (list == null || list.Count == 0) { Watched = ""; Target = null; return; }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) index++;
                var d = mouse.delta.ReadValue() * 0.12f * Meta.GameSettings.Current.sensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -20f, 70f);
            }
            index = ((index % list.Count) + list.Count) % list.Count;
            var (target, name) = list[index];
            Watched = name;
            Target = target;
            var focus = target.position + Vector3.up * 1.5f;
            var look = Quaternion.Euler(pitch, yaw, 0f);
            var want = focus - look * Vector3.forward * 3.2f;
            // Don't put the camera through the wall behind them.
            if (Physics.SphereCast(focus, 0.2f, want - focus, out var hit, 3.2f, ~0, QueryTriggerInteraction.Ignore) && Players.Owns(hit.collider) == null)
                want = focus + (want - focus).normalized * Mathf.Max(0.4f, hit.distance - 0.1f);
            cam.transform.SetPositionAndRotation(want, Quaternion.LookRotation(focus - want));
        }
    }
}
