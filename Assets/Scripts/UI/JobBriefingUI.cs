using UnityEngine;
using UnityEngine.UI;
using RoofingSimulator.Core;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.UI
{
    /// <summary>
    /// Job briefing panel. Shows the selected job's objectives and constraints
    /// (coverage %, quality, material budget, time limit) before the player starts.
    /// Shown by <see cref="CareerUI"/> when a job is selected.
    /// </summary>
    public class JobBriefingUI : MonoBehaviour
    {
        [Header("Panel Root")]
        [Tooltip("The root object toggled on/off. Defaults to this GameObject.")]
        [SerializeField] private GameObject panelRoot;

        [Header("Labels")]
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text descriptionLabel;
        [SerializeField] private Text coverageLabel;
        [SerializeField] private Text qualityLabel;
        [SerializeField] private Text materialLabel;
        [SerializeField] private Text timeLabel;

        [Header("Buttons")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button backButton;

        private void Awake()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }
            if (startButton != null)
            {
                startButton.onClick.AddListener(OnStartClicked);
            }
            if (backButton != null)
            {
                backButton.onClick.AddListener(Hide);
            }
            Hide();
        }

        public void Show(int jobIndex)
        {
            RoofingJob job = CareerManager.Instance.GetJob(jobIndex);
            if (job == null)
            {
                Debug.LogError($"Cannot brief unknown job index {jobIndex}.");
                return;
            }

            Populate(job);
            panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Populate(RoofingJob job)
        {
            SetText(titleLabel, job.name);
            SetText(descriptionLabel, job.description);
            SetText(coverageLabel, $"Coverage required: {job.minCoveragePercent:0}%  ({job.surfaceArea:0} m²)");
            SetText(qualityLabel, $"Quality: {FormatQuality(job.minQuality)}");

            SetText(materialLabel, job.materialBudget.HasValue
                ? $"Material budget: {job.materialBudget.Value:0} kg"
                : "Material: unlimited");

            SetText(timeLabel, job.timeLimitSeconds.HasValue
                ? $"Time limit: {FormatTime(job.timeLimitSeconds.Value)}"
                : "Time: no limit");
        }

        private void OnStartClicked()
        {
            Hide();
            GameManager.Instance.StartSelectedJob();
        }

        private static string FormatQuality(QualityThreshold quality)
        {
            return quality switch
            {
                QualityThreshold.STANDARD => "Standard (5mm)",
                QualityThreshold.HIGH => "High (10mm)",
                QualityThreshold.PRISTINE => "Pristine (15mm)",
                _ => quality.ToString()
            };
        }

        private static string FormatTime(int seconds)
        {
            int minutes = seconds / 60;
            int remainder = seconds % 60;
            return $"{minutes}:{remainder:00}";
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
