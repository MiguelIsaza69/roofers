using UnityEngine;

namespace RoofingSimulator.Player
{
    /// <summary>
    /// Optional rope harness. The player chooses to clip in (look at the roof/ridge and
    /// press the attach key); the rope then constrains them to a sphere of
    /// <see cref="ropeLength"/> around the anchor and, crucially, catches a fall once it
    /// goes taut. A basic harness ships with everyone; the shop sells longer ropes and
    /// better clips that lower the chance of the catch slipping.
    ///
    /// Works with <see cref="CharacterController"/> (no Rigidbody/joint needed): the
    /// constraint is applied as a positional correction after locomotion has moved.
    /// </summary>
    public class SafetyHarness : MonoBehaviour
    {
        [Header("Rope")]
        [Tooltip("Maximum rope length (upgradable in the shop).")]
        [SerializeField] private float ropeLength = 9f;
        [Tooltip("Where the rope ties on the body, measured up from the feet.")]
        [SerializeField] private float tieHeight = 1.25f;
        [Tooltip("How far you can be from a surface to clip in.")]
        [SerializeField] private float maxAttachDistance = 7f;

        [Header("Reliability (shop upgrades lower this)")]
        [Range(0f, 1f)]
        [Tooltip("Chance the catch slips the moment it should save you. 0 = never fails.")]
        [SerializeField] private float slipChance = 0f;

        [Header("Input")]
        [SerializeField] private KeyCode attachKey = KeyCode.F;
        [SerializeField] private LayerMask anchorMask = ~0;

        [Header("References")]
        [SerializeField] private Camera aimCamera;

        private LineRenderer rope;
        private Vector3 anchor;
        private Transform anchorParent; // follows a moving anchor if one is set

        public bool IsEquipped { get; private set; }
        public float RopeLength => ropeLength;

        private void Awake()
        {
            if (aimCamera == null) aimCamera = GetComponentInChildren<Camera>();
            if (aimCamera == null) aimCamera = Camera.main;
            SetupRopeRenderer();
        }

        private void Update()
        {
            if (UnityEngine.Input.GetKeyDown(attachKey))
            {
                if (IsEquipped) Detach();
                else TryAttach();
            }

            UpdateRopeVisual();
        }

        private void TryAttach()
        {
            if (aimCamera == null) return;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, maxAttachDistance, anchorMask,
                    QueryTriggerInteraction.Ignore))
            {
                anchor = hit.point;
                // If we hit a dedicated anchor (e.g. the ridge), follow it if it moves.
                anchorParent = hit.collider.GetComponentInParent<HarnessAnchor>()?.transform;
                if (anchorParent != null) anchor = anchorParent.position;
                IsEquipped = true;
                rope.enabled = true;
            }
        }

        public void Detach()
        {
            IsEquipped = false;
            anchorParent = null;
            rope.enabled = false;
        }

        /// <summary>
        /// Called by <see cref="PlayerLocomotion"/> right after it moves the body. Pulls
        /// the player back inside the rope sphere and kills downward velocity when the
        /// rope goes taut (the catch). Returns true if the rope was taut this frame.
        /// </summary>
        public bool ConstrainBody(CharacterController controller, ref float verticalVelocity)
        {
            if (!IsEquipped) return false;
            if (anchorParent != null) anchor = anchorParent.position;

            Vector3 tie = transform.position + Vector3.up * tieHeight;
            Vector3 fromAnchor = tie - anchor;
            float dist = fromAnchor.magnitude;
            if (dist <= ropeLength) return false;

            // The catch can slip on a cheap harness.
            bool falling = verticalVelocity < -1f;
            if (falling && slipChance > 0f && Random.value < slipChance * Time.deltaTime * 4f)
            {
                return false;
            }

            Vector3 clampedTie = anchor + fromAnchor.normalized * ropeLength;
            controller.Move(clampedTie - tie);
            if (verticalVelocity < 0f) verticalVelocity = 0f;
            return true;
        }

        private void UpdateRopeVisual()
        {
            if (!IsEquipped || rope == null) return;
            if (anchorParent != null) anchor = anchorParent.position;

            Vector3 tie = transform.position + Vector3.up * tieHeight;
            // A little sag in the middle when there's slack.
            float slack = Mathf.Clamp01(1f - Vector3.Distance(tie, anchor) / Mathf.Max(0.01f, ropeLength));
            Vector3 mid = (tie + anchor) * 0.5f + Vector3.down * slack * 0.5f;
            rope.positionCount = 3;
            rope.SetPosition(0, tie);
            rope.SetPosition(1, mid);
            rope.SetPosition(2, anchor);
        }

        private void SetupRopeRenderer()
        {
            rope = gameObject.GetComponent<LineRenderer>();
            if (rope == null) rope = gameObject.AddComponent<LineRenderer>();
            rope.widthMultiplier = 0.03f;
            rope.material = new Material(Shader.Find("Sprites/Default"));
            rope.startColor = rope.endColor = new Color(0.85f, 0.7f, 0.3f);
            rope.numCapVertices = 2;
            rope.enabled = false;
        }

        // ----- Shop hooks -----
        public void SetRopeLength(float length) => ropeLength = Mathf.Max(1f, length);
        public void SetSlipChance(float chance) => slipChance = Mathf.Clamp01(chance);
    }

    /// <summary>Marker for a point a harness can clip onto (e.g. the roof ridge).</summary>
    public class HarnessAnchor : MonoBehaviour { }
}
