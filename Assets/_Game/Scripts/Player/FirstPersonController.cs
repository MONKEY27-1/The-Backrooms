using UnityEngine;
using UnityEngine.InputSystem;
using Backrooms.World;

namespace Backrooms.Player
{
    /// First-person walking with mouse/keyboard or gamepad: sprint (with stamina), crouch, head bob, footsteps.
    /// Uses the project-wide input actions (Assets/InputSystem_Actions).
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        [SerializeField] Transform cameraPivot;
        [SerializeField] AudioSource footstepSource;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 2.6f;
        [SerializeField] float sprintSpeed = 4.8f;
        [SerializeField] float crouchSpeed = 1.4f;
        [SerializeField] float acceleration = 20f;

        [Header("Stamina (seconds)")]
        [SerializeField] float sprintTime = 14f;    // full stamina lasts this long
        [SerializeField] float recoverTime = 4f;    // empty to full

        [Header("Jumping")]
        [SerializeField] float jumpHeight = 0.6f;
        [SerializeField] float hopBoost = 1.25f;      // chaining a jump right as you land carries you 25% faster
        [SerializeField] float hopWindow = 0.2f;
        [SerializeField] float recoverDelay = 1f;

        [Header("Slide, vault, climb")]
        [SerializeField] float slideSpeed = 6.4f, slideTime = 0.8f;
        [SerializeField] float climbSpeed = 2.2f;

        [Header("Look")]
        [SerializeField] float mouseSensitivity = 0.08f;   // degrees per pixel
        [SerializeField] float gamepadLookSpeed = 160f;    // degrees per second at full stick

        [Header("Feel")]
        [SerializeField] float eyeHeight = 1.6f, crouchEyeHeight = 0.95f;
        [SerializeField] float standHeight = 1.8f, crouchHeight = 1.1f;
        [SerializeField] float bobAmount = 0.035f;
        [SerializeField] float stepLength = 0.9f, sprintStepLength = 1.3f;
        [SerializeField] float footstepVolume = 0.5f;

        [Header("Swimming")]
        [SerializeField] float diveSpeed = 2.2f;
        [SerializeField] float swimBreathSeconds = 22f;
        [SerializeField] float breathRecoverySeconds = 4f;
        [SerializeField] float drowningGraceSeconds = 6f;

        CharacterController body;
        PlayerStatus status;
        InputAction move, look, sprint, crouch, jump;
        float landedAt = -1f, hop = 1f;
        int hopChain;

        /// A bunny hop: how many jumps are chained so far.
        public event System.Action<int> Hopped;

        /// Set while something is chasing you: fear keeps you running (no stamina drain).
        public static bool Chased;

        /// The floor squelches (Level 0's always-moist carpet).
        public static bool WetFloor;

        /// You're in water deeper than you can stand in.
        public bool Swimming { get; private set; }
        public bool Underwater { get; private set; }
        public float Breath { get; private set; } = 1f;
        public bool Drowning { get; private set; }
        public event System.Action Drowned;
        public bool HasSwimControl { get; set; }
        AudioClip[] steps;
        Vector3 horizontalVelocity;
        float pitch, verticalVelocity, stepPhase, eye, sinceSprint;
        bool exhausted;
        float drowningTimer;
        bool drowningEventSent;
        bool wasSwimming;
        float underwaterBlend;
        bool lowBreathWarned;
        ChunkStreamer waterSource;

        public bool Crouching { get; private set; }
        public bool Running { get; private set; }

        /// Set by tools (the bag) to free the mouse; movement still works.
        public bool LookLocked { get; set; }

        /// Rummaging in the bag: half speed, no sprinting or jumping.
        public bool Encumbered { get; set; }
        /// Until this time you wade at half speed (Level 3's sludge sets it every frame you stand in it).
        public float SlowedUntil { get; set; }
        /// How fast your breath goes under water (Level 7: the diving gear halves it, the pressure of the deep raises it).
        public float BreathRate { get; set; } = 1f;

