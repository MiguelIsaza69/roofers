using UnityEngine;
using UnityEngine.UI;
using RoofingSimulator.Gameplay;

namespace RoofingSimulator.UI
{
    /// <summary>
    /// In-job heads-up display. Shows live coverage (text + fill bar), material budget
    /// remaining, time remaining (if the job is timed), and whether the current
    /// coverage/quality meets the job's completion threshold.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        [Header("Coverage")]
        [SerializeField] private Text coverageLabel;
        [Tooltip("Image with Image Type = Filled; fillAmount is driven by coverage.")]
        [SerializeField] private Image coverageBarFill;
        [SerializeField] private Image coverageTargetMarker;

        [Header("Constraints")]
        [SerializeField] private Text materialLabel;
        [SerializeField] private Text timeLabel;

        [Header("Quality / Objective")]
        [SerializeField] private Text qualityLabel;
        [SerializeField] private Color metColor = new Color(0.3f, 0.8f, 0.3f);
        [SerializeField] private Color unmetColor = new Color(0.85f, 0.75f, 0.3f);

        private RoofingJobInstance job;

        // In multiplayer the authoritative material budget lives on the server, so the
        // local job instance's value is stale on clients. When set, this override is
        // displayed instead (see NetworkRoofingMaterial). Null = use the local value.
        private float? materialOverrideKg;

        /// <summary>Display a server-authoritative material-remaining value (multiplayer).</summary>
        public void SetMaterialRemaining(float kg)
        {
            materialOverrideKg = kg;
        }

        /// <summary>Connect the HUD to the active job instance.</summary>
        public void Bind(RoofingJobInstance instance)
        {
            job = instance;

            // Position the target marker along the coverage bar (0-1 of required %).
            if (coverageTargetMarker != null && job?.job != null)
            {
                var rt = coverageTargetMarker.rectTransform;
                var anchor = rt.anchorMin;
                anchor.x = Mathf.Clamp01(job.job.minCoveragePercent / 100f);
                rt.anchorMin = new Vector2(anchor.x, rt.anchorMin.y);
                rt.anchorMax = new Vector2(anchor.x, rt.anchorMax.y);
            }
        }

        private void Update()
        {
            if (job == null || job.job == null)
            {
                return;
            }

            UpdateCoverage();
            UpdateMaterial();
            UpdateTime();
            UpdateQuality();
        }

        private void UpdateCoverage()
        {
            float coverage = job.CurrentCoveragePercent;

            if (coverageLabel != null)
            {
                coverageLabel.text = $"Coverage: {coverage:0.0}% / {job.job.minCoveragePercent:0}%";
            }
            if (coverageBarFill != null)
            {
                coverageBarFill.fillAmount = Mathf.Clamp01(coverage / 100f);
            }
        }

        private void UpdateMaterial()
        {
            if (materialLabel == null)
            {
                return;
            }

            if (job.HasUnlimitedMaterial)
            {
                materialLabel.text = "Material: ∞";
                return;
            }

            float remaining = materialOverrideKg ?? job.MaterialRemaining;
            materialLabel.text = $"Material: {Mathf.Max(0f, remaining):0} kg";
        }

        private void UpdateTime()
        {
            if (timeLabel == null)
            {
                return;
            }

            float? remaining = job.TimeRemainingSeconds;
            if (remaining.HasValue)
            {
                int total = Mathf.CeilToInt(remaining.Value);
                timeLabel.text = $"Time: {total / 60}:{total % 60:00}";
            }
            else
            {
                timeLabel.text = "Time: --:--";
            }
        }

        private void UpdateQuality()
        {
            if (qualityLabel == null)
            {
                return;
            }

            bool meets = job.job.MeetsCompletionCriteria(
                job.CurrentCoveragePercent, job.CurrentAverageThickness);

            qualityLabel.text = meets
                ? "Objective met — finish any time!"
                : $"Avg thickness: {job.CurrentAverageThickness:0.0} mm";
            qualityLabel.color = meets ? metColor : unmetColor;
        }
    }
}
