using System.Text;

namespace EcomAE.Platform.Erp;

/// <summary>Flat INDUS field → ecomae table.column → module / workflow / GL map, generated from <see cref="ErpJwIndusImportSpec"/>.</summary>
public static class ErpJwIndusFieldMap
{
    public static IEnumerable<string[]> Rows()
    {
        foreach (var t in ErpJwIndusImportSpec.Templates)
        {
            yield return [t.Step.ToString("00", System.Globalization.CultureInfo.InvariantCulture), t.Key, "source_ref", "INDUS key (Branch-VocType-VocNo / code)", "ecomae_jw_indus_import_registry.source_ref", t.Module, t.Workflow, "yes", "Duplicate guard + traceability"];
            if (t.ParentKey is not null)
            {
                yield return [t.Step.ToString("00", System.Globalization.CultureInfo.InvariantCulture), t.Key, "parent_ref", "source_ref of the parent " + t.ParentKey + " row", t.Table + "." + t.ParentColumn, t.Module, t.Workflow, "yes", "Links line to its header"];
            }

            foreach (var c in t.Columns)
            {
                yield return
                [
                    t.Step.ToString("00", System.Globalization.CultureInfo.InvariantCulture), t.Key, c.Header, c.IndusField,
                    c.Column.Length > 0 ? t.Table + "." + c.Column : "(loader only)", t.Module, t.Workflow, c.Required ? "yes" : "", c.Links,
                ];
            }
        }
    }

    public static readonly string[] Header = ["step", "template", "csv_column", "indus_field", "ecomae_table_column", "module", "workflow", "required", "links_to (report / GL / downstream)"];

    public static string Csv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header.Select(Quote)));
        foreach (var row in Rows()) sb.AppendLine(string.Join(",", row.Select(Quote)));
        return sb.ToString();
    }

    private static string Quote(string v) => v.Contains(',') || v.Contains('"') ? "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : v;
}
