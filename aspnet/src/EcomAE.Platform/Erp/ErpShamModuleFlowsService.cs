namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM blueprint §14: module data-flow coverage. Reports, per ERP
/// module, whether its tables exist and hold live data on this tenant —
/// the "no empty module" check in data form. Read-only; tenant-generic
/// (each module probes its own tables and degrades to missing/absent
/// instead of erroring). Grant: <c>vat-view</c> family — any ERP admin.
/// </summary>
public interface IErpShamModuleFlowsService
{
    Task<ErpShamModuleFlowsResult> LoadAsync(string userEmail, CancellationToken cancellationToken = default);
}

public sealed record ErpShamModuleFlow(string Module, string Table, long Rows, bool Linked, string Feeds);
public sealed record ErpShamModuleFlowsResult(bool Ok, IReadOnlyList<ErpShamModuleFlow> Modules, int Live, int Total);

public sealed class ErpShamModuleFlowsService : IErpShamModuleFlowsService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;

    private static readonly (string Module, string Table, string Feeds)[] Probes =
    {
        ("organisation",    "epc_erp_pm_legal_entities",        "entity/BU scope for every module"),
        ("business-units",  "epc_erp_pm_business_units",       "cost centres, default warehouses"),
        ("access-control",  "epc_erp_org_user_access",          "entity×BU×action grants on all writes"),
        ("chart-of-accounts","epc_erp_gl_coa",                  "postings, VAT, statements"),
        ("general-ledger",  "epc_erp_gl_journals",              "TB, P&L/BS, VAT summary, eliminations"),
        ("period-close",    "epc_erp_org_period_locks",         "blocks posts into locked periods"),
        ("inventory-holds", "epc_erp_sham_stock_holds",         "reservation → invoice consume"),
        ("purchasing",      "epc_erp_purchases",                "po-receive → stock + AP"),
        ("sales-invoices",  "epc_erp_sham_sales_invoices",      "invoice-post → GL, e-invoice, VAT"),
        ("intercompany",    "epc_erp_gl_journals",              "IC-* references → elim-run"),
        ("consolidations",  "epc_entity_members",               "group perimeter for TB + eliminations"),
        ("treasury",        "epc_erp_org_treasury_instruments", "LC/CAD/FX exposure per entity"),
        ("pricing",         "epc_erp_org_msp_versions",         "margin policy per entity×BU"),
        ("landed-cost",     "epc_erp_org_landed_costs",         "receipt charges → item landed cost"),
        ("transfer-price",  "epc_erp_org_transfer_price_rules", "entity→entity IC pricing rules"),
        ("warehouses",      "epc_erp_inv_warehouses",           "BU default warehouse, stock"),
        ("bank-accounts",   "epc_erp_org_entity_bank_accounts", "treasury + payments per entity"),
    };

    public ErpShamModuleFlowsService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation)
    {
        _connections = connections;
        _foundation = foundation;
    }

    public async Task<ErpShamModuleFlowsResult> LoadAsync(string userEmail, CancellationToken cancellationToken = default)
    {
        if (!await _foundation.AccessAllowedAsync(userEmail, null, null, "vat-view", cancellationToken).ConfigureAwait(false))
        {
            return new(false, Array.Empty<ErpShamModuleFlow>(), 0, 0);
        }
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        var flows = new List<ErpShamModuleFlow>(Probes.Length);
        var live = 0;
        foreach (var (module, table, feeds) in Probes)
        {
            long rows = -1;
            try
            {
                if (await ErpTableCatalog.ContainsAsync(conn, table, cancellationToken).ConfigureAwait(false))
                {
                    rows = await ErpDb.LongAsync(conn, null, "SELECT COUNT(*) FROM `" + table + "`", cancellationToken).ConfigureAwait(false);
                }
            }
            catch { rows = -1; }
            var linked = rows > 0;
            if (linked) live++;
            flows.Add(new ErpShamModuleFlow(module, table, rows, linked, feeds));
        }
        return new(true, flows, live, Probes.Length);
    }
}
