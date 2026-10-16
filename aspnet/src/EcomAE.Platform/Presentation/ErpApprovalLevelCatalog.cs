namespace EcomAE.Platform.Presentation;

/// <summary>
/// Standard approval bands used by business ERPs. Saving stores the document.
/// Posting is the act that hits the books. The company replaces these bands
/// with its own levels on the approvals page.
/// </summary>
public static class ErpApprovalLevelCatalog
{
    public sealed record Band(int Level, string Role, string SaveUpTo, string PostUpTo, string Meaning);

    public static IReadOnlyList<Band> Standard { get; } =
    [
        new(1, "Supervisor", "5,000", "1,000", "Day-to-day work. May save a draft and post only a small amount."),
        new(2, "Manager", "25,000", "10,000", "Approves the supervisor before a larger amount is posted."),
        new(3, "Controller", "100,000", "50,000", "Reviews the books. Required before a large post."),
        new(4, "Director", "Any amount", "Any amount", "Final post. The person who saved the document cannot be the only approver."),
    ];
}
