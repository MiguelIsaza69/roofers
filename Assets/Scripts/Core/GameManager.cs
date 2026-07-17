using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.Core
{
    public enum GameState
    {
        MainMenu,
        CareerOverview,
        JobBriefing,
        InJob,
        JobComplete
    }

    /// <summary>
    /// Top-level game orchestrator. Owns the high-level <see cref="GameState"/>,
    /// drives scene transitions, and carries the selected job / last completion
    /// across scene loads so UI controllers can pick them up. Persists across scenes
    /// alongside <see cref="CareerManager"/>.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public const string MainMenuScene = "MainMenu";
        public const string CareerScene = "Career";
        public const string JobScene = "RoofingJob";

        private static GameManager instance;
        public static GameManager Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<GameManager>();
                    if (instance == null)
                    {
                        var go = new GameObject("GameManager");
                        instance = go.AddComponent<GameManager>();
                    }
                }
                return instance;
            }
        }

        public GameState State { get; private set; } = GameState.MainMenu;

        /// <summary>Job index the player chose to play next (read by the job scene).</summary>
        public int SelectedJobIndex { get; private set; } = -1;

        /// <summary>Result of the most recently finished job (read by the completion UI).</summary>
        public JobCompletion LastCompletion { get; private set; }

        public event Action<GameState> OnStateChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Ensure the career manager exists alongside us.
            _ = CareerManager.Instance;
        }

        private void SetState(GameState newState)
        {
            State = newState;
            OnStateChanged?.Invoke(newState);
        }

        // ----- Career entry points -----

        public void StartNewCareer(string playerName)
        {
            Career career = CareerManager.Instance.CreateCareer(playerName);
            if (career == null)
            {
                return;
            }
            GoToCareerOverview();
        }

        public bool ContinueCareer(string playerName)
        {
            Career career = CareerManager.Instance.LoadCareer(playerName);
            if (career == null)
            {
                return false;
            }
            GoToCareerOverview();
            return true;
        }

        // ----- Navigation -----

        public void GoToMainMenu()
        {
            SetState(GameState.MainMenu);
            SceneManager.LoadScene(MainMenuScene);
        }

        public void GoToCareerOverview()
        {
            SetState(GameState.CareerOverview);
            SceneManager.LoadScene(CareerScene);
        }

        /// <summary>Select a job and show its briefing (still in the career scene).</summary>
        public bool OpenJobBriefing(int jobIndex)
        {
            if (!CareerManager.Instance.SelectJob(jobIndex))
            {
                return false;
            }
            SelectedJobIndex = jobIndex;
            SetState(GameState.JobBriefing);
            return true;
        }

        /// <summary>Begin the selected job: loads the gameplay scene.</summary>
        public void StartSelectedJob()
        {
            if (SelectedJobIndex < 0)
            {
                Debug.LogWarning("StartSelectedJob called with no job selected.");
                return;
            }
            SetState(GameState.InJob);
            SceneManager.LoadScene(JobScene);
        }

        /// <summary>
        /// Called by the gameplay scene once a RoofingJobInstance exists, to load the
        /// selected job's configuration into it.
        /// </summary>
        public bool InitializeActiveJob(RoofingJobInstance instance)
        {
            return CareerManager.Instance.InitializeJobInstance(instance, SelectedJobIndex);
        }

        /// <summary>
        /// Called by the gameplay scene when the job ends. Evaluates completion,
        /// records progress on success, and returns to the career overview where the
        /// completion screen is shown.
        /// </summary>
        public void FinishJob(RoofingJobInstance instance)
        {
            LastCompletion = CareerManager.Instance.TryCompleteJob(instance);
            SetState(GameState.JobComplete);
            SceneManager.LoadScene(CareerScene);
        }

        /// <summary>Clears the stored completion once the completion screen is dismissed.</summary>
        public void AcknowledgeCompletion()
        {
            LastCompletion = null;
            SetState(GameState.CareerOverview);
        }

        /// <summary>
        /// Stash a completion produced by a co-op session (the host's authoritative
        /// outcome) so the completion screen can show it when the player returns to the
        /// career overview. Career recording/persistence is done by CareerManager.
        /// </summary>
        public void NotifyMultiplayerCompletion(JobCompletion completion)
        {
            LastCompletion = completion;
            SetState(GameState.JobComplete);
        }

        /// <summary>
        /// Same, for the solo cell-flow scene (Fase B reconnection): the scene records
        /// the completion itself and stashes it here so the completion screen can show
        /// it back at the career overview.
        /// </summary>
        public void NotifySoloCompletion(JobCompletion completion)
        {
            LastCompletion = completion;
            SetState(GameState.JobComplete);
        }
    }
}
