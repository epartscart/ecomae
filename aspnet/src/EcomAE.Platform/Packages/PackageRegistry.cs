using System.Data.Common;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Packages;

/// <summary>
/// Registry access for the package model: global deployed packages (<c>epc_packages</c>),
/// per-tenant installs (<c>epc_tenant_package_installs</c>), scoped migrations
/// (<c>epc_package_migrations</c>) and audit (<c>epc_package_history</c>).
/// Registry DB via <see cref="IErpWriteConnectionFactory"/>; all writes record history.
/// </summary>
public interface IPackageRegistry
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeployedPackage>> ListDeployedAsync(CancellationToken cancellationToken = default);
    Task<DeployedPackage?> GetDeployedAsync(string packageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantPackageInstall>> ListInstallsAsync(string? siteKey, CancellationToken cancellationToken = default);
    Task<TenantPackageInstall?> GetInstallAsync(string siteKey, string packageId, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> InstallAsync(string siteKey, string packageId, string actor, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> SetEnabledAsync(string siteKey, string packageId, bool enabled, string actor, CancellationToken cancellationToken = default);
}

public sealed class PackageRegistry : IPackageRegistry
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IPackageResolver _resolver;
    private readonly ILogger<PackageRegistry> _log;

    public PackageRegistry(
        IErpWriteConnectionFactory connections,
        IPackageResolver resolver,
        ILogger<PackageRegistry> log)
    {
        _connections = connections;
        _resolver = resolver;
        _log = log;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (var ddl in SchemaDdl)
        {
            await ErpDb.ExecuteAsync(connection, null, ddl, cancellationToken).ConfigureAwait(false);
        }

        // Sync the compile-time deployed catalog into epc_packages.
        foreach (var p in PackageCatalog.Deployed)
        {
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_packages` (`package_id`,`name`,`package_type`,`version`,`core_min_version`,`core_max_version`,`owner_site_key`,`status`,`installed_at`,`updated_at`) VALUES (?,?,?,?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `name`=VALUES(`name`),`package_type`=VALUES(`package_type`),`version`=VALUES(`version`),`core_min_version`=VALUES(`core_min_version`),`core_max_version`=VALUES(`core_max_version`),`owner_site_key`=VALUES(`owner_site_key`),`updated_at`=VALUES(`updated_at`)"),
                cancellationToken,
                p.PackageId, p.Name, p.PackageType, p.Version,
                (object?)p.CoreMinVersion ?? DBNull.Value,
                (object?)p.CoreMaxVersion ?? DBNull.Value,
                (object?)p.OwnerSiteKey ?? DBNull.Value,
                p.Status,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ConfigureAwait(false);
        }
    }

    private static readonly string[] SchemaDdl =
    [
        @"CREATE TABLE IF NOT EXISTS `epc_packages` (
            `package_id` VARCHAR(64) NOT NULL,
            `name` VARCHAR(128) NOT NULL,
            `package_type` VARCHAR(16) NOT NULL DEFAULT 'extension',
            `version` VARCHAR(32) NOT NULL,
            `manifest` MEDIUMTEXT NULL,
            `checksum` VARCHAR(64) NULL,
            `core_min_version` VARCHAR(32) NULL,
            `core_max_version` VARCHAR(32) NULL,
            `depends_on` TEXT NULL,
            `owner_site_key` VARCHAR(64) NULL,
            `status` VARCHAR(16) NOT NULL DEFAULT 'deployed',
            `installed_at` INT NULL,
            `updated_at` INT NULL,
            PRIMARY KEY (`package_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        @"CREATE TABLE IF NOT EXISTS `epc_tenant_package_installs` (
            `site_key` VARCHAR(64) NOT NULL,
            `package_id` VARCHAR(64) NOT NULL,
            `installed_version` VARCHAR(32) NOT NULL,
            `install_status` VARCHAR(16) NOT NULL DEFAULT 'installed',
            `migration_status` VARCHAR(16) NOT NULL DEFAULT 'none',
            `enabled` TINYINT NOT NULL DEFAULT 0,
            `settings` MEDIUMTEXT NULL,
            `installed_at` INT NULL,
            `activated_at` INT NULL,
            `activated_by` VARCHAR(255) NULL,
            PRIMARY KEY (`site_key`, `package_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        @"CREATE TABLE IF NOT EXISTS `epc_package_migrations` (
            `scope` VARCHAR(8) NOT NULL DEFAULT 'tenant',
            `site_key` VARCHAR(64) NOT NULL DEFAULT '',
            `package_id` VARCHAR(64) NOT NULL,
            `migration_id` VARCHAR(128) NOT NULL,
            `checksum` VARCHAR(64) NULL,
            `batch` INT NULL,
            `status` VARCHAR(16) NOT NULL DEFAULT 'applied',
            `applied_at` INT NULL,
            `detail` VARCHAR(1024) NULL,
            PRIMARY KEY (`scope`, `site_key`, `package_id`, `migration_id`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        @"CREATE TABLE IF NOT EXISTS `epc_package_history` (
            `id` BIGINT NOT NULL AUTO_INCREMENT,
            `scope` VARCHAR(8) NOT NULL DEFAULT 'tenant',
            `site_key` VARCHAR(64) NOT NULL DEFAULT '',
            `package_id` VARCHAR(64) NOT NULL,
            `action` VARCHAR(16) NOT NULL,
            `from_version` VARCHAR(32) NULL,
            `to_version` VARCHAR(32) NULL,
            `actor` VARCHAR(255) NULL,
            `detail` TEXT NULL,
            `created_at` INT NOT NULL,
            PRIMARY KEY (`id`),
            KEY `ix_pkg_hist` (`package_id`, `site_key`, `created_at`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",

        @"CREATE TABLE IF NOT EXISTS `epc_package_revision` (
            `site_key` VARCHAR(64) NOT NULL,
            `revision` BIGINT NOT NULL DEFAULT 0,
            `updated_at` INT NULL,
            PRIMARY KEY (`site_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
    ];

    public async Task<IReadOnlyList<DeployedPackage>> ListDeployedAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<DeployedPackage>();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT `package_id`,`name`,`package_type`,`version`,`core_min_version`,`core_max_version`,`owner_site_key`,`status` FROM `epc_packages` ORDER BY `package_id`";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new DeployedPackage(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetString(7)));
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "epc_packages list failed");
        }

        return list;
    }

    public async Task<DeployedPackage?> GetDeployedAsync(string packageId, CancellationToken cancellationToken = default)
    {
        var all = await ListDeployedAsync(cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(p => string.Equals(p.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<TenantPackageInstall>> ListInstallsAsync(string? siteKey, CancellationToken cancellationToken = default)
    {
        var list = new List<TenantPackageInstall>();
        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = string.IsNullOrWhiteSpace(siteKey)
                ? "SELECT `site_key`,`package_id`,`installed_version`,`install_status`,`migration_status`,`enabled`,`installed_at`,`activated_at`,`activated_by` FROM `epc_tenant_package_installs` ORDER BY `site_key`,`package_id`"
                : "SELECT `site_key`,`package_id`,`installed_version`,`install_status`,`migration_status`,`enabled`,`installed_at`,`activated_at`,`activated_by` FROM `epc_tenant_package_installs` WHERE `site_key` = ? ORDER BY `package_id`";
            if (!string.IsNullOrWhiteSpace(siteKey))
            {
                var p = command.CreateParameter();
                p.Value = siteKey;
                command.Parameters.Add(p);
            }

            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(new TenantPackageInstall(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.GetString(4), reader.GetInt32(5) == 1,
                    reader.IsDBNull(6) ? null : reader.GetValue(6)?.ToString(),
                    reader.IsDBNull(7) ? null : reader.GetValue(7)?.ToString(),
                    reader.IsDBNull(8) ? null : reader.GetString(8)));
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "epc_tenant_package_installs list failed");
        }

        return list;
    }

    public async Task<TenantPackageInstall?> GetInstallAsync(string siteKey, string packageId, CancellationToken cancellationToken = default)
    {
        var all = await ListInstallsAsync(siteKey, cancellationToken).ConfigureAwait(false);
        return all.FirstOrDefault(i => string.Equals(i.PackageId, packageId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ErpSimpleWriteResult> InstallAsync(
        string siteKey, string packageId, string actor, CancellationToken cancellationToken = default)
    {
        var deployed = PackageCatalog.Find(packageId);
        var invalid = PackageRules.ValidateLifecycle(deployed, siteKey, packageId, null, forInstall: true, _ => null);
        if (invalid is not null)
        {
            return ErpSimpleWriteResult.Fail("invalid", invalid);
        }

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("INSERT INTO `epc_tenant_package_installs` (`site_key`,`package_id`,`installed_version`,`install_status`,`migration_status`,`enabled`,`installed_at`) VALUES (?,?,?,?,?,0,?) ON DUPLICATE KEY UPDATE `installed_version`=VALUES(`installed_version`),`install_status`=VALUES(`install_status`)"),
                cancellationToken,
                siteKey, packageId, deployed!.Version, "installed", "none", now).ConfigureAwait(false);
            await WriteHistoryAsync(connection, null, "tenant", siteKey, packageId, "install", null, deployed!.Version, actor, null, now, cancellationToken).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok($"Package {packageId} installed for {siteKey}.", 1);
        }
        catch (DbException ex)
        {
            _log.LogWarning(ex, "package install failed {Package} {Site}", packageId, siteKey);
            return ErpSimpleWriteResult.Fail("db", "Package tables missing — schema ensure first.");
        }
    }

    public async Task<ErpSimpleWriteResult> SetEnabledAsync(
        string siteKey, string packageId, bool enabled, string actor, CancellationToken cancellationToken = default)
    {
        var deployed = PackageCatalog.Find(packageId);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

            var install = await GetInstallAsync(siteKey, packageId, cancellationToken).ConfigureAwait(false);

            if (enabled)
            {
                var depCache = new Dictionary<string, TenantPackageInstall?>(StringComparer.OrdinalIgnoreCase);
                var invalid = PackageRules.ValidateLifecycle(
                    deployed, siteKey, packageId, install, forInstall: false,
                    dep => depCache.TryGetValue(dep, out var cached)
                        ? cached
                        : depCache[dep] = GetInstallAsync(siteKey, dep, cancellationToken).GetAwaiter().GetResult());
                if (invalid is not null)
                {
                    return ErpSimpleWriteResult.Fail("invalid", invalid);
                }
            }
            else if (deployed is null)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Unknown package.");
            }
            else if (!string.IsNullOrWhiteSpace(deployed.OwnerSiteKey)
                     && !string.Equals(deployed.OwnerSiteKey, siteKey, StringComparison.OrdinalIgnoreCase))
            {
                return ErpSimpleWriteResult.Fail("invalid", $"Package is owned by tenant '{deployed.OwnerSiteKey}'.");
            }
            if (install is null)
            {
                return ErpSimpleWriteResult.Fail("invalid", "Install the package for this tenant first.");
            }

            if (!enabled)
            {
                // Dependent gate: refuse deactivation while an enabled package depends on it.
                foreach (var other in PackageCatalog.Deployed)
                {
                    if (!PackageCatalog.DependenciesOf(other.PackageId).Contains(packageId, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var depInstall = await GetInstallAsync(siteKey, other.PackageId, cancellationToken).ConfigureAwait(false);
                    if (depInstall?.Enabled == true)
                    {
                        return ErpSimpleWriteResult.Fail("invalid", $"Deactivate {other.PackageId} first — it depends on this package.");
                    }
                }
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // One transaction: enabled flag + revision bump + history commit together — a
            // mid-sequence failure leaves package state (and the cached view) untouched.
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await ErpDb.ExecuteAsync(
                connection, transaction,
                ErpDb.Positional("UPDATE `epc_tenant_package_installs` SET `enabled`=?, `activated_at`=?, `activated_by`=? WHERE `site_key`=? AND `package_id`=?"),
                cancellationToken,
                enabled ? 1 : 0,
                enabled ? now : (object)DBNull.Value,
                (object?)actor ?? DBNull.Value,
                siteKey, packageId).ConfigureAwait(false);
            await BumpRevisionAsync(connection, transaction, siteKey, now, cancellationToken).ConfigureAwait(false);
            await WriteHistoryAsync(connection, transaction, "tenant", siteKey, packageId,
                enabled ? "activate" : "deactivate", install.InstalledVersion, install.InstalledVersion, actor, null, now, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            // Immediate deactivation: invalidate the resolver cache in-process so a disabled
            // package cannot execute on the next request.
            _resolver.Invalidate(siteKey);
            return ErpSimpleWriteResult.Ok(
                $"Package {packageId} {(enabled ? "activated" : "deactivated")} for {siteKey}.", 1);
        }
        catch (DbException ex)
        {
            _log.LogWarning(ex, "package set-enabled failed {Package} {Site}", packageId, siteKey);
            return ErpSimpleWriteResult.Fail("db", "Package tables missing — schema ensure first.");
        }
    }

    private static async Task BumpRevisionAsync(
        DbConnection connection, DbTransaction transaction, string siteKey, long now, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection, transaction,
            ErpDb.Positional("INSERT INTO `epc_package_revision` (`site_key`,`revision`,`updated_at`) VALUES (?,1,?) ON DUPLICATE KEY UPDATE `revision`=`revision`+1,`updated_at`=VALUES(`updated_at`)"),
            cancellationToken,
            siteKey, now).ConfigureAwait(false);
    }

    private static async Task WriteHistoryAsync(
        DbConnection connection, DbTransaction? transaction, string scope, string siteKey, string packageId,
        string action, string? fromVersion, string? toVersion, string? actor,
        string? detail, long now, CancellationToken cancellationToken)
    {
        await ErpDb.ExecuteAsync(
            connection, transaction,
            ErpDb.Positional("INSERT INTO `epc_package_history` (`scope`,`site_key`,`package_id`,`action`,`from_version`,`to_version`,`actor`,`detail`,`created_at`) VALUES (?,?,?,?,?,?,?,?,?)"),
            cancellationToken,
            scope, siteKey, packageId, action,
            (object?)fromVersion ?? DBNull.Value,
            (object?)toVersion ?? DBNull.Value,
            (object?)actor ?? DBNull.Value,
            (object?)detail ?? DBNull.Value,
            now).ConfigureAwait(false);
    }
}
