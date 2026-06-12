using System;
using UnityEngine;
using RoofingSimulator.Persistence;

namespace RoofingSimulator.Core
{
    /// <summary>
    /// Owns the active <see cref="Career"/> at runtime and coordinates loading,
    /// restoring, and saving it through <see cref="SaveManager"/>. Job progression
    /// and unlock logic (Phase 3) build on top of the career state restored here.
    /// </summary>
    public class CareerManager : MonoBehaviour
    {
        private static CareerManager instance;
        public static CareerManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<CareerManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("CareerManager");
                        instance = go.AddComponent<CareerManager>();
                    }
                }
                return instance;
            }
        }

        /// <summary>The career currently loaded into play, or null if none.</summary>
        public Career ActiveCareer { get; private set; }
        public bool HasActiveCareer => ActiveCareer != null;

        /// <summary>Raised after a career is loaded/created and restored into play.</summary>
        public event Action<Career> OnCareerLoaded;
        /// <summary>Raised after the active career is persisted.</summary>
        public event Action<Career> OnCareerSaved;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Create a brand-new career and make it active.</summary>
        public Career CreateCareer(string playerName)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                Debug.LogError("Cannot create a career with an empty player name.");
                return null;
            }

            ActiveCareer = new Career(playerName);
            SaveManager.Instance.SaveCareer(ActiveCareer);

            OnCareerLoaded?.Invoke(ActiveCareer);
            return ActiveCareer;
        }

        /// <summary>
        /// Load a career from disk and restore its progression state into play.
        /// Returns null (and logs) if the save is missing or invalid.
        /// </summary>
        public Career LoadCareer(string playerName)
        {
            Career loaded = SaveManager.Instance.LoadCareer(playerName);
            if (loaded == null)
            {
                Debug.LogWarning($"No career could be loaded for '{playerName}'.");
                return null;
            }

            ActiveCareer = RestoreCareerState(loaded);
            OnCareerLoaded?.Invoke(ActiveCareer);
            return ActiveCareer;
        }

        /// <summary>
        /// Validates and repairs invariants on a freshly deserialized career so the
        /// rest of the game can rely on consistent progression indices.
        /// </summary>
        private Career RestoreCareerState(Career career)
        {
            career.jobCompletions ??= new System.Collections.Generic.List<JobCompletion>();
            career.performanceMetrics ??= new PerformanceMetrics();

            // totalJobsCompleted must match the recorded completion history.
            career.totalJobsCompleted = career.jobCompletions.Count;

            // unlockedJobIndex must always cover the current job and the highest
            // completed job (+1 to unlock the next one).
            int highestCompleted = -1;
            foreach (var completion in career.jobCompletions)
            {
                if (completion.jobId > highestCompleted)
                {
                    highestCompleted = completion.jobId;
                }
            }

            career.unlockedJobIndex = Mathf.Max(career.unlockedJobIndex, highestCompleted + 1);
            career.currentJobIndex = Mathf.Clamp(career.currentJobIndex, 0, career.unlockedJobIndex);

            Debug.Log($"Restored career '{career.playerName}': " +
                      $"{career.totalJobsCompleted} jobs completed, " +
                      $"current job {career.currentJobIndex}, unlocked up to {career.unlockedJobIndex}.");

            return career;
        }

        /// <summary>Persist the active career to disk.</summary>
        public void SaveActiveCareer()
        {
            if (ActiveCareer == null)
            {
                Debug.LogWarning("SaveActiveCareer called with no active career.");
                return;
            }

            SaveManager.Instance.SaveCareer(ActiveCareer);
            OnCareerSaved?.Invoke(ActiveCareer);
        }

        /// <summary>Record a completed job on the active career and persist progress.</summary>
        public void RecordJobCompletion(JobCompletion completion)
        {
            if (ActiveCareer == null || completion == null)
            {
                Debug.LogError("Cannot record completion without an active career and completion data.");
                return;
            }

            ActiveCareer.CompleteJob(completion);
            SaveActiveCareer();
        }

        /// <summary>List player names with existing saves (for a load menu).</summary>
        public string[] GetSavedCareerNames()
        {
            return SaveManager.Instance.GetSavedCareerNames();
        }
    }
}
