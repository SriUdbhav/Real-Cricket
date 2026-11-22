using System.Collections.Generic;
using Cricket.Simulation.Core.DataModels;
using Cricket.Simulation.Core.Math;

namespace Cricket.Simulation.Tests.TestFixtures
{
    internal static class MatchTestDataFactory
    {
        public static MatchConfig CreateConfig(CompetitionRulesProfile? rules = null, string matchId = "match_001")
        {
            TeamData homeTeam = CreateOriginalTeam("team_azure", "Azure Falcons", "AZF", "#0F4C81", "#E0F2FE");
            TeamData awayTeam = CreateOriginalTeam("team_crimson", "Crimson Hawks", "CRH", "#991B1B", "#FEE2E2");

            PlayingLineup homeLineup = CreateLineup(homeTeam);
            PlayingLineup awayLineup = CreateLineup(awayTeam);

            return new MatchConfig(
                matchId: matchId,
                rules: rules ?? CompetitionPresets.CreateStandardT20(),
                homeTeam: homeTeam,
                awayTeam: awayTeam,
                homeLineup: homeLineup,
                awayLineup: awayLineup,
                stadiumId: "stadium_meridian_oval",
                isOfflineSinglePlayer: true);
        }

        public static TeamData CreateOriginalTeam(
            string teamId,
            string name,
            string shortCode,
            string primaryHex,
            string secondaryHex)
        {
            var squad = new List<PlayerProfile>(11);
            for (int i = 1; i <= 11; i++)
            {
                PlayerRole role = i <= 5
                    ? PlayerRole.SpecialistBatter
                    : i == 6
                        ? PlayerRole.WicketKeeperBatter
                        : i <= 8
                            ? PlayerRole.BowlingAllRounder
                            : PlayerRole.FastBowler;

                BowlingStyle style = i <= 5
                    ? BowlingStyle.RightArmMedium
                    : i <= 8
                        ? BowlingStyle.RightArmOffBreak
                        : BowlingStyle.RightArmFast;

                squad.Add(new PlayerProfile(
                    playerId: $"{teamId}_p{i:00}",
                    fullName: $"{shortCode} Player {i:00}",
                    shortName: $"{shortCode}{i:00}",
                    regionOrClubCode: shortCode,
                    role: role,
                    battingHand: i % 3 == 0 ? Handedness.Left : Handedness.Right,
                    bowlingStyle: style,
                    batting: new BattingAttributes(78, 75, 76, 74, 72, 77),
                    bowling: new BowlingAttributes(136f, 80, 74, 68, 75, 70, 72),
                    fielding: new FieldingAttributes(78, 76, 80, 79, 77, i == 6 ? 88 : 40),
                    baseStamina: 85,
                    licenseInfo: ContentLicenseInfo.Original($"orig_player_{teamId}_{i:00}")));
            }

            return new TeamData(
                teamId,
                name,
                shortCode,
                primaryHex,
                secondaryHex,
                squad,
                ContentLicenseInfo.Original($"orig_team_{teamId}"));
        }

        public static PlayingLineup CreateLineup(TeamData team)
        {
            var ids = new List<string>(11);
            for (int i = 0; i < 11; i++)
            {
                ids.Add(team.Squad[i].PlayerId);
            }

            return new PlayingLineup(team.TeamId, ids, captainId: ids[0], wicketKeeperId: ids[5]);
        }

        public static DeliveryResolutionInput DotBall()
        {
            return new DeliveryResolutionInput(
                new DeliveryTelemetry(138f, new SimVector3(0.2f, 2.0f, 8.84f), new SimVector2(0.15f, -4.5f)),
                new ShotTelemetry(ShotIntent.Defensive, FootworkType.FrontFoot, ContactZone.SweetSpot, 24f, -2f),
                new FieldingAndRunningEvidence(completedRuns: 0));
        }

        public static DeliveryResolutionInput RunsOffBat(int runs, bool boundaryFour = false, bool boundarySix = false, int shortRuns = 0)
        {
            return new DeliveryResolutionInput(
                new DeliveryTelemetry(135f, new SimVector3(0.2f, 2.0f, 8.84f), new SimVector2(0.1f, -5.5f)),
                new ShotTelemetry(
                    boundarySix ? ShotIntent.LoftedStroke : ShotIntent.GroundStroke,
                    FootworkType.FrontFoot,
                    ContactZone.SweetSpot,
                    boundarySix ? 132f : 98f,
                    boundarySix ? 32f : 6f),
                new FieldingAndRunningEvidence(
                    completedRuns: runs,
                    shortRunsDetected: shortRuns,
                    reachedBoundaryOnBounce: boundaryFour,
                    clearedBoundaryOnFull: boundarySix));
        }
    }
}
