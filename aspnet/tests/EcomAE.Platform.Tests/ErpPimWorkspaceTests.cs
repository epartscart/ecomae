using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;
using Xunit;

namespace EcomAE.Platform.Tests;

public class ErpPimWorkspaceTests
{
    [Fact]
    public void DataTabs_CoverEveryPimListTab()
    {
        // Every PIM page that is not a dedicated form must either serve real
        // data or be a deliberate generic workspace (cases, setup masters,
        // launch-action tabs, scratch uploads).
        var generic = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pim_all_cases", "pim_my_cases", "pim_open_cases",
            "pim_paper_setup", "pim_not_stocked", "pim_cleanup_conv",
            "pim_cost_conv", "pim_std_cost_conv", "pim_track_group",
            "pim_populate_distinct", "pim_cleanup_attr", "pim_populate_attr",
            "pim_price_to_date", "pim_parameters", "pim_compliance",
            "pim_restricted", "pim_sds_validity", "pim_coo_vendor",
            "pim_dual_use", "pim_approved_cust", "pim_approved_item",
            "pim_haz_class", "pim_haz_label", "pim_haz_packing", "pim_warranty",
            "pim_product_upload",
        };

        var uncovered = ErpD365WorkspaceCatalog.ForArea("pim")
            .Where(p => p.ExistingHref is null)
            .Select(p => p.Tab)
            .Where(t => !generic.Contains(t))
            .Where(t => !ErpPimWorkspaceReadService.Specs.ContainsKey(t))
            .ToList();

        Assert.True(uncovered.Count == 0, "PIM tabs with no data backing: " + string.Join(", ", uncovered));
    }

    [Fact]
    public void Specs_SqlTargetsRealTables()
    {
        foreach (var (tab, spec) in ErpPimWorkspaceReadService.Specs)
        {
            Assert.StartsWith("SELECT", spec.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("epc_erp_", spec.Sql, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(spec.EmptyHint), tab);
            Assert.True(spec.Headers.Count > 0, tab);
        }
    }

    [Fact]
    public void Upload_ParsesHeaderAndRows()
    {
        var csv = "sku,name,item_type,product_type,item_group,unit,purchase_unit,sales_unit,sales_price,costing_method,barcode,reorder_level,track_expiry,active\n" +
                  "BRG-6203,Ball bearing,standard,item,fasteners,pcs,pcs,pcs,45.00,average,8714101000,10,0,1\n" +
                  "OIL-5W30,\"Engine oil, synthetic\",perishable,item,lube,L,L,L,12.5,fifo,6006621000,5,1,yes";
        var (rows, errors) = ErpPimProductUploadService.Parse(csv);
        Assert.Empty(errors);
        Assert.Equal(2, rows.Count);
        Assert.Equal("BRG-6203", rows[0].Sku);
        Assert.Equal("Engine oil, synthetic", rows[1].Name);
        Assert.Equal("perishable", rows[1].ItemType);
        Assert.Equal("lube", rows[1].ItemGroup);
        Assert.Equal(12.5m, rows[1].SalesPrice);
        Assert.Equal("6006621000", rows[1].Barcode);
        Assert.True(rows[1].TrackExpiry);
        Assert.True(rows[1].Active);
    }

    [Fact]
    public void Upload_PartialRow_OnlyProvidesGivenColumns()
    {
        var (rows, errors) = ErpPimProductUploadService.Parse("A-1,Only name\nB-2,With price,,,,,,,99.5");
        Assert.Empty(errors);
        Assert.Equal(0, rows[0].Provided & 4095);          // no optional column given
        Assert.Equal(64, rows[1].Provided & 64);           // sales_price given
        Assert.Equal(0, rows[1].Provided & ~64 & 4095);    // nothing else
    }

    [Fact]
    public void Upload_MalformedNumber_IsErrorNotZero()
    {
        var (rows, errors) = ErpPimProductUploadService.Parse("A-1,Name,,,,,,,abc");
        Assert.Empty(rows);
        Assert.Contains(errors, e => e.Contains("sales_price"));
    }

    [Fact]
    public void Upload_RejectsMissingSkuAndDuplicate()
    {
        var (rows, errors) = ErpPimProductUploadService.Parse(
            "sku,name\n,Nope\nA-1,One\nA-1,Dup");
        Assert.Single(rows);
        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Upload_EmptyCsv_Fails()
    {
        var result = new ErpPimProductUploadService(
            new StubConnections(), new StubAudit()).DryRun("");
        Assert.False(result.Ok);
    }

    private sealed class StubConnections : IErpWriteConnectionFactory
    {
        public bool IsConfigured => false;
        public Task<System.Data.Common.DbConnection> OpenAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubAudit : IErpAuditLogWriter
    {
        public Task LogAsync(System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction? transaction,
            int adminId, string action, string entityType, long entityId, string summary,
            IReadOnlyDictionary<string, string?>? detail, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
