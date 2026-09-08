using System.Globalization;
using System.Text;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Migration;
using EcomAE.Platform.Routing;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Presentation;

/// <summary>Off-system import / intake / template download for External Reporting. No shop writes.</summary>
public sealed class ErpExternalReportingFormService
{
    private readonly ILegacySessionValidator _sessions;
    private readonly IErpExternalReportingPackCache _packs;

    public ErpExternalReportingFormService(ILegacySessionValidator sessions, IErpExternalReportingPackCache packs)
    {
        _sessions = sessions;
        _packs = packs;
    }

    public IResult Template(HttpContext context)
    {
        var kind = (context.Request.Query["kind"].ToString() ?? "vat").Trim().ToLowerInvariant();
        if (kind is not ("vat" or "ct" or "fin")) kind = "vat";
        var name = kind switch
        {
            "ct" => "CT_return_import_template.csv",
            "fin" => "IFRS18_financials_import_template.csv",
            _ => "VAT201_import_template.csv",
        };
        var csv = ErpExternalReportingCatalog.ImportTemplateCsv(kind);
        return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", name);
    }

    public async Task<IResult> ImportAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var session = await _sessions.ValidateAsync(context, cancellationToken);
        var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, EcomAeRoutes.ErpTaxExternalReportingApp + "?tool=import");
        if (session.Kind != LegacySessionKind.Admin
            || !(session.Capabilities.Contains("erp") || session.Capabilities.Contains("cp")))
        {
            return Results.Redirect("/erp/login");
        }

        var kind = (DryRunHtmlForm.Read(context.Request, "kind") ?? "vat").Trim().ToLowerInvariant();
        if (kind is not ("vat" or "ct" or "fin")) kind = "vat";
        var file = context.Request.HasFormContentType
            ? context.Request.Form.Files["imp_file"]
            : null;
        if (file is null || file.Length == 0)
        {
            return Fail(ret, kind, "Pick a .xlsx or .csv file using the provided template.");
        }

        if (file.Length > 64 * 1024 * 1024)
        {
            return Fail(ret, kind, "Upload failed — file must be under 64 MB.");
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        ErpExternalReportingImportMap map;
        try
        {
            map = ErpExternalReportingImport.ParseFile(ms.ToArray(), file.FileName);
        }
        catch
        {
            return Fail(ret, kind, "Could not read the file. Save it as .xlsx or .csv using the provided template and try again.");
        }

        var detected = ErpExternalReportingImport.DetectKind(map, kind);
        var notice = detected != kind
            ? "Detected " + detected.ToUpperInvariant() + " lines in the file — switched the builder."
            : "";
        kind = detected;
        var invalid = ErpExternalReportingImport.ValidateKind(map, kind);
        if (invalid is not null) return Fail(ret, kind, invalid);

        var ccy = map.Meta.TryGetValue("META_CURRENCY", out var mc) && mc.Length > 0 ? mc : "AED";
        var country = map.Meta.TryGetValue("META_COUNTRY", out var cc) && cc.Length > 0
            ? ErpExternalReportingCatalog.NormalizeCountry(cc)
            : "AE";
        var built = ErpExternalReportingImport.Build(kind, map, ccy, country);
        var id = _packs.Store(built, kind, notice);
        return Results.Redirect(Append(ret, "tool=import&kind=" + Uri.EscapeDataString(kind) + "&pack=" + id
            + (notice.Length > 0 ? "&ok=" + Uri.EscapeDataString(notice) : "")));
    }

    public async Task<IResult> IntakeAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var session = await _sessions.ValidateAsync(context, cancellationToken);
        var ret = DryRunHtmlForm.SafeReturnUrl(context.Request, EcomAeRoutes.ErpTaxExternalReportingApp + "?tool=intake");
        if (session.Kind != LegacySessionKind.Admin
            || !(session.Capabilities.Contains("erp") || session.Capabilities.Contains("cp")))
        {
            return Results.Redirect("/erp/login");
        }

        var entity = DryRunHtmlForm.Read(context.Request, "entity");
        var units = DryRunHtmlForm.Read(context.Request, "units");
        var yearRaw = DryRunHtmlForm.Read(context.Request, "year");
        if (!int.TryParse(yearRaw, out var year) || year < 2000 || year > 2100)
            year = DateTime.UtcNow.Year;
        var built = ErpExternalReportingImport.BuildIntake(entity, year, units, "AED", "AE");
        var id = _packs.Store(built, "fin", "Built from guided intake.");
        return Results.Redirect(Append(ret, "tool=intake&pack=" + id + "&ok=" + Uri.EscapeDataString("Built from guided intake · IFRS 18-compliant.")));
    }

    public async Task<IResult> XlsxAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var session = await _sessions.ValidateAsync(context, cancellationToken);
        if (session.Kind != LegacySessionKind.Admin
            || !(session.Capabilities.Contains("erp") || session.Capabilities.Contains("cp")))
        {
            return Results.Redirect("/erp/login");
        }

        var kind = (context.Request.Query["kind"].ToString() ?? "audit").Trim().ToLowerInvariant();
        if (kind is not ("audit" or "finmodel")) kind = "audit";
        var ccy = (context.Request.Query["ccy"].ToString() ?? "AED").Trim();
        if (ccy.Length == 0) ccy = "AED";
        var entity = context.Request.Query["entity"].ToString();
        if (string.IsNullOrWhiteSpace(entity)) entity = "Company";
        var country = ErpExternalReportingCatalog.NormalizeCountry(context.Request.Query["country"].ToString());
        DateTime from = DateTime.TryParse(context.Request.Query["from"].ToString(), out var pf) ? pf : new DateTime(DateTime.UtcNow.Year, 1, 1);
        DateTime to = DateTime.TryParse(context.Request.Query["to"].ToString(), out var pt) ? pt : new DateTime(DateTime.UtcNow.Year, 12, 31);
        decimal.TryParse(context.Request.Query["sales"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sales);
        decimal.TryParse(context.Request.Query["purch"].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var purch);
        var data = ErpExternalReportingFin.Dataset(from, to, sales, purch);
        var bytes = kind == "finmodel"
            ? ErpExternalReportingXlsx.FinModel(data)
            : ErpExternalReportingXlsx.Audit(data, ccy, entity, country);
        var name = kind == "finmodel"
            ? "Financial_Model_FY" + data.CurYear + ".xlsx"
            : "External_Audit_Report_IFRS_FY" + data.CurYear + ".xlsx";
        return Results.File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    private static IResult Fail(string ret, string kind, string message)
    {
        var sep = ret.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return Results.Redirect(ret + sep + "tool=import&kind=" + Uri.EscapeDataString(kind) + "&err=" + Uri.EscapeDataString(message));
    }

    private static string Append(string url, string qs)
    {
        var sep = url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return url + sep + qs;
    }
}
