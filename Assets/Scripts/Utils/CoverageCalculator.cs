using System.Collections.Generic;
using UnityEngine;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.Utils
{
    /// <summary>
    /// Utility for calculating material coverage on roof surfaces.
    /// Uses raycast-based grid sampling for objective coverage measurement.
    /// </summary>
    public static class CoverageCalculator
    {
        private const float COVERAGE_THRESHOLD = 0.02f; // 2cm threshold

        /// <summary>
        /// Calculate coverage percentage for a roof surface with applied materials.
        /// </summary>
        public static float CalculateCoveragePercent(RoofSurface surface, float sampleSpacing = 0.1f)
        {
            if (surface == null)
                return 0f;

            List<Vector3> samplePoints = GenerateCoverageGrid(surface.GetComponent<MeshFilter>().mesh, sampleSpacing);
            int coveredCount = 0;

            foreach (var point in samplePoints)
            {
                if (IsSampleCovered(point))
                {
                    coveredCount++;
                }
            }

            if (samplePoints.Count == 0)
                return 0f;

            return (coveredCount / (float)samplePoints.Count) * 100f;
        }

        /// <summary>
        /// Calculate average thickness of material at coverage points.
        /// </summary>
        public static float CalculateAverageThickness(List<Vector3> samplePoints)
        {
            if (samplePoints == null || samplePoints.Count == 0)
                return 0f;

            float totalThickness = 0f;
            int coveredCount = 0;

            foreach (var point in samplePoints)
            {
                float thickness = GetMaterialThicknessAtPoint(point);
                if (thickness > 0)
                {
                    totalThickness += thickness;
                    coveredCount++;
                }
            }

            return coveredCount > 0 ? totalThickness / coveredCount : 0f;
        }

        /// <summary>
        /// Generate a grid of sample points across roof surface.
        /// </summary>
        public static List<Vector3> GenerateCoverageGrid(Mesh roofMesh, float spacing = 0.1f)
        {
            var points = new List<Vector3>();

            if (roofMesh == null)
                return points;

            Bounds bounds = roofMesh.bounds;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float height = bounds.max.y + 0.5f; // Sample slightly above surface

            for (float x = min.x; x <= max.x; x += spacing)
            {
                for (float z = min.z; z <= max.z; z += spacing)
                {
                    Vector3 samplePoint = new Vector3(x, height, z);
                    points.Add(samplePoint);
                }
            }

            return points;
        }

        /// <summary>
        /// Check if a single point on the roof is covered by material.
        /// </summary>
        private static bool IsSampleCovered(Vector3 samplePoint)
        {
            Ray ray = new Ray(samplePoint, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 10f);

            foreach (var hit in hits)
            {
                // Check if hit is roofing material
                if (hit.collider.CompareTag("RoofingMaterial") ||
                    hit.collider.GetComponent<RoofingMaterial>() != null)
                {
                    if (hit.distance < COVERAGE_THRESHOLD)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Get the thickness of material at a specific point (in millimeters).
        /// </summary>
        private static float GetMaterialThicknessAtPoint(Vector3 samplePoint)
        {
            Ray ray = new Ray(samplePoint, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 10f);

            float closestDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                if (hit.collider.CompareTag("RoofingMaterial") ||
                    hit.collider.GetComponent<RoofingMaterial>() != null)
                {
                    if (hit.distance < closestDistance)
                    {
                        closestDistance = hit.distance;
                    }
                }
            }

            if (closestDistance < COVERAGE_THRESHOLD)
            {
                return closestDistance * 1000f; // Convert to millimeters
            }

            return 0f;
        }

        /// <summary>
        /// Determine quality rating based on coverage and thickness.
        /// </summary>
        public static QualityRating DetermineQualityRating(float coveragePercent, float averageThickness)
        {
            // Excellent: >95% coverage and good thickness
            if (coveragePercent >= 95)
                return QualityRating.EXCELLENT;

            // Good: 85-95% coverage
            if (coveragePercent >= 85)
                return QualityRating.GOOD;

            // Adequate: 70-85% coverage
            if (coveragePercent >= 70)
                return QualityRating.ADEQUATE;

            // Poor: <70% coverage
            return QualityRating.POOR;
        }

        /// <summary>
        /// Check if coverage meets minimum requirements.
        /// </summary>
        public static bool MeetsCoverageRequirement(float currentCoverage, float minCoverage, float currentThickness, float minThickness)
        {
            return currentCoverage >= minCoverage && currentThickness >= minThickness;
        }
    }
}
