using System.Globalization;
using System.Text;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Resolves spoken customer, supplier, and bank names to master ids, then
/// builds the existing page link. Posting still happens on that page.
/// </summary>
public sealed class ErpVoicePrepareService
{
    private readonly IErpMasterLookupReadService _lookups;

    public ErpVoicePrepareService(IErpMasterLookupReadService lookups)
    {
        _lookups = lookups;
    }

    public async Task<ErpVoiceInterpreter.Draft> PrepareAsync(
        string? transcript,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        var draft = ErpVoiceInterpreter.Interpret(transcript, today);
        if (draft.Intent is not ("receipt" or "payment"))
        {
            return draft;
        }

        var slots = new Dictionary<string, string>(draft.Slots, StringComparer.Ordinal);
        var missing = draft.Missing.ToList();
        var notes = new List<string>();

        if (draft.Intent == "receipt")
        {
            await ResolveAsync(slots, missing, notes, "customer", "customer", "user_id", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ResolveAsync(slots, missing, notes, "supplier", "supplier", "supplier_id", cancellationToken).ConfigureAwait(false);
        }

        await ResolveAsync(slots, missing, notes, "account", "account", "account_id", cancellationToken).ConfigureAwait(false);

        var ready = missing.Count == 0 && slots.ContainsKey("amount");
        var href = ready ? BuildHref(draft.Intent, slots) : RequirementHref(draft.Intent);
        var say = BuildSay(draft.Intent, slots, missing, notes, ready);
        return draft with
        {
            Say = say,
            Href = href,
            Ready = ready,
            Missing = missing,
            Slots = slots,
        };
    }

    private async Task ResolveAsync(
        Dictionary<string, string> slots,
        List<string> missing,
        List<string> notes,
        string slot,
        string kind,
        string idSlot,
        CancellationToken cancellationToken)
    {
        if (!slots.TryGetValue(slot, out var name) || name.Length == 0)
        {
            return;
        }

        var found = await _lookups.SearchAsync(kind, name, 5, cancellationToken).ConfigureAwait(false);
        var pick = Pick(found.Options, name, out var ambiguous, out var choices);
        if (pick is not null)
        {
            slots[idSlot] = pick.Id.ToString(CultureInfo.InvariantCulture);
            slots[slot] = pick.Name.Length > 0 ? pick.Name : name;
            missing.Remove(slot);
            return;
        }

        if (!missing.Contains(slot))
        {
            missing.Add(slot);
        }

        notes.Add(ambiguous
            ? "More than one " + slot + " matches " + name + ": " + string.Join(", ", choices) + "."
            : "No " + slot + " named " + name + " is on file.");
    }

    private static ErpMasterOption? Pick(
        IReadOnlyList<ErpMasterOption> rows,
        string query,
        out bool ambiguous,
        out IReadOnlyList<string> choices)
    {
        choices = rows.Select(row => row.Name).Where(name => name.Length > 0).Take(3).ToArray();
        var exact = rows.Where(row =>
            string.Equals(row.Name, query, StringComparison.OrdinalIgnoreCase)
            || string.Equals(row.Code, query, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1)
        {
            ambiguous = false;
            return exact[0];
        }

        if (rows.Count == 1)
        {
            ambiguous = false;
            return rows[0];
        }

        ambiguous = rows.Count > 1;
        return null;
    }

    private static string BuildHref(string intent, IReadOnlyDictionary<string, string> slots)
    {
        var query = new List<string> { "voice=" + intent };
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Add(key + "=" + Uri.EscapeDataString(value));
            }
        }

        if (intent == "receipt")
        {
            Add("rv_user", Slot(slots, "user_id"));
            Add("rv_account", Slot(slots, "account_id"));
            Add("rv_amount", Slot(slots, "amount"));
            Add("rv_invoice", Slot(slots, "invoice"));
            Add("rv_order", Slot(slots, "order"));
            Add("rv_note", Slot(slots, "note"));
            Add("rv_date", Slot(slots, "date"));
            if (Slot(slots, "advance") == "1")
            {
                query.Add("rv_advance=1");
            }

            if (Slot(slots, "allocate") == "1")
            {
                query.Add("rv_allocate=1");
            }

            return "/erp/cash-entries-app?" + string.Join("&", query) + "#receipt-voucher";
        }

        Add("pv_supplier", Slot(slots, "supplier_id"));
        Add("pv_account", Slot(slots, "account_id"));
        Add("pv_amount", Slot(slots, "amount"));
        Add("pv_bill", Slot(slots, "bill"));
        Add("pv_purchase", Slot(slots, "purchase"));
        Add("pv_note", Slot(slots, "note"));
        Add("pv_date", Slot(slots, "date"));
        if (Slot(slots, "advance") == "1")
        {
            query.Add("pv_advance=1");
        }

        if (Slot(slots, "allocate") == "1")
        {
            query.Add("pv_allocate=1");
        }

        return "/erp/cash-entries-app?" + string.Join("&", query) + "#payment-voucher";
    }

    private static string BuildSay(
        string intent,
        IReadOnlyDictionary<string, string> slots,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> notes,
        bool ready)
    {
        var title = intent == "receipt" ? "Receipt voucher" : "Payment voucher";
        var party = intent == "receipt" ? "customer" : "supplier";
        var builder = new StringBuilder();
        builder.Append(title).Append(". ");
        foreach (var note in notes)
        {
            builder.Append(note).Append(' ');
        }

        if (ready)
        {
            builder.Append("Ready for ")
                .Append(Slot(slots, party))
                .Append(", account ")
                .Append(Slot(slots, "account"))
                .Append(", amount ")
                .Append(Slot(slots, "amount"))
                .Append(". The form is filled. Press Validate and post when it looks right. Nothing is posted by voice alone.");
            return builder.ToString();
        }

        builder.Append("Still needed: ").Append(missing.Count == 0 ? "a matching master" : string.Join(", ", missing)).Append('.');
        return builder.ToString();
    }

    private static string RequirementHref(string intent)
        => intent == "payment" ? "/erp/cash-entries-app#payment-voucher" : "/erp/cash-entries-app#receipt-voucher";

    private static string Slot(IReadOnlyDictionary<string, string> slots, string key)
        => slots.TryGetValue(key, out var value) ? value : string.Empty;
}
