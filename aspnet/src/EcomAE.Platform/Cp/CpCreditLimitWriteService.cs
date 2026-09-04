using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>epc_credit_set_limit</c>.</summary>
public interface ICpCreditLimitWriteService
{
    Task<ErpSimpleWriteResult> SetLimitAsync(string siteKey, int customerId, decimal limit, string currency, int approvedBy, CancellationToken cancellationToken = default);
}

public sealed class CpCreditLimitWriteService : ICpCreditLimitWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpCreditLimitWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> SetLimitAsync(
        string siteKey,
        int customerId,
        decimal limit,
        string currency,
        int approvedBy,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var key = (siteKey ?? string.Empty).Trim();
        if (key.Length == 0 || customerId <= 0 || limit < 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Site, customer, and a non-negative limit are required.");
        }

        var ccy = string.IsNullOrWhiteSpace(currency) ? "AED" : currency.Trim().ToUpperInvariant();
        var nextReview = DateTime.UtcNow.AddDays(90).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO `epc_credit_limits`
                (`site_key`, `customer_id`, `credit_limit`, `currency`, `payment_terms`, `approved_by`, `notes`, `last_review`, `next_review`)
            VALUES (@site, @customer, @limit, @ccy, 'net30', @approvedBy, '', CURDATE(), @nextReview)
            ON DUPLICATE KEY UPDATE
                `credit_limit` = VALUES(`credit_limit`),
                `currency` = VALUES(`currency`),
                `approved_by` = VALUES(`approved_by`),
                `last_review` = CURDATE(),
                `next_review` = VALUES(`next_review`)
            """;
        Add(cmd, "@site", key);
        Add(cmd, "@customer", customerId);
        Add(cmd, "@limit", limit);
        Add(cmd, "@ccy", ccy);
        Add(cmd, "@approvedBy", approvedBy);
        Add(cmd, "@nextReview", nextReview);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Credit limit saved.", customerId);
    }

    private static void Add(DbCommand command, string name, object? value)
    {
        var p = command.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        command.Parameters.Add(p);
    }
}
