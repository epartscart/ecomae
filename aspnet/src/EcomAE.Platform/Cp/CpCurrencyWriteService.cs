using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>currencies_turning.php</c> single-rate UPDATE twin. Bulk available + live FX stay PHP.</summary>
public interface ICpCurrencyWriteService
{
    Task<ErpSimpleWriteResult> SetRateAsync(string? isoCode, decimal rate, CancellationToken cancellationToken = default);
}

public sealed class CpCurrencyWriteService : ICpCurrencyWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCurrencyWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetRateAsync(string? isoCode, decimal rate, CancellationToken cancellationToken = default)
    {
        var iso = NormalizeIso(isoCode);
        if (iso.Length != 3)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A 3-letter ISO currency code is required.");
        }

        if (rate <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A rate greater than zero is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var money = decimal.Round(rate, 6, MidpointRounding.AwayFromZero);
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_currencies` SET `rate` = ? WHERE `iso_code` = ?"),
            cancellationToken,
            money.ToString(CultureInfo.InvariantCulture), iso);
        return ErpSimpleWriteResult.Ok("Currency rate saved.", 0);
    }

    internal static string NormalizeIso(string? isoCode)
    {
        var raw = (isoCode ?? string.Empty).Trim().ToUpperInvariant();
        if (raw.Length != 3)
        {
            return string.Empty;
        }

        foreach (var ch in raw)
        {
            if (ch is < 'A' or > 'Z')
            {
                return string.Empty;
            }
        }

        return raw;
    }
}
