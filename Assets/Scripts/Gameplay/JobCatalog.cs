using System.Collections.Generic;

namespace RoofingSimulator.Gameplay
{
    /// <summary>
    /// Ordered source of <see cref="RoofingJob"/> definitions that drives career
    /// progression. Phase 3 ships a built-in default progression; Phase 6 replaces
    /// <see cref="BuildDefaultProgression"/> with JSON-loaded configurations
    /// (job-configuration-schema.json) without changing consumers.
    /// </summary>
    public class JobCatalog
    {
        private readonly List<RoofingJob> jobs = new List<RoofingJob>();

        public int Count => jobs.Count;

        public JobCatalog()
        {
            BuildDefaultProgression();
        }

        /// <summary>Returns the job at <paramref name="index"/>, or null if out of range.</summary>
        public RoofingJob GetJob(int index)
        {
            if (index < 0 || index >= jobs.Count)
            {
                return null;
            }
            return jobs[index];
        }

        public IReadOnlyList<RoofingJob> AllJobs => jobs;

        /// <summary>Replace the catalog contents (used by the Phase 6 JSON loader).</summary>
        public void Load(IEnumerable<RoofingJob> loadedJobs)
        {
            jobs.Clear();
            jobs.AddRange(loadedJobs);
        }

        /// <summary>
        /// Builds a 12-job progression that ramps difficulty smoothly: early jobs are
        /// small with lenient coverage/quality, later jobs are large and complex with
        /// pristine quality, tight material budgets, and time limits.
        /// </summary>
        private void BuildDefaultProgression()
        {
            jobs.Clear();

            // (name, area m2, coverage%, quality, materialBudget, timeLimitSec, complexity)
            AddJob(0, "Smith Residence", "A small starter roof. Learn the basics of spreading putty.",
                1, 45f, 85f, QualityThreshold.STANDARD, 600f, null, 0.10f);
            AddJob(1, "Baker Bungalow", "A slightly larger home with a gentle slope.",
                2, 55f, 87f, QualityThreshold.STANDARD, 700f, null, 0.20f);
            AddJob(2, "Corner Store", "A flat commercial roof. Watch your coverage near the edges.",
                3, 80f, 88f, QualityThreshold.HIGH, 950f, 900, 0.30f);
            AddJob(3, "Maple Duplex", "Two pitched sections meeting at a valley.",
                4, 110f, 89f, QualityThreshold.HIGH, 1250f, 1080, 0.40f);
            AddJob(4, "Hillside Cabin", "Steep slopes mean gravity is not your friend.",
                5, 95f, 90f, QualityThreshold.HIGH, 1050f, 900, 0.55f);
            AddJob(5, "Warehouse West Wing", "A big flat span with a tight material budget.",
                7, 220f, 90f, QualityThreshold.HIGH, 2300f, 1500, 0.50f);
            AddJob(6, "Clocktower Annex", "Complex geometry around a raised structure.",
                8, 160f, 91f, QualityThreshold.HIGH, 1750f, 1200, 0.70f);
            AddJob(7, "Civic Center", "Multiple slopes and skylights to work around.",
                9, 280f, 92f, QualityThreshold.PRISTINE, 3000f, 1800, 0.70f);
            AddJob(8, "Cathedral Nave", "Tall, steep, pristine quality required throughout.",
                11, 240f, 93f, QualityThreshold.PRISTINE, 2550f, 1680, 0.85f);
            AddJob(9, "Stadium Concourse", "An enormous ring roof. Manage your budget carefully.",
                12, 420f, 93f, QualityThreshold.PRISTINE, 4400f, 2100, 0.80f);
            AddJob(10, "Glass Atrium", "Intricate framing with very little margin for waste.",
                14, 300f, 94f, QualityThreshold.PRISTINE, 3100f, 1800, 0.90f);
            AddJob(11, "Grand Terminal", "The ultimate job: huge, complex, pristine, on the clock.",
                15, 500f, 95f, QualityThreshold.PRISTINE, 5100f, 2400, 1.00f);
        }

        private void AddJob(int id, string name, string description, int difficulty,
            float area, float coverage, QualityThreshold quality,
            float? materialBudget, int? timeLimitSeconds, float complexity)
        {
            var job = new RoofingJob
            {
                id = id,
                name = name,
                description = description,
                difficulty = difficulty,
                roofGeometryId = $"roof_{id:D2}",
                surfaceArea = area,
                minCoveragePercent = coverage,
                minQuality = quality,
                materialBudget = materialBudget,
                timeLimitSeconds = timeLimitSeconds,
                unlocksNextJob = true,
                difficultyScaling = new DifficultyScaling
                {
                    sizeMultiplier = area / 45f,
                    materialAvailability = materialBudget.HasValue ? 0.9f : 1f,
                    requiredQuality = quality,
                    geometryComplexity = complexity
                }
            };
            jobs.Add(job);
        }
    }
}
