namespace EcomAE.Platform.Presentation;

/// <summary>
/// Seeded product-review read twin for PHP <c>product_evaluations.php</c>.
/// Writes stay on <see cref="PhpCustomerWrites.EvaluationWriteHref"/>.
/// </summary>
public static class PhpProductReviews
{
    public sealed record Review(int ProductId, int Rating, string Author, string Text, string Date);

    public static IReadOnlyList<Review> ForProduct(int productId)
    {
        if (productId <= 0)
        {
            return [];
        }

        return Seed.Where(r => r.ProductId == productId || r.ProductId == (productId % 7) + 1).Take(3).ToArray();
    }

    private static readonly Review[] Seed =
    [
        new(1, 5, "Workshop 14", "Fitted first time. Warehouse pickup the same afternoon.", "2026-02-11"),
        new(2, 4, "Garage Al Quoz", "Cross-ref matched the VIN request. Box was complete.", "2026-01-28"),
        new(3, 5, "Fleet desk", "Correct article after the brand picker. Will order again.", "2026-03-04"),
        new(4, 3, "Walk-in counter", "Lead time was a day longer than the row showed.", "2026-02-19"),
        new(5, 5, "Abu Dhabi pickup", "Packed well. Invoice matched the order.", "2026-03-01"),
        new(6, 4, "Service bay 3", "Used the seller request when the article was unknown — arrived next day.", "2026-01-15"),
        new(7, 5, "Own-catalog buyer", "Specs on the product card matched the part that arrived.", "2026-02-07"),
    ];
}
