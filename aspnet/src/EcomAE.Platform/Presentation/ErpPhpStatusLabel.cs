namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>label-success|info|danger|default</c> mapping for ERP digest statuses.</summary>
public static class ErpPhpStatusLabel
{
    public static string Class(string? status)
    {
        var key = (status ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "done" or "paid" or "posted" or "closed" or "approved" or "active" or "confirmed" => "success",
            "open" or "draft" or "pending" or "in_progress" or "issued" or "unpaid" => "info",
            "overdue" or "rejected" or "void" or "cancelled" or "canceled" or "failed" => "danger",
            _ => "default",
        };
    }
}
