using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Backrooms.Player
{
    /// Your own hazmat arms in first person (Resources/Models/Player_HazmatArms, cut from the user's suit model):
    /// the left hand holds the flashlight, the right one reaches, grabs, drinks and pries. Items you pick up or
    /// take from the bag are followed by the hand that holds them. Drawn on top (like the bag) so walls never cut
    /// them, with a little lag behind the camera and a small step bob.
    [DefaultExecutionOrder(100)]   // after the controller and the tools have moved things this frame
    public sealed class FirstPersonArms : MonoBehaviour
    {
        public const int Layer = 28;
        // Where your shoulders sit, seen from your eyes.
        static readonly Vector3 ShoulderAt = new Vector3(0f, -0.17f, 0.04f);   // a touch forward of real, so the bag sides are in reach
        const float ShoulderWidth = 0.36f;
        static readonly Vector3 PoleRight = new Vector3(0.6f, -1f, -0.3f);   // elbows down and out

        PlayerTools tools;
        FirstPersonController body;
        CharacterController mover;
        PlayerAgent agent;
        PlayerStatus status;
        Camera cam;
        Transform pivot, torch;
        ArmChain left, right;
        GameObject leftStump;
        readonly HandDriver dl = new HandDriver(), dr = new HandDriver();
        SkinnedMeshRenderer[] skins;
        int suit = -1;
        Quaternion lastCam, sway = Quaternion.identity;
        float bobPhase, landing;

        void Start()
        {
            tools = GetComponent<PlayerTools>();
            body = GetComponent<FirstPersonController>();
            mover = GetComponent<CharacterController>();
            agent = GetComponent<PlayerAgent>();
            status = GetComponent<PlayerStatus>();
            cam = GetComponentInChildren<Camera>();
            var prefab = Resources.Load<GameObject>("Models/Player_HazmatArms");
            if (prefab == null || cam == null || tools == null || body == null) { enabled = false; return; }

            pivot = new GameObject("Arms").transform;
            pivot.SetParent(cam.transform, false);
            var model = Instantiate(prefab, pivot, false).transform;
            var la = Deep(model, "LeftArm");
            var ra = Deep(model, "RightArm");
            if (la == null || ra == null) { enabled = false; return; }
            // Face the camera's way whatever axes the FBX came with, at a real arm's size, shoulders by the eyes.
            if (pivot.InverseTransformPoint(la.position).x > 0f) model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation;
            model.localScale *= ShoulderWidth / Mathf.Max(0.01f, Vector3.Distance(la.position, ra.position));
            model.localPosition += ShoulderAt - pivot.InverseTransformPoint((la.position + ra.position) * 0.5f);

            left = new ArmChain(model, "Left", cam.transform.forward, cam.transform.up);
            right = new ArmChain(model, "Right", cam.transform.forward, cam.transform.up);
            skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var s in skins)
            {
                s.updateWhenOffscreen = true;   // bones leave the T-pose bounds
                s.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            torch = Hands.MakeFlashlight(pivot).transform;
            SetLayer(model.gameObject);
            SetLayer(torch.gameObject);
            BuildRenderPass();
            lastCam = cam.transform.rotation;
            body.Landed += OnLanded;
        }

        void OnDestroy()
        {
            if (body != null) body.Landed -= OnLanded;
        }

        void OnLanded(float drop) => landing = Mathf.Min(0.035f, drop * 0.008f);

        /// Clear only the depth before this pass. The arms then depth-test against themselves normally, so the
        /// wall cannot slice them but the far hand and the backs of the sleeves still occlude correctly.
        /// It runs just before the bag's own passes (same volume, so the order is fixed): the open bag and what you
        /// hold are drawn over the hands, so hands under the bag only show past its edges.
        void BuildRenderPass()
        {
            var bagPasses = transform.Find("Bag Overlay");
            var volume = bagPasses != null ? bagPasses.GetComponent<CustomPassVolume>() : null;
            if (volume == null)
            {
                var go = new GameObject("First-person Arms Overlay");
                go.transform.SetParent(transform, false);
                volume = go.AddComponent<CustomPassVolume>();
                volume.isGlobal = true;
                volume.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
            }
            volume.customPasses.Insert(0, new DrawRenderersCustomPass
            {
                name = "First-person arms",
                layerMask = 1 << Layer,
                renderQueueType = CustomPass.RenderQueueType.All,
                overrideDepthState = true,
                depthCompareFunction = CompareFunction.LessEqual,
                depthWrite = true,
                clearFlags = ClearFlag.Depth,
            });
        }

        int State()
        {
            if (agent != null && !agent.Alive) return Hands.None;
            if (tools.ReadingText != null) return Hands.Read;
            if (tools.CamcorderUp) return Hands.Camcorder;
            if (body.Vaulting) return Hands.Vault;
            if (body.Climbing) return Hands.Ladder;
            if (body.Swimming) return Hands.Swim;
            if (body.Sliding) return Hands.Slide;
            if (tools.Prying) return Hands.Pry;
            if (tools.BagTransform != null) return Hands.Bag;
            return Hands.None;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Hands.Resolve(State());
            int id = Hands.Current;
            float t = Time.time - Hands.Since;
            bool torchOn = tools.FlashlightOn;
            float cycle = id == Hands.Vault ? body.VaultProgress : id == Hands.Ladder ? transform.position.y / 0.35f : t;
            var pr = Hands.Get(id, t, torchOn, false, cycle);
            var pl = Hands.Get(id, t, torchOn, true, cycle);
            // Reading a paper or looking through the camcorder fills the view; the arms drop away.
            bool away = id == Hands.Read || id == Hands.Camcorder || (agent != null && !agent.Alive);
            if (away || pr.Weight <= 0f) pr = Hands.Down(false);
            if (away || pl.Weight <= 0f) pl = Hands.Down(true);

            var held = tools.Holding;
            var bag = tools.BagTransform;
            if (bag != lastBag) { lastBag = bag; if (bag != null) MeasureBag(bag); }
            if (!away && bag != null)
            {
                // Palms flat on the bag's side walls, fingers hooked toward its base, clear of the opening.
                // The right hand carries it round from your back; the left takes the other side once it's in front.
                bool front = pivot.InverseTransformPoint(bag.position).z > 0.35f;
                pr = BagSide(bag, true, front);
                if (front) pl = BagSide(bag, false, true);
            }
            if (!away && held != null) pr = Grip(held);   // taking something out: the left hand keeps the bag

            Sway(dt);
            var r = dr.Step(pr, dt, held != null || bag != null ? 30f : 11f);
            var l = dl.Step(pl, dt, bag != null ? 30f : 11f);
            right.Solve(pivot.TransformPoint(r.Pos), pivot.TransformDirection(r.Fingers), pivot.TransformDirection(r.Dorsal), pivot.TransformDirection(PoleRight), r.Curl, 1f);
            left.Solve(pivot.TransformPoint(l.Pos), pivot.TransformDirection(l.Fingers), pivot.TransformDirection(l.Dorsal), pivot.TransformDirection(new Vector3(-PoleRight.x, PoleRight.y, PoleRight.z)), l.Curl, 1f);

            // The torch sits in the left palm, beam roughly where you look; the real light comes from its lens.
            bool severed = status != null && status.SeveredArm;
            if (severed)
            {
                if (leftStump == null && left.Upper != null)
                {
                    leftStump = Creatures.GoreKit.AttachStump(left.Upper, new Vector3(0f, 0f, 0.22f), Quaternion.Euler(90f, 0f, 0f), 0.7f);
                    if (leftStump != null) SetLayer(leftStump);
                }
                if (left.Fore != null) left.Fore.localScale = Vector3.zero;
                if (left.Hand != null) left.Hand.localScale = Vector3.zero;
            }
            else
            {
                if (leftStump != null) { Destroy(leftStump); leftStump = null; }
                if (left.Fore != null) left.Fore.localScale = Vector3.one;
                if (left.Hand != null) left.Hand.localScale = Vector3.one;
            }

            torch.gameObject.SetActive(torchOn);
            if (torchOn)
            {
                if (severed)
                {
                    torch.position = pivot.TransformPoint(new Vector3(-0.09f, -0.15f, 0.26f));
                    torch.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                }
                else
                {
                    var f = pivot.TransformDirection(l.Fingers);
                    torch.position = left.Hand.position + f * 0.08f - pivot.TransformDirection(l.Dorsal) * 0.03f;
                    torch.rotation = Quaternion.LookRotation(cam.transform.forward * 0.85f + f * 0.15f, cam.transform.up);
                }
                if (tools.Lamp != null) tools.Lamp.transform.position = torch.position + torch.forward * 0.08f;
            }

            int want = Net.CoopAvatar.Mine != null ? Net.CoopAvatar.Mine.Suit : 0;
            if (want != suit)
            {
                suit = want;
                var mat = Resources.Load<Material>("Suits/Suit_" + suit);
                if (mat != null) foreach (var s in skins) s.sharedMaterial = mat;
            }
        }

        Transform lastBag;
        Vector3 bagMin = new Vector3(-0.18f, -0.15f, -0.1f), bagMax = new Vector3(0.18f, 0.15f, 0.1f);

        /// The bag's size in its own space (the shell only, not the items), read once each time it comes out.
        void MeasureBag(Transform bag)
        {
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var r in bag.GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.layer != bag.gameObject.layer) continue;   // contents are drawn on their own layer
                var b = r.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = bag.InverseTransformPoint(r.transform.TransformPoint(c));
                    if (!any) { min = max = p; any = true; }
                    else { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
                }
            }
            if (any) { bagMin = min; bagMax = max; }
        }

        /// One hand flat on the side of the bag nearest it; `settled` once the bag is in front of you.
        Hands.Pose BagSide(Transform bag, bool rightHand, bool settled)
        {
            // Whichever bag side faces this hand (the bag turns round while it swings off your back).
            float sx = Mathf.Sign(pivot.InverseTransformDirection(bag.right).x) * (rightHand ? 1f : -1f);
            var mid = (bagMin + bagMax) * 0.5f;
            // Palm up under the base, near its outer edge, fingers reaching forward and curling up round the edge.
            float edge = sx > 0f ? Mathf.Lerp(mid.x, bagMax.x, 0.75f) : Mathf.Lerp(mid.x, bagMin.x, 0.75f);
            var local = new Vector3(edge, bagMin.y - 0.015f, Mathf.Lerp(mid.z, bagMin.z, 0.3f));
            var fingers = (bag.forward - bag.right * sx * 0.25f).normalized;
            float bob = settled ? 0.006f * Mathf.Sin(Time.time * 1.7f) : 0f;   // the weight shifting a little, so it doesn't look frozen
            var pos = pivot.InverseTransformPoint(bag.TransformPoint(local) - fingers * 0.07f - bag.up * (0.025f + bob));
            return new Hands.Pose(pos, pivot.InverseTransformDirection(fingers), pivot.InverseTransformDirection(-bag.up), settled ? 0.4f : 0.7f);
        }

        /// The right hand closed round an item, thumb toward its top, following it wherever it goes.
        Hands.Pose Grip(Transform item)
        {
            var thumb = pivot.InverseTransformDirection(item.up);
            var dorsal = Vector3.ProjectOnPlane(Vector3.right, thumb);
            if (dorsal.sqrMagnitude < 1e-4f) dorsal = Vector3.up;
            dorsal.Normalize();
            var fingers = Vector3.Cross(dorsal, thumb);
            return new Hands.Pose(pivot.InverseTransformPoint(item.position) - fingers * 0.07f + dorsal * 0.045f, fingers, dorsal, 0.75f);
        }

        /// The arms trail the camera a touch when you turn and bob a little with your steps.
        void Sway(float dt)
        {
            var now = cam.transform.rotation;
            var delta = Quaternion.Inverse(now) * lastCam;
            lastCam = now;
            bool on = Meta.GameSettings.Current.headBob;
            sway = on ? Quaternion.Slerp(delta * sway, Quaternion.identity, 1f - Mathf.Exp(-dt * 10f)) : Quaternion.identity;
            sway.ToAngleAxis(out float angle, out var axis);
            if (angle > 180f) angle -= 360f;
            if (Mathf.Abs(angle) > 4f) sway = Quaternion.AngleAxis(Mathf.Sign(angle) * 4f, axis);
            pivot.localRotation = sway;

            var v = mover != null && mover.isGrounded ? mover.velocity : Vector3.zero;
            v.y = 0f;
            float speed = v.magnitude;
            bobPhase += speed * dt * 1.9f;
            float k = on ? Mathf.Clamp01(speed / 2f) : 0f;
            landing = Mathf.MoveTowards(landing, 0f, dt * 0.12f);
            var bob = new Vector3(Mathf.Sin(bobPhase) * 0.004f, -Mathf.Abs(Mathf.Cos(bobPhase)) * 0.006f, 0f) * k;
            bob.y -= landing;
            if (status != null && (status.Health < 0.38f || status.BleedRate > 0f))
            {
                float factor = Mathf.Clamp01((0.38f - status.Health) / 0.38f) + (status.BleedRate > 0f ? 0.4f : 0f);
                float tx = (Mathf.PerlinNoise(Time.time * 24f, 0f) - 0.5f) * 0.007f * factor;
                float ty = (Mathf.PerlinNoise(0f, Time.time * 28f) - 0.5f) * 0.007f * factor;
                bob += new Vector3(tx, ty, 0f);
            }
            pivot.localPosition = bob;
        }

        static void SetLayer(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        }

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = Deep(c, name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
