using System;
using Newtonsoft.Json;
using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Serializable job definition matching contracts/job-configuration-schema.json.
    /// Deserialized from Assets/Resources/Jobs/*.json by <see cref="JobConfigurationLoader"/>
    /// and converted to a runtime <see cref="RoofingJob"/> via <see cref="ToRoofingJob"/>.
    /// </summary>
    [Serializable]
    public class JobConfiguration
    {
        [JsonProperty("id")] public int id;
        [JsonProperty("name")] public string name;
        [JsonProperty("description")] public string description;
        [JsonProperty("difficulty")] public int difficulty;
        [JsonProperty("roofGeometryId")] public string roofGeometryId;
        [JsonProperty("minCoveragePercent")] public float minCoveragePercent;
        [JsonProperty("minQuality")] public string minQuality;
        [JsonProperty("surfaceArea")] public float surfaceArea;
        [JsonProperty("materialBudget")] public float? materialBudget;
        [JsonProperty("timeLimitSeconds")] public int? timeLimitSeconds;
        [JsonProperty("difficultyScaling")] public DifficultyScalingConfig difficultyScaling;

        /// <summary>Convert this serialized config into a runtime job definition.</summary>
        public RoofingJob ToRoofingJob()
        {
            var job = new RoofingJob
            {
                id = id,
                name = name,
                description = description,
                difficulty = difficulty,
                roofGeometryId = roofGeometryId,
                minCoveragePercent = minCoveragePercent,
                minQuality = ParseQuality(minQuality, QualityThreshold.STANDARD),
                surfaceArea = surfaceArea,
                materialBudget = materialBudget,
                timeLimitSeconds = timeLimitSeconds,
                unlocksNextJob = true
            };

            if (difficultyScaling != null)
            {
                job.difficultyScaling = new DifficultyScaling
                {
                    sizeMultiplier = difficultyScaling.sizeMultiplier,
                    materialAvailability = difficultyScaling.materialAvailability,
                    requiredQuality = ParseQuality(difficultyScaling.requiredQuality, job.minQuality),
                    geometryComplexity = difficultyScaling.geometryComplexity
                };
            }

            return job;
        }

        /// <summary>Basic validation against the schema's hard constraints.</summary>
        public bool IsValid(out string error)
        {
            if (id < 0) { error = $"Job id {id} is negative."; return false; }
            if (string.IsNullOrWhiteSpace(name)) { error = $"Job {id} has no name."; return false; }
            if (minCoveragePercent < 60f || minCoveragePercent > 99f)
            {
                error = $"Job {id} coverage {minCoveragePercent} outside 60-99%."; return false;
            }
            if (surfaceArea <= 0f) { error = $"Job {id} surface area must be positive."; return false; }
            if (materialBudget.HasValue && materialBudget.Value < 0f)
            {
                error = $"Job {id} material budget is negative."; return false;
            }
            if (timeLimitSeconds.HasValue && timeLimitSeconds.Value < 60)
            {
                error = $"Job {id} time limit must be >= 60s."; return false;
            }

            error = null;
            return true;
        }

        private static QualityThreshold ParseQuality(string value, QualityThreshold fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }
            if (Enum.TryParse(value.Trim(), true, out QualityThreshold parsed))
            {
                return parsed;
            }
            Debug.LogWarning($"Unknown quality '{value}', defaulting to {fallback}.");
            return fallback;
        }
    }

    [Serializable]
    public class DifficultyScalingConfig
    {
        [JsonProperty("sizeMultiplier")] public float sizeMultiplier = 1f;
        [JsonProperty("materialAvailability")] public float materialAvailability = 1f;
        [JsonProperty("requiredQuality")] public string requiredQuality = "STANDARD";
        [JsonProperty("geometryComplexity")] public float geometryComplexity = 0f;
    }
}
