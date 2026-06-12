using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Loads job definitions from JSON files in Assets/Resources/Jobs/ (one job per
    /// file, matching job-configuration-schema.json). Invalid files are skipped with a
    /// warning; the result is ordered by job id so progression is deterministic.
    /// </summary>
    public static class JobConfigurationLoader
    {
        private const string ResourceFolder = "Jobs";

        /// <summary>
        /// Loads and validates all job configs. Returns runtime jobs ordered by id, or
        /// an empty list if none could be loaded (callers fall back to a built-in set).
        /// </summary>
        public static List<RoofingJob> LoadAll()
        {
            var jobs = new List<RoofingJob>();

            TextAsset[] assets = Resources.LoadAll<TextAsset>(ResourceFolder);
            if (assets == null || assets.Length == 0)
            {
                Debug.Log($"No job configs found in Resources/{ResourceFolder}; using built-in progression.");
                return jobs;
            }

            var seenIds = new HashSet<int>();

            foreach (TextAsset asset in assets)
            {
                JobConfiguration config = Deserialize(asset);
                if (config == null)
                {
                    continue;
                }

                if (!config.IsValid(out string error))
                {
                    Debug.LogWarning($"Skipping invalid job config '{asset.name}': {error}");
                    continue;
                }

                if (!seenIds.Add(config.id))
                {
                    Debug.LogWarning($"Duplicate job id {config.id} in '{asset.name}'; skipping.");
                    continue;
                }

                jobs.Add(config.ToRoofingJob());
            }

            // Deterministic progression order.
            jobs.Sort((a, b) => a.id.CompareTo(b.id));

            ValidateDifficultyCurve(jobs);
            return jobs;
        }

        private static JobConfiguration Deserialize(TextAsset asset)
        {
            try
            {
                return JsonConvert.DeserializeObject<JobConfiguration>(asset.text);
            }
            catch (JsonException e)
            {
                Debug.LogWarning($"Failed to parse job config '{asset.name}': {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Logs a warning if difficulty does not increase monotonically across the
        /// progression — a smooth ramp is a design goal (T070), not a hard rule.
        /// </summary>
        private static void ValidateDifficultyCurve(List<RoofingJob> jobs)
        {
            for (int i = 1; i < jobs.Count; i++)
            {
                if (jobs[i].difficulty < jobs[i - 1].difficulty)
                {
                    Debug.LogWarning(
                        $"Difficulty curve dips at job {jobs[i].id} " +
                        $"({jobs[i - 1].difficulty} -> {jobs[i].difficulty}); review progression.");
                }
            }
        }
    }
}