        /// Knocked to the floor (a Hound's maul): no moving, the view drops and tilts, then you get up.
        public float KnockedUntil { get; private set; }
        float knockRoll;

        float smotheredUntil;
        /// Something over your face: air goes instead of coming back.
        public bool Smothered => Time.time < smotheredUntil;
        public void Smother(float air)
        {
            smotheredUntil = Time.time + 0.2f;
            if (Meta.Difficulty.SurvivalEnabled) Breath = Mathf.Max(0f, Breath - air);
        }

        /// Dragged along the floor (a Clump reeling you in).
        public void Pull(Vector3 delta) { if (body != null && body.enabled) body.Move(delta); }

        public void KnockDown(float seconds, float roll = 0f)
        {
            KnockedUntil = Time.time + seconds;
            knockRoll = roll != 0f ? roll : Random.Range(18f, 30f) * (Random.value < 0.5f ? -1f : 1f);
            landDip = 0.25f;
        }

        public bool Sliding => sliding > 0f;
        public bool Vaulting => vaultT < 1f;
        public float VaultProgress => vaultT;
        public bool Climbing => Time.time - ladderSeen < 0.15f;
        public bool IsSeated { get; private set; }
        Seat seatedAt;

        float sliding, vaultT = 1f, vaultTime, ladderSeen = -1f, ladderTop;
        Vector3 slideDir, vaultFrom, vaultTo, vaultPeak, ladderNormal;

        /// Called by a ladder while you're touching it; `outward` points away from the wall it's on.
        public void OnLadder(Vector3 outward, float top) { ladderSeen = Time.time; ladderNormal = outward; ladderTop = top; }

        /// Height of the water surface you're standing in (Level -1's pool); -infinity when dry.
        public float WaterLevel { get; set; } = float.NegativeInfinity;

        /// How far you've dropped since leaving the ground (0 when standing).
        public float FallDistance { get; private set; }

        /// You hit the ground after a fall of this many metres.
        public event System.Action<float> Landed;
        float airTop;
        bool wasGrounded = true;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            status = GetComponent<PlayerStatus>();
            waterSource = FindFirstObjectByType<ChunkStreamer>();
            if (!TryGetComponent<PlayerAgent>(out _)) gameObject.AddComponent<PlayerAgent>();   // how creatures and co-op see you
            var map = InputSystem.actions.FindActionMap("Player", true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            sprint = map.FindAction("Sprint", true);
            crouch = map.FindAction("Crouch", true);
            jump = map.FindAction("Jump", true);
            map.Enable();
            steps = Audio.ProceduralAudio.Footsteps();
            eye = eyeHeight;
        }

        void OnEnable() => LockCursor(!LookLocked);

        /// Moves the player instantly (level changes, checkpoints) and kills any momentum.
        public bool Sit(Seat seat)
        {
            if (seat == null || IsSeated || Swimming || Vaulting || Sliding || !body.isGrounded) return false;
            seatedAt = seat;
            IsSeated = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            Running = false;
            body.enabled = false;
            transform.SetPositionAndRotation(seat.SitPosition, seat.SitRotation);
            cameraPivot.localPosition = new Vector3(0f, seat.EyeHeight, 0f);
            return true;
        }

        public void Stand()
        {
            if (!IsSeated) return;
            Vector3 position = seatedAt != null ? seatedAt.StandPosition : transform.position + transform.forward * 0.8f;
            seatedAt = null;
            IsSeated = false;
            body.enabled = false;
            transform.position = position;
            body.enabled = true;
            eye = eyeHeight;
            cameraPivot.localPosition = new Vector3(0f, eye, 0f);
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            wasGrounded = true;
        }

