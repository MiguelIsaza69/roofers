using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// A deformable roofing material blob. Holds material properties and coverage
    /// state, and delegates the actual mesh deformation/settling simulation to
    /// <see cref="MaterialPhysics"/>. Spreading and adhesion behaviour live here as
    /// gameplay logic; the per-vertex physics live in MaterialPhysics.
    /// </summary>
    [RequireComponent(typeof(MaterialPhysics))]
    public class RoofingMaterial : MonoBehaviour
    {
        [Header("Material Properties")]
        public float totalMass = 5f;
        public float elasticity = 0.3f;
        [Range(0f, 1f)] public float adhesion = 0.7f;
        public float density = 1200f;

        [Header("Application Tuning")]
        [Tooltip("Base radius (m) affected when material is pressed onto the roof.")]
        [SerializeField] private float applyRadius = 0.25f;
        [Tooltip("Base downward press strength applied per application.")]
        [SerializeField] private float applyStrength = 0.05f;
        [Tooltip("Centre-to-centre distance (m) under which blobs merge.")]
        [SerializeField] private float mergeDistance = 0.3f;

        /// <summary>Surface area (m^2) this blob currently contributes to coverage.</summary>
        public float CoverageArea { get; private set; }
        /// <summary>Average thickness (mm) of this blob on the roof.</summary>
        public float AverageThickness { get; private set; }

        private MaterialPhysics physics;

        private void Awake()
        {
            physics = GetComponent<MaterialPhysics>();
            gameObject.tag = "RoofingMaterial";
        }

        /// <summary>
        /// Apply more material at a contact point: adds mass and deforms the mesh so
        /// the blob presses onto and spreads across the roof surface.
        /// </summary>
        public void ApplyAt(Vector3 worldPoint, float addedMass, float pressureScale = 1f)
        {
            totalMass += addedMass;

            if (physics != null)
            {
                float radius = applyRadius * Mathf.Sqrt(pressureScale);
                physics.Deform(worldPoint, radius, applyStrength * pressureScale);
            }
        }

        /// <summary>
        /// Merge another nearby blob into this one, combining mass and averaging
        /// material properties. The absorbed blob is destroyed.
        /// </summary>
        public void MergeWith(RoofingMaterial other)
        {
            if (other == null || other == this)
            {
                return;
            }

            float combinedMass = totalMass + other.totalMass;
            if (combinedMass <= 0f)
            {
                return;
            }

            elasticity = (elasticity * totalMass + other.elasticity * other.totalMass) / combinedMass;
            adhesion = (adhesion * totalMass + other.adhesion * other.totalMass) / combinedMass;
            totalMass = combinedMass;

            Destroy(other.gameObject);
        }

        /// <summary>True if <paramref name="other"/> is close enough and sticky enough to merge.</summary>
        public bool CanMergeWith(RoofingMaterial other)
        {
            if (other == null || other == this)
            {
                return false;
            }

            float distance = Vector3.Distance(transform.position, other.transform.position);
            return distance <= mergeDistance && adhesion >= 0.5f;
        }

        /// <summary>Updates coverage metrics (called by the job instance after a coverage pass).</summary>
        public void SetCoverageMetrics(float coverageArea, float averageThickness)
        {
            CoverageArea = coverageArea;
            AverageThickness = averageThickness;
        }
    }
}
