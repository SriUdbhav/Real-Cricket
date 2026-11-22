using System;
using System.Collections.Generic;

namespace Cricket.Simulation.Core.DataModels
{
    [Serializable]
    public sealed class TeamData
    {
        public string TeamId { get; }
        public string Name { get; }
        public string ShortCode { get; }
        public string PrimaryColorHex { get; }
        public string SecondaryColorHex { get; }
        public IReadOnlyList<PlayerProfile> Squad { get; }
        public ContentLicenseInfo LicenseInfo { get; }

        public TeamData(
            string teamId,
            string name,
            string shortCode,
            string primaryColorHex,
            string secondaryColorHex,
            IReadOnlyList<PlayerProfile> squad,
            ContentLicenseInfo licenseInfo)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                throw new ArgumentException("TeamId cannot be empty.", nameof(teamId));
            }

            if (squad == null || squad.Count < 11)
            {
                throw new ArgumentException("A valid cricket team squad must contain at least 11 players.", nameof(squad));
            }

            TeamId = teamId;
            Name = string.IsNullOrWhiteSpace(name) ? teamId : name;
            ShortCode = string.IsNullOrWhiteSpace(shortCode) ? teamId.Substring(0, System.Math.Min(3, teamId.Length)).ToUpperInvariant() : shortCode;
            PrimaryColorHex = primaryColorHex ?? "#1E3A8A";
            SecondaryColorHex = secondaryColorHex ?? "#FFFFFF";
            Squad = squad;
            LicenseInfo = licenseInfo ?? throw new ArgumentNullException(nameof(licenseInfo));
        }
    }

    /// <summary>
    /// Validated Playing XI locked prior to the match toss.
    /// </summary>
    [Serializable]
    public sealed class PlayingLineup
    {
        public const int RequiredPlayerCount = 11;

        public string TeamId { get; }
        public IReadOnlyList<string> BattingOrderPlayerIds { get; }
        public string CaptainId { get; }
        public string WicketKeeperId { get; }

        public PlayingLineup(
            string teamId,
            IReadOnlyList<string> battingOrderPlayerIds,
            string captainId,
            string wicketKeeperId)
        {
            if (string.IsNullOrWhiteSpace(teamId))
            {
                throw new ArgumentException("TeamId cannot be empty.", nameof(teamId));
            }

            if (battingOrderPlayerIds == null || battingOrderPlayerIds.Count != RequiredPlayerCount)
            {
                throw new ArgumentException($"Playing XI must contain exactly {RequiredPlayerCount} players.", nameof(battingOrderPlayerIds));
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < battingOrderPlayerIds.Count; i++)
            {
                string id = battingOrderPlayerIds[i];
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                {
                    throw new ArgumentException("Playing XI contains empty or duplicate player IDs.", nameof(battingOrderPlayerIds));
                }
            }

            if (!seen.Contains(captainId))
            {
                throw new ArgumentException("Captain must be part of the Playing XI.", nameof(captainId));
            }

            if (!seen.Contains(wicketKeeperId))
            {
                throw new ArgumentException("Wicketkeeper must be part of the Playing XI.", nameof(wicketKeeperId));
            }

            TeamId = teamId;
            BattingOrderPlayerIds = new List<string>(battingOrderPlayerIds);
            CaptainId = captainId;
            WicketKeeperId = wicketKeeperId;
        }

        public bool ContainsPlayer(string playerId)
        {
            for (int i = 0; i < BattingOrderPlayerIds.Count; i++)
            {
                if (string.Equals(BattingOrderPlayerIds[i], playerId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
