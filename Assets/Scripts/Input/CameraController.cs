using UnityEngine;

namespace RoofingSimulator.Input
{
    /// <summary>
    /// First-person camera controller. Handles mouse look (pitch/yaw) and keeps the
    /// tool/hand anchor positioned relative to the camera so the player sees their
    /// roofing tool in view.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Look Settings")]
        [SerializeField] private float mouseSensitivity = 2f;
        [SerializeField] private float maxPitch = 85f;
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private bool invertY = false;

        [Header("References")]
        [Tooltip("Transform rotated horizontally (yaw). Usually the player body.")]
        [SerializeField] private Transform yawTarget;
        [Tooltip("Transform that holds the roofing tool, kept in front of the camera.")]
        [SerializeField] private Transform toolAnchor;
        [SerializeField] private Vector3 toolAnchorOffset = new Vector3(0.3f, -0.3f, 0.6f);

        [Header("Aim zoom (nail-gun precision, Etapa 8)")]
        [SerializeField] private float zoomFov = 32f;
        [Tooltip("Mouse sensitivity multiplier while fully zoomed, for fine aiming.")]
        [SerializeField] private float zoomSensitivityScale = 0.42f;
        [SerializeField] private float zoomLerpSpeed = 9f;

        private float pitch;
        private float yaw;
        private bool lookEnabled = true;
        private Camera cam;
        private float baseFov = 60f;
        private bool zoomHeld;
        private float zoomBlend; // 0 = normal view, 1 = fully zoomed

        /// <summary>How zoomed-in the view currently is (0..1) — HUD overlays read this.</summary>
        public float ZoomBlend => zoomBlend;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam != null) baseFov = cam.fieldOfView;
            // If no explicit yaw target is set, rotate this object's parent (the body).
            if (yawTarget == null)
            {
                yawTarget = transform.parent != null ? transform.parent : transform;
            }

            Vector3 startEuler = transform.localEulerAngles;
            pitch = NormalizeAngle(startEuler.x);
            yaw = yawTarget.eulerAngles.y;
        }

        private void Start()
        {
            SetCursorLocked(true);
        }

        private void Update()
        {
            // Ease the zoom in/out and narrow the FOV with it (a temporary close-up, not a scene).
            zoomBlend = Mathf.MoveTowards(zoomBlend, zoomHeld ? 1f : 0f, zoomLerpSpeed * Time.deltaTime);
            if (cam != null) cam.fieldOfView = Mathf.Lerp(baseFov, zoomFov, zoomBlend);

            if (lookEnabled)
            {
                ApplyLook();
            }

            if (toolAnchor != null)
            {
                PositionToolAnchor();
            }
        }

        private void ApplyLook()
        {
            // Zooming also slows the mouse so the nail can be placed precisely.
            float sensitivity = mouseSensitivity * Mathf.Lerp(1f, zoomSensitivityScale, zoomBlend);
            float mouseX = UnityEngine.Input.GetAxis("Mouse X") * sensitivity;
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * sensitivity;

            yaw += mouseX;
            pitch += invertY ? mouseY : -mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Yaw rotates the body, pitch rotates only the camera.
            yawTarget.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void PositionToolAnchor()
        {
            // While zoomed the tool tucks toward the centre, like raising the gun to sight it.
            Vector3 off = Vector3.Lerp(toolAnchorOffset, new Vector3(0.12f, -0.22f, 0.5f), zoomBlend);
            toolAnchor.position = transform.position
                + transform.right * off.x
                + transform.up * off.y
                + transform.forward * off.z;
            toolAnchor.rotation = transform.rotation;
        }

        /// <summary>Enable/disable mouse look (e.g. when a menu is open).</summary>
        public void SetLookEnabled(bool enabled)
        {
            lookEnabled = enabled;
            SetCursorLocked(enabled);
        }

        /// <summary>Hold/release the nail-gun precision zoom (Etapa 8) — eased, not a snap.</summary>
        public void SetAimZoom(bool zoomed) => zoomHeld = zoomed;

        /// <summary>
        /// Assign the yaw body and tool anchor when the player rig is built from code
        /// (see <see cref="RoofingSimulator.Player.PlayerRigBuilder"/>). Recomputes the
        /// look angles from the current transforms so there's no snap.
        /// </summary>
        public void Configure(Transform yawBody, Transform tool)
        {
            if (yawBody != null) yawTarget = yawBody;
            toolAnchor = tool;
            pitch = NormalizeAngle(transform.localEulerAngles.x);
            yaw = yawTarget != null ? yawTarget.eulerAngles.y : transform.eulerAngles.y;
        }

        private void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            if (angle > 180f) angle -= 360f;
            return angle;
        }
    }
}
