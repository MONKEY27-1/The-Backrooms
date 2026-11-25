using System;
using System.Collections;
using Backrooms.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Player
{
    /// What you carry and use:
    /// - Flashlight (F / gamepad Y): drains its battery, stutters when low.
    /// - Camcorder (hold right mouse / gamepad left trigger): REC overlay, night vision, slight zoom; own battery.
    /// - Bag (Tab / gamepad View): swings in front of you so you can grab what's inside. The game keeps running.
    ///   Click a thermos to drink, a battery to recharge the emptier device. Gamepad: X drinks, Y uses a battery.
    /// - Interact (E / gamepad X): pick up what you're looking at, turn a painting over, read a note.
    /// - Logbook (J): the notes you've found.
    public sealed class PlayerTools : MonoBehaviour
    {
        const int BagLayer = 30;
        const int BagContentsLayer = 29;
        [SerializeField] float flashlightTime = 240f, camcorderTime = 300f, reach = 2.2f;

        public float FlashlightBattery = 1f, CamcorderBattery = 1f;
        public bool FlashlightOn { get; private set; }
        public bool CamcorderUp { get; private set; }
        public bool BagOpen { get; private set; }
        public Light Lamp => lamp;
        public Transform BagTransform => bagRoot != null && bagRoot.activeSelf ? bagRoot.transform : null;
        public Transform Holding { get; private set; }
        public bool Prying => LookingAtBoards != null && interact != null && interact.IsPressed();
        public Pickup LookingAt { get; private set; }
        public Readable LookingAtReadable { get; private set; }
        public PoolSwitch LookingAtSwitch { get; private set; }
        public BoardedDoor LookingAtBoards { get; private set; }
        public Game.GoalDoor LookingAtGoalDoor { get; private set; }
        public Game.PartyFood LookingAtFood { get; private set; }
        public Seat LookingAtSeat { get; private set; }
        public WaterSpot LookingAtWater { get; private set; }
        public IInteractable LookingAtThing { get; private set; }
        float level6Time;      // the flashlight weakens the longer you're in Level 6
        Vector3 chalkFrom;     // where you were a moment ago, so a chalk arrow points the way you were going   // hotel doors, elevator buttons and the like

        /// A level goal's item was picked up (its index).
        public event Action<int> GoalTaken;
        /// Tried a door that's still locked: what it says.
        public event Action<string> DoorLocked;

        /// Threw a glowstick or an Exterminator: what, from where, how fast (the game spawns it, for friends too).
        public event Action<PropKind, Vector3, Vector3> Threw;

        /// Boards pried off a doorway (the game remembers them and the noise carries).
        public event Action<BoardedDoor> Pried;

        /// A crate was opened: what you found, in words.
        public event Action<string> Looted;

        static readonly string[] CrateNotes =
        {
            "Inventory, crate 44: water x12, batteries (D) x30, blankets x4. Took 2 water. - R.",
            "If you're reading this, the crates refill after the lights go out. Don't take everything.",
            "The ramps go up forever. I counted nine decks before I stopped counting.",
            "Saw the same tarp-covered car three decks in a row. Same dent. Same oil stain.",
        };
        /// What's being read right now (a painting's back, a note), or null.
        public string ReadingTitle { get; private set; }
        public string ReadingText { get; private set; }
        public bool LogbookOpen { get; private set; }
        Vector3 readAt;

        /// Reports notes read, so the game can keep them in the logbook and remember they're taken.
        public event Action<Readable> Read;

        /// Reports pickups so the game can remember they're gone.
        public event Action<Pickup> PickedUp;

        /// Co-op: the friend you're looking at (to hand them almond water or a battery), and the hand-over itself.
        public Net.CoopAvatar LookingAtFriend { get; private set; }
        public event Action<Net.CoopAvatar, PropKind> Gave;

        Camera cam, bagCam;
        Light lamp;
        Volume nightVision;
        PlayerStatus status;
        FirstPersonController body;
        InputAction flashlight, camcorder, bag, interact, drinkPad, batteryPad, logbook;
        GameObject bagRoot;
        bool grabbing;   // an item is on its way out of the bag
        Coroutine swing;
        AudioSource rustle;
        // Where the bag sits when it's open in front of you, and where it hangs on your back.
        static readonly Vector3 BagFront = new Vector3(0f, -0.4f, 0.61f), BagShoulder = new Vector3(0.42f, 0.02f, 0.12f), BagBack = new Vector3(0.32f, -0.05f, -0.45f);
        static readonly Quaternion BagFrontRot = Quaternion.Euler(-42f, 0f, 0f), BagShoulderRot = Quaternion.Euler(15f, 70f, 30f), BagBackRot = Quaternion.Euler(-20f, 160f, 45f);
        int shownWater = -1, shownBatteries = -1;
        float baseFov;

        void Awake()
        {
            status = GetComponent<PlayerStatus>();
            body = GetComponent<FirstPersonController>();
            cam = GetComponentInChildren<Camera>();
            baseFov = cam.fieldOfView;
            if (GetComponent<FirstPersonArms>() == null) gameObject.AddComponent<FirstPersonArms>();

            var go = new GameObject("Flashlight");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0.15f, -0.15f, 0.1f);
            var hdLamp = go.AddHDLight(LightType.Spot);
            lamp = go.GetComponent<Light>();
            lamp.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            lamp.range = 22f;
            lamp.spotAngle = 55f;
            lamp.innerSpotAngle = 25f;
            lamp.intensity = 900f;   // a decent LED torch
            hdLamp.volumetricDimmer = 0.8f;
            lamp.color = new Color(1f, 0.96f, 0.88f);
            lamp.shadows = LightShadows.Soft;
            lamp.enabled = false;

            nightVision = MakeNightVision();
            BuildBag();

            Controls.EnableAll();
            flashlight = Controls.Flashlight;
            camcorder = Controls.Camcorder;
            bag = Controls.Bag;
            interact = Controls.Interact;
            drinkPad = Controls.DrinkPad;
            batteryPad = Controls.BatteryPad;
            logbook = Controls.Logbook;
        }

        void OnDestroy()
        {
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (bag.WasPressedThisFrame()) SetBag(!BagOpen);

            // Flashlight.
            if (!BagOpen && flashlight.WasPressedThisFrame() && FlashlightBattery > 0f) FlashlightOn = !FlashlightOn;
            var here = ChunkStreamer.Instance != null && ChunkStreamer.Instance.Theme != null ? ChunkStreamer.Instance.Theme.id : "";
            if (FlashlightOn) FlashlightBattery = Mathf.Max(0f, FlashlightBattery - dt / flashlightTime * Meta.Difficulty.Battery * (here == "8" ? 2f : 1f));   // Level 8 eats batteries
            if (!Meta.Difficulty.SurvivalEnabled) { FlashlightBattery = 1f; CamcorderBattery = 1f; }
            if (FlashlightBattery <= 0f) FlashlightOn = false;

            var streamer = ChunkStreamer.Instance ?? FindAnyObjectByType<ChunkStreamer>();
            bool inLevel6 = streamer != null && streamer.Theme != null && streamer.Theme.id == "6";
            if (inLevel6)
            {
                // Level 6 "Lights Out": the dark eats the beam, about two metres of it, and more the longer you stay.
                level6Time += dt;
                float worn = Mathf.Clamp01(level6Time / 600f);
                lamp.range = Mathf.Lerp(2.4f, 1.6f, worn);
                lamp.intensity = Mathf.Lerp(40f, 18f, worn);
                if (Controls.Chalk.WasPressedThisFrame() && !BagOpen)
                {
                    var walking = transform.position - chalkFrom;
                    if (Chalk.Draw(cam.transform.position, cam.transform.forward, walking.sqrMagnitude > 0.04f ? walking : Vector3.zero)) UI.Captions.Say("[chalk scratching on concrete]");
                }
                if ((transform.position - chalkFrom).sqrMagnitude > 1f) chalkFrom = Vector3.Lerp(chalkFrom, transform.position, 0.5f);
            }
            else
            {
                level6Time = 0f;
                lamp.range = here == "8" ? 15f : 22f;   // and swallows the beam
                lamp.intensity = here == "8" ? 850f : here == "7" && Players.Local != null && Players.Local.Body.Underwater ? Mathf.Lerp(2500f, 24000f, (OceanLayout.OceanWaterLevel - transform.position.y) / 70f) : 900f;   // a diver's beam: water eats most of it
            }

            bool stutter = ((FlashlightBattery < 0.15f) || (inLevel6 && Mathf.PerlinNoise(Time.time * 2.5f, 9.4f) < 0.35f))
                && Mathf.PerlinNoise(Time.time * 6f, 0f) < 0.3f;
            lamp.enabled = FlashlightOn && !stutter;

            // Throwables: a glowstick (Q) to lure a Smiler away, an Exterminator (X) to drive it off.
            if (!BagOpen && ReadingText == null)
            {
                if (Controls.ThrowLight.WasPressedThisFrame() && status.Glowsticks > 0) { status.Glowsticks--; Throw(PropKind.Glowstick, 8f); }
                else if (Controls.ThrowBottle.WasPressedThisFrame() && status.Exterminators > 0) { status.Exterminators--; Throw(PropKind.Exterminator, 11f); }
                else if ((Controls.Flare.WasPressedThisFrame() || (Controls.ThrowBottle.WasPressedThisFrame() && status.Exterminators == 0)) && status.Flares > 0) { status.Flares--; Throw(PropKind.Flare, 9f); }
                else if (Controls.Repellent.WasPressedThisFrame() && status.Repellent > 0) { status.Repellent--; Hands.Play(Hands.Give); Threw?.Invoke(PropKind.Repellent, cam.transform.position + cam.transform.forward * 0.4f, cam.transform.forward * 4f); }
                else if (Controls.SetDown.WasPressedThisFrame() && status.AlmondWater > 0) { status.AlmondWater--; Hands.Play(Hands.Give); Threw?.Invoke(PropKind.AlmondWater, cam.transform.position + cam.transform.forward * 0.5f + Vector3.down * 0.4f, cam.transform.forward * 1.2f); UI.Captions.Say("[you set a bottle down, open]"); }
                else if (Controls.FirstAid.WasPressedThisFrame() && status.Patch()) { Hands.Play(Hands.Give); UI.Captions.Say("[tearing bandages]"); }
            }

            // Camcorder: raised while held.
            CamcorderUp = !BagOpen && camcorder.IsPressed() && CamcorderBattery > 0f;
            if (CamcorderUp) CamcorderBattery = Mathf.Max(0f, CamcorderBattery - dt / camcorderTime * Meta.Difficulty.Battery);
            nightVision.weight = Mathf.MoveTowards(nightVision.weight, CamcorderUp ? 1f : 0f, dt * 6f);
            baseFov = Meta.GameSettings.Current.fov;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, CamcorderUp ? baseFov * 0.8f : baseFov, dt * 8f);

            // Looking at a pickup, a painting or a note.
            LookingAt = null;
            LookingAtGoalDoor = null;
            LookingAtFood = null;
            LookingAtReadable = null;
            LookingAtSwitch = null;
            LookingAtFriend = null;
            LookingAtSeat = null;
            LookingAtWater = null;
            LookingAtThing = null;
            var lastBoards = LookingAtBoards;
            LookingAtBoards = null;
            if (!BagOpen && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, reach, ~(1 << BagLayer), QueryTriggerInteraction.Collide))
            {
                LookingAt = hit.collider.GetComponentInParent<Pickup>();
                LookingAtGoalDoor = hit.collider.GetComponentInParent<Game.GoalDoor>();
                LookingAtFood = hit.collider.GetComponent<Game.PartyFood>();
                LookingAtReadable = hit.collider.GetComponentInParent<Readable>();
                LookingAtSwitch = hit.collider.GetComponentInParent<PoolSwitch>();
                LookingAtBoards = hit.collider.GetComponentInParent<BoardedDoor>();
                LookingAtFriend = hit.collider.GetComponentInParent<Net.CoopAvatar>();
                LookingAtSeat = hit.collider.GetComponentInParent<Seat>();
                LookingAtWater = hit.collider.GetComponentInParent<WaterSpot>();
                LookingAtThing = hit.collider.GetComponentInParent<IInteractable>();
                if (LookingAtFriend != null && (LookingAtFriend.IsOwner || !LookingAtFriend.Alive)) LookingAtFriend = null;
            }
            // Hold Interact to pry boards off; let go (or look away) and you lose your grip.
            if (lastBoards != null && lastBoards != LookingAtBoards) lastBoards.Release();
            if (LookingAtBoards != null)
            {
                if (interact.IsPressed() && LookingAtBoards.Pry(dt))
                {
                    Pried?.Invoke(LookingAtBoards);
                    if (LookingAtBoards.Crate) OpenCrate(LookingAtBoards);
                    else if (LookingAtBoards.LockedDoor) LookingAtBoards.ForceOpen();   // the door stays, swung open
                    else Destroy(LookingAtBoards.gameObject);
                    LookingAtBoards = null;
                }
                else if (!interact.IsPressed()) LookingAtBoards.Release();
            }
            if (logbook.WasPressedThisFrame()) LogbookOpen = !LogbookOpen;
            if (ReadingText != null && Vector3.Distance(transform.position, readAt) > 2.5f) ReadingText = null;   // walked away
            if (interact.WasPressedThisFrame())
            {
                if (ReadingText != null) ReadingText = null;
                else if (LookingAt != null) Collect(LookingAt);
                else if (LookingAtReadable != null) ReadIt(LookingAtReadable);
                else if (LookingAtSwitch != null) { Hands.Play(Hands.Interact); LookingAtSwitch.Press(); }
                else if (LookingAtGoalDoor != null && LookingAtGoalDoor.Locked)
                {
                    Hands.Play(Hands.Push);
                    DoorLocked?.Invoke(LookingAtGoalDoor.LockedText);
                }
                else if (LookingAtFood != null) Eat(LookingAtFood);
                else if (LookingAtFriend != null) GiveTo(LookingAtFriend);
                else if (LookingAtSeat != null && body.Sit(LookingAtSeat)) Hands.Play(Hands.Interact);
                else if (LookingAtWater != null) DrinkHere();
                else if (LookingAtThing != null) LookingAtThing.Interact();
            }

            if (BagOpen) UpdateBag();
        }

        /// Level 4's coolers and fountains: almond water, as much as you like.
        void DrinkHere()
        {
            Hands.Play(Hands.Drink);
            status.Thirst = 1f;
            status.Sanity = Mathf.Min(1f, status.Sanity + 0.05f);
            Looted?.Invoke("Almond water, cold. Faintly sweet, like vanilla.");
        }

        /// Turns a painting over to read the back, or reads a note and keeps it in the logbook.
        void ReadIt(Readable r)
        {
            ReadingTitle = r.Title;
            ReadingText = string.IsNullOrEmpty(r.Text) ? "(nothing written here)" : r.Text;
            readAt = transform.position;
            if (r.Kind != ReadableKind.Note) return;
            status.Logbook.Add(r.Title + "\n" + r.Text);
            Read?.Invoke(r);
            Destroy(r.gameObject);
        }

        /// Hands a friend a bottle of almond water (or, with none left, a battery).
        void GiveTo(Net.CoopAvatar friend)
        {
            if (status.AlmondWater > 0) { status.AlmondWater--; Hands.Play(Hands.Give); Gave?.Invoke(friend, PropKind.AlmondWater); }
            else if (status.Batteries > 0) { status.Batteries--; Hands.Play(Hands.Give); Gave?.Invoke(friend, PropKind.Battery); }
        }

        void Collect(Pickup p)
        {
            if (p.Goal != null)
            {
                GoalTaken?.Invoke(p.Id);
                StartCoroutine(PickUp(p.gameObject));
                return;
            }
            if (p.Kind == PropKind.AlmondWater) status.AlmondWater++;
            else if (p.Kind == PropKind.Glowstick) status.Glowsticks += 2;
            else if (p.Kind == PropKind.Exterminator) status.Exterminators++;
            else if (p.Kind == PropKind.FirstAid) status.FirstAid++;
            else if (p.Kind == PropKind.Repellent) status.Repellent += 3;
            else if (p.Kind == PropKind.Flare) status.Flares++;
            else status.Batteries++;
            PickedUp?.Invoke(p);
            StartCoroutine(PickUp(p.gameObject));
        }

        void Throw(PropKind kind, float speed)
        {
            Hands.Play(Hands.Give);
            var from = cam.transform.position + cam.transform.forward * 0.45f + cam.transform.right * 0.12f;
            var cc = GetComponent<CharacterController>();
            var velocity = cam.transform.forward * speed + Vector3.up * 1.5f + (cc != null ? cc.velocity : Vector3.zero);
            Threw?.Invoke(kind, from, velocity);
        }

        /// What you picked up comes to your hand, then down into the bag.
        IEnumerator PickUp(GameObject go)
        {
            var item = go.transform;
            Holding = item;
            Hands.Play(Hands.Pickup);
            try
            {
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                Destroy(go.GetComponent<Pickup>());
                item.SetParent(cam.transform, true);
                SetLayer(go);   // drawn on top, like the bag
                var t0 = item.localPosition;
                var r0 = item.localRotation;
                var s0 = item.localScale;
                var hand = new Vector3(0.12f, -0.2f, 0.42f);
                var held = Quaternion.Euler(-20f, 25f, 0f);
                yield return Move(item, t0, hand, r0, held, 0.3f, 0.06f);
                yield return new WaitForSeconds(0.15f);
                var bag = new Vector3(0.05f, -0.6f, 0.2f);
                for (float t = 0f; t < 1f && go != null; t += Time.deltaTime / 0.25f)
                {
                    float e = t * t;
                    item.localPosition = Vector3.Lerp(hand, bag, e);
                    item.localScale = s0 * Mathf.Lerp(1f, 0.6f, e);
                    yield return null;
                }
                if (go != null) Destroy(go);
            }
            finally { if (Holding == item) Holding = null; }
        }

        /// Eases `item` from one pose to another (local to the camera), with a little lift on the way.
        static IEnumerator Move(Transform item, Vector3 from, Vector3 to, Quaternion fromRot, Quaternion toRot, float time, float lift)
        {
            for (float t = 0f; t < 1f && item != null; t += Time.deltaTime / time)
            {
                float e = Mathf.SmoothStep(0f, 1f, t);
                item.localPosition = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * lift;
                item.localRotation = Quaternion.Slerp(fromRot, toRot, e);
                yield return null;
            }
            if (item != null) { item.localPosition = to; item.localRotation = toRot; }
        }

        /// Level Fun's cake and drinks: they fill you up and take something from your mind.
        void Eat(Game.PartyFood food)
        {
            Hands.Play(food.Drink ? Hands.Drink : Hands.Eat);
            if (food.Drink) status.Thirst = Mathf.Min(1f, status.Thirst + 0.4f);
            else { status.Thirst = Mathf.Min(1f, status.Thirst + 0.1f); status.Stamina = 1f; }
            status.Sanity = Mathf.Max(0f, status.Sanity - 0.3f);
            Destroy(food.gameObject);
            Looted?.Invoke(food.Drink ? "Sweet and flat. For a moment everything is very, very funny." : "It tastes like nothing at all. Somewhere, people cheer.");
            UI.Captions.Say("[cheering, far away]");
        }

        void OpenCrate(BoardedDoor crate)
        {
            crate.ForceOpen();
            string found;
            switch (crate.Loot)
            {
                case "water": status.AlmondWater++; found = "Inside: a bottle of almond water."; break;
                case "battery": status.Batteries++; found = "Inside: a battery, still sealed."; break;
                case "note":
                    var text = CrateNotes[Mathf.Abs(crate.Id) % CrateNotes.Length];
                    status.Logbook.Add("Note in a crate\n" + text);
                    found = "Inside: a folded note. (It's in your logbook.)";
                    break;
                default: found = "Empty. Someone got here first."; break;
            }
            Looted?.Invoke(found);
        }

        /// A battery recharges whichever device is emptier.
        public bool UseBattery()
        {
            if (status.Batteries <= 0) return false;
            Hands.Play(Hands.Battery);
            status.Batteries--;
            if (FlashlightBattery <= CamcorderBattery) FlashlightBattery = 1f;
            else CamcorderBattery = 1f;
            return true;
        }

        // ---------- the bag ----------

        /// Puts everything away (back to the main menu).
        public void Stow()
        {
            FlashlightOn = false;
            lamp.enabled = false;
            CamcorderUp = false;
            nightVision.weight = 0f;
            if (swing != null) StopCoroutine(swing);
            swing = null;
            BagOpen = false;
            body.Encumbered = false;
            body.LookLocked = false;
            bagRoot.SetActive(false);
        }

        /// Opening: you swing the bag off your back, over your shoulder, and pull it round in front of you. Closing is
        /// the same in reverse. While it's open you walk at half speed.
        void SetBag(bool open)
        {
            if (open == BagOpen) return;
            BagOpen = open;
            body.Encumbered = open;
            body.LookLocked = open;
            FirstPersonController.LockCursor(!open);
            if (open)
            {
                BagModel();
                shownWater = shownBatteries = -1;   // refill: the item models may have arrived since
                bagRoot.SetActive(true);
            }
            if (swing != null) StopCoroutine(swing);
            swing = StartCoroutine(Swing(open));
            if (rustle == null)
            {
                rustle = gameObject.AddComponent<AudioSource>();
                rustle.spatialBlend = 0f;
                rustle.playOnAwake = false;
            }
            rustle.pitch = open ? 0.33f : 0.4f;
            rustle.PlayOneShot(Audio.ProceduralAudio.Static(), 0.06f * Meta.AudioMix.Effects);   // canvas and straps
        }

        IEnumerator Swing(bool open)
        {
            var t0 = bagRoot.transform;
            float k0 = open ? 0f : 1f, k1 = open ? 1f : 0f, time = open ? 0.55f : 0.4f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                float k = Mathf.Lerp(k0, k1, Mathf.SmoothStep(0f, 1f, t));
                // Back -> over the right shoulder -> in front, along a curve.
                t0.localPosition = (1 - k) * (1 - k) * BagBack + 2 * (1 - k) * k * BagShoulder + k * k * BagFront;
                t0.localRotation = k < 0.5f ? Quaternion.Slerp(BagBackRot, BagShoulderRot, k * 2f) : Quaternion.Slerp(BagShoulderRot, BagFrontRot, k * 2f - 1f);
                yield return null;
            }
            t0.localPosition = open ? BagFront : BagBack;
            t0.localRotation = open ? BagFrontRot : BagBackRot;
            if (!open) bagRoot.SetActive(false);
            swing = null;
        }

        void UpdateBag()
        {
            if (grabbing || swing != null) return;   // still coming round
            if (shownWater != status.AlmondWater || shownBatteries != status.Batteries) FillBag();
            if (drinkPad.WasPressedThisFrame()) GrabFromBag("Water");
            if (batteryPad.WasPressedThisFrame()) GrabFromBag("Battery");
            if (Mouse.current == null) return;
            var pick = PickItem(Mouse.current.position.ReadValue());
            Hover(pick);
            if (pick != null && Mouse.current.leftButton.wasPressedThisFrame) { Hover(null); StartCoroutine(Grab(pick)); }
        }

        Transform hovered;
        Vector3 hoverBase;

        /// The item lifts a little while the cursor is on it, so you can see what a click will take.
        void Hover(Transform item)
        {
            if (item == hovered) return;
            if (hovered != null) hovered.localPosition = hoverBase;
            hovered = item;
            if (item == null) return;
            hoverBase = item.localPosition;
            item.localPosition = hoverBase + Vector3.up * 0.025f;
        }

        static bool IsBagItem(Transform t) => t.name == "Water" || t.name == "Battery";

        /// What's under the cursor, or failing that the item drawn nearest to it (within a short reach on screen).
        Transform PickItem(Vector2 mouse)
        {
            var ray = bagCam.ScreenPointToRay(mouse);
            if (Physics.Raycast(ray, out var hit, 2f, 1 << BagContentsLayer, QueryTriggerInteraction.Collide) && IsBagItem(hit.collider.transform))
                return hit.collider.transform;
            var items = bagRoot.transform.Find("Items");
            if (items == null) return null;
            Transform best = null;
            float bestDistance = 110f * Screen.height / 1080f;
            foreach (Transform item in items)
            {
                if (!IsBagItem(item) || !item.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) continue;
                var p = bagCam.WorldToScreenPoint(item.TransformPoint(mf.sharedMesh.bounds.center));
                if (p.z <= 0f) continue;
                float d = Vector2.Distance(p, mouse);
                if (d < bestDistance) { bestDistance = d; best = item; }
            }
            return best;
        }

        void GrabFromBag(string kind)
        {
            var items = bagRoot.transform.Find("Items");
            if (items == null) return;
            for (int i = items.childCount - 1; i >= 0; i--)
                if (items.GetChild(i).name == kind) { StartCoroutine(Grab(items.GetChild(i))); return; }
        }

        /// Takes an item out of the bag: almond water goes up to your mouth and tips back; a battery goes into the
        /// flashlight (or the camcorder, whichever is emptier). It only counts once the motion is done.
        IEnumerator Grab(Transform item)
        {
            grabbing = true;
            if (hovered == item) hovered = null;   // it's leaving the bag; don't snap it back
            Holding = item;
            bool water = item.name == "Water";
            if (water) Hands.Play(Hands.Drink);
            try
            {
                Destroy(item.GetComponent<Collider>());
                item.SetParent(cam.transform, true);
                var hand = new Vector3(0.1f, -0.15f, 0.34f);
                var held = Quaternion.Euler(-10f, 20f, 8f);
                yield return Move(item, item.localPosition, hand, item.localRotation, held, 0.3f, 0.08f);
                if (water)
                {
                    // Bottom away from you, top at your mouth.
                    yield return Move(item, hand, new Vector3(0f, 0.02f, 0.32f), held, Quaternion.Euler(-115f, 0f, 0f), 0.35f, 0f);
                    yield return new WaitForSeconds(0.6f);
                    status.Drink();
                }
                else
                {
                    yield return Move(item, hand, new Vector3(0.22f, -0.32f, 0.22f), held, Quaternion.Euler(60f, 0f, 0f), 0.3f, 0.02f);
                    UseBattery();
                }
                if (item != null) Destroy(item.gameObject);
            }
            finally
            {
                if (Holding == item) Holding = null;
                grabbing = false;
            }
        }

        /// The bag: an open canvas bag held in front of you, drawn by its own camera so it never clips into walls.
        void BuildBag()
        {
            // HDRP has no camera stacking: the bag renders normally and again on top of everything, so walls never cut it.
            bagCam = cam;
            var passGo = new GameObject("Bag Overlay");
            passGo.transform.SetParent(transform, false);
            var passes = passGo.AddComponent<CustomPassVolume>();
            passes.isGlobal = true;
            passes.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
            passes.customPasses.Add(new DrawRenderersCustomPass
            {
                name = "Bag on top",
                layerMask = 1 << BagLayer,
                renderQueueType = CustomPass.RenderQueueType.All,
                overrideDepthState = true,
                depthCompareFunction = UnityEngine.Rendering.CompareFunction.Always,
                depthWrite = true,
                clearFlags = UnityEngine.Rendering.ClearFlag.Depth,
            });
            // Draw the actual contents after the shell so the bag's front wall can't hide the items.
            passes.customPasses.Add(new DrawRenderersCustomPass
            {
                name = "Bag contents visible",
                layerMask = 1 << BagContentsLayer,
                renderQueueType = CustomPass.RenderQueueType.All,
                overrideDepthState = true,
                depthCompareFunction = UnityEngine.Rendering.CompareFunction.Always,
                depthWrite = true,
                clearFlags = UnityEngine.Rendering.ClearFlag.Depth,
            });

            bagRoot = new GameObject("Bag");
            bagRoot.transform.SetParent(cam.transform, false);
            bagRoot.transform.localPosition = BagFront;
            bagRoot.transform.localRotation = BagFrontRot;   // mouth tipped toward you so you can see inside
            if (!BagModel())
            {
                var shell = new BoxMeshBuilder(Vector3.zero, 1);
                void Panel(float x, float y, float z, float sx, float sy, float sz) => shell.AddOrientedBox(Matrix4x4.identity, new Vector3(x, y, z), new Vector3(sx, sy, sz), 0);
                Panel(0f, 0f, 0f, 0.5f, 0.02f, 0.32f);
                Panel(0f, 0.09f, -0.16f, 0.5f, 0.2f, 0.02f);
                Panel(0f, 0.09f, 0.16f, 0.5f, 0.2f, 0.02f);
                Panel(-0.25f, 0.09f, 0f, 0.02f, 0.2f, 0.32f);
                Panel(0.25f, 0.09f, 0f, 0.02f, 0.2f, 0.32f);
                Child("Shell", shell.ToMesh("Bag"), HdMat.Make(new Color(0.32f, 0.36f, 0.24f), 0.15f));
            }
            SetLayer(bagRoot);
            bagRoot.SetActive(false);
        }

        /// The rucksack made in Blender (Art/BagItems.blend), replacing the plain box shell once the model is there.
        bool BagModel()
        {
            if (bagRoot.transform.Find("Backpack") != null) return true;
            var model = ModelLibrary.Standalone("Backpack", out var names);
            if (model == null) return false;
            var plain = bagRoot.transform.Find("Shell");
            if (plain != null) Destroy(plain.gameObject);
            // The rucksack made in Blender (Art/BagItems.blend): pocket toward you, lid flap folded away.
            var shellGo = new GameObject("Backpack");
            shellGo.transform.SetParent(bagRoot.transform, false);
            shellGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            shellGo.AddComponent<MeshFilter>().sharedMesh = model;
            var mats = new Material[names.Length];
            for (int i = 0; i < names.Length; i++)
                mats[i] = names[i] switch
                {
                    "Canvas" => HdMat.Make(new Color(0.30f, 0.33f, 0.20f), 0.12f),
                    "CanvasDark" => HdMat.Make(new Color(0.18f, 0.20f, 0.11f), 0.1f),
                    "Lining" => HdMat.Make(new Color(0.42f, 0.30f, 0.2f), 0.08f),   // a lighter lining so you can see what's inside
                    "Strap" => HdMat.Make(new Color(0.04f, 0.04f, 0.04f), 0.15f),
                    _ => HdMat.Make(new Color(0.03f, 0.03f, 0.03f), 0.55f),   // buckles: hard plastic
                };
            shellGo.AddComponent<MeshRenderer>().sharedMaterials = mats;
            // A little light over the opening (no shadows, under a metre of reach): the bag's own walls shade it otherwise.
            var glow = new GameObject("Bag Light");
            glow.transform.SetParent(bagRoot.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.45f, -0.15f);
            glow.AddHDLight(LightType.Point);
            var l = glow.GetComponent<Light>();
            l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen;
            l.intensity = 60f;
            l.range = 0.75f;
            l.color = new Color(1f, 0.95f, 0.85f);
            l.shadows = LightShadows.None;
            SetLayer(bagRoot);
            return true;
        }

        /// The thermoses and batteries actually in the bag, each its own clickable object.
        void FillBag()
        {
            shownWater = status.AlmondWater;
            shownBatteries = status.Batteries;
            var old = bagRoot.transform.Find("Items");
            if (old != null) { old.name = "Old Items"; Destroy(old.gameObject); }
            var streamer = FindAnyObjectByType<ChunkStreamer>();
            var paints = streamer != null && streamer.Paints != null ? streamer.Paints : FallbackBagPaints();
            var items = new GameObject("Items").transform;
            items.SetParent(bagRoot.transform, false);
            // The backpack shell faces the player; keep the contents in the same local orientation.
            items.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var thermos = ModelLibrary.Standalone("AlmondWater", out var tn);
            var battery = ModelLibrary.Standalone("Battery", out var bn);
            for (int i = 0; i < Mathf.Min(status.AlmondWater, 5) && thermos != null; i++)
                Item(items, "Water", thermos, tn, new Vector3(-0.15f + 0.075f * i, 0.02f, -0.06f), i * 37f, paints, ModelLibrary.CapPaint(i + 1));
            for (int i = 0; i < Mathf.Min(status.Batteries, 7) && battery != null; i++)
                Item(items, "Battery", battery, bn, new Vector3(-0.15f + 0.05f * i, 0.02f, 0.035f), i * 53f, paints, PropPaint.Metal);
            SetLayer(items.gameObject, BagContentsLayer);
        }

        static Material[] fallbackBagPaints;
        static Material[] FallbackBagPaints()
        {
            if (fallbackBagPaints != null) return fallbackBagPaints;
            var colors = new[]
            {
                new Color(0.48f, 0.5f, 0.52f), new Color(0.12f, 0.12f, 0.12f), new Color(0.32f, 0.2f, 0.1f),
                new Color(0.55f, 0.42f, 0.25f), new Color(0.82f, 0.8f, 0.72f), new Color(0.8f, 0.58f, 0.08f),
                new Color(0.65f, 0.08f, 0.06f), new Color(0.04f, 0.2f, 0.65f), new Color(0.1f, 0.42f, 0.12f),
                new Color(0.75f, 0.82f, 0.65f), new Color(0.18f, 0.45f, 0.65f), Color.black, new Color(0.3f, 0.32f, 0.22f),
                new Color(0.55f, 0.72f, 0.75f), new Color(0.3f, 0.3f, 0.3f), new Color(0.65f, 0.82f, 0.92f),
                new Color(0.35f, 0.18f, 0.1f), new Color(0.08f, 0.07f, 0.06f), new Color(0.55f, 0.4f, 0.25f),
                new Color(0.95f, 0.12f, 0.55f), new Color(0.05f, 0.75f, 0.8f), new Color(0.18f, 0.38f, 0.16f),
                new Color(0.26f, 0.3f, 0.18f), new Color(0.38f, 0.38f, 0.36f),
            };
            fallbackBagPaints = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                fallbackBagPaints[i] = HdMat.Make(colors[i], i == (int)PropPaint.Metal ? 0.65f : 0.25f,
                    i == (int)PropPaint.Metal ? 0.55f : 0f);
            return fallbackBagPaints;
        }

        static void Item(Transform parent, string name, Mesh mesh, string[] materials, Vector3 at, float yaw, Material[] paints, PropPaint cap)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mats = new Material[materials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                var paint = materials[i] == "Green" ? cap : Enum.TryParse(materials[i], true, out PropPaint p) ? p : PropPaint.Metal;
                mats[i] = paints[(int)paint];
            }
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            var box = go.AddComponent<BoxCollider>();
            box.center = mesh.bounds.center;
            box.size = mesh.bounds.size + Vector3.one * 0.035f;   // generous, so a near miss still counts
        }

        void Child(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(bagRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void SetLayer(GameObject go, int layer = BagLayer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        /// Camcorder night vision: green, grainy, bright, a little distorted.
        Volume MakeNightVision()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(1.6f);
            color.saturation.Override(-100f);
            color.colorFilter.Override(new Color(0.55f, 1f, 0.5f));
            color.contrast.Override(15f);
            profile.Add<FilmGrain>(true).intensity.Override(0.9f);
            profile.Add<Vignette>(true).intensity.Override(0.45f);
            profile.Add<LensDistortion>(true).intensity.Override(-0.18f);
            profile.Add<ChromaticAberration>(true).intensity.Override(0.35f);
            var go = new GameObject("Night Vision");
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 10f;
            v.weight = 0f;
            v.sharedProfile = profile;
            return v;
        }
    }
}
