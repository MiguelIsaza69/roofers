using System;
using System.Collections.Generic;
using UnityEngine;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.Core
{
    /// <summary>
    /// Represents a player's career progression through roofing jobs.
    /// Tracks completed jobs, current progress, and performance metrics.
    /// </summary>
    [System.Serializable]
    public class Career
    {
        public string id;
        public string playerName;
        public DateTime createdDate;
        public DateTime lastModifiedDate;
        public int totalJobsCompleted;
        public int currentJobIndex;
        public int unlockedJobIndex;
        /// <summary>Money earned across jobs — the Fase E shop spends this.</summary>
        public int money;
        /// <summary>Gear bought in the shop (Fase E) — item ids; old saves load as empty.</summary>
        public List<string> ownedUpgrades = new List<string>();
        /// <summary>Consumable charges (Fase E2) — one entry per charge, spent in-level.</summary>
        public List<string> consumableCharges = new List<string>();
        public TimeSpan totalPlayTime;
        public PerformanceMetrics performanceMetrics;
        public List<JobCompletion> jobCompletions = new List<JobCompletion>();

        public Career(string playerName)
        {
            this.id = System.Guid.NewGuid().ToString();
            this.playerName = playerName;
            this.createdDate = DateTime.UtcNow;
            this.lastModifiedDate = DateTime.UtcNow;
            this.totalJobsCompleted = 0;
            this.currentJobIndex = 0;
            this.unlockedJobIndex = 0;
            this.totalPlayTime = TimeSpan.Zero;
            this.performanceMetrics = new PerformanceMetrics();
        }

        public void UpdateLastModified()
        {
            lastModifiedDate = DateTime.UtcNow;
        }

        public void AddPlayTime(TimeSpan duration)
        {
            totalPlayTime = totalPlayTime.Add(duration);
            UpdateLastModified();
        }

        public void CompleteJob(JobCompletion completion)
        {
            jobCompletions.Add(completion);
            totalJobsCompleted = jobCompletions.Count;
            money += Mathf.Max(0, completion.payment);

            // Unlock the job after the one that was completed, and advance to it.
            unlockedJobIndex = Mathf.Max(unlockedJobIndex, completion.jobId + 1);
            currentJobIndex = unlockedJobIndex;

            performanceMetrics.UpdateFromCompletion(completion);
            UpdateLastModified();
        }
    }

    [System.Serializable]
    public class PerformanceMetrics
    {
        public float totalCoverageArea;
        public TimeSpan averageCompletionTime;
        public float totalMaterialUsed;
        public TimeSpan bestCompletionTime = TimeSpan.FromHours(24);
        public int highestDifficultyCompleted;

        public void UpdateFromCompletion(JobCompletion completion)
        {
            totalMaterialUsed += completion.materialUsed;

            if (completion.timeToComplete < bestCompletionTime)
            {
                bestCompletionTime = completion.timeToComplete;
            }

            // Job is [NonSerialized] and may be absent (e.g. a completion built without
            // the catalog at hand) — the metrics that need it just skip that update.
            if (completion.Job != null)
            {
                totalCoverageArea += completion.Job.surfaceArea * (completion.finalCoveragePercent / 100f);
                highestDifficultyCompleted = Mathf.Max(highestDifficultyCompleted, completion.Job.difficulty);
            }
        }
    }

    [System.Serializable]
    public class JobCompletion
    {
        public int jobId;
        public DateTime completedDate;
        public TimeSpan timeToComplete;
        public float materialUsed;
        public float finalCoveragePercent;
        public int attemptCount;
        public QualityRating qualityRating;
        // Cell-flow results (Fase B reconnection): what the job actually paid and why.
        public int payment;        // dollars credited to the career
        public float qualityScore; // 0..1 average nail/placement quality
        public float cleanliness;  // 0..1 site cleanliness at handover

        [System.NonSerialized]
        public RoofingJob Job;
    }

    public enum QualityRating
    {
        EXCELLENT = 4,
        GOOD = 3,
        ADEQUATE = 2,
        POOR = 1
    }
}
