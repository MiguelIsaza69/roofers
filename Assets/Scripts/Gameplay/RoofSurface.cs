using System.Collections.Generic;
using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Represents the roof surface where material is applied. Tracks coverage by
    /// sampling a grid of points and raycasting down to detect material.
    ///
    /// Performance: the grid is sampled **amortized across frames** (a bounded budget of
    /// samples per call) with **live aggregates**, so per-frame cost stays flat no matter
    /// how large the roof is. Raycasts use the single-closest-hit, non-allocating
    /// <see cref="Physics.Raycast"/> overload (the previous implementation called the
    /// allocating <c>RaycastAll</c> over the whole grid every frame).
    /// </summary>
    public class RoofSurface : MonoBehaviour
    {
        [Header("Sampling")]
        [Tooltip("Max sample points evaluated per UpdateCoverage call (amortization budget).")]
        [SerializeField] private int samplesPerUpdate = 2048;
        [Tooltip("Nominal thickness (mm) credited when the roof under a blob can't be found.")]
        [SerializeField] private float nominalThicknessMm = 8f;

        public string id;
        public float totalArea;
        public Mesh roofMesh;

        private MeshCollider meshCollider;
        private readonly List<CoverageSample> coverageSamples = new List<CoverageSample>();
        private float sampleSpacing = 0.1f;
        private float sampleOriginY;
        private float rayDistance = 12f;
        private int cursor;

        // Live aggregates maintained incrementally as samples are (re)evaluated.
        private int liveCoveredCount;
        private float liveThicknessSumMm;

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
            cursor = 0;
            liveCoveredCount = 0;
            liveThicknessSumMm = 0f;

            if (roofMesh == null)
            {
                var mf = GetComponent<MeshFilter>();
                if (mf != null) roofMesh = mf.sharedMesh;
            }
            if (roofMesh == null)
            {
                return;
            }

            // Sample positions are in WORLD space, just above the roof, pointing down.
            Bounds bounds = roofMesh.bounds;
            Vector3 worldMin = transform.TransformPoint(bounds.min);
            Vector3 worldMax = transform.TransformPoint(bounds.max);

            sampleOriginY = worldMax.y + 1f;
            rayDistance = (sampleOriginY - worldMin.y) + 2f;

            for (float x = worldMin.x; x < worldMax.x; x += spacing)
            {
                for (float z = worldMin.z; z < worldMax.z; z += spacing)
                {
                    coverageSamples.Add(new CoverageSample
                    {
                        position = new Vector3(x, sampleOriginY, z)
                    });
                }
            }
        }

        /// <summary>
        /// Re-evaluates up to <see cref="samplesPerUpdate"/> grid points this call and
        /// republishes the aggregate coverage. The <paramref name="materials"/> list is
        /// unused (detection is raycast-based) but kept for call-site compatibility.
        /// </summary>
        public void UpdateCoverage(List<RoofingMaterial> materials)
        {
            if (coverageSamples.Count == 0)
            {
                InitializeCoverageSampling(sampleSpacing);
                if (coverageSamples.Count == 0) return;
            }

            int budget = Mathf.Min(samplesPerUpdate, coverageSamples.Count);
            for (int i = 0; i < budget; i++)
            {
                EvaluateSample(coverageSamples[cursor]);
                cursor++;
                if (cursor >= coverageSamples.Count)
                {
                    cursor = 0;
                }
            }

            float sampleArea = sampleSpacing * sampleSpacing;
            totalCovered = liveCoveredCount * sampleArea;
            coveragePercent = totalArea > 0f ? (totalCovered / totalArea) * 100f : 0f;
            averageThickness = liveCoveredCount > 0 ? liveThicknessSumMm / liveCoveredCount : 0f;
        }

        /// <summary>
        /// Re-evaluate a single sample, adjusting the live aggregates by the delta of its
        /// old vs new contribution (so the published totals are always current without an
        /// O(n) recompute).
        /// </summary>
        private void EvaluateSample(CoverageSample sample)
        {
            // Remove the sample's previous contribution.
            if (sample.isCovered)
            {
                liveCoveredCount--;
                liveThicknessSumMm -= sample.currentThickness;
            }

            bool covered = false;
            float thicknessMm = 0f;

            // Closest hit straight down: material if present, otherwise the bare roof.
            if (Physics.Raycast(sample.position, Vector3.down, out RaycastHit top,
                    rayDistance, ~0, QueryTriggerInteraction.Ignore)
                && top.collider.CompareTag("RoofingMaterial"))
            {
                covered = true;

                // Find the roof beneath the material to estimate thickness. Starting just
                // below the material's top skips the (convex) material collider so the
                // second ray reaches the roof.
                Vector3 below = top.point + Vector3.down * 0.02f;
                if (Physics.Raycast(below, Vector3.down, out RaycastHit roofHit,
                        rayDistance, ~0, QueryTriggerInteraction.Ignore)
                    && !roofHit.collider.CompareTag("RoofingMaterial"))
                {
                    thicknessMm = Mathf.Max(0f, top.point.y - roofHit.point.y) * 1000f;
                }
                else
                {
                    thicknessMm = nominalThicknessMm;
                }
            }

            sample.isCovered = covered;
            sample.currentThickness = thicknessMm;

            // Add the new contribution.
            if (covered)
            {
                liveCoveredCount++;
                liveThicknessSumMm += thicknessMm;
            }
        }

        public bool MeetsCriteria(float minCoveragePercent, float minThickness)
        {
            return coveragePercent >= minCoveragePercent && averageThickness >= minThickness;
        }

        public void ResetCoverage()
        {
            totalCovered = 0f;
            coveragePercent = 0f;
            averageThickness = 0f;
            liveCoveredCount = 0;
            liveThicknessSumMm = 0f;
            cursor = 0;

            foreach (var sample in coverageSamples)
            {
                sample.isCovered = false;
                sample.currentThickness = 0f;
            }
        }
    }

    [System.Serializable]
    public class CoverageSample
    {
        public Vector3 position;
        public bool isCovered;
        public float currentThickness;
    }
}
