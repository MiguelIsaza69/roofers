using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Represents a deformable roofing material blob with physics properties.
    /// Materials deform, spread, and respond to gravity and player input.
    /// </summary>
    public class RoofingMaterial : MonoBehaviour
    {
        public float totalMass = 5f;
        public float elasticity = 0.3f;
        public float adhesion = 0.7f;
        public float density = 1200f;

        private Vector3 velocity;
        private Vector3 angularVelocity;
        private Mesh deformationMesh;
        private MeshCollider meshCollider;

        public float coverageArea { get; private set; }
        public float avgThickness { get; private set; }

        private void OnEnable()
        {
            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                deformationMesh = Instantiate(GetComponent<MeshFilter>().mesh);
                GetComponent<MeshFilter>().mesh = deformationMesh;
                meshCollider.convex = true;
            }
        }

        public void ApplyForce(Vector3 force)
        {
            velocity += force / totalMass;
        }

        public void ApplyMaterial(Vector3 applicationPoint, Vector3 applicationForce, float materialMass)
        {
            // Apply force at contact point
            ApplyForce(applicationForce);

            // Increase mass
            totalMass += materialMass;

            // Update coverage (to be calculated in RoofSurface)
        }

        public void UpdatePhysics(float deltaTime)
        {
            // Apply gravity
            velocity += Physics.gravity * deltaTime;

            // Update position
            transform.position += velocity * deltaTime;

            // Damping
            velocity *= 0.95f;
            angularVelocity *= 0.95f;

            // Update mesh collider if mesh changed
            if (meshCollider != null)
            {
                meshCollider.convex = true;
            }
        }

        public void DeformMesh(Vector3 deformPoint, float deformRadius, float deformStrength)
        {
            if (deformationMesh == null)
                return;

            Vector3[] vertices = deformationMesh.vertices;

            for (int i = 0; i < vertices.Length; i++)
            {
                float distance = Vector3.Distance(vertices[i], deformPoint);
                if (distance < deformRadius)
                {
                    float influence = 1f - (distance / deformRadius);
                    vertices[i] += Vector3.down * influence * deformStrength;
                }
            }

            deformationMesh.vertices = vertices;
            deformationMesh.RecalculateNormals();
            deformationMesh.RecalculateBounds();
        }

        public void MergeWith(RoofingMaterial other)
        {
            if (other == null)
                return;

            // Combine mass
            float combinedMass = totalMass + other.totalMass;

            // Average material properties
            elasticity = (elasticity * totalMass + other.elasticity * other.totalMass) / combinedMass;
            adhesion = (adhesion * totalMass + other.adhesion * other.totalMass) / combinedMass;

            totalMass = combinedMass;

            // Destroy the other material blob
            Destroy(other.gameObject);
        }
    }
}
