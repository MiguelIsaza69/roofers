using UnityEngine;

namespace RoofingSimulator.Player
{
    /// <summary>
    /// Fall consequences and the PEAK-style self-save. While falling, if the player is
    /// descending past a grabbable edge and still has stamina, they automatically catch
    /// it and hang; from there they can mantle up (costs stamina) or drop. Landing from
    /// height costs stamina and, past a bigger threshold, briefly stuns (a stand-in for
    /// the ragdoll until a real ragdoll rig is added). There is no death — only injury.
    /// </summary>
    [RequireComponent(typeof(PlayerLocomotion))]
    [RequireComponent(typeof(PlayerStamina))]
    public class LedgeGrabAndFall : MonoBehaviour
    {
        [Header("Fall damage")]
        [Tooltip("Fall height (m) that starts hurting.")]
        [SerializeField] private float safeFallHeight = 3.2f;
        [SerializeField] private float injuryPerMeter = 6f;
        [Tooltip("Fall height (m) that also stuns on landing.")]
        [SerializeField] private float stunFallHeight = 6.5f;
        [SerializeField] private float stunSeconds = 1.4f;

        [Header("Ledge grab")]
        [SerializeField] private float chestHeight = 1.3f;
        [SerializeField] private float reachDistance = 0.7f;
        [SerializeField] private float grabStaminaCost = 10f;
        [SerializeField] private float hangDrainPerSecond = 6f;
        [SerializeField] private float mantleStaminaCost = 18f;
        [SerializeField] private LayerMask grabMask = ~0;

        private PlayerLocomotion locomotion;
        private PlayerStamina stamina;
        private CharacterController controller;

        private bool wasGrounded = true;
        private float highestY;
        private bool hanging;
        private Vector3 hangTarget;
        private Vector3 ledgeTop;
        private float stunUntil;

        private void Awake()
        {
            locomotion = GetComponent<PlayerLocomotion>();
            stamina = GetComponent<PlayerStamina>();
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (hanging)
            {
                TickHang();
                return;
            }

            if (Time.time < stunUntil)
            {
                return; // stunned: no control (locomotion is in Disabled mode)
            }
            if (locomotion.Mode == PlayerLocomotion.ControlMode.Disabled)
            {
                locomotion.SetMode(PlayerLocomotion.ControlMode.Normal);
            }

            TrackFallAndLanding();
            TryGrabWhileFalling();
        }

        private void TrackFallAndLanding()
        {
            bool grounded = locomotion.IsGrounded;

            if (!grounded)
            {
                highestY = Mathf.Max(highestY, transform.position.y);
            }
            else
            {
                if (!wasGrounded)
                {
                    float fall = highestY - transform.position.y;
                    OnLanded(fall);
                }
                highestY = transform.position.y;
            }
            wasGrounded = grounded;
        }

        // Fase E gear (reinforced harness): softens the injury a hard landing costs.
        private float fallProtection;
        public void SetFallProtection(float p01) => fallProtection = Mathf.Clamp(p01, 0f, 0.8f);

        // Fase E2 consumable (emergency rope): once armed, it fully absorbs the next hard
        // landing — injury AND stun — then it's spent. One arming per charge.
        private bool ropeArmed;
        public bool RopeArmed => ropeArmed;
        public void ArmEmergencyRope() => ropeArmed = true;

        private void OnLanded(float fallHeight)
        {
            if (fallHeight <= safeFallHeight) return;

            if (ropeArmed)
            {
                ropeArmed = false;
                Gameplay.HudNotice.Show("¡La cuerda de emergencia te agarró! (se gastó)", 3f);
                return;
            }

            float over = fallHeight - safeFallHeight;
            stamina.AddInjury(over * injuryPerMeter * (1f - fallProtection));

            if (fallHeight >= stunFallHeight)
            {
                stunUntil = Time.time + stunSeconds;
                locomotion.SetMode(PlayerLocomotion.ControlMode.Disabled);
            }
        }

        private void TryGrabWhileFalling()
        {
            // Only while genuinely falling and not already supported.
            if (locomotion.IsGrounded) return;
            if (locomotion.Velocity.y > -1f) return;
            if (stamina.Current < grabStaminaCost) return;

            if (FindLedge(out Vector3 hangPos, out Vector3 top) && stamina.TrySpend(grabStaminaCost))
            {
                BeginHang(hangPos, top);
            }
        }

        /// <summary>
        /// Looks for a wall directly ahead with an open, walkable top edge just above
        /// chest height — the thing you'd grab onto.
        /// </summary>
        private bool FindLedge(out Vector3 hangPos, out Vector3 top)
        {
            hangPos = default;
            top = default;

            Vector3 chest = transform.position + Vector3.up * chestHeight;
            Vector3 fwd = transform.forward;

            if (!Physics.Raycast(chest, fwd, out RaycastHit wall, reachDistance, grabMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Probe down from above-and-ahead of the wall to find the top surface.
            Vector3 probe = chest + fwd * (reachDistance * 0.8f) + Vector3.up * 0.9f;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit topHit, 1.3f, grabMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }
            if (Vector3.Angle(topHit.normal, Vector3.up) > 50f) return false; // not standable

            top = topHit.point;
            // Hang with the head just under the edge, body pulled to the wall.
            hangPos = new Vector3(transform.position.x, top.y - chestHeight - 0.2f, transform.position.z);
            hangPos += new Vector3(wall.normal.x, 0f, wall.normal.z) * -0.1f;
            return true;
        }

        private void BeginHang(Vector3 hangPos, Vector3 top)
        {
            hanging = true;
            hangTarget = hangPos;
            ledgeTop = top;
            locomotion.SetMode(PlayerLocomotion.ControlMode.Hanging);
            locomotion.Teleport(hangPos);
        }

        private void TickHang()
        {
            // Hold position at the ledge.
            Vector3 delta = hangTarget - transform.position;
            locomotion.MoveDirect(delta);

            // Hanging is tiring; running out drops you.
            if (!stamina.Drain(hangDrainPerSecond))
            {
                EndHang(false);
                return;
            }

            bool mantle = UnityEngine.Input.GetKeyDown(KeyCode.W)
                          || UnityEngine.Input.GetButtonDown("Jump")
                          || UnityEngine.Input.GetKeyDown(KeyCode.Space);
            bool drop = UnityEngine.Input.GetKeyDown(KeyCode.LeftControl)
                        || UnityEngine.Input.GetKeyDown(KeyCode.S);

            if (mantle && stamina.TrySpend(mantleStaminaCost))
            {
                Vector3 onTop = ledgeTop + transform.forward * 0.4f + Vector3.up * 0.1f;
                locomotion.Teleport(onTop);
                EndHang(true);
            }
            else if (drop)
            {
                EndHang(false);
            }
        }

        private void EndHang(bool _)
        {
            hanging = false;
            highestY = transform.position.y; // don't count the hang as a new fall
            locomotion.SetMode(PlayerLocomotion.ControlMode.Normal);
        }

        public bool IsHanging => hanging;
    }
}
