using UnityEngine;
using RoofingSimulator.World;

namespace RoofingSimulator.Player
{
    /// <summary>
    /// Simple ladder climbing. While standing in a ladder's trigger volume and pushing
    /// forward (W) toward it, the player latches on and moves up/down the rungs. Reaching
    /// the top and pushing up steps them onto the roof; jumping hops off.
    /// </summary>
    [RequireComponent(typeof(PlayerLocomotion))]
    public class LadderClimber : MonoBehaviour
    {
        [SerializeField] private float climbSpeed = 2.6f;

        private PlayerLocomotion locomotion;
        private Ladder current;
        private bool climbing;

        private void Awake()
        {
            locomotion = GetComponent<PlayerLocomotion>();
        }

        private void OnTriggerEnter(Collider other)
        {
            var ladder = other.GetComponentInParent<Ladder>();
            if (ladder != null) current = ladder;
        }

        private void OnTriggerExit(Collider other)
        {
            var ladder = other.GetComponentInParent<Ladder>();
            if (ladder != null && ladder == current && !climbing) current = null;
        }

        private void Update()
        {
            if (climbing) TickClimb();
            else TryStartClimb();
        }

        private void TryStartClimb()
        {
            if (current == null) return;

            float forward = UnityEngine.Input.GetAxisRaw("Vertical");
            bool facingLadder = Vector3.Dot(transform.forward, current.FaceDirection) > 0.25f;

            if (forward > 0.1f && facingLadder)
            {
                BeginClimb();
            }
        }

        private void BeginClimb()
        {
            climbing = true;
            locomotion.SetMode(PlayerLocomotion.ControlMode.Climbing);
            SnapToLadderLine();
        }

        private void TickClimb()
        {
            if (current == null)
            {
                EndClimb();
                return;
            }

            // Hop off with jump.
            if (UnityEngine.Input.GetButtonDown("Jump") || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                EndClimb();
                locomotion.AddImpulse(-current.FaceDirection * 3f + Vector3.up * 2f);
                return;
            }

            float input = UnityEngine.Input.GetAxisRaw("Vertical");
            float feetY = transform.position.y;

            // Step onto the roof at the top.
            if (feetY >= current.ClimbTop.y - 0.15f && input > 0.1f)
            {
                locomotion.SetMode(PlayerLocomotion.ControlMode.Normal);
                climbing = false;
                locomotion.Teleport(current.TopExit);
                current = null;
                return;
            }

            // Step off at the bottom.
            if (feetY <= current.ClimbBottom.y + 0.05f && input < -0.1f)
            {
                EndClimb();
                return;
            }

            float newY = Mathf.Clamp(feetY + input * climbSpeed * Time.deltaTime,
                current.ClimbBottom.y, current.ClimbTop.y);
            Vector3 target = new Vector3(current.ClimbBottom.x, newY, current.ClimbBottom.z);
            locomotion.MoveDirect(target - transform.position);
        }

        private void SnapToLadderLine()
        {
            Vector3 pos = transform.position;
            locomotion.MoveDirect(new Vector3(current.ClimbBottom.x - pos.x, 0f, current.ClimbBottom.z - pos.z));
        }

        private void EndClimb()
        {
            climbing = false;
            locomotion.SetMode(PlayerLocomotion.ControlMode.Normal);
        }

        public bool IsClimbing => climbing;
    }
}
