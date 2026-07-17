using UnityEngine;

namespace RoofingSimulator.Player
{
    /// <summary>
    /// First-person, CharacterController-based locomotion tuned for the PEAK-style
    /// feel: a bit of weight without losing arcade responsiveness. Handles walking,
    /// sprinting (stamina), jumping, crouching, slope behaviour (steep faces both
    /// slow you and, past a limit, slide you down), head-bob, carrying penalties and
    /// external pushes (wind). Looking is handled by <see cref="CameraController"/>.
    ///
    /// Other systems (ladder, ledge-grab, harness) take over by switching the
    /// <see cref="Mode"/> so this script doesn't fight them for control.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerLocomotion : MonoBehaviour
    {
        public enum ControlMode { Normal, Climbing, Hanging, Disabled }

        [Header("Speeds (m/s)")]
        [SerializeField] private float walkSpeed = 4.2f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float crouchSpeed = 2f;
        [SerializeField] private float airControl = 0.5f;
        [SerializeField] private float acceleration = 14f;

        [Header("Jump / Gravity")]
        [SerializeField] private float jumpHeight = 1.15f;
        [SerializeField] private float gravity = 22f;
        [SerializeField] private float jumpStaminaCost = 12f;

        [Header("Crouch")]
        [SerializeField] private float standHeight = 1.78f; // real ~1.75 m person
        [SerializeField] private float crouchHeight = 1.1f;

        [Header("Sprint")]
        [SerializeField] private float sprintStaminaPerSecond = 14f;

        [Header("Slopes")]
        [Tooltip("Above this ground angle the player starts sliding downhill. Set just under a 7/12 roof (30.3°) so the real base roof still gives the PEAK 'pull'.")]
        [SerializeField] private float slideStartAngle = 28f;
        [SerializeField] private float slideAcceleration = 16f;
        [Tooltip("How much steep ground saps walk speed (0..1 at the slide angle).")]
        [SerializeField] private float steepSpeedPenalty = 0.4f;

        [Header("Carry penalty (shingle bundle)")]
        [SerializeField] private float carrySpeedMultiplier = 0.62f;
        [SerializeField] private float carryStaminaPerSecond = 4f;

        [Header("Head-bob")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private float bobAmplitude = 0.05f;
        [SerializeField] private float bobFrequency = 9f;

        [Header("References")]
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private SafetyHarness harness;

        private CharacterController controller;
        private Vector3 horizontalVelocity;
        private float verticalVelocity;
        private Vector3 windVelocity;
        private Vector3 impulseVelocity;
        private Vector3 groundNormal = Vector3.up;
        private float groundAngle;
        private bool crouching;
        private float carryLoad;
        private float gripUntil; // foam pads keep refreshing this while you stand on them
        private float slipperiness; // 0 dry .. 1 soaked — wet roofs slide sooner (weather)
        private float bobPhase;
        private Vector3 cameraPivotBase;

        public ControlMode Mode { get; private set; } = ControlMode.Normal;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public Vector3 Velocity => horizontalVelocity + Vector3.up * verticalVelocity;
        public float CurrentSpeed => horizontalVelocity.magnitude;
        public bool IsCarrying => carryLoad > 0.05f;
        /// <summary>Crouched low — the weather system won't call you the tallest point (Fase C3).</summary>
        public bool IsCrouching => crouching;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (stamina == null) stamina = GetComponent<PlayerStamina>();
            if (harness == null) harness = GetComponent<SafetyHarness>();
            if (cameraPivot == null)
            {
                Transform p = transform.Find("CameraPivot");
                if (p != null) cameraPivot = p;
            }
            controller.height = standHeight;
            controller.center = new Vector3(0f, standHeight * 0.5f, 0f);
            if (cameraPivot != null) cameraPivotBase = cameraPivot.localPosition;
        }

        private void Update()
        {
            switch (Mode)
            {
                case ControlMode.Normal:
                    TickNormal();
                    break;
                case ControlMode.Climbing:
                case ControlMode.Hanging:
                    // Driven by LadderClimber / LedgeGrabAndFall; we just hold gravity off.
                    verticalVelocity = 0f;
                    break;
                case ControlMode.Disabled:
                    ApplyExternalOnly();
                    break;
            }
        }

        private void TickNormal()
        {
            float dt = Time.deltaTime;

            float h = UnityEngine.Input.GetAxisRaw("Horizontal");
            float v = UnityEngine.Input.GetAxisRaw("Vertical");
            Vector3 wish = (transform.right * h + transform.forward * v);
            wish = Vector3.ClampMagnitude(wish, 1f);

            bool wantsCrouch = UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.C);
            UpdateCrouch(wantsCrouch);

            bool moving = wish.sqrMagnitude > 0.01f;
            bool wantsSprint = UnityEngine.Input.GetKey(KeyCode.LeftShift) && moving && !crouching && carryLoad < 0.4f;
            bool sprinting = wantsSprint && stamina != null && stamina.Drain(sprintStaminaPerSecond);

            float targetSpeed = crouching ? crouchSpeed : (sprinting ? sprintSpeed : walkSpeed);
            if (carryLoad > 0.01f)
            {
                // Heavier loads slow you more; using material up makes you lighter and faster.
                targetSpeed *= Mathf.Lerp(1f, carrySpeedMultiplier, carryLoad);
                if (moving) stamina?.Drain(carryStaminaPerSecond * carryLoad);
            }

            // Steep ground both slows control and (past the limit) slides you down.
            // Rain-soaked roofs let go sooner; a foam pad underfoot (grip) cancels both.
            float slideAngle = slideStartAngle - 9f * slipperiness;
            float steep01 = HasGrip ? 0f
                : Mathf.InverseLerp(slideAngle * 0.5f, slideAngle, groundAngle);
            targetSpeed *= Mathf.Lerp(1f, 1f - steepSpeedPenalty, steep01);

            float control = controller.isGrounded ? 1f : airControl;
            Vector3 targetVel = wish * targetSpeed;
            horizontalVelocity = Vector3.MoveTowards(
                horizontalVelocity, targetVel, acceleration * control * dt);

            // Slide down faces steeper than the slide angle (unless a foam pad grips you).
            // Wet shingles also pull harder once you're going.
            if (controller.isGrounded && groundAngle > slideAngle && !HasGrip)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
                float steepness = Mathf.InverseLerp(slideAngle, 80f, groundAngle);
                horizontalVelocity += downhill * slideAcceleration
                    * (1f + 0.75f * slipperiness) * steepness * dt;
            }

