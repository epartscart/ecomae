namespace EcomAE.Platform.LifeOs.Purpose;

public interface ILifeOsDailyRoutineMatrix
{
    LifeOsDailyRoutineDigest Digest();

    IReadOnlyList<LifeOsDailyRoutineSegment> Segments { get; }

    IReadOnlyList<LifeOsDailyRoutineCoverageRow> Coverage { get; }
}

public sealed record LifeOsDailyRoutineSegment(
    string Key,
    string Window,
    string Mode,
    string HumanActivity,
    string ProactiveAssistance,
    string ClonedVoiceSample,
    string Domain,
    bool IsCorePurposeRow,
    IReadOnlyList<string> Engines,
    IReadOnlyList<string> Devices);

public sealed record LifeOsDailyRoutineCoverageRow(
    string SegmentKey,
    string Mode,
    bool Covered,
    string Status,
    string Evidence);

public sealed record LifeOsDailyRoutineDigest(
    string Product,
    string Title,
    string PurposeStatement,
    bool Complete24x7,
    int CoreRows,
    int ContinuityRows,
    int CoveredRows,
    string CoverageVerdict,
    IReadOnlyList<LifeOsDailyRoutineSegment> Segments,
    IReadOnlyList<LifeOsDailyRoutineCoverageRow> Coverage,
    IReadOnlyList<string> Notes);
