using System;

namespace Cricket.Simulation.Core.DataModels
{
    /// <summary>
    /// Tracks asset and identity licensing provenance so shipped content remains original or properly licensed.
    /// </summary>
    [Serializable]
    public sealed class ContentLicenseInfo
    {
        public ContentLicenseType LicenseType { get; }
        public string LicenseReferenceId { get; }
        public string AttributionNotes { get; }

        public ContentLicenseInfo(
            ContentLicenseType licenseType,
            string licenseReferenceId,
            string attributionNotes = "")
        {
            LicenseType = licenseType;
            LicenseReferenceId = licenseReferenceId ?? string.Empty;
            AttributionNotes = attributionNotes ?? string.Empty;
        }

        public static ContentLicenseInfo Original(string assetCode)
        {
            return new ContentLicenseInfo(ContentLicenseType.OriginalStudioCreation, assetCode, "Original studio asset");
        }
    }

    [Serializable]
    public sealed class BattingAttributes
    {
        public int Timing { get; }
        public int Power { get; }
        public int Footwork { get; }
        public int AgainstPace { get; }
        public int AgainstSpin { get; }
        public int Temperament { get; }

        public BattingAttributes(int timing, int power, int footwork, int againstPace, int againstSpin, int temperament)
        {
            Timing = ClampRating(timing);
            Power = ClampRating(power);
            Footwork = ClampRating(footwork);
            AgainstPace = ClampRating(againstPace);
            AgainstSpin = ClampRating(againstSpin);
            Temperament = ClampRating(temperament);
        }

        private static int ClampRating(int value) => System.Math.Max(1, System.Math.Min(100, value));
    }

    [Serializable]
    public sealed class BowlingAttributes
    {
        public float MaxPaceKmh { get; }
        public int Control { get; }
        public int ConventionalSwing { get; }
        public int ReverseSwing { get; }
        public int SeamMovement { get; }
        public int SpinTurn { get; }
        public int Variations { get; }

        public BowlingAttributes(
            float maxPaceKmh,
            int control,
            int conventionalSwing,
            int reverseSwing,
            int seamMovement,
            int spinTurn,
            int variations)
        {
            MaxPaceKmh = System.Math.Max(60f, System.Math.Min(165f, maxPaceKmh));
            Control = ClampRating(control);
            ConventionalSwing = ClampRating(conventionalSwing);
            ReverseSwing = ClampRating(reverseSwing);
            SeamMovement = ClampRating(seamMovement);
            SpinTurn = ClampRating(spinTurn);
            Variations = ClampRating(variations);
        }

        private static int ClampRating(int value) => System.Math.Max(1, System.Math.Min(100, value));
    }

    [Serializable]
    public sealed class FieldingAttributes
    {
        public int SprintSpeed { get; }
        public int Agility { get; }
        public int Catching { get; }
        public int ArmStrength { get; }
        public int ThrowAccuracy { get; }
        public int WicketKeeping { get; }

        public FieldingAttributes(
            int sprintSpeed,
            int agility,
            int catching,
            int armStrength,
            int throwAccuracy,
            int wicketKeeping)
        {
            SprintSpeed = ClampRating(sprintSpeed);
            Agility = ClampRating(agility);
            Catching = ClampRating(catching);
            ArmStrength = ClampRating(armStrength);
            ThrowAccuracy = ClampRating(throwAccuracy);
            WicketKeeping = ClampRating(wicketKeeping);
        }

        private static int ClampRating(int value) => System.Math.Max(1, System.Math.Min(100, value));
    }

    [Serializable]
    public sealed class PlayerProfile
    {
        public string PlayerId { get; }
        public string FullName { get; }
        public string ShortName { get; }
        public string RegionOrClubCode { get; }
        public PlayerRole Role { get; }
        public Handedness BattingHand { get; }
        public BowlingStyle BowlingStyle { get; }
        public BattingAttributes Batting { get; }
        public BowlingAttributes Bowling { get; }
        public FieldingAttributes Fielding { get; }
        public int BaseStamina { get; }
        public ContentLicenseInfo LicenseInfo { get; }

        public PlayerProfile(
            string playerId,
            string fullName,
            string shortName,
            string regionOrClubCode,
            PlayerRole role,
            Handedness battingHand,
            BowlingStyle bowlingStyle,
            BattingAttributes batting,
            BowlingAttributes bowling,
            FieldingAttributes fielding,
            int baseStamina,
            ContentLicenseInfo licenseInfo)
        {
            if (string.IsNullOrWhiteSpace(playerId))
            {
                throw new ArgumentException("PlayerId cannot be empty.", nameof(playerId));
            }

            PlayerId = playerId;
            FullName = string.IsNullOrWhiteSpace(fullName) ? playerId : fullName;
            ShortName = string.IsNullOrWhiteSpace(shortName) ? FullName : shortName;
            RegionOrClubCode = regionOrClubCode ?? string.Empty;
            Role = role;
            BattingHand = battingHand;
            BowlingStyle = bowlingStyle;
            Batting = batting ?? throw new ArgumentNullException(nameof(batting));
            Bowling = bowling ?? throw new ArgumentNullException(nameof(bowling));
            Fielding = fielding ?? throw new ArgumentNullException(nameof(fielding));
            BaseStamina = System.Math.Max(1, System.Math.Min(100, baseStamina));
            LicenseInfo = licenseInfo ?? throw new ArgumentNullException(nameof(licenseInfo));
        }
    }
}