        public void Teleport(Vector3 position, float yaw)
        {
            if (IsSeated)
            {
                IsSeated = false;
                seatedAt = null;
                eye = eyeHeight;
                cameraPivot.localPosition = new Vector3(0f, eye, 0f);
            }
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            body.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
            Breath = 1f;
            Drowning = false;
            drowningTimer = 0f;
            drowningEventSent = false;
            wasSwimming = false;
            underwaterBlend = 0f;
            lowBreathWarned = false;
            if (Atmosphere.Instance != null) Atmosphere.Instance.SetUnderwater(0f);
            wasGrounded = true;
            FallDistance = 0f;
            pitch = 0f;
            cameraPivot.localRotation = Quaternion.identity;
            Backrooms.Game.AntiCheat.Instance?.TrustPosition(1.5f);
        }

        void Update()
        {
            HandleCursor();
            if (!LookLocked) Look();
            if (HasSwimControl && waterSource != null)
                WaterLevel = waterSource.TryWaterSurfaceAt(transform.position, out float surface) ? surface : float.NegativeInfinity;
            if (IsSeated)
            {
                bool stand = move.ReadValue<Vector2>().sqrMagnitude > 0.04f || jump.WasPressedThisFrame() || crouch.WasPressedThisFrame();
                if (!stand) { Running = false; return; }
                Stand();
            }
            if (Vaulting) { Vault(); return; }
            // Crouch while sprinting: a slide that carries your momentum.
            if (crouch.WasPressedThisFrame() && Running && body.isGrounded && !Encumbered && horizontalVelocity.magnitude > sprintSpeed * 0.75f)
            {
                sliding = slideTime;
                slideDir = horizontalVelocity.normalized;
                SetCrouch(true);
                if (status != null) status.Stamina = Mathf.Max(0f, status.Stamina - 0.05f);
                footstepSource.PlayOneShot(steps[Random.Range(0, steps.Length)], footstepVolume * Meta.AudioMix.Effects);
            }
            // Hold Ctrl (or gamepad east) to crouch; let go to stand, as soon as there is headroom.
            if (!Swimming && !Sliding && crouch.IsPressed() != Crouching) SetCrouch(crouch.IsPressed());
            if (jump.WasPressedThisFrame() && !Swimming && !Encumbered && !Climbing && TryVault()) return;
            bool running = Move();
            BobAndFootsteps(running);

            // The player is the camera: blend the level fog into a close blue-green murk as the eyes submerge.
            float uwTarget = Underwater && HasSwimControl ? 1f : 0f;
            underwaterBlend = Mathf.MoveTowards(underwaterBlend, uwTarget, Time.deltaTime * 2.2f);
            if (Atmosphere.Instance != null) Atmosphere.Instance.SetUnderwater(underwaterBlend);

            // A splash on the way in (and a softer one on the way out) grounds the surface crossing in the world.
            if (Swimming != wasSwimming)
            {
                footstepSource.pitch = Random.Range(0.9f, 1.05f);
                footstepSource.PlayOneShot(Audio.ProceduralAudio.Splash(), (Swimming ? 0.55f : 0.3f) * Meta.AudioMix.Effects);
                wasSwimming = Swimming;
            }
        }

