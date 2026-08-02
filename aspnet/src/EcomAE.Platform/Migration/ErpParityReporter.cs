namespace EcomAE.Platform.Migration;

public sealed class ErpParityReporter : IErpParityReporter
{
    public ErpParityReport BuildReport()
    {
        return new ErpParityReport(
            "Platform ERP",
            "ecomae.com/ERP, /erp, and cp/content/shop/finance/erp/",
            "/erp/parity plus /erp shell",
            "finance-shell-parity-visible",
            [
                "Canonical ERP route aliases are mapped to the ASP.NET Core shell.",
                "Tenant resolver classifies ERP-only tenants before route cutover.",
                "Surface parity report tracks finance dashboard, chart of accounts, vouchers, invoices, inventory, and reports."
            ],
            [
                "Replay PHP finance dashboard, chart-of-accounts, voucher, invoice, and inventory fixtures.",
                "Port ERP permissions, tenant scoping, exports, and audit evidence.",
                "Validate ERP-only tenant navigation and rollback behavior in staging."
            ]);
    }
}
