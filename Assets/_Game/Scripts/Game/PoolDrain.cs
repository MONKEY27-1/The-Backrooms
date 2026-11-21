using Backrooms.Player;
using Backrooms.World;
using UnityEngine;

namespace Backrooms.Game
{
    /// Level -1's pool: chest-deep water in the basin you land in. It drains a little every second; the pump
    /// lever in the control room drains it fast. The door at the bottom only opens once it's empty.
    public sealed class PoolDrain : MonoBehaviour
    {
        const float StartDepth = 1.4f;     // chest-deep
        const float SlowRate = 0.0015f;    // metres per second: about 15 minutes to drain on its own
        const float FastRate = 0.12f;      // with the pumps running: about 12 seconds

        public float Depth { get; private set; } = StartDepth;
        public bool Drained => Depth <= 0.01f;

        FirstPersonController player;
        PlayerHud hud;
        Transform water;
        bool pumping;

        public void Init(Material material, FirstPersonController player, PlayerHud hud)
        {
            this.player = player;
            this.hud = hud;
            PoolSwitch.Pulled = false;
            var area = DistrictLayout.PoolArea;
            var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(plane.GetComponent<Collider>());
            plane.name = "Pool Water";
            plane.transform.SetParent(transform, false);
            plane.transform.position = new Vector3(area.center.x, 0f, area.center.y);
            plane.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // a quad faces -z; this turns it to face up
            plane.transform.localScale = new Vector3(area.width, area.height, 1f);
            plane.GetComponent<MeshRenderer>().sharedMaterial = material;
            water = plane.transform;
            Place();
        }

        void OnEnable() => PoolSwitch.Pressed += Pump;
        void OnDisable() => PoolSwitch.Pressed -= Pump;

        void OnDestroy()
        {
            if (player != null) player.WaterLevel = float.NegativeInfinity;
            if (water != null) Destroy(water.gameObject);
        }

        void Pump()
        {
            pumping = true;
            if (hud != null) hud.Flash("Somewhere below you, pumps shudder awake. The pool is draining.");
        }

        void Update()
        {
            float before = Depth;
            Depth = Mathf.MoveTowards(Depth, 0f, (pumping ? FastRate : SlowRate) * Time.deltaTime);
            if (before > 0.01f && Drained && hud != null) hud.Flash("The pool is empty. The door at the bottom is clear.");
            Place();
        }

        void Place()
        {
            if (water == null) return;
            float surface = -DistrictLayout.PoolDepth + Depth;
            var p = water.position;
            water.position = new Vector3(p.x, surface, p.z);
            water.gameObject.SetActive(!Drained);
            if (player == null) return;
            var at = player.transform.position;
            bool inBasin = DistrictLayout.PoolArea.Contains(new Vector2(at.x, at.z));
            player.WaterLevel = inBasin && !Drained ? surface : float.NegativeInfinity;
        }
    }
}
