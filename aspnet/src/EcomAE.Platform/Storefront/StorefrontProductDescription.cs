using System.Data.Common;
using System.Globalization;

namespace EcomAE.Platform.Storefront;

/// <summary>
/// PHP <c>content/shop/catalogue/product_description.php</c>: the product's <c>shop_products_text.content</c>
/// language-string id, or 4147 when the row is missing or the content is PHP-empty (<c>""</c>, <c>"0"</c>, null).
/// The PHP file prints <c>translate_str_by_id</c>; callers render the id through the same translator.
/// Verified against PHP 8.3 by <c>Fixtures/StorefrontFragments/golden.json</c>.
/// </summary>
public static class StorefrontProductDescription
{
    public const int FallbackStringId = 4147;

    public static string TranslateKey(string? content)
        => PhpEmpty(content) ? "{" + FallbackStringId.ToString(CultureInfo.InvariantCulture) + "}" : "{" + content + "}";

    public static async Task<string> RenderAsync(DbConnection connection, object? productId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT `content` FROM `shop_products_text` WHERE `product_id` = @id LIMIT 1";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = productId ?? DBNull.Value;
        command.Parameters.Add(parameter);
        var raw = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return TranslateKey(raw is null or DBNull ? null : Convert.ToString(raw, CultureInfo.InvariantCulture));
    }

    private static bool PhpEmpty(string? value)
        => string.IsNullOrEmpty(value) || string.Equals(value, "0", StringComparison.Ordinal);
}
