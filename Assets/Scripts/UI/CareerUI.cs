using UnityEngine;
using UnityEngine.UI;
using RoofingSimulator.Core;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.UI
{
    /// <summary>
    /// Career overview controller. Renders the job progression list with per-job
    /// lock/completion status, shows aggregate career metrics, and routes a selected
    /// job into the job briefing panel.
    /// </summary>
    public class CareerUI : MonoBehaviour
    {
        [Header("Job List")]
        [SerializeField] private Transform jobListContainer;
        [SerializeField] private Button jobButtonPrefab;

        [Header("Career Metrics")]
        [SerializeField] private Text playerNameLabel;
        [SerializeField] private Text jobsCompletedLabel;
        [SerializeField] private Text bestTimeLabel;

        [Header("Navigation")]
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private JobBriefingUI jobBriefingPanel;

        private void Awake()
        {
            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.AddListener(() => GameManager.Instance.GoToMainMenu());
            }
        }

        private void Start()
        {
            Refresh();
        }

        public void Refresh()
        {
            Career career = CareerManager.Instance.ActiveCareer;
            if (career == null)
            {
                Debug.LogWarning("CareerUI shown with no active career.");
                return;
            }

            UpdateMetrics(career);
            PopulateJobs();
        }

        private void UpdateMetrics(Career career)
        {
            if (playerNameLabel != null)
            {
                playerNameLabel.text = career.playerName;
            }
            if (jobsCompletedLabel != null)
            {
                jobsCompletedLabel.text = $"Jobs completed: {career.totalJobsCompleted} / {CareerManager.Instance.TotalJobs}";
            }
            if (bestTimeLabel != null && career.performanceMetrics != null)
            {
                var best = career.performanceMetrics.bestCompletionTime;
                bestTimeLabel.text = best.TotalHours < 24
                    ? $"Best time: {best.Minutes:00}:{best.Seconds:00}"
                    : "Best time: --:--";
            }
        }

        private void PopulateJobs()
        {
            if (jobListContainer == null || jobButtonPrefab == null)
            {
                return;
            }

            for (int i = jobListContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(jobListContainer.GetChild(i).gameObject);
            }

            int total = CareerManager.Instance.TotalJobs;
            for (int i = 0; i < total; i++)
            {
                RoofingJob job = CareerManager.Instance.GetJob(i);
                if (job == null)
                {
                    continue;
                }

                bool unlocked = CareerManager.Instance.IsJobUnlocked(i);
                bool completed = CareerManager.Instance.IsJobCompleted(i);

                Button entry = Instantiate(jobButtonPrefab, jobListContainer);
                entry.interactable = unlocked;

                var label = entry.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = FormatJobLabel(job, unlocked, completed);
                }

                int captured = i;
                entry.onClick.AddListener(() => OnJobSelected(captured));
            }
        }

        private static string FormatJobLabel(RoofingJob job, bool unlocked, bool completed)
        {
            string status = completed ? "✓ Completed"
                          : unlocked ? "Available"
                          : "🔒 Locked";
            return $"{job.id + 1}. {job.name}  —  {status}";
        }

        private void OnJobSelected(int index)
        {
            if (!GameManager.Instance.OpenJobBriefing(index))
            {
                return;
            }

            if (jobBriefingPanel != null)
            {
                jobBriefingPanel.Show(index);
            }
        }
    }
}
