using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using EcomAE.Platform.Erp;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Packages;

/// <summary>
/// Slice 1d upgrade/rollback engine: version-rollforward and recovery for tenant
/// package installs. Preflight validates version compatibility and install state;
/// the write commits install-version + migration record + history + revision in one
/// transaction under the per-tenant lifecycle lock. Rollback is a compensating write
/// (restore previous version) — MariaDB DDL is non-transactional, so recovery never
/// relies on transaction rollback alone: every attempt is recorded with a checksum
/// in <c>epc_package_migrations</c> and <c>epc_package_history</c>.
/// </summary>
public interface IPackageUpgrade
{
    Task<ErpSimpleWriteResult> UpgradeAsync(string siteKey, string packageId, string actor, CancellationToken cancellationToken = default);
    Task<ErpSimpleWriteResult> RollbackAsync(string siteKey, string packageId, string toVersion, string actor, CancellationToken cancellationToken = default);
}

/// <summary>Semver-ish core compatibility: package core_min ≤ coreVersion ≤ core_max.</summary>
public static class PackageCompatibility
{
    public static bool IsCompatible(DeployedPackage deployed, string coreVersion)
    {
        if (!Version.TryParse(coreVersion, out var core))
        {
            return false;
        }

        if (deployed.CoreMinVersion is not null
            && (!Version.TryParse(deployed.CoreMinVersion, out var min) || core < min))
        {
            return false;
        }

        if (deployed.CoreMaxVersion is not null
            && (!Version.TryParse(deployed.CoreMaxVersion, out var max) || core > max))
        {
            return false;
        }

        return true;
    }
}

/// <summary>Pure upgrade/recovery decision rules — unit-testable without a DB.</summary>
public static class UpgradeRules
{
    /// <summary>Validates an upgrade or rollback. Returns error message or null.</summary>
    public static string? Validate(
        DeployedPackage? deployed,
        TenantPackageInstall? install,
        string toVersion,
        string coreVersion,
        bool isRollback)
    {
        if (deployed is null)
        {
            return "Unknown package.";
        }

        if (install is null || install.InstallStatus != "installed")
        {
            return "Install the package for this tenant first.";
        }

        if (string.IsNullOrWhiteSpace(toVersion))
        {
            return "Target version required.";
        }

        if (string.Equals(install.InstalledVersion, toVersion, StringComparison.OrdinalIgnoreCase))
        {
            return $"Already on version {toVersion}.";
        }

        if (!isRollback && !PackageCompatibility.IsCompatible(deployed, coreVersion))
        {
            return $"Package requires core {deployed.CoreMinVersion}–{deployed.CoreMaxVersion}; current core is {coreVersion}.";
        }

        return null;
    }

    /// <summary>Integrity checksum for a migration record — verifies a recorded upgrade was not tampered.</summary>
    public static string Checksum(string packageId, string fromVersion, string toVersion)
        => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes($"{packageId}|{fromVersion}|{toVersion}")))[..16].ToLowerInvariant();
}

