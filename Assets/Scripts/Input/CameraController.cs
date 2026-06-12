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

        private float pitch;
        private float yaw;
        private bool lookEnabled = true;

        private void Awake()
        {
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
            float mouseX = UnityEngine.Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y") * mouseSensitivity;

            yaw += mouseX;
            pitch += invertY ? mouseY : -mouseY;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Yaw rotates the body, pitch rotates only the camera.
            yawTarget.rotation = Quaternion.Euler(0f, yaw, 0f);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void PositionToolAnchor()
        {
            toolAnchor.position = transform.position
                + transform.right * toolAnchorOffset.x
                + transform.up * toolAnchorOffset.y
                + transform.forward * toolAnchorOffset.z;
            toolAnchor.rotation = transform.rotation;
        }

        /// <summary>Enable/disable mouse look (e.g. when a menu is open).</summary>
        public void SetLookEnabled(bool enabled)
        {
            lookEnabled = enabled;
            SetCursorLocked(enabled);
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
