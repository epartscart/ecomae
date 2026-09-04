using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>Live PHP <c>manufacturers_synonyms/ajax_operations.php</c> write twins. List/get stay digest.</summary>
public interface ICpManufacturerSynonymWriteService
{
    Task<ErpSimpleWriteResult> AddManufacturerAsync(string? name, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveManufacturerAsync(long id, string? name, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteManufacturerAsync(long id, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> AddSynonymAsync(long manufacturerId, string? name, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveSynonymAsync(long id, string? name, CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> DeleteSynonymAsync(long id, CancellationToken cancellationToken = default);
}

public sealed class CpManufacturerSynonymWriteService : ICpManufacturerSynonymWriteService
{
    private readonly IErpWriteConnectionFactory _connections;

    public CpManufacturerSynonymWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public async Task<ErpSimpleWriteResult> AddManufacturerAsync(string? name, CancellationToken cancellationToken = default)
    {
        var clean = CleanName(name);
        if (clean.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A manufacturer name is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var exists = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_manufacturers` WHERE `name` = ?"),
            cancellationToken,
            clean);
        if (exists > 0)
        {
            return ErpSimpleWriteResult.Fail("duplicate", "Manufacturer already exists.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_docpart_manufacturers` (`name`) VALUES (?)"),
            cancellationToken,
            clean);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Manufacturer added.", id);
    }

    public async Task<ErpSimpleWriteResult> SaveManufacturerAsync(long id, string? name, CancellationToken cancellationToken = default)
    {
        var clean = CleanName(name);
        if (id <= 0 || clean.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A manufacturer id and name are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var clash = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_manufacturers` WHERE `name` = ? AND `id` <> ?"),
            cancellationToken,
            clean, id);
        if (clash > 0)
        {
            return ErpSimpleWriteResult.Fail("duplicate", "Manufacturer already exists.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_manufacturers` SET `name` = ? WHERE `id` = ?"),
            cancellationToken,
            clean, id);
        return ErpSimpleWriteResult.Ok("Manufacturer saved.", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteManufacturerAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A manufacturer id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `shop_docpart_manufacturers_synonyms` WHERE `manufacturer_id` = ?"),
            cancellationToken,
            id);
        await ErpDb.ExecuteAsync(
            connection,
            transaction,
            ErpDb.Positional("DELETE FROM `shop_docpart_manufacturers` WHERE `id` = ?"),
            cancellationToken,
            id);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Manufacturer deleted.", id);
    }

    public async Task<ErpSimpleWriteResult> AddSynonymAsync(long manufacturerId, string? name, CancellationToken cancellationToken = default)
    {
        var clean = CleanName(name);
        if (manufacturerId <= 0 || clean.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A manufacturer id and synonym are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var mfr = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_manufacturers` WHERE `id` = ?"),
            cancellationToken,
            manufacturerId);
        if (mfr != 1)
        {
            return ErpSimpleWriteResult.Fail("not_found", "Manufacturer not found.");
        }

        var nameClash = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_manufacturers` WHERE `name` = ?"),
            cancellationToken,
            clean);
        if (nameClash > 0)
        {
            return ErpSimpleWriteResult.Fail("duplicate", "Synonym collides with a manufacturer name.");
        }

        var synClash = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ?"),
            cancellationToken,
            clean);
        if (synClash > 0)
        {
            return ErpSimpleWriteResult.Fail("duplicate", "Synonym already exists.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("INSERT INTO `shop_docpart_manufacturers_synonyms` (`manufacturer_id`, `synonym`) VALUES (?, ?)"),
            cancellationToken,
            manufacturerId, clean);
        var id = await ErpDb.LastInsertIdAsync(connection, null, cancellationToken).ConfigureAwait(false);
        return ErpSimpleWriteResult.Ok("Synonym added.", id);
    }

    public async Task<ErpSimpleWriteResult> SaveSynonymAsync(long id, string? name, CancellationToken cancellationToken = default)
    {
        var clean = CleanName(name);
        if (id <= 0 || clean.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A synonym id and name are required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await EnsureSchemaAsync(connection, cancellationToken).ConfigureAwait(false);
        var nameClash = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_manufacturers` WHERE `name` = ?"),
            cancellationToken,
            clean);
        if (nameClash > 0)
        {
            return ErpSimpleWriteResult.Fail("duplicate", "Synonym collides with a manufacturer name.");
        }

        var synClash = await ErpDb.LongAsync(
            connection,
            null,
            ErpDb.Positional("SELECT COUNT(*) FROM `shop_docpart_manufacturers_synonyms` WHERE `synonym` = ? AND `id` <> ?"),
            cancellationToken,
            clean, id);
        if (synClash > 0)
        {
            return ErpSimpleWriteResult.Fail("duplicate", "Synonym already exists.");
        }

        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("UPDATE `shop_docpart_manufacturers_synonyms` SET `synonym` = ? WHERE `id` = ?"),
            cancellationToken,
            clean, id);
        return ErpSimpleWriteResult.Ok("Synonym saved.", id);
    }

    public async Task<ErpSimpleWriteResult> DeleteSynonymAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "A synonym id is required.");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ErpDb.ExecuteAsync(
            connection,
            null,
            ErpDb.Positional("DELETE FROM `shop_docpart_manufacturers_synonyms` WHERE `id` = ?"),
            cancellationToken,
            id);
        return ErpSimpleWriteResult.Ok("Synonym deleted.", id);
    }

    private static async Task EnsureSchemaAsync(System.Data.Common.DbConnection connection, CancellationToken cancellationToken)
    {
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `shop_docpart_manufacturers` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `name` VARCHAR(255) NOT NULL DEFAULT '',
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_name` (`name`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
        await ErpDb.TryExecuteAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `shop_docpart_manufacturers_synonyms` (
                `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
                `manufacturer_id` INT UNSIGNED NOT NULL DEFAULT 0,
                `synonym` VARCHAR(255) NOT NULL DEFAULT '',
                PRIMARY KEY (`id`),
                KEY `idx_mfr` (`manufacturer_id`),
                KEY `idx_synonym` (`synonym`(191))
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
            """,
            cancellationToken).ConfigureAwait(false);
    }

    internal static string CleanName(string? name)
    {
        var raw = (name ?? string.Empty).Trim();
        if (raw.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(raw.Length);
        foreach (var ch in raw)
        {
            if (ch is '\0' or '\r' or '\n' or '\t' || char.IsControl(ch))
            {
                continue;
            }

            builder.Append(ch);
        }

        var clean = builder.ToString().Trim();
        return clean.Length > 255 ? clean[..255] : clean;
    }
}