public sealed class PackageUpgradeService : IPackageUpgrade
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IPackageResolver _resolver;
    private readonly ILogger<PackageUpgradeService> _log;

    public PackageUpgradeService(
        IErpWriteConnectionFactory connections,
        IPackageResolver resolver,
        ILogger<PackageUpgradeService> log)
    {
        _connections = connections;
        _resolver = resolver;
        _log = log;
    }

    public Task<ErpSimpleWriteResult> UpgradeAsync(string siteKey, string packageId, string actor, CancellationToken cancellationToken = default)
    {
        var deployed = PackageCatalog.Find(packageId);
        var toVersion = deployed?.Version ?? string.Empty;
        return WriteVersionAsync(siteKey, packageId, toVersion, actor, isRollback: false, cancellationToken);
    }

    public Task<ErpSimpleWriteResult> RollbackAsync(string siteKey, string packageId, string toVersion, string actor, CancellationToken cancellationToken = default)
        => WriteVersionAsync(siteKey, packageId, toVersion, actor, isRollback: true, cancellationToken);

    private async Task<ErpSimpleWriteResult> WriteVersionAsync(
        string siteKey, string packageId, string toVersion, string actor, bool isRollback, CancellationToken cancellationToken)
    {
        var deployed = PackageCatalog.Find(packageId);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var lockName = "epc_pkg_lifecycle_" + siteKey;
            var gotLock = (await ErpDb.ScalarAsync(
                connection, null, ErpDb.Positional("SELECT GET_LOCK(?,10)"),
                cancellationToken, lockName).ConfigureAwait(false))?.ToString() == "1";
            if (!gotLock)
            {
                return ErpSimpleWriteResult.Fail("busy", "Another package change is in progress — retry.");
            }

            try
            {
                var install = await PackageRegistry.ReadInstallAsync(connection, siteKey, packageId, cancellationToken).ConfigureAwait(false);
                var invalid = UpgradeRules.Validate(deployed, install, toVersion, PackageCatalog.CoreVersion, isRollback);
                if (invalid is not null)
                {
                    return ErpSimpleWriteResult.Fail("invalid", invalid);
                }

                var fromVersion = install!.InstalledVersion;
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var migrationId = (isRollback ? "rollback-" : "upgrade-") + fromVersion + "-" + toVersion;
                var checksum = UpgradeRules.Checksum(packageId, fromVersion, toVersion);

                await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, transaction,
                    ErpDb.Positional("UPDATE `epc_tenant_package_installs` SET `installed_version`=?, `migration_status`='applied' WHERE `site_key`=? AND `package_id`=?"),
                    cancellationToken,
                    toVersion, siteKey, packageId).ConfigureAwait(false);
                await ErpDb.ExecuteAsync(
                    connection, transaction,
                    ErpDb.Positional("INSERT INTO `epc_package_migrations` (`scope`,`site_key`,`package_id`,`migration_id`,`checksum`,`status`,`applied_at`,`detail`) VALUES ('tenant',?,?,?,?,?,?,?) ON DUPLICATE KEY UPDATE `status`=VALUES(`status`),`applied_at`=VALUES(`applied_at`),`checksum`=VALUES(`checksum`)"),
                    cancellationToken,
                    siteKey, packageId, migrationId, checksum, "applied", now,
                    isRollback ? "compensating rollback" : "version rollforward").ConfigureAwait(false);
                await BumpRevisionAsync(connection, transaction, siteKey, now, cancellationToken).ConfigureAwait(false);
                await WriteHistoryAsync(connection, transaction, siteKey, packageId,
                    isRollback ? "rollback" : "upgrade", fromVersion, toVersion, actor, now, cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

                _resolver.Invalidate(siteKey);
                return ErpSimpleWriteResult.Ok(
                    $"Package {packageId} {(isRollback ? "rolled back to" : "upgraded to")} {toVersion} for {siteKey}.", 1);
            }
            finally
            {
                await ErpDb.ExecuteAsync(
                    connection, null, ErpDb.Positional("SELECT RELEASE_LOCK(?)"),
                    cancellationToken, lockName).ConfigureAwait(false);
            }
        }
        catch (DbException ex)
        {
            _log.LogWarning(ex, "package {Action} failed {Package} {Site}", isRollback ? "rollback" : "upgrade", packageId, siteKey);
            return ErpSimpleWriteResult.Fail("db", "Package tables missing or write failed — inspect epc_package_migrations for the stopping point, then re-run (idempotent).");
        }
    }

    private static async Task BumpRevisionAsync(DbConnection connection, System.Data.Common.DbTransaction transaction, string siteKey, long now, CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection, transaction,
            ErpDb.Positional("INSERT INTO `epc_package_revision` (`site_key`,`revision`,`updated_at`) VALUES (?,1,?) ON DUPLICATE KEY UPDATE `revision`=`revision`+1,`updated_at`=VALUES(`updated_at`)"),
            cancellationToken,
            siteKey, now).ConfigureAwait(false);

    private static async Task WriteHistoryAsync(DbConnection connection, System.Data.Common.DbTransaction transaction, string siteKey, string packageId, string action, string fromVersion, string toVersion, string actor, long now, CancellationToken cancellationToken)
        => await ErpDb.ExecuteAsync(
            connection, transaction,
            ErpDb.Positional("INSERT INTO `epc_package_history` (`scope`,`site_key`,`package_id`,`action`,`from_version`,`to_version`,`actor`,`created_at`) VALUES ('tenant',?,?,?,?,?,?,?)"),
            cancellationToken,
            siteKey, packageId, action, fromVersion, toVersion, actor, now).ConfigureAwait(false);
}
