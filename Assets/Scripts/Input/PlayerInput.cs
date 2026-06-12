using System;
using UnityEngine;

namespace RoofingSimulator.Input
{
    /// <summary>
    /// Detects player input for roofing tool use and exposes the current raycast
    /// hit against roof surfaces. Other systems (material tool, networking) read
    /// <see cref="CurrentHit"/> and subscribe to the apply events.
    /// </summary>
    public class PlayerInput : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [Tooltip("Camera used as the origin of the application raycast. Defaults to Camera.main.")]
        [SerializeField] private Camera aimCamera;
        [SerializeField] private float maxApplyDistance = 8f;
        [SerializeField] private LayerMask applyMask = ~0;

        [Header("Input Settings")]
        [Tooltip("Mouse button index used to apply material (0 = left).")]
        [SerializeField] private int applyMouseButton = 0;
        [Tooltip("Gamepad/secondary button name from the Input Manager.")]
        [SerializeField] private string applyButtonName = "Fire1";

        /// <summary>Most recent raycast result against the roof. Valid only when <see cref="HasValidTarget"/> is true.</summary>
        public RoofingRaycastHit CurrentHit { get; private set; }
        public bool HasValidTarget { get; private set; }
        public bool IsApplying { get; private set; }

        /// <summary>Fired on the frame the player begins applying material at a valid target.</summary>
        public event Action<RoofingRaycastHit> OnApplyStarted;
        /// <summary>Fired every frame the player holds apply over a valid target.</summary>
        public event Action<RoofingRaycastHit> OnApplyHeld;
        /// <summary>Fired on the frame the player releases the apply input.</summary>
        public event Action OnApplyReleased;

        private bool wasApplyingLastFrame;

        private void Awake()
        {
            if (aimCamera == null)
            {
                aimCamera = Camera.main;
            }
        }

        private void Update()
        {
            UpdateRaycast();
            UpdateApplyInput();
        }

        private void UpdateRaycast()
        {
            HasValidTarget = false;

            if (aimCamera == null)
            {
                return;
            }

            // Ray from the centre of the screen (first-person crosshair).
            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(ray, out RaycastHit hit, maxApplyDistance, applyMask, QueryTriggerInteraction.Ignore))
            {
                CurrentHit = new RoofingRaycastHit
                {
                    point = hit.point,
                    normal = hit.normal,
                    distance = hit.distance,
                    collider = hit.collider,
                    isRoofSurface = hit.collider.CompareTag("RoofSurface"),
                    isExistingMaterial = hit.collider.CompareTag("RoofingMaterial")
                };
                HasValidTarget = true;
            }
        }

        private void UpdateApplyInput()
        {
            bool applyPressed =
                UnityEngine.Input.GetMouseButton(applyMouseButton) ||
                SafeGetButton(applyButtonName);

            IsApplying = applyPressed && HasValidTarget;

            if (IsApplying && !wasApplyingLastFrame)
            {
                OnApplyStarted?.Invoke(CurrentHit);
            }
            else if (IsApplying)
            {
                OnApplyHeld?.Invoke(CurrentHit);
            }
            else if (!applyPressed && wasApplyingLastFrame)
            {
                OnApplyReleased?.Invoke();
            }

            wasApplyingLastFrame = applyPressed;
        }

        /// <summary>
        /// Reads a named button without throwing if it is not defined in the
        /// Input Manager (common during early setup).
        /// </summary>
        private static bool SafeGetButton(string buttonName)
        {
            if (string.IsNullOrEmpty(buttonName))
            {
                return false;
            }

            try
            {
                return UnityEngine.Input.GetButton(buttonName);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Lightweight, serializable snapshot of a roofing raycast result.
    /// </summary>
    public struct RoofingRaycastHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public Collider collider;
        public bool isRoofSurface;
        public bool isExistingMaterial;
    }
}
