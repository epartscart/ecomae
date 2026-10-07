using EcomAE.Platform.Cp;

namespace EcomAE.Platform.Storefront;

public static partial class StorefrontPhpAjax
{
    public const string SitemapNoDb = "No DB connect";
    public const string SitemapForbidden = "Forbidden";
    public const string SitemapCatalogueMissing = "Catalogue tables are not in this database.";

    public static async Task<object> CreateSitemapAsync(
        System.Data.Common.DbConnection connection,
        string? adminSession,
        string? adminUser,
        IReadOnlyDictionary<string, string> fields,
        ICpSitemapEditorService sitemap,
        CancellationToken cancellationToken)
    {
        string? csrf = fields.ContainsKey("csrf_guard_key") ? fields["csrf_guard_key"] : null;
        var denied = await AdminThenCsrfAsync(
            connection,
            adminSession,
            adminUser,
            csrf,
            new FlagBody(false, SitemapForbidden),
            cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return denied;
        }

        var written = await sitemap.CreateAsync(OmsField(fields, "url_list"), cancellationToken).ConfigureAwait(false);
        if (!written.Succeeded)
        {
            var message = SitemapSchemaMissing(written.Message) ? SitemapCatalogueMissing : written.Message;
            return new FlagBody(false, message);
        }

        return "Ok";
    }

    private static bool SitemapSchemaMissing(string message)
        => message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Unknown column", StringComparison.OrdinalIgnoreCase);
}
