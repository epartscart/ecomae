using System.Data.Common;
using System.Globalization;
using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Live PHP <c>epc_cust_groups_create</c> / <c>epc_cust_groups_assign</c> twin. Schema-ensure stays PHP.
/// </summary>
public interface IErpCustomerGroupsWriteService
{
    Task<ErpSimpleWriteResult> CreateAsync(
        ErpCustomerGroupCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AssignAsync(
        ErpCustomerGroupAssignRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record ErpCustomerGroupCreateRequest(
    int CompanyId = 0,
    string? GroupCode = null,
    string? GroupName = null,
    string? GroupType = null,
    decimal DiscountPct = 0,
    decimal CreditLimit = 0,
    int PaymentTermsDays = 30,
    int PriceListId = 0,
    string? Description = null);

public sealed record ErpCustomerGroupAssignRequest(
    long GroupId = 0,
    int CustomerId = 0);

public sealed class ErpCustomerGroupsWriteService : IErpCustomerGroupsWriteService
{
    private static readonly HashSet<string> GroupTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "general", "vip", "wholesale", "retail", "corporate", "government"
    };

    private readonly IErpWriteConnectionFactory _connections;

    public ErpCustomerGroupsWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> CreateAsync(
        ErpCustomerGroupCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var code = Clip((request.GroupCode ?? string.Empty).Trim(), 32);
        var name = Clip((request.GroupName ?? string.Empty).Trim(), 200);
        if (code.Length == 0 && name.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Group code or name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_customer_groups", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_customer_groups", "group_code", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Customer group tables are not provisioned");
        }

        var companyId = request.CompanyId < 0 ? 0 : request.CompanyId;
        if (code.Length == 0)
        {
            var count = await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_customer_groups` WHERE `company_id` = ?"),
                cancellationToken,
                companyId).ConfigureAwait(false);
            code = "CG-" + DateTime.Now.Year.ToString(CultureInfo.InvariantCulture) + "-"
                   + (count + 1).ToString("D4", CultureInfo.InvariantCulture);
        }

        var hasPriceList = await ColumnExistsAsync(connection, "epc_customer_groups", "price_list_id", cancellationToken)
            .ConfigureAwait(false);
        var hasDescription = await ColumnExistsAsync(connection, "epc_customer_groups", "description", cancellationToken)
            .ConfigureAwait(false);

        var columns = new StringBuilder(
            "`company_id`,`group_code`,`group_name`,`group_type`,`discount_pct`,`credit_limit`,`payment_terms_days`");
        var placeholders = new StringBuilder("?, ?, ?, ?, ?, ?, ?");
        var values = new List<object?>
        {
            companyId,
            code,
            name,
            NormalizeType(request.GroupType),
            NormalizeDiscount(request.DiscountPct),
            RoundNonNeg(request.CreditLimit, 2),
            request.PaymentTermsDays <= 0 ? 30 : request.PaymentTermsDays
        };
        if (hasPriceList)
        {
            columns.Append(",`price_list_id`");
            placeholders.Append(", ?");
            values.Add(request.PriceListId < 0 ? 0 : request.PriceListId);
        }

        if (hasDescription)
        {
            columns.Append(",`description`");
            placeholders.Append(", ?");
            values.Add(request.Description ?? string.Empty);
        }

        columns.Append(",`time_created`");
        placeholders.Append(", ?");
        values.Add(UnixNow());

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_customer_groups` (" + columns + ") VALUES (" + placeholders + ")"),
            cancellationToken,
            values.ToArray()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Customer group " + code + " created", id);
    }

    public async Task<ErpSimpleWriteResult> AssignAsync(
        ErpCustomerGroupAssignRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.GroupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Group id is required.");
        }

        if (request.CustomerId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Customer id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (!await TableExistsAsync(connection, "epc_customer_groups", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_customer_groups", "group_code", cancellationToken).ConfigureAwait(false)
            || !await TableExistsAsync(connection, "epc_customer_group_members", cancellationToken).ConfigureAwait(false)
            || !await ColumnExistsAsync(connection, "epc_customer_group_members", "customer_id", cancellationToken).ConfigureAwait(false))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Customer group tables are not provisioned");
        }

        var groupId = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_customer_groups` WHERE `id` = ? LIMIT 1"),
            cancellationToken,
            request.GroupId).ConfigureAwait(false);
        if (groupId <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Group is missing.");
        }

        var existing = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT `id` FROM `epc_customer_group_members` WHERE `group_id` = ? AND `customer_id` = ? LIMIT 1"),
            cancellationToken,
            groupId,
            request.CustomerId).ConfigureAwait(false);
        if (existing > 0)
        {
            return ErpSimpleWriteResult.Ok("Customer already in group", existing);
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `epc_customer_group_members` (`group_id`,`customer_id`,`time_added`) VALUES (?, ?, ?)"),
            cancellationToken,
            groupId,
            request.CustomerId,
            UnixNow()).ConfigureAwait(false);

        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Failed");
        }

        return ErpSimpleWriteResult.Ok("Customer assigned to group", id);
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string table, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ?"),
            cancellationToken,
            table).ConfigureAwait(false);
        return n > 0;
    }

    private static async Task<bool> ColumnExistsAsync(DbConnection connection, string table, string column, CancellationToken cancellationToken)
    {
        var n = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = ? AND COLUMN_NAME = ?"),
            cancellationToken,
            table,
            column).ConfigureAwait(false);
        return n > 0;
    }

    private static string NormalizeType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return GroupTypes.Contains(value) ? value : "general";
    }

    private static decimal NormalizeDiscount(decimal value)
    {
        if (value < 0)
        {
            return 0;
        }

        return decimal.Round(value > 100 ? 100 : value, 2, MidpointRounding.AwayFromZero);
    }

    private static int UnixNow()
        => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static decimal RoundNonNeg(decimal value, int decimals)
        => decimal.Round(value < 0 ? 0 : value, decimals, MidpointRounding.AwayFromZero);

    private static string Clip(string value, int maxLen)
        => value.Length <= maxLen ? value : value[..maxLen];
}
