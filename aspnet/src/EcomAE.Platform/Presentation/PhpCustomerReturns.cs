namespace EcomAE.Platform.Presentation;

/// <summary>
/// Seeded returns-list twin for PHP <c>shop/returns/returns_list</c>.
/// New returns still POST to <see cref="PhpVendorPortal.ReturnsWriteHref"/>.
/// </summary>
public static class PhpCustomerReturns
{
    public sealed record Row(int Id, string OrderId, string Status, string Opened);

    public static IReadOnlyList<Row> SampleForAccount() =>
    [
        new(1001, "20", "Review", "2026-03-01"),
        new(1002, "21", "Approved", "2026-02-18"),
    ];
}
