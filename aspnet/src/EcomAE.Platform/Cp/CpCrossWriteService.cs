using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>crosses/ajax_operations.php</c> save/delete twins. Add + brand resolve + search-delete stay PHP.</summary>
public interface ICpCrossWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? article,
        string? manufacturerArticle,
        string? analog,
        string? manufacturerAnalog,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class CpCrossWriteService : ICpCrossWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCrossWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SaveAsync(
        long id,
        string? article,
        string? manufacturerArticle,
        string? analog,
        string? manufacturerAnalog,
        CancellationToken cancellationToken = default)
    {
        var art = CleanArticle(article);
        var analogArt = CleanArticle(analog);
        var mfr = CleanBrand(manufacturerArticle);
        var analogMfr = CleanBrand(manufacturerAnalog);
        if (id <= 0 || art.Length == 0 || analogArt.Length == 0 || mfr.Length == 0 || analogMfr.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cross id and all four article/brand fields are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional(
                "UPDATE `shop_docpart_articles_analogs_list` SET `article` = ?, `manufacturer_article` = ?, `analog` = ?, `manufacturer_analog` = ? WHERE `id` = ?"),
            cancellationToken,
            art, mfr, analogArt, analogMfr, id);
        return ErpSimpleWriteResult.Ok("Cross saved.", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A cross id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_docpart_articles_analogs_list` WHERE `id` = ?"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Cross deleted.", id);
    }

    internal static string CleanArticle(string? value)
    {
        var raw = (value ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is ' ' or '-' or '_' or '`' or '/' or '\'' or '"' or '\\' or '.' or ',' or '#'
                or '\r' or '\n' or '\t' or '\0' || char.IsControl(ch))
            {
                continue;
            }

            builder.Append(ch);
        }

        var clean = builder.ToString();
        return clean.Length > 255 ? clean[..255] : clean;
    }

    internal static string CleanBrand(string? value)
    {
        var raw = (value ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is '#' or '`' or '\'' or '"' or '\r' or '\n' or '\t' or '\0' || char.IsControl(ch))
            {
                continue;
            }

            builder.Append(ch);
        }

        var clean = builder.ToString().Trim();
        return clean.Length > 255 ? clean[..255] : clean;
    }
}
