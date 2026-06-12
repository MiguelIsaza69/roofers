using System;
using NUnit.Framework;
using RoofingSimulator.Core;
using RoofingSimulator.Gameplay;
using RoofingSimulator.Utils;

namespace RoofingSimulator.Tests
{
    /// <summary>
    /// EditMode unit tests for pure game logic (no scene required): career progression,
    /// job config validation/conversion, completion criteria, and quality rating.
    ///
    /// NOTE: written but not yet executed (no Unity test runner was available at authoring
    /// time). Run via Window ▸ General ▸ Test Runner ▸ EditMode.
    /// </summary>
    public class DataModelTests
    {
        // ----- Career progression -----

        private static JobCompletion MakeCompletion(int jobId, float coverage = 90f)
        {
            var job = new RoofingJob { id = jobId, surfaceArea = 45f, difficulty = jobId + 1 };
            return new JobCompletion
            {
                jobId = jobId,
                finalCoveragePercent = coverage,
                timeToComplete = TimeSpan.FromMinutes(10),
                materialUsed = 500f,
                attemptCount = 1,
                qualityRating = QualityRating.GOOD,
                Job = job
            };
        }

        [Test]
        public void CompleteJob_UnlocksNextAndAdvances()
        {
            var career = new Career("Tester");
            Assert.AreEqual(0, career.unlockedJobIndex);

            career.CompleteJob(MakeCompletion(0));

            Assert.AreEqual(1, career.totalJobsCompleted);
            Assert.AreEqual(1, career.unlockedJobIndex, "completing job 0 should unlock job 1");
            Assert.AreEqual(1, career.currentJobIndex);
        }

        [Test]
        public void CompleteJob_IsIdBased_NotSequentialCount()
        {
            var career = new Career("Tester");
            career.CompleteJob(MakeCompletion(0));
            career.CompleteJob(MakeCompletion(1));

            Assert.AreEqual(2, career.totalJobsCompleted);
            Assert.AreEqual(2, career.unlockedJobIndex);
            Assert.AreEqual(2, career.currentJobIndex);
        }

        [Test]
        public void Career_TracksBestCompletionTime()
        {
            var career = new Career("Tester");
            career.CompleteJob(MakeCompletion(0));

            Assert.Less(career.performanceMetrics.bestCompletionTime, TimeSpan.FromHours(1));
        }

        // ----- Job configuration validation & conversion -----

        private static JobConfiguration ValidConfig() => new JobConfiguration
        {
            id = 0,
            name = "Test Job",
            difficulty = 1,
            roofGeometryId = "roof_00",
            minCoveragePercent = 85f,
            minQuality = "PRISTINE",
            surfaceArea = 45f,
            materialBudget = 600f,
            timeLimitSeconds = 600
        };

        [Test]
        public void JobConfiguration_Valid_PassesValidation()
        {
            Assert.IsTrue(ValidConfig().IsValid(out _));
        }

        [Test]
        public void JobConfiguration_CoverageOutOfRange_Fails()
        {
            var cfg = ValidConfig();
            cfg.minCoveragePercent = 50f; // below the 60% floor
            Assert.IsFalse(cfg.IsValid(out string error));
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void JobConfiguration_EmptyName_Fails()
        {
            var cfg = ValidConfig();
            cfg.name = "  ";
            Assert.IsFalse(cfg.IsValid(out _));
        }

        [Test]
        public void JobConfiguration_ShortTimeLimit_Fails()
        {
            var cfg = ValidConfig();
            cfg.timeLimitSeconds = 30; // below 60s minimum
            Assert.IsFalse(cfg.IsValid(out _));
        }

        [Test]
        public void ToRoofingJob_ParsesQualityString()
        {
            RoofingJob job = ValidConfig().ToRoofingJob();
            Assert.AreEqual(QualityThreshold.PRISTINE, job.minQuality);
            Assert.AreEqual(600f, job.materialBudget);
            Assert.AreEqual(600, job.timeLimitSeconds);
        }

        // ----- Completion criteria -----

        [Test]
        public void MeetsCompletionCriteria_RequiresCoverageAndThickness()
        {
            var job = new RoofingJob
            {
                minCoveragePercent = 85f,
                minQuality = QualityThreshold.STANDARD // 5mm
            };

            Assert.IsTrue(job.MeetsCompletionCriteria(90f, 6f), "coverage + thickness both met");
            Assert.IsFalse(job.MeetsCompletionCriteria(80f, 6f), "coverage below target");
            Assert.IsFalse(job.MeetsCompletionCriteria(90f, 3f), "thickness below quality threshold");
        }

        [Test]
        public void MeetsCompletionCriteria_PristineNeedsThickerMaterial()
        {
            var job = new RoofingJob
            {
                minCoveragePercent = 90f,
                minQuality = QualityThreshold.PRISTINE // 15mm
            };

            Assert.IsFalse(job.MeetsCompletionCriteria(95f, 10f), "10mm < pristine 15mm");
            Assert.IsTrue(job.MeetsCompletionCriteria(95f, 16f));
        }

        // ----- Quality rating -----

        [Test]
        public void DetermineQualityRating_MapsCoverageBands()
        {
            Assert.AreEqual(QualityRating.EXCELLENT, CoverageCalculator.DetermineQualityRating(96f, 20f));
            Assert.AreEqual(QualityRating.GOOD, CoverageCalculator.DetermineQualityRating(90f, 12f));
            Assert.AreEqual(QualityRating.ADEQUATE, CoverageCalculator.DetermineQualityRating(78f, 8f));
            Assert.AreEqual(QualityRating.POOR, CoverageCalculator.DetermineQualityRating(60f, 4f));
        }
    }
}
