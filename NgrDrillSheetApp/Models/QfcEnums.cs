namespace NgrDrillSheetApp.Models;

/// <summary>
/// Quality Factor levels used across QFC processing.
/// Maps to VBA: QF_GOOD=0, QF_MARGINAL=1, QF_BAD=2, QF_INVALID=3
/// </summary>
public enum QualityFactor
{
    Good = 0,
    Marginal = 1,
    Bad = 2,
    Invalid = 3
}

/// <summary>
/// QFC correction flags indicating what action was taken.
/// Maps to VBA: CORR_NONE=0, CORR_CORR=1, CORR_ADJ=2, CORR_STOP=3, CORR_INVALID=4
/// </summary>
public enum CorrectionFlag
{
    None = 0,
    Corrected = 1,
    Adjusted = 2,
    Stop = 3,
    Invalid = 4
}

public static class QualityFactorExtensions
{
    public static string ToDisplayString(this QualityFactor qf) => qf switch
    {
        QualityFactor.Good => "Good",
        QualityFactor.Marginal => "Marginal",
        QualityFactor.Bad => "Bad",
        QualityFactor.Invalid => "Invalid",
        _ => "Unknown"
    };

    public static string ToCorrectionString(this CorrectionFlag cf) => cf switch
    {
        CorrectionFlag.None => "Normal",
        CorrectionFlag.Corrected => "CORRECTED",
        CorrectionFlag.Adjusted => "ADJUSTED",
        CorrectionFlag.Stop => "EOP ERR",
        CorrectionFlag.Invalid => "INVALID",
        _ => "Unknown"
    };
}
