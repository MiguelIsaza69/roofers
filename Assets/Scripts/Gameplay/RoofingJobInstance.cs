using System;
using System.Collections.Generic;
using UnityEngine;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Represents an active roofing job instance - the runtime state of a job being played.
    /// Manages job state, timing, material tracking, and completion detection.
    /// </summary>
    public class RoofingJobInstance : MonoBehaviour
    {
        public RoofingJob job;
        public RoofSurface roofSurface;
        public List<RoofingMaterial> appliedMaterials = new List<RoofingMaterial>();

        private JobState currentState = JobState.BRIEFING;
        private DateTime startTime;
        private float elapsedSeconds = 0f;
        private float materialRemaining;
        private bool isCompleted = false;
        private bool hasFailed = false;

        public JobState CurrentState => currentState;
        public TimeSpan ElapsedTime => new TimeSpan(0, 0, (int)elapsedSeconds);
        public float MaterialRemaining => materialRemaining;
        public float CurrentCoveragePercent => roofSurface?.coveragePercent ?? 0;
        public float CurrentAverageThickness => roofSurface?.averageThickness ?? 0;

        /// <summary>True when this job has no material budget cap.</summary>
        public bool HasUnlimitedMaterial => !job?.materialBudget.HasValue ?? true;

        /// <summary>Seconds left if the job is timed, else null.</summary>
        public float? TimeRemainingSeconds =>
            job != null && job.timeLimitSeconds.HasValue
                ? Mathf.Max(0f, job.timeLimitSeconds.Value - elapsedSeconds)
                : (float?)null;

        /// <summary>Raised when the job's completion criteria are met.</summary>
        public event Action<RoofingJobInstance> OnJobCompleted;
        /// <summary>Raised when the job fails (time/material). Carries the reason.</summary>
        public event Action<RoofingJobInstance, JobFailureReason> OnJobFailed;

        private void OnEnable()
        {
            if (roofSurface == null)
            {
                roofSurface = GetComponentInChildren<RoofSurface>();
            }
        }

        public void InitializeJob(RoofingJob jobConfig)
        {
            job = jobConfig;
            startTime = DateTime.UtcNow;
            elapsedSeconds = 0f;
            materialRemaining = jobConfig.materialBudget ?? float.MaxValue;
            currentState = JobState.BRIEFING;
            isCompleted = false;
            hasFailed = false;
            appliedMaterials.Clear();

            if (roofSurface != null)
            {
                roofSurface.totalArea = jobConfig.surfaceArea;
                roofSurface.InitializeCoverageSampling();
                roofSurface.ResetCoverage();
            }
        }

        private void Update()
        {
            if (currentState != JobState.IN_PROGRESS && currentState != JobState.PAUSED)
                return;

            elapsedSeconds += Time.deltaTime;

            // Update coverage
            if (roofSurface != null)
            {
                roofSurface.UpdateCoverage(appliedMaterials);
            }

            // Check time limit
            if (job.timeLimitSeconds.HasValue && elapsedSeconds > job.timeLimitSeconds.Value)
            {
                FailJob(JobFailureReason.TIME_EXCEEDED);
                return;
            }

            // Check material budget
            if (materialRemaining < 0 && job.materialBudget.HasValue)
            {
                FailJob(JobFailureReason.OUT_OF_MATERIAL);
                return;
            }

            // Check for completion
            if (CheckCompletion())
            {
                CompleteJob();
            }
        }

        public void StartJob()
        {
            if (currentState == JobState.BRIEFING)
            {
                currentState = JobState.IN_PROGRESS;
                startTime = DateTime.UtcNow;
            }
        }

        public void PauseJob()
        {
            if (currentState == JobState.IN_PROGRESS)
            {
                currentState = JobState.PAUSED;
            }
        }

        public void ResumeJob()
        {
            if (currentState == JobState.PAUSED)
            {
                currentState = JobState.IN_PROGRESS;
            }
        }

        public void AbandonJob()
        {
            currentState = JobState.ABANDONED;
        }

        /// <summary>Register a newly spawned blob with this job and consume its mass.</summary>
        public void ApplyMaterial(RoofingMaterial material, float mass)
        {
            RegisterMaterial(material);
            ConsumeMaterial(mass);
        }

        /// <summary>Track a material blob as part of this job (no budget change).</summary>
        public void RegisterMaterial(RoofingMaterial material)
        {
            if (material == null || appliedMaterials.Contains(material))
            {
                return;
            }
            appliedMaterials.Add(material);
            material.gameObject.tag = "RoofingMaterial";
        }

        /// <summary>Deduct material from the budget (used when adding to an existing blob).</summary>
        public void ConsumeMaterial(float mass)
        {
            if (currentState != JobState.IN_PROGRESS)
            {
                return;
            }
            materialRemaining -= mass;
        }

        /// <summary>True if there is budget left to apply more material (or unlimited).</summary>
        public bool HasMaterialAvailable => HasUnlimitedMaterial || materialRemaining > 0f;

        private bool CheckCompletion()
        {
            if (roofSurface == null || job == null)
                return false;

            // Get quality threshold value in mm
            float minThickness = GetMinThicknessForQuality(job.minQuality);

            return roofSurface.MeetsCriteria(job.minCoveragePercent, minThickness);
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

        private void CompleteJob()
        {
            if (isCompleted)
                return;

            currentState = JobState.COMPLETED;
            isCompleted = true;

            // Determine quality rating
            QualityRating rating = DetermineQualityRating();

            Debug.Log($"Job {job.name} completed with rating: {rating}");
            OnJobCompleted?.Invoke(this);
        }

        private void FailJob(JobFailureReason reason)
        {
            if (hasFailed)
                return;

            currentState = JobState.FAILED;
            hasFailed = true;

            Debug.Log($"Job {job.name} failed: {reason}");
            OnJobFailed?.Invoke(this, reason);
        }

        private QualityRating DetermineQualityRating()
        {
            float coverage = CurrentCoveragePercent;

            if (coverage >= 95)
                return QualityRating.EXCELLENT;
            else if (coverage >= 85)
                return QualityRating.GOOD;
            else if (coverage >= 70)
                return QualityRating.ADEQUATE;
            else
                return QualityRating.POOR;
        }

        public JobCompletion GenerateCompletion()
        {
            var completion = new JobCompletion
            {
                jobId = job.id,
                completedDate = DateTime.UtcNow,
                timeToComplete = ElapsedTime,
                materialUsed = (job.materialBudget ?? 0) - materialRemaining,
                finalCoveragePercent = CurrentCoveragePercent,
                attemptCount = 1, // Track this separately in CareerManager
                qualityRating = DetermineQualityRating(),
                Job = job
            };

            return completion;
        }
    }

    public enum JobState
    {
        BRIEFING,
        IN_PROGRESS,
        PAUSED,
        COMPLETED,
        FAILED,
        ABANDONED
    }

    public enum JobFailureReason
    {
        TIME_EXCEEDED,
        OUT_OF_MATERIAL,
        PLAYER_ABANDONED
    }
}
