using UnityEngine;
using UnityEngine.UI;
using RoofingSimulator.Core;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.UI
{
    /// <summary>
    /// Job completion summary. Appears on returning to the career scene after a job
    /// finishes successfully, showing time, coverage, quality, and material used.
    /// Reads <see cref="GameManager.LastCompletion"/>.
    /// </summary>
    public class CompletionScreenUI : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Labels")]
        [SerializeField] private Text headerLabel;
        [SerializeField] private Text timeLabel;
        [SerializeField] private Text coverageLabel;
        [SerializeField] private Text qualityLabel;
        [SerializeField] private Text materialLabel;

        [Header("Navigation")]
        [SerializeField] private Button continueButton;
        [SerializeField] private CareerUI careerUI;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(OnContinueClicked);
            }
            Hide();
        }

        private void Start()
        {
            JobCompletion completion = GameManager.Instance.LastCompletion;
            if (completion != null)
            {
                Show(completion);
            }
        }

        public void Show(JobCompletion completion)
        {
            Populate(completion);
            panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Populate(JobCompletion completion)
        {
            RoofingJob job = completion.Job ?? CareerManager.Instance.GetJob(completion.jobId);
            string jobName = job != null ? job.name : $"Job {completion.jobId + 1}";

            SetText(headerLabel, $"{jobName} — Complete!");
            SetText(timeLabel, $"Time: {completion.timeToComplete.Minutes:00}:{completion.timeToComplete.Seconds:00}");
            SetText(coverageLabel, $"Coverage: {completion.finalCoveragePercent:0.0}%");
            SetText(qualityLabel, $"Quality: {completion.qualityRating}");
            SetText(materialLabel, $"Material used: {completion.materialUsed:0} kg");
        }

        private void OnContinueClicked()
        {
            Hide();
            GameManager.Instance.AcknowledgeCompletion();

            // Refresh the career list so the newly unlocked job appears.
            if (careerUI != null)
            {
                careerUI.Refresh();
            }
        }

        private static void SetText(Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }
    }
}
