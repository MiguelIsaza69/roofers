using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Drives the per-frame deformation simulation for a single roofing material blob.
    /// Owns the working mesh: applies player deformation, gravity-driven settling, and
    /// projection onto the roof surface so material never floats. Designed for
    /// arcade-style "putty" behaviour rather than physically accurate soft-body sim.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public class MaterialPhysics : MonoBehaviour
    {
        [Header("Settling")]
        [Tooltip("How strongly gravity pulls un-settled vertices downward (m/s^2 scale).")]
        [SerializeField] private float gravityInfluence = 0.5f;
        [Tooltip("Velocity damping per fixed step (0-1). Higher = settles faster.")]
        [SerializeField] private float damping = 0.85f;
        [Tooltip("Vertex speed below which a vertex is considered at rest.")]
        [SerializeField] private float restThreshold = 0.001f;

        [Header("Surface Projection")]
        [Tooltip("Layers treated as the roof surface for projection raycasts.")]
        [SerializeField] private LayerMask roofMask = ~0;
        [Tooltip("Maximum distance a vertex is pushed up to sit on the surface.")]
        [SerializeField] private float projectionRayLength = 2f;

        [Header("Spreading")]
        [Tooltip("Rate at which deformed material spreads outward and thins (0-1).")]
        [SerializeField, Range(0f, 1f)] private float spreadingRate = 0.2f;

        private Mesh workingMesh;
        private Vector3[] baseVertices;     // local-space rest positions
        private Vector3[] vertices;         // local-space current positions
        private Vector3[] vertexVelocities; // local-space velocities
        private bool[] settled;
        private bool isDirty;

        private void Awake()
        {
            InitializeWorkingMesh();
        }

        /// <summary>
        /// Clones the source mesh into an instance we can safely mutate and sets up
        /// the per-vertex simulation buffers.
        /// </summary>
        private void InitializeWorkingMesh()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return;
            }

            workingMesh = Instantiate(filter.sharedMesh);
            workingMesh.name = filter.sharedMesh.name + " (Deformable)";
            workingMesh.MarkDynamic();
            filter.mesh = workingMesh;

            vertices = workingMesh.vertices;
            baseVertices = (Vector3[])vertices.Clone();
            vertexVelocities = new Vector3[vertices.Length];
            settled = new bool[vertices.Length];
        }

        private void FixedUpdate()
        {
            if (workingMesh == null)
            {
                return;
            }

            SimulateSettling(Time.fixedDeltaTime);

            if (isDirty)
            {
                CommitMesh();
            }
        }

        /// <summary>
        /// Push vertices within <paramref name="worldRadius"/> of the contact point
        /// downward/inward to model the player pressing putty onto the roof, then let
        /// the displaced volume spread outward.
        /// </summary>
        public void Deform(Vector3 worldPoint, float worldRadius, float strength)
        {
            if (workingMesh == null || vertices == null)
            {
                return;
            }

            Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
            float localRadius = worldRadius / Mathf.Max(transform.lossyScale.x, 0.0001f);
            float sqrRadius = localRadius * localRadius;

            for (int i = 0; i < vertices.Length; i++)
            {
                float sqrDist = (vertices[i] - localPoint).sqrMagnitude;
                if (sqrDist > sqrRadius)
                {
                    continue;
                }

                float influence = 1f - Mathf.Sqrt(sqrDist) / localRadius;

                // Press inward toward the contact point and downward.
                Vector3 inward = (localPoint - vertices[i]) * influence * spreadingRate;
                Vector3 press = Vector3.down * influence * strength;

                vertices[i] += press + inward;
                vertexVelocities[i] += press;
                settled[i] = false;
            }

            isDirty = true;
        }

        /// <summary>
        /// Gravity-driven settling: un-settled vertices accelerate downward, damp,
        /// and project onto the roof surface so the blob conforms without floating.
        /// </summary>
        private void SimulateSettling(float deltaTime)
        {
            bool anyMoved = false;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (settled[i])
                {
                    continue;
                }

                // Gravity in local space.
                Vector3 gravityLocal = transform.InverseTransformDirection(Physics.gravity);
                vertexVelocities[i] += gravityLocal * gravityInfluence * deltaTime;
                vertexVelocities[i] *= damping;

                vertices[i] += vertexVelocities[i] * deltaTime;

                ProjectVertexToSurface(ref vertices[i], ref vertexVelocities[i], i);

                if (vertexVelocities[i].sqrMagnitude < restThreshold * restThreshold)
                {
                    vertexVelocities[i] = Vector3.zero;
                    settled[i] = true;
                }
                else
                {
                    anyMoved = true;
                }
            }

            if (anyMoved)
            {
                isDirty = true;
            }
        }

        /// <summary>
        /// Prevents a vertex from sinking below the roof: raycasts down in world space
        /// and clamps the vertex to the surface hit point.
        /// </summary>
        private void ProjectVertexToSurface(ref Vector3 localVertex, ref Vector3 localVelocity, int index)
        {
            Vector3 worldVertex = transform.TransformPoint(localVertex);
            Ray ray = new Ray(worldVertex + Vector3.up * projectionRayLength, Vector3.down);

            if (Physics.Raycast(ray, out RaycastHit hit, projectionRayLength * 2f, roofMask, QueryTriggerInteraction.Ignore))
            {
                // If the vertex has dropped to or below the surface, clamp it and stop.
                if (worldVertex.y <= hit.point.y)
                {
                    Vector3 clampedWorld = new Vector3(worldVertex.x, hit.point.y, worldVertex.z);
                    localVertex = transform.InverseTransformPoint(clampedWorld);
                    localVelocity = Vector3.zero;
                    settled[index] = true;
                }
            }
        }

        /// <summary>Writes the working vertex buffer back to the mesh and refreshes derived data.</summary>
        private void CommitMesh()
        {
            workingMesh.vertices = vertices;
            workingMesh.RecalculateNormals();
            workingMesh.RecalculateBounds();

            MeshCollider meshCollider = GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                // Force the convex collider to refresh against the new mesh.
                meshCollider.sharedMesh = null;
                meshCollider.sharedMesh = workingMesh;
            }

            isDirty = false;
        }

        /// <summary>Resets the blob to its original undeformed shape.</summary>
        public void ResetShape()
        {
            if (baseVertices == null)
            {
                return;
            }

            baseVertices.CopyTo(vertices, 0);
            System.Array.Clear(vertexVelocities, 0, vertexVelocities.Length);
            System.Array.Clear(settled, 0, settled.Length);
            isDirty = true;
        }
    }
}
