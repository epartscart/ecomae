using System.Data.Common;
using System.Globalization;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Storefront;

/// <summary>Read twin for PHP <c>epc_vendor_get_account</c> + dashboard KPIs. No writes.</summary>
public interface IStorefrontVendorPortalReader
{
    Task<StorefrontVendorPortalAccount?> LoadAsync(int userId, CancellationToken cancellationToken = default);
}

public sealed record StorefrontVendorPortalAccount(
    long Id,
    string VendorFull,
    string VendorShort,
    string Status,
    long StorageId,
    string LegalName,
    string Trn,
    string LegalRegType,
    string LegalRegNo,
    string PeppolEndpoint,
    string AddressLine1,
    string City,
    string Emirate,
    int SkuCount,
    int PriceListCount,
    IReadOnlyList<StorefrontVendorUploadRow> Uploads)
{
    public bool CanAccess =>
        string.Equals(Status, "approved", StringComparison.OrdinalIgnoreCase)
        || string.Equals(Status, "active", StringComparison.OrdinalIgnoreCase);
}

public sealed record StorefrontVendorUploadRow(
    string CreatedAt,
    string OriginalFilename,
    string PriceName,
    int RowsImported,
    string Status);

public sealed class StorefrontVendorPortalReader : IStorefrontVendorPortalReader
{
    private readonly IErpWriteConnectionFactory _connections;

