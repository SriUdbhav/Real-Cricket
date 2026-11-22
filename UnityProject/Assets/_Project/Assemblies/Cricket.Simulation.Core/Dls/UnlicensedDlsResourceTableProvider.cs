using Cricket.Simulation.Core.DataModels;

namespace Cricket.Simulation.Core.Dls
{
    /// <summary>
    /// Default safe resource provider shipped prior to obtaining a licensed official DLS resource table.
    /// Explicitly reports no licensed resource table available and refuses to fabricate numbers.
    /// </summary>
    public sealed class UnlicensedDlsResourceTableProvider : IDlsResourceTableProvider
    {
        public DlsResourceTableMetadata Metadata { get; } = new DlsResourceTableMetadata(
            tableId: "none_unlicensed",
            version: "0.0.0",
            isVerifiedLicensedTable: false,
            licenseInfo: new ContentLicenseInfo(
                ContentLicenseType.OriginalStudioCreation,
                "DLS_DISABLED_PENDING_LICENSE",
                "Official DLS resource table not bundled; requires properly licensed and verified table."));

        public bool IsLicensedTableAvailable => false;

        public bool TryGetRemainingResourcePercentage(
            int oversRemaining,
            int ballsRemainingInOver,
            int wicketsLost,
            out double resourcePercentage)
        {
            resourcePercentage = 0.0;
            return false;
        }

        public bool TryGetCompetitionG50(MatchFormat format, out double g50Score)
        {
            g50Score = 0.0;
            return false;
        }
    }
}
