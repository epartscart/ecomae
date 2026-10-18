namespace EcomAE.Platform.Erp;

/// <summary>
/// Posted transactions cannot be edited in place.
/// The register keeps both Void (row stays on file) and Delete (row is removed).
/// Reverse remains available for posted documents.
/// </summary>
public static class ErpPostedTransactionPolicy
{
    public const string RefuseModify = "Posted transactions cannot be modified. Void or delete them.";
    public const string RefuseDelete = "This transaction is already voided or reversed.";
    public const string RefuseVoid = "This transaction is already voided or reversed.";
    public const string AlreadyClosed = "This transaction is already voided or reversed.";

    public static bool IsVoided(string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        return value is "voided" or "void" or "reversed" or "reversal";
    }

    public static bool IsDraft(string? kind, string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (IsVoided(value))
        {
            return false;
        }

        return NormalizeKind(kind) switch
        {
            "acquisition" or "disposal" or "valuation" or "certificate" or "contract"
                or "sale" or "deposit" or "lease" or "boq" or "milestone" => value is "draft" or "open" or "planned",
            "instalment" => value is "due" or "open" or "draft",
            "escrow" => value is "draft" or "open",
            "allocation" => value is "draft" or "open",
            "opportunity" => value is "open" or "screening" or "offer" or "due_diligence",
            "cheque" => value is "received" or "held" or "due_for_deposit" or "deposited",
            _ => value is "draft" or "open",
        };
    }

    public static bool IsPosted(string? kind, string? status)
    {
        var value = (status ?? string.Empty).Trim().ToLowerInvariant();
        if (value.Length == 0 || IsVoided(value) || IsDraft(kind, value))
        {
            return false;
        }

        return NormalizeKind(kind) switch
        {
            "acquisition" or "disposal" => value is "completed",
            "valuation" or "certificate" => value is "approved",
            "opportunity" => value is "acquired",
            "lease" => value is "active" or "renewed" or "terminated",
            "cheque" => value is "cleared" or "bounced" or "replaced" or "returned",
            "sale" => value is "reserved" or "sold" or "handed_over",
            "deposit" => value is "held" or "refunded" or "applied",
            "contract" => value is "active" or "certified",
            "escrow" => value is "received" or "disbursed",
            "instalment" => value is "collected",
            "allocation" => value is "allocated",
            "boq" => value is "approved",
            "milestone" => value is "reached",
            _ => true,
        };
    }

    public static bool CanModify(string? kind, string? status) => IsDraft(kind, status);

    public static bool CanDelete(string? kind, string? status)
        => IsLedgerDocument(kind) && !IsVoided(status);

    public static bool CanVoid(string? kind, string? status)
        => IsLedgerDocument(kind) && !IsVoided(status);

    public static bool CanReverse(string? kind, string? status) => IsPosted(kind, status);

    public static bool IsLedgerDocument(string? kind)
        => NormalizeKind(kind) is "acquisition" or "disposal" or "valuation" or "certificate"
            or "contract" or "sale" or "deposit" or "lease" or "cheque" or "opportunity"
            or "boq" or "escrow" or "milestone" or "instalment" or "allocation";

    public static string NormalizeKind(string? kind)
        => (kind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "buy" or "acq" => "acquisition",
            "sell" or "disposal" => "disposal",
            "chq" or "pdc" => "cheque",
            "cert" or "ipc" => "certificate",
            "boq_line" or "tender" => "boq",
            "escrow_txn" or "escrow_receipt" or "escrow_disbursement" => "escrow",
            "sale_instalment" or "offplan" => "instalment",
            "cost_alloc" or "shared_cost" => "allocation",
            var value => value,
        };
}
