using System.Collections.Generic;
using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Represents the roof surface where material is applied.
    /// Tracks coverage through raycast-based grid sampling and manages completion detection.
    /// </summary>
    public class RoofSurface : MonoBehaviour
    {
        public string id;
        public float totalArea;
        public Mesh roofMesh;

        private MeshCollider meshCollider;
        private List<CoverageSample> coverageSamples = new List<CoverageSample>();
        private float sampleSpacing = 0.1f; // 10cm spacing by default

        public float totalCovered { get; private set; }
        public float coveragePercent { get; private set; }
        public float averageThickness { get; private set; }

        private void OnEnable()
        {
            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }
        }

        public void InitializeCoverageSampling(float spacing = 0.1f)
        {
            sampleSpacing = spacing;
            coverageSamples.Clear();

            if (roofMesh == null)
            {
                roofMesh = GetComponent<MeshFilter>().mesh;
            }

            // Generate sample points across roof surface
            // This is a simplified implementation - a full implementation would
            // distribute points more intelligently across the mesh surface
            Bounds bounds = roofMesh.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            for (float x = min.x; x < max.x; x += spacing)
            {
                for (float z = min.z; z < max.z; z += spacing)
                {
                    Vector3 samplePos = new Vector3(x, bounds.max.y + 1f, z);
                    coverageSamples.Add(new CoverageSample { position = samplePos });
                }
            }
        }

        public void UpdateCoverage(List<RoofingMaterial> materials)
        {
            if (coverageSamples.Count == 0)
            {
                InitializeCoverageSampling();
            }

            float coveredCount = 0;
            float totalThickness = 0;
            const float COVERAGE_THRESHOLD = 0.02f; // 2cm threshold for coverage

            foreach (var sample in coverageSamples)
            {
                // Raycast downward from sample point
                Ray ray = new Ray(sample.position, Vector3.down);
                RaycastHit[] hits = Physics.RaycastAll(ray, 10f);

                float closestMaterialDistance = float.MaxValue;

                foreach (var hit in hits)
                {
                    if (hit.collider.CompareTag("RoofingMaterial") ||
                        hit.collider.GetComponent<RoofingMaterial>() != null)
                    {
                        closestMaterialDistance = Mathf.Min(closestMaterialDistance, hit.distance);
                    }
                }

                // Check if covered
                if (closestMaterialDistance < COVERAGE_THRESHOLD)
                {
                    sample.isCovered = true;
                    sample.currentThickness = closestMaterialDistance * 100f; // Convert to mm
                    coveredCount++;
                    totalThickness += sample.currentThickness;
                }
                else
                {
                    sample.isCovered = false;
                    sample.currentThickness = 0;
                }

                sample.lastUpdated = System.DateTime.UtcNow;
            }

            // Update statistics
            float sampleArea = sampleSpacing * sampleSpacing;
            totalCovered = coveredCount * sampleArea;
            coveragePercent = totalArea > 0 ? (totalCovered / totalArea) * 100f : 0f;
            averageThickness = coveredCount > 0 ? totalThickness / coveredCount : 0;
        }

        public bool MeetsCriteria(float minCoveragePercent, float minThickness)
        {
            return coveragePercent >= minCoveragePercent && averageThickness >= minThickness;
        }

        public void ResetCoverage()
        {
            totalCovered = 0;
            coveragePercent = 0;
            averageThickness = 0;

            foreach (var sample in coverageSamples)
            {
                sample.isCovered = false;
                sample.currentThickness = 0;
            }
        }
    }

    [System.Serializable]
    public class CoverageSample
    {
        public Vector3 position;
        public bool isCovered;
        public float currentThickness;
        public System.DateTime lastUpdated;
    }
}
