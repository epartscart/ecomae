using Xunit;
using System.Text.RegularExpressions;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Fresh ERP-only tenants have no epc_credit_profiles / epc_coll_* tables. PHP
/// creates them lazily (epc_credit_ensure_schema / epc_coll_ensure_schema) at
/// the top of every credit, collections and credit-limit entry point; the
/// ASP.NET writers and readers must do the same instead of refusing.
/// </summary>
public sealed class ErpCreditSchemaLazyEnsureTests
{
    private static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "aspnet", "src", "EcomAE.Platform", "EcomAE.Platform.csproj")))
            {
                dir = dir.Parent;
            }

            return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
        }
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(Root, relative));

    [Theory]
    [InlineData("aspnet/src/EcomAE.Platform/Erp/ErpCustomerMasterWriteService.cs", "ErpCreditSchema.EnsureAsync(connection, cancellationToken)")]
    [InlineData("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsActivityLogWriteService.cs", "ErpCreditSchema.EnsureCollectionsAsync(connection, cancellationToken)")]
    [InlineData("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsCasePromiseWriteService.cs", "ErpCreditSchema.EnsureCollectionsAsync(connection, cancellationToken)")]
    [InlineData("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsCaseSaveWriteService.cs", "ErpCreditSchema.EnsureCollectionsAsync(connection, cancellationToken)")]
    [InlineData("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsCaseStatusWriteService.cs", "ErpCreditSchema.EnsureCollectionsAsync(connection, cancellationToken)")]
    [InlineData("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsDunningRunWriteService.cs", "ErpCreditSchema.EnsureCollectionsAsync(connection, cancellationToken)")]
    [InlineData("aspnet/src/EcomAE.Platform/Erp/ErpCollectionsHoldSetWriteService.cs", "ErpCreditSchema.EnsureCollectionsAsync(connection, cancellationToken)")]
    public void Writers_ensure_credit_schema_before_table_checks(string relative, string ensureCall)
    {
        var source = Read(relative);
        var ensureAt = source.IndexOf(ensureCall, StringComparison.Ordinal);
        var checkAt = source.IndexOf("TableExistsAsync(connection", StringComparison.Ordinal);
        var writeAt = source.IndexOf("ErpDb.ExecuteAsync(", StringComparison.Ordinal);
        Assert.True(ensureAt >= 0, relative + " must call " + ensureCall);
        var firstUse = new[] { checkAt, writeAt }.Where(i => i >= 0).Min();
        Assert.True(ensureAt < firstUse, relative + " must ensure schema before the first table check or write");
    }

    [Fact]
    public void Dashboard_credit_reads_ensure_schema_first()
    {
        var source = Read("aspnet/src/EcomAE.Platform/Migration/SurfaceDashboardSummaryReporter.cs");
        foreach (var caseName in new[] { "ar_customer_list", "credit_holds" })
        {
            var at = source.IndexOf("case \"" + caseName + "\":", StringComparison.Ordinal);
            Assert.True(at >= 0, caseName);
            var block = source.Substring(at, 400);
            Assert.Contains("ErpCreditSchema.EnsureAsync(connection, cancellationToken)", block);
        }
    }

    [Fact]
    public void Schema_ddl_matches_php_credit_and_collections_ensure()
    {
        var cs = Read("aspnet/src/EcomAE.Platform/Erp/ErpCreditSchema.cs");
        var credit = Read("content/shop/finance/epc_erp_credit.php");
        var coll = Read("content/shop/finance/epc_erp_collections.php");

        foreach (var table in new[] { "epc_credit_profiles" })
        {
            Assert.Equal(Normalize(ExtractDdl(credit, table)), Normalize(ExtractDdl(cs, table)));
        }

        foreach (var table in new[] { "epc_coll_cases", "epc_coll_activity", "epc_coll_dunning", "epc_coll_hold" })
        {
            Assert.Equal(Normalize(ExtractDdl(coll, table)), Normalize(ExtractDdl(cs, table)));
        }

        foreach (Match m in Regex.Matches(credit, @"'([a-z_]+)' => ""([^""]+)"","))
        {
            Assert.Contains("(\"" + m.Groups[1].Value + "\", \"" + m.Groups[2].Value + "\")", cs);
        }
    }

    private static string ExtractDdl(string source, string table)
    {
        var start = source.IndexOf("CREATE TABLE IF NOT EXISTS `" + table + "`", StringComparison.Ordinal);
        Assert.True(start >= 0, table);
        var end = source.IndexOf("ENGINE=InnoDB", start, StringComparison.Ordinal);
        return source.Substring(start, end - start);
    }

    private static string Normalize(string ddl) =>
        Regex.Replace(ddl.Replace("\" +", "").Replace("\"", ""), @"\s+", "");
}
