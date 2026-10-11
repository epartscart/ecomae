namespace EcomAE.Platform.Storefront;

/// <summary>
/// Customer compare and bookmarks pages. Mutations stay on
/// <c>PhpCustomerWrites</c> (confirmWrites + CSRF).
/// </summary>
public static class StorefrontComparePages
{
    public const string ComparePath = "content/shop/catalogue/compare/compare.php";
    public const string BookmarksPath = "content/shop/catalogue/bookmarks/bookmarks.php";
    public const string CompareHref = "/storefront/compare-app";
    public const string BookmarksHref = "/storefront/wishlist-app";

    public const string allReview = nameof(allReview);
    public const string compareNumbers = nameof(compareNumbers);
    public const string compareStrings = nameof(compareStrings);
    public const string field_action = nameof(field_action);
    public const string onCategoryChange = nameof(onCategoryChange);
    public const string removeCurrentGroup = nameof(removeCurrentGroup);
}
