using UnityEngine;
using RoofingSimulator.Input;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// The player's roofing tool. Reads aim/apply input from <see cref="PlayerInput"/>
    /// and applies material to the roof: continuing an existing blob when one is under
    /// the cursor or nearby (adhesion/merge), or spawning a new blob otherwise. Shows a
    /// placement preview and respects the job's material budget.
    /// </summary>
    public class RoofingMaterialTool : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private RoofingJobInstance jobInstance;

        [Header("Application")]
        [Tooltip("Mass (kg) applied per application tick.")]
        [SerializeField] private float massPerTick = 2f;
        [Tooltip("Seconds between application ticks while the apply button is held.")]
        [SerializeField] private float applyInterval = 0.08f;
        [Tooltip("Distance (m) within which a new application merges into an existing blob.")]
        [SerializeField] private float mergeRadius = 0.3f;

        [Header("Material Properties")]
        [SerializeField] private float baseElasticity = 0.3f;
        [SerializeField] private float baseAdhesion = 0.7f;
        [SerializeField] private float baseDensity = 1200f;

        [Header("Preview")]
        [SerializeField] private bool showPreview = true;

        private float applyTimer;
        private GameObject preview;
        private MaterialProperties materialProps;

        private void Awake()
        {
            if (playerInput == null)
            {
                playerInput = GetComponentInParent<PlayerInput>();
            }

            materialProps = new MaterialProperties
            {
                baseElasticity = baseElasticity,
                baseAdhesion = baseAdhesion,
                baseDensity = baseDensity
            };
        }

        private void OnEnable()
        {
            if (playerInput != null)
            {
                playerInput.OnApplyHeld += HandleApply;
                playerInput.OnApplyStarted += HandleApply;
            }
        }

        private void OnDisable()
        {
            if (playerInput != null)
            {
                playerInput.OnApplyHeld -= HandleApply;
                playerInput.OnApplyStarted -= HandleApply;
            }
        }

        /// <summary>Connect the tool to the active job instance (called by the scene controller).</summary>
        public void Bind(RoofingJobInstance instance, PlayerInput input = null)
        {
            jobInstance = instance;
            if (input != null)
            {
                if (playerInput != null)
                {
                    playerInput.OnApplyHeld -= HandleApply;
                    playerInput.OnApplyStarted -= HandleApply;
                }
                playerInput = input;
                playerInput.OnApplyHeld += HandleApply;
                playerInput.OnApplyStarted += HandleApply;
            }
        }

        private void Update()
        {
            applyTimer += Time.deltaTime;
            UpdatePreview();
        }

        private void HandleApply(RoofingRaycastHit hit)
        {
            if (jobInstance == null || jobInstance.CurrentState != JobState.IN_PROGRESS)
            {
                return;
            }
            if (!jobInstance.HasMaterialAvailable)
            {
                return;
            }
            if (!hit.isRoofSurface && !hit.isExistingMaterial)
            {
                return;
            }

            // Throttle so holding the button applies at a steady rate.
            if (applyTimer < applyInterval)
            {
                return;
            }
            applyTimer = 0f;

            RoofingMaterial target = ResolveTarget(hit);

            if (target == null)
            {
                target = MaterialBlobFactory.Spawn(hit.point, massPerTick, materialProps);
                jobInstance.RegisterMaterial(target);
            }
            else
            {
                target.ApplyAt(hit.point, massPerTick);
            }

            jobInstance.ConsumeMaterial(massPerTick);
        }

        /// <summary>
        /// Finds the blob to add to: the one directly hit, otherwise the nearest
        /// already-applied blob within <see cref="mergeRadius"/>. Returns null to
        /// indicate a fresh blob should be spawned.
        /// </summary>
        private RoofingMaterial ResolveTarget(RoofingRaycastHit hit)
        {
            if (hit.isExistingMaterial && hit.collider != null)
            {
                var direct = hit.collider.GetComponent<RoofingMaterial>();
                if (direct != null)
                {
                    return direct;
                }
            }

            RoofingMaterial nearest = null;
            float nearestSqr = mergeRadius * mergeRadius;
            foreach (var blob in jobInstance.appliedMaterials)
            {
                if (blob == null)
                {
                    continue;
                }
                float sqr = (blob.transform.position - hit.point).sqrMagnitude;
                if (sqr <= nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = blob;
                }
            }
            return nearest;
        }

        private void UpdatePreview()
        {
            if (!showPreview || playerInput == null)
            {
                return;
            }

            bool shouldShow = playerInput.HasValidTarget && !playerInput.IsApplying
                              && jobInstance != null
                              && jobInstance.CurrentState == JobState.IN_PROGRESS
                              && jobInstance.HasMaterialAvailable
                              && (playerInput.CurrentHit.isRoofSurface || playerInput.CurrentHit.isExistingMaterial);

            if (shouldShow)
            {
                EnsurePreview();
                preview.transform.position = playerInput.CurrentHit.point;
                if (!preview.activeSelf)
                {
                    preview.SetActive(true);
                }
            }
            else if (preview != null && preview.activeSelf)
            {
                preview.SetActive(false);
            }
        }

        private void EnsurePreview()
        {
            if (preview != null)
            {
                return;
            }

            preview = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            preview.name = "MaterialPreview";

            // Preview is non-interactive.
            var col = preview.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }
            preview.transform.localScale = new Vector3(0.25f, 0.08f, 0.25f);

            var renderer = preview.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
                var mat = new Material(shader);
                mat.color = new Color(0.55f, 0.35f, 0.2f, 0.4f);
                renderer.sharedMaterial = mat;
            }
        }
    }
}
