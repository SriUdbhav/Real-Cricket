using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Dls;
using NUnit.Framework;

namespace Cricket.Simulation.Tests
{
    [TestFixture]
    public sealed class DlsPreparationTests
    {
        /// <summary>
        /// Test-only unverified provider to confirm defense-in-depth rejects unverified tables.
        /// </summary>
        private sealed class UnverifiedMockProvider : IDlsResourceTableProvider
        {
            public DlsResourceTableMetadata Metadata { get; } = new DlsResourceTableMetadata(
                "unverified_test",
                "0.1",
                isVerifiedLicensedTable: false,
                licenseInfo: ContentLicenseInfo.Original("unverified"));

            public bool IsLicensedTableAvailable => true;

            public bool TryGetRemainingResourcePercentage(int oversRemaining, int ballsRemainingInOver, int wicketsLost, out double resourcePercentage)
            {
                resourcePercentage = 50.0;
                return true;
            }

            public bool TryGetCompetitionG50(MatchFormat format, out double g50Score)
            {
                g50Score = 245.0;
                return true;
            }
        }

        [Test]
        public void DefaultDlsCalculator_IsDisabledUntilLicensedResourceTableIsSupplied()
        {
            var calculator = new DlsCalculator();
            CompetitionRulesProfile odi = CompetitionPresets.CreateStandardOdi();

            Assert.IsFalse(calculator.IsOperational(odi));

            var targetReq = new DlsTargetRequest(
                matchId: "m1",
                initialOversPerInnings: 50,
                team1TotalRuns: 280,
                team1WasAllOut: false,
                team1Interruptions: null,
                team2RevisedMaxOvers: 30,
                team2Interruptions: null);

            DlsCalculationResult targetRes = calculator.CalculateRevisedTarget(odi, targetReq);
            Assert.AreEqual(DlsCalculationStatus.UnsupportedNoLicensedResourceTable, targetRes.Status);
            Assert.IsFalse(targetRes.IsAvailable);
            Assert.IsNull(targetRes.RevisedTargetRuns);

            var parReq = new DlsParScoreRequest(
                matchId: "m1",
                initialOversPerInnings: 50,
                team1TotalRuns: 280,
                team1Interruptions: null,
                team2MaxOvers: 50,
                team2OversCompleted: 25,
                team2BallsInCurrentOver: 3,
                team2WicketsLost: 3,
                team2Interruptions: null);

            DlsCalculationResult parRes = calculator.CalculateParScore(odi, parReq);
            Assert.AreEqual(DlsCalculationStatus.UnsupportedNoLicensedResourceTable, parRes.Status);
            Assert.IsFalse(parRes.IsAvailable);
            Assert.IsNull(parRes.ParScore);
        }

        [Test]
        public void TestProfile_ReturnsDisabledByCompetitionProfile()
        {
            var calculator = new DlsCalculator();
            CompetitionRulesProfile testProfile = CompetitionPresets.CreateStandardTest();

            var req = new DlsTargetRequest("m_test", 90, 320, false, null, 70, null);
            DlsCalculationResult result = calculator.CalculateRevisedTarget(testProfile, req);

            Assert.AreEqual(DlsCalculationStatus.DisabledByCompetitionProfile, result.Status);
            Assert.IsFalse(result.IsAvailable);
        }

        [Test]
        public void Validation_RejectsInvalidRunsOversWicketsBallsAndInterruptionsBeforeResourceLookup()
        {
            var calculator = new DlsCalculator();
            CompetitionRulesProfile odi = CompetitionPresets.CreateStandardOdi();

            // Negative runs
            DlsCalculationResult negRuns = calculator.CalculateRevisedTarget(
                odi,
                new DlsTargetRequest("m1", 50, -5, false, null, 40, null));
            Assert.AreEqual(DlsCalculationStatus.InvalidInput, negRuns.Status);

            // Team 2 revised overs > initial overs
            DlsCalculationResult excessOvers = calculator.CalculateRevisedTarget(
                odi,
                new DlsTargetRequest("m1", 50, 250, false, null, 55, null));
            Assert.AreEqual(DlsCalculationStatus.InvalidInput, excessOvers.Status);

            // Interruption loses more overs than remaining
            var badInterruption = new RainInterruptionRecord(
                inningsNumber: 1,
                oversCompletedBeforeInterruption: 40,
                ballsInOverBeforeInterruption: 2,
                wicketsLostAtInterruption: 4,
                oversLost: 15,
                reason: "Heavy Rain");

            DlsCalculationResult badStop = calculator.CalculateRevisedTarget(
                odi,
                new DlsTargetRequest("m1", 50, 220, false, new[] { badInterruption }, 35, null));
            Assert.AreEqual(DlsCalculationStatus.InvalidInput, badStop.Status);

            // Par score with 10 wickets lost (invalid for active innings)
            DlsCalculationResult tenWickets = calculator.CalculateParScore(
                odi,
                new DlsParScoreRequest("m1", 50, 250, null, 50, 20, 0, 10, null));
            Assert.AreEqual(DlsCalculationStatus.InvalidInput, tenWickets.Status);

            // Par score with BallsInCurrentOver >= BallsPerOver
            DlsCalculationResult invalidBalls = calculator.CalculateParScore(
                odi,
                new DlsParScoreRequest("m1", 50, 250, null, 50, 20, 6, 2, null));
            Assert.AreEqual(DlsCalculationStatus.InvalidInput, invalidBalls.Status);
        }

        [Test]
        public void DefenseInDepth_RejectsUnverifiedResourceTableProvider()
        {
            var calculator = new DlsCalculator(new UnverifiedMockProvider());
            CompetitionRulesProfile t20 = CompetitionPresets.CreateStandardT20();

            Assert.IsFalse(calculator.IsOperational(t20));
            DlsCalculationResult res = calculator.CalculateRevisedTarget(
                t20,
                new DlsTargetRequest("m2", 20, 165, false, null, 12, null));

            Assert.AreEqual(DlsCalculationStatus.UnsupportedNoLicensedResourceTable, res.Status);
            Assert.IsNull(res.RevisedTargetRuns);
        }
    }
}
