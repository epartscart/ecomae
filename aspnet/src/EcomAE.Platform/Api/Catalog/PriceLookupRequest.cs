namespace EcomAE.Platform.Api.Catalog;

public sealed record PriceLookupRequest(string Brand, string Article)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(Brand) && !string.IsNullOrWhiteSpace(Article);

    public string NormalizedBrand => Brand.Trim().ToUpperInvariant();

    public string NormalizedArticle => NormalizeArticle(Article);

    public static string NormalizeArticle(string article)
    {
        return new string(article.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }
}
