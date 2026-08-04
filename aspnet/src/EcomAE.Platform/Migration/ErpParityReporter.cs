namespace EcomAE.Platform.Migration;

public sealed class ErpParityReporter : IErpParityReporter
{
    public ErpParityReport BuildReport()
    {
        return new ErpParityReport(
            "Platform ERP",
            "ecomae.com/ERP, /erp, and cp/content/shop/finance/erp/",
            "/erp/parity plus admin-session-gated /erp shell",
            "presentation-shell-scaffolded-awaiting-staging",
            [
                "Canonical ERP route aliases are mapped to the ASP.NET Core shell.",
                "ERP shell requires admin session via DbBackedLegacySessionValidator (401 when anonymous).",
                "ERP shell negotiates presentation-preserving HTML (PHP erp_theme / bootstrap_admin CSS) while defaulting to JSON for tooling.",
                "Read-only digests cover dashboard, accounts, suppliers, purchases, cash, invoices, GL, COA, warehouses, sales-orders, purchase-orders, and inventory-stock KPIs.",
                "Tenant resolver classifies ERP-only tenants before route cutover.",
                "On-premises ERP product track is separate from SaaS ERP-only: /erp/on-premises-app scaffold + /migration/on-premises-parity board; installer/license/health remain PHP-authoritative.",
                "Surface parity report tracks finance dashboard, chart of accounts, vouchers, invoices, inventory, and reports."
            ],
            [
                "On CloudPanel: ensure_epc_api_clients_table.sh → issue_smoke_credentials.sh → validate_final_gate_env.sh → capture surface digests.",
                "Replay PHP finance dashboard, chart-of-accounts, voucher, invoice, and inventory fixtures.",
                "Port ERP permissions, tenant scoping, exports, and audit evidence.",
                "Validate ERP-only tenant navigation and rollback behavior in staging.",
                "Dual-sample on-premises tab + health dry-run; ASP.NET Core installer pack is a later NextBuild (never invent cutover).",
                "Promote only location = digests via nginx-surface-digests-shadow-example.conf (never broad /erp)."
            ]);
    }
}
