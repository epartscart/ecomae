namespace EcomAE.Platform.Erp;

/// <summary>
/// Accounting rule: a posted transaction is never edited or deleted.
/// Correct it with void (cancel in place) or reverse (opposite document).
/// Drafts may still be edited or deleted.
/// </summary>
public static class ErpPostedTransactionPolicy
{
    public const string RefuseModify = "Posted transactions cannot be modified. Void or reverse them.";
    public const string RefuseDelete = "Posted transactions cannot be deleted. Void or reverse them.";
    public const string RefuseVoid = "Only a posted, not-yet-voided transaction can be voided.";
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
                or "sale" or "deposit" or "lease" => value is "draft" or "open" or "planned",
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
            _ => true,
        };
    }

    public static bool CanModify(string? kind, string? status) => IsDraft(kind, status);

    public static bool CanDelete(string? kind, string? status) => IsDraft(kind, status);

    public static bool CanVoid(string? kind, string? status) => IsPosted(kind, status);

    public static bool CanReverse(string? kind, string? status) => IsPosted(kind, status);

    public static string NormalizeKind(string? kind)
        => (kind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "buy" or "acq" => "acquisition",
            "sell" or "disposal" => "disposal",
            "chq" or "pdc" => "cheque",
            "cert" or "ipc" => "certificate",
            var value => value,
        };
}
