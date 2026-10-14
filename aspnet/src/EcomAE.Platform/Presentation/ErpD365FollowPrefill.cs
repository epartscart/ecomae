using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

public sealed record ErpD365FollowPrefill(
    string SourceTab,
    long SourceId,
    string SourceCode,
    string Relation,
    string Party,
    string Name,
    string Worker,
    decimal Qty,
    decimal Amount,
    string Note)
{
    public bool HasValues =>
        SourceId > 0
        || SourceTab.Length > 0
        || SourceCode.Length > 0
        || Party.Length > 0
        || Name.Length > 0
        || Amount != 0
        || Qty != 0
        || Note.Length > 0;

    public string Banner
    {
        get
        {
            var source = SourceCode.Length > 0 ? SourceCode : SourceTab;
            var rel = Relation.Length > 0 ? Relation : "followed";
            var text = rel + (source.Length > 0 ? " from " + source : "");
            if (Party.Length > 0)
            {
                text += " · " + Party;
            }

            if (Name.Length > 0)
            {
                text += " · " + Name;
            }

            if (Amount != 0)
            {
                text += " · " + Amount.ToString("0.##", CultureInfo.InvariantCulture);
            }

            return text.Trim();
        }
    }

    public static ErpD365FollowPrefill Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var q = request.Query;
        return new(
            (q["source_tab"].ToString() ?? string.Empty).Trim(),
            long.TryParse(q["source_id"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0,
            (q["source_code"].ToString() ?? string.Empty).Trim(),
            (q["relation"].ToString() ?? string.Empty).Trim(),
            (q["party"].ToString() ?? string.Empty).Trim(),
            (q["name"].ToString() ?? string.Empty).Trim(),
            (q["worker"].ToString() ?? string.Empty).Trim(),
            decimal.TryParse(q["qty"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var qty) ? qty : 0,
            decimal.TryParse(q["amount"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var amount) ? amount : 0,
            (q["note"].ToString() ?? string.Empty).Trim());
    }
}