        void HandleCursor()
        {
            if (LookLocked) return;
            if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) LockCursor(true);
        }

        public static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        void Look()
        {
            Vector2 input = look.ReadValue<Vector2>();
            // Mouse gives a per-frame pixel delta; a stick gives a -1..1 rate.
            bool mouse = look.activeControl?.device is Pointer;
            if (mouse && Cursor.lockState != CursorLockMode.Locked) return;
            var settings = Meta.GameSettings.Current;
            Vector2 delta = (mouse ? input * mouseSensitivity : input * (gamepadLookSpeed * Time.deltaTime)) * settings.sensitivity;
            if (settings.invertY) delta.y = -delta.y;
            transform.Rotate(0f, delta.x, 0f);
            pitch = Mathf.Clamp(pitch - delta.y, -85f, 85f);
            knockRoll = Time.time < KnockedUntil ? knockRoll : Mathf.MoveTowards(knockRoll, 0f, Time.deltaTime * 40f);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, knockRoll + limpRoll);
        }

        /// Crouching lets you through the low doorways copy drift leaves behind. You can't stand up under a low ceiling.
        void SetCrouch(bool down)
        {
            if (!down)
            {
                // Only test the space above the crouched body, or the check hits our own collider and you never stand.
                var bottom = transform.position + Vector3.up * (crouchHeight + 0.05f + body.radius);
                var top = transform.position + Vector3.up * (standHeight - body.radius);
                if (Physics.CheckCapsule(bottom, Vector3.Max(bottom, top), body.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore)) return;
            }
            Crouching = down;
            body.height = down ? crouchHeight : standHeight;
            body.center = Vector3.up * (body.height / 2f);
        }

        bool Move()
        {
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            float depth = WaterLevel - transform.position.y;
            // Standing on a submerged gallery is walking, not swimming: the character controller still gets a
            // floor, but the eyes stay under water so breath and fog keep behaving as if you were diving.
            bool standingUnderWater = body.isGrounded && depth > 0.2f;
            Swimming = !standingUnderWater && (depth > 1.3f || (Swimming && depth > 0.85f));
            // Submerged means the water is above your eyes (the camera rides at `eye`, not at your feet).
            Underwater = depth > eye + 0.08f;
            bool grounded = body.isGrounded && (!Swimming || standingUnderWater);
            bool wantsRun = sprint.IsPressed() && input.y > 0.1f && !Crouching && !Swimming && !Encumbered;
            if (status != null)
            {
                if (!Meta.Difficulty.SurvivalEnabled) { status.Stamina = 1f; exhausted = false; }
                else
                {
                    if (status.Stamina <= 0.01f) exhausted = true;
                    if (exhausted && status.Stamina > 0.3f) exhausted = false;   // catch your breath before sprinting again
                }
            }
            bool running = wantsRun && (!exhausted || Chased);
            bool swimFast = HasSwimControl && Swimming && sprint.IsPressed() && input.sqrMagnitude > 0.01f && !Encumbered && !exhausted;
            Running = running;
            if (status != null)
            {
                if (!Meta.Difficulty.SurvivalEnabled) status.Stamina = 1f;
                else if ((running && !Chased) || swimFast) { status.Stamina = Mathf.Max(0f, status.Stamina - Time.deltaTime / sprintTime); sinceSprint = 0f; }
                else if (!running && (sinceSprint += Time.deltaTime) > recoverDelay) status.Stamina = Mathf.Min(1f, status.Stamina + Time.deltaTime / recoverTime);
            }

            // Jumping. A jump made right as you land (a bunny hop) costs nothing and carries you 25% faster.
            if (grounded && hop > 1f && Time.time - landedAt > hopWindow + 0.1f) hop = 1f;   // stayed on the ground: the chain is over
            if (Climbing) { Climb(input); return false; }
            if (grounded && !Crouching && !Encumbered && jump.WasPressedThisFrame())
            {
                bool chained = Time.time - landedAt <= hopWindow;
                if (chained) { hop = hopBoost; Hopped?.Invoke(++hopChain); }
                else
                {
                    hopChain = 1;
                    if (status != null) status.Stamina = Mathf.Max(0f, status.Stamina - 0.04f);
                }
                verticalVelocity = Mathf.Sqrt(2f * -Physics.gravity.y * jumpHeight);
                grounded = false;
            }

            bool limping = status != null && status.Limping;
            float limpSpeedMult = limping ? (running ? 0.65f : 0.78f) : 1f;
            float speed = (Crouching ? crouchSpeed : running ? sprintSpeed : walkSpeed) * hop * (Encumbered ? 0.5f : 1f) * (Time.time < SlowedUntil ? 0.5f : 1f) * (Time.time < KnockedUntil ? 0f : 1f) * limpSpeedMult;
            if (Swimming) speed = walkSpeed * (Underwater ? 0.85f : 0.65f) * (swimFast ? 1.7f : 1f);
            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            if (Sliding)
            {
                // Momentum fades from a slide to a crouch-walk; you can only lean a little left or right.
                sliding -= Time.deltaTime;
                float k = Mathf.Clamp01(sliding / slideTime);
                target = slideDir * Mathf.Lerp(crouchSpeed, slideSpeed, k) + transform.right * input.x * 0.8f;
                horizontalVelocity = target;
                if (body.velocity.sqrMagnitude < 1f && k < 0.8f) sliding = 0f;   // ran into something
            }
            float accel = grounded || Swimming ? acceleration : acceleration * 0.25f;   // keep your momentum in the air
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, accel * Time.deltaTime);
            if (Swimming)
            {
                // Rise and dive cancel each other; otherwise settle just under the surface with a slow drift.
                bool rising = jump.IsPressed(), diving = HasSwimControl && crouch.IsPressed();
                float kick = sprint.IsPressed() ? 1.7f : 1f;   // sprint to dive and surface faster
                if (rising && !diving) verticalVelocity = 4.2f * Mathf.Lerp(1f, kick, 0.6f);
                else if (diving && !rising) verticalVelocity = -diveSpeed * kick;
                else
                {
                    float want = WaterLevel - eye * 0.62f + Mathf.Sin(Time.time * 1.25f) * 0.045f;
                    verticalVelocity = Mathf.Clamp((want - transform.position.y) * 2f, -1.6f, 1.6f);
                }
            }
            else
            {
                if (grounded && verticalVelocity <= 0f) verticalVelocity = -1f;
                else verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            // Air runs out whenever the eyes are under water, including while walking on a submerged gallery.
            if (Underwater && HasSwimControl && Meta.Difficulty.SurvivalEnabled)
            {
                Breath = Mathf.Max(0f, Breath - Time.deltaTime / swimBreathSeconds * BreathRate);
                if (Breath < 0.25f && !lowBreathWarned)
                {
                    lowBreathWarned = true;
                    UI.Captions.Say("[your lungs are burning]");
                }
                if (Breath <= 0f)
                {
                    Drowning = true;
                    drowningTimer += Time.deltaTime;
                    if (!drowningEventSent && drowningTimer >= drowningGraceSeconds)
                    {
                        drowningEventSent = true;
                        Drowned?.Invoke();
                    }
                }
            }
            else
            {
                if (!Meta.Difficulty.SurvivalEnabled) Breath = 1f;
                else if (!Smothered) Breath = Mathf.MoveTowards(Breath, 1f, Time.deltaTime / breathRecoverySeconds);
                if (Breath > 0.5f) lowBreathWarned = false;
                Drowning = false;
                drowningTimer = 0f;
                drowningEventSent = false;
            }
            bool wasAir = !body.isGrounded;
            body.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            if (wasAir && body.isGrounded) landedAt = Time.time;
            TrackFall();
            return running;
        }

        /// Jump at something waist-high: climb onto it if you fit on top, vault over it if it's thin and there's
        /// floor on the other side. Returns false when there's nothing to climb.
        bool TryVault()
        {
            var fwd = transform.forward;
            var feet = transform.position;
            const int mask = ~0;
            if (!Physics.Raycast(feet + Vector3.up * 0.45f, fwd, out var face, 0.9f, mask, QueryTriggerInteraction.Ignore)) return false;
            Vector3 probe = face.point + fwd * 0.25f;
            probe.y = feet.y + 1.7f;
            if (!Physics.Raycast(probe, Vector3.down, out var top, 1.7f, mask, QueryTriggerInteraction.Ignore)) return false;
            float h = top.point.y - feet.y;
            if (h < 0.4f || h > 1.35f) return false;
            // Room to get your body over the top at all.
            if (Physics.SphereCast(feet + Vector3.up * (h + 0.75f), 0.3f, fwd, out _, 0.9f, mask, QueryTriggerInteraction.Ignore)) return false;

            bool Fits(Vector3 at) => !Physics.CheckCapsule(at + Vector3.up * (body.radius + 0.05f), at + Vector3.up * (standHeight - body.radius), body.radius * 0.95f, mask, QueryTriggerInteraction.Ignore);
            // Is it thin? Look for its top again further in.
            Vector3 far = face.point + fwd * 1.0f;
            far.y = feet.y + 1.7f;
            bool deep = Physics.Raycast(far, Vector3.down, out var farTop, 1.7f, mask, QueryTriggerInteraction.Ignore) && Mathf.Abs(farTop.point.y - top.point.y) < 0.15f;
            Vector3 onTop = top.point + Vector3.up * 0.02f;
            Vector3 target;
            float time;
            if (deep && Fits(onTop)) { target = onTop + fwd * 0.2f; time = 0.45f; }                 // mantle up onto it
            else
            {
                Vector3 land = face.point + fwd * 1.3f;
                land.y = feet.y + 1.2f;
                if (!Physics.Raycast(land, Vector3.down, out var ground, 3f, mask, QueryTriggerInteraction.Ignore) || !Fits(ground.point + Vector3.up * 0.02f))
                {
                    if (!Fits(onTop)) return false;
                    target = onTop + fwd * 0.2f; time = 0.45f;
                }
                else { target = ground.point + Vector3.up * 0.02f; time = 0.4f; }                      // vault over
            }
            var eyes = feet + Vector3.up * (standHeight - 0.15f);
            if (Physics.Linecast(eyes, target + Vector3.up * 1.0f, mask, QueryTriggerInteraction.Ignore)) return false;
            vaultFrom = feet;
            vaultTo = target;
            vaultPeak = (feet + target) / 2f;
            vaultPeak.y = Mathf.Max(feet.y, target.y, top.point.y) + 0.35f;
            vaultTime = time;
            vaultT = 0f;
            body.enabled = false;
            if (status != null) status.Stamina = Mathf.Max(0f, status.Stamina - 0.05f);
            footstepSource.PlayOneShot(steps[Random.Range(0, steps.Length)], footstepVolume * Meta.AudioMix.Effects);
            return true;
        }

        void Vault()
        {
            vaultT = Mathf.Min(1f, vaultT + Time.deltaTime / vaultTime);
            float t = vaultT;
            // A curve through the peak, so your head rises over the edge before you come down.
            transform.position = (1 - t) * (1 - t) * vaultFrom + 2 * (1 - t) * t * vaultPeak + t * t * vaultTo;
            cameraPivot.localPosition = new Vector3(0f, eye - Mathf.Sin(t * Mathf.PI) * 0.25f, 0f);
            if (vaultT < 1f) return;
            body.enabled = true;
            horizontalVelocity = transform.forward * walkSpeed;
            verticalVelocity = -1f;
            wasGrounded = true;
            FallDistance = 0f;
            footstepSource.PlayOneShot(steps[Random.Range(0, steps.Length)], footstepVolume * 0.8f * Meta.AudioMix.Effects);
        }

        /// On a ladder: forward/back climbs, Jump pushes you off.
        void Climb(Vector2 input)
        {
            if (jump.WasPressedThisFrame())
            {
                ladderSeen = -1f;
                horizontalVelocity = ladderNormal * 3f;
                verticalVelocity = 2f;
                return;
            }
            // Facing the ladder, forward goes up; facing away, forward goes down.
            float facing = -Vector3.Dot(transform.forward, ladderNormal) >= -0.2f ? 1f : -1f;
            verticalVelocity = input.y * climbSpeed * facing;
            if (verticalVelocity > 0f && transform.position.y > ladderTop - 0.3f)
            {
                // At the top: over the edge and onto the floor.
                ladderSeen = -1f;
                transform.position += Vector3.up * 0.35f;
                horizontalVelocity = -ladderNormal * 2.5f;
                verticalVelocity = 1.5f;
                return;
            }
            horizontalVelocity = transform.right * input.x * 0.8f - ladderNormal * 0.4f * Mathf.Abs(input.y);
            body.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            FallDistance = 0f;
            wasGrounded = true;
        }

        void TrackFall()
        {
            float y = transform.position.y;
            // Water catches you: swimming or diving is never a fall (a deep dive used to count as falling out of the world).
            if (Swimming || y < WaterLevel)
            {
                airTop = y;
                FallDistance = 0f;
                wasGrounded = false;
                return;
            }
            if (!body.isGrounded)
            {
                if (wasGrounded) airTop = y;
                airTop = Mathf.Max(airTop, y);
                FallDistance = airTop - y;
            }
            else if (!wasGrounded)
            {
                float drop = airTop - y;
                FallDistance = 0f;
                if (drop > 0.5f) Landed?.Invoke(drop);
                landDip = Mathf.Min(0.06f, Mathf.Max(0f, drop - 0.2f) * 0.03f);   // a short knee-bend on landing
            }
            wasGrounded = body.isGrounded;
        }

        void BobAndFootsteps(bool running)
        {
            Vector3 v = body.velocity;
            v.y = 0f;
            float speed = body.isGrounded ? v.magnitude : 0f;
            float previous = stepPhase;
            stepPhase += speed * Time.deltaTime / (running ? sprintStepLength : stepLength) * Mathf.PI;

            bool limping = status != null && status.Limping;
            float limpWave = Mathf.Sin(stepPhase * 0.5f);
            float limpDip = limping ? Mathf.Max(0f, limpWave) * 0.05f * Mathf.Clamp01(speed / walkSpeed) : 0f;
            limpRoll = limping ? limpWave * 2.4f * Mathf.Clamp01(speed / walkSpeed) : 0f;

            // One footstep per half wave of the bob: the foot lands when the head is lowest. Crouching is quieter.
            if (Mathf.Floor(stepPhase / Mathf.PI) > Mathf.Floor(previous / Mathf.PI))
            {
                bool heavyStep = limping && limpWave > 0f;
                footstepSource.pitch = heavyStep ? Random.Range(0.82f, 0.95f) : Random.Range(0.92f, 1.08f);
                footstepSource.PlayOneShot(steps[Random.Range(0, steps.Length)], footstepVolume * Meta.AudioMix.Effects * (running ? 1.3f : Crouching ? 0.4f : 1f) * (heavyStep ? 1.25f : 1f));
                if (WetFloor) footstepSource.PlayOneShot(Audio.ProceduralAudio.Squelch(), footstepVolume * Meta.AudioMix.Effects * 0.45f * (Crouching ? 0.5f : 1f));
            }

            eye = Time.time < KnockedUntil ? Mathf.MoveTowards(eye, 0.4f, Time.deltaTime * 6f) : Mathf.MoveTowards(eye, Crouching ? crouchEyeHeight : eyeHeight, Time.deltaTime * 3f);
            float bob = (Meta.GameSettings.Current.headBob ? Meta.GameSettings.Current.effects : 0f) * bobAmount * Mathf.Abs(Mathf.Sin(stepPhase)) * Mathf.Clamp01(speed / walkSpeed);
            landDip *= Mathf.Exp(-Time.deltaTime * 9f);
            float dip = Meta.GameSettings.Current.headBob ? landDip : 0f;
            cameraPivot.localPosition = new Vector3(limping ? limpWave * 0.015f : 0f, eye + bob - dip - limpDip, 0f);
        }

        float landDip, limpRoll;
    }
}