            // Gravity & jump.
            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (UnityEngine.Input.GetButtonDown("Jump") || UnityEngine.Input.GetKeyDown(KeyCode.Space))
                {
                    if (!crouching && stamina != null && stamina.TrySpend(jumpStaminaCost))
                    {
                        verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
                    }
                }
            }
            else
            {
                verticalVelocity -= gravity * dt;
            }

            DecayExternal(dt);

            Vector3 motion = (horizontalVelocity + windVelocity + impulseVelocity
                              + Vector3.up * verticalVelocity) * dt;
            controller.Move(motion);

            // Harness catches a fall and reins you in to the rope length.
            if (harness != null && harness.IsEquipped)
            {
                if (harness.ConstrainBody(controller, ref verticalVelocity))
                {
                    horizontalVelocity *= 0.4f;
                }
            }

            UpdateHeadBob(dt, moving && controller.isGrounded ? CurrentSpeed : 0f, sprinting);
        }

        private void ApplyExternalOnly()
        {
            float dt = Time.deltaTime;
            verticalVelocity -= gravity * dt;
            DecayExternal(dt);
            controller.Move((windVelocity + impulseVelocity + Vector3.up * verticalVelocity) * dt);
        }

        private void UpdateCrouch(bool wantsCrouch)
        {
            // Don't stand up into a ceiling.
            if (crouching && !wantsCrouch && Physics.Raycast(
                    transform.position + Vector3.up * crouchHeight, Vector3.up,
                    standHeight - crouchHeight + 0.1f))
            {
                wantsCrouch = true;
            }

            crouching = wantsCrouch;
            float target = crouching ? crouchHeight : standHeight;
            controller.height = Mathf.MoveTowards(controller.height, target, 8f * Time.deltaTime);
            controller.center = new Vector3(0f, controller.height * 0.5f, 0f);
        }

        private void UpdateHeadBob(float dt, float speed, bool sprinting)
        {
            if (cameraPivot == null) return;

            if (speed > 0.1f)
            {
                bobPhase += dt * bobFrequency * (sprinting ? 1.4f : 1f);
                float bob = Mathf.Sin(bobPhase) * bobAmplitude * (sprinting ? 1.5f : 1f);
                float sway = Mathf.Cos(bobPhase * 0.5f) * bobAmplitude * 0.6f;
                cameraPivot.localPosition = cameraPivotBase + new Vector3(sway, bob, 0f);
            }
            else
            {
                cameraPivot.localPosition = Vector3.Lerp(
                    cameraPivot.localPosition, cameraPivotBase, dt * 6f);
            }
        }

        private void DecayExternal(float dt)
        {
            impulseVelocity = Vector3.MoveTowards(impulseVelocity, Vector3.zero, 18f * dt);
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            groundNormal = hit.normal;
            groundAngle = Vector3.Angle(hit.normal, Vector3.up);

            // Light shove so the player can knock loose objects around (physical comedy).
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body != null && !body.isKinematic && hit.moveDirection.y > -0.3f)
            {
                body.AddForceAtPosition(hit.moveDirection * 2.5f, hit.point, ForceMode.Impulse);
            }
        }

        // ----- Public API used by ladder / ledge / weather / carry systems -----

        /// <summary>Continuous wind push (set by the weather system).</summary>
        public void SetWind(Vector3 velocity) => windVelocity = velocity;

        /// <summary>0 = dry, 1 = soaked. Wet roofs slide sooner and pull harder (Fase C).
        /// Foam pads (<see cref="GripAssist"/>) still cancel the slide.</summary>
        public void SetSlipperiness(float s01) => slipperiness = Mathf.Clamp01(s01) * (1f - slipResistance);

        // Fase E gear (anti-slip boots): scales down whatever slipperiness the weather sends.
        private float slipResistance;
        public void SetSlipResistance(float r01) => slipResistance = Mathf.Clamp(r01, 0f, 0.9f);

        /// <summary>One-off shove (gust, bump from another player).</summary>
        public void AddImpulse(Vector3 impulse) => impulseVelocity += impulse;

        /// <summary>0 = empty-handed (full speed), 1 = max load (slowest).</summary>
        public void SetCarryLoad(float load) => carryLoad = Mathf.Clamp01(load);

        /// <summary>Foam pads call this every frame the player is on them: no steep-slope
        /// speed penalty and no sliding while it holds (Etapa 7).</summary>
        public void GripAssist(float duration = 0.2f) => gripUntil = Time.time + duration;

        private bool HasGrip => Time.time < gripUntil;

        public void SetMode(ControlMode mode)
        {
            Mode = mode;
            if (mode == ControlMode.Climbing || mode == ControlMode.Hanging)
            {
                horizontalVelocity = Vector3.zero;
                verticalVelocity = 0f;
            }
        }

        /// <summary>Used by climb/hang systems to move the body directly.</summary>
        public void MoveDirect(Vector3 worldDelta) => controller.Move(worldDelta);

        /// <summary>Teleport (respawn at the supply area). Disables the controller around the move.</summary>
        public void Teleport(Vector3 position)
        {
            controller.enabled = false;
            transform.position = position;
            controller.enabled = true;
            horizontalVelocity = Vector3.zero;
            verticalVelocity = 0f;
        }
    }
}