    public StorefrontVendorPortalReader(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<StorefrontVendorPortalAccount?> LoadAsync(int userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || !_connections.IsConfigured)
        {
            return null;
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = ErpDb.Positional("""
                SELECT `id`, `vendor_full`, `vendor_short`, `status`, IFNULL(`storage_id`,0) AS storage_id,
                       IFNULL(`legal_name`,'') AS legal_name, IFNULL(`trn`,'') AS trn,
                       IFNULL(`legal_reg_type`,'') AS legal_reg_type, IFNULL(`legal_reg_no`,'') AS legal_reg_no,
                       IFNULL(`peppol_endpoint`,'') AS peppol_endpoint, IFNULL(`address_line1`,'') AS address_line1,
                       IFNULL(`city`,'') AS city, IFNULL(`emirate`,'') AS emirate
                FROM `epc_vendor_accounts`
                WHERE `user_id` = ?
                LIMIT 1
                """);
            ErpDb.AddParameters(command, userId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            var accountId = Convert.ToInt64(reader["id"], CultureInfo.InvariantCulture);
            var vendorFull = Convert.ToString(reader["vendor_full"], CultureInfo.InvariantCulture) ?? "";
            var vendorShort = Convert.ToString(reader["vendor_short"], CultureInfo.InvariantCulture) ?? "";
            var status = Convert.ToString(reader["status"], CultureInfo.InvariantCulture) ?? "";
            var storageId = Convert.ToInt64(reader["storage_id"], CultureInfo.InvariantCulture);
            var legalName = Convert.ToString(reader["legal_name"], CultureInfo.InvariantCulture) ?? "";
            var trn = Convert.ToString(reader["trn"], CultureInfo.InvariantCulture) ?? "";
            var legalRegType = Convert.ToString(reader["legal_reg_type"], CultureInfo.InvariantCulture) ?? "";
            var legalRegNo = Convert.ToString(reader["legal_reg_no"], CultureInfo.InvariantCulture) ?? "";
            var peppol = Convert.ToString(reader["peppol_endpoint"], CultureInfo.InvariantCulture) ?? "";
            var address = Convert.ToString(reader["address_line1"], CultureInfo.InvariantCulture) ?? "";
            var city = Convert.ToString(reader["city"], CultureInfo.InvariantCulture) ?? "";
            var emirate = Convert.ToString(reader["emirate"], CultureInfo.InvariantCulture) ?? "";
            await reader.DisposeAsync().ConfigureAwait(false);

            var priceIds = await LoadPriceIdsAsync(connection, vendorShort, cancellationToken).ConfigureAwait(false);
            var skuCount = await CountSkuAsync(connection, priceIds, cancellationToken).ConfigureAwait(false);
            var uploads = await LoadUploadsAsync(connection, priceIds, userId, cancellationToken).ConfigureAwait(false);
            return new StorefrontVendorPortalAccount(
                accountId, vendorFull, vendorShort, status, storageId,
                legalName, trn, legalRegType, legalRegNo, peppol, address, city, emirate,
                skuCount, priceIds.Count, uploads);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<List<long>> LoadPriceIdsAsync(
        DbConnection connection,
        string vendorShort,
        CancellationToken cancellationToken)
    {
        var ids = new List<long>();
        if (!string.IsNullOrWhiteSpace(vendorShort))
        {
            foreach (var name in new[] { vendorShort, vendorShort + " · Sales", vendorShort + " · Purchase" })
            {
                var id = await ErpDb.LongAsync(
                    connection,
                    null,
                    ErpDb.Positional("SELECT `id` FROM `shop_docpart_prices` WHERE `name` = ? LIMIT 1"),
                    cancellationToken,
                    name);
                if (id > 0)
                {
                    ids.Add(id);
                }
            }
        }

        return ids.Distinct().ToList();
    }

    private static async Task<int> CountSkuAsync(
        DbConnection connection,
        IReadOnlyList<long> priceIds,
        CancellationToken cancellationToken)
    {
        if (priceIds.Count == 0)
        {
            return 0;
        }

        try
        {
            var placeholders = string.Join(",", priceIds.Select(_ => "?"));
            return (int)await ErpDb.LongAsync(
                connection,
                null,
                ErpDb.Positional($"SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` IN ({placeholders})"),
                cancellationToken,
                priceIds.Cast<object?>().ToArray());
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static async Task<IReadOnlyList<StorefrontVendorUploadRow>> LoadUploadsAsync(
        DbConnection connection,
        IReadOnlyList<long> priceIds,
        int userId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            if (priceIds.Count > 0)
            {
                var placeholders = string.Join(",", priceIds.Select(_ => "?"));
                command.CommandText = ErpDb.Positional($"""
                    SELECT IFNULL(`created_at`,'') AS created_at,
                           IFNULL(`original_filename`,'') AS original_filename,
                           IFNULL(`price_name`,'') AS price_name,
                           IFNULL(`rows_imported`,0) AS rows_imported,
                           IFNULL(`status`,'') AS status
                    FROM `epc_price_upload_history`
                    WHERE (`price_id` IN ({placeholders}) OR (`uploaded_by` = ? AND `upload_source` = 'vendor_portal'))
                    ORDER BY `id` DESC
                    LIMIT 25
                    """);
                var bind = priceIds.Cast<object?>().Append(userId).ToArray();
                ErpDb.AddParameters(command, bind);
            }
            else
            {
                command.CommandText = ErpDb.Positional("""
                    SELECT IFNULL(`created_at`,'') AS created_at,
                           IFNULL(`original_filename`,'') AS original_filename,
                           IFNULL(`price_name`,'') AS price_name,
                           IFNULL(`rows_imported`,0) AS rows_imported,
                           IFNULL(`status`,'') AS status
                    FROM `epc_price_upload_history`
                    WHERE `uploaded_by` = ? AND `upload_source` = 'vendor_portal'
                    ORDER BY `id` DESC
                    LIMIT 25
                    """);
                ErpDb.AddParameters(command, userId);
            }

            var rows = new List<StorefrontVendorUploadRow>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add(new StorefrontVendorUploadRow(
                    Convert.ToString(reader["created_at"], CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader["original_filename"], CultureInfo.InvariantCulture) ?? "",
                    Convert.ToString(reader["price_name"], CultureInfo.InvariantCulture) ?? "",
                    Convert.ToInt32(reader["rows_imported"], CultureInfo.InvariantCulture),
                    Convert.ToString(reader["status"], CultureInfo.InvariantCulture) ?? ""));
            }

            return rows;
        }
        catch (Exception)
        {
            return [];
        }
    }
}
