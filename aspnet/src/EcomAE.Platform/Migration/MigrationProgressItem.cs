namespace EcomAE.Platform.Migration;

public sealed record MigrationProgressItem(
    string Area,
    int WeightPercent,
    int CompletePercent,
    string Status,
    string NextAction)
{
    public string NextMilestone => NextAction;
}
