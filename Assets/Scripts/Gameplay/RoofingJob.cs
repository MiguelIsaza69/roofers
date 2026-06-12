using System;
using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Defines a single roofing job with geometry, requirements, and constraints.
    /// Jobs are data-driven and parameterized for progressive difficulty.
    /// </summary>
    [System.Serializable]
    public class RoofingJob
    {
        public int id;
        public string name;
        public string description;
        public int difficulty;
        public string roofGeometryId;

        // Completion criteria
        public float minCoveragePercent;
        public QualityThreshold minQuality;
        public float surfaceArea;

        // Constraints
        public float? materialBudget;
        public int? timeLimitSeconds;

        // Progression
        public bool unlocksNextJob;
        public DifficultyScaling difficultyScaling;

        public RoofingJob()
        {
            difficultyScaling = new DifficultyScaling();
        }

        public bool MeetsCompletionCriteria(float currentCoveragePercent, float averageThickness)
        {
            // Check coverage requirement
            if (currentCoveragePercent < minCoveragePercent)
                return false;

            // Check quality requirement
            float minThickness = GetMinThicknessForQuality(minQuality);
            if (averageThickness < minThickness)
                return false;

            return true;
        }

        private float GetMinThicknessForQuality(QualityThreshold quality)
        {
            return quality switch
            {
                QualityThreshold.STANDARD => 5f,
                QualityThreshold.HIGH => 10f,
                QualityThreshold.PRISTINE => 15f,
                _ => 5f
            };
        }
    }

    [System.Serializable]
    public class DifficultyScaling
    {
        public float sizeMultiplier = 1f;
        public float materialAvailability = 1f;
        public QualityThreshold requiredQuality = QualityThreshold.STANDARD;
        public float geometryComplexity = 0f;
    }

    public enum QualityThreshold
    {
        STANDARD = 1,
        HIGH = 2,
        PRISTINE = 3
    }
}
