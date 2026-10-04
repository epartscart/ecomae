using EcomAE.Platform.Erp;
using EcomAE.Platform.Migration;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpEditLockAcquirePhpParityTests
{
    [Fact]
    public void Program_RegistersEditLockAcquireWriteService()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Program.cs"));
        Assert.Contains("IErpEditLockAcquireWriteService", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_MarksEditLockAcquireLiveGated()
    {
        var row = SurfacePayloadContractCatalog.Functions.First(item =>
            item.AspNetRouteOrCapability == "/erp/ajax/edit-lock-acquire");
        Assert.Equal("write-live-gated", row.Status);
        Assert.Contains("epc_erp_edit_lock_acquire", row.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain("confirm_writes refused", row.Notes, StringComparison.Ordinal);
    }

    [Fact]
    public void Handler_WiresAcquireServiceAndConflictStatuses()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Modules/ErpModule.cs"));
        Assert.Contains("HandleEditLockAcquireAsync", text, StringComparison.Ordinal);
        Assert.Contains("IErpEditLockAcquireWriteService", text, StringComparison.Ordinal);
        Assert.Contains("Status409Conflict", text, StringComparison.Ordinal);
        Assert.Contains("force_denied", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_ProvisionsPhpConcurrencySchemaAndAudits()
    {
        var text = File.ReadAllText(FindRepoFile("aspnet/src/EcomAE.Platform/Erp/ErpEditLockAcquireWriteService.cs"));
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_erp_edit_locks`", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_erp_presence`", text, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS `epc_erp_idempotency`", text, StringComparison.Ordinal);
        Assert.Contains("edit_lock_acquire", text, StringComparison.Ordinal);
        Assert.Contains("force_denied", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Service_IsDiAssignable()
    {
        Assert.IsAssignableFrom<IErpEditLockAcquireWriteService>(
            new ErpEditLockAcquireWriteService(new UnconfiguredConnections(), new ErpAuditLogWriter()));
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "EcomAE.AspNetCore.sln"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, relative);
    }

    private sealed class UnconfiguredConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("unconfigured");
    }
}
