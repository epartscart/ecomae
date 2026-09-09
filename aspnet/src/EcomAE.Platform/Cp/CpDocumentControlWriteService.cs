using System.Data.Common;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Cp;

/// <summary>
/// Live PHP <c>ajax_document_control.php</c> twin of <c>epc_dc_save_company</c> and <c>epc_dc_save_template</c>.
/// Logo upload, attachments, and send stay Classic. Schema-ensure stays Classic.
/// This service does not invent a send.
/// </summary>
public interface ICpDocumentControlWriteService
{
    Task<ErpSimpleWriteResult> SaveCompanyAsync(
        CpDocumentCompanySaveRequest request,
        CancellationToken cancellationToken = default);

    Task<ErpSimpleWriteResult> SaveTemplateAsync(
        CpDocumentTemplateSaveRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CpDocumentCompanySaveRequest(
    int ExpectedVersion,
    string? LegalName,
    string? TradeName,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Country,
    string? Trn,
    string? Phone,
    string? Email,
    string? Website,
    string? LogoPath,
    string? BankName,
    string? BankIban,
    string? LegalFooter,
    IReadOnlySet<string>? PostedFields);

public sealed record CpDocumentTemplateSaveRequest(
    string? Code,
    string? Title,
    string? Description,
    string? HeaderHtml,
    string? BodyHtml,
    string? FooterHtml,
    string? CssExtra,
    bool Active,
    IReadOnlySet<string>? PostedFields);

public sealed class CpDocumentControlWriteService : ICpDocumentControlWriteService
{
    public static readonly IReadOnlyDictionary<string, int> FieldMax = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["legal_name"] = 255,
        ["trade_name"] = 255,
        ["address_line1"] = 255,
        ["address_line2"] = 255,
        ["city"] = 120,
        ["country"] = 80,
        ["trn"] = 32,
        ["phone"] = 64,
        ["email"] = 120,
        ["website"] = 120,
        ["logo_path"] = 255,
        ["bank_name"] = 120,
        ["bank_iban"] = 64,
        ["legal_footer"] = 4000,
    };

    public static readonly IReadOnlyDictionary<string, int> TemplateFieldMax = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["title"] = 120,
        ["description"] = 255,
        ["header_html"] = 200_000,
        ["body_html"] = 200_000,
        ["footer_html"] = 200_000,
        ["css_extra"] = 16_000,
    };

    private readonly IErpWriteConnectionFactory _connections;

    public CpDocumentControlWriteService(IErpWriteConnectionFactory connections)
    {
        _connections = connections;
    }

    public static string Clip(string? raw, int max)
    {
        var value = (raw ?? string.Empty).Trim();
        return value.Length <= max ? value : value[..max];
    }

    public static string ClipRaw(string? raw, int max)
    {
        var value = raw ?? string.Empty;
        return value.Length <= max ? value : value[..max];
    }

    public static string NormalizeTemplateCode(string? code)
        => Clip(code, 32);

    public async Task<ErpSimpleWriteResult> SaveCompanyAsync(
        CpDocumentCompanySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var posted = request.PostedFields;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["legal_name"] = Clip(request.LegalName, FieldMax["legal_name"]),
            ["trade_name"] = Clip(request.TradeName, FieldMax["trade_name"]),
            ["address_line1"] = Clip(request.AddressLine1, FieldMax["address_line1"]),
            ["address_line2"] = Clip(request.AddressLine2, FieldMax["address_line2"]),
            ["city"] = Clip(request.City, FieldMax["city"]),
            ["country"] = Clip(request.Country, FieldMax["country"]),
            ["trn"] = Clip(request.Trn, FieldMax["trn"]),
            ["phone"] = Clip(request.Phone, FieldMax["phone"]),
            ["email"] = Clip(request.Email, FieldMax["email"]),
            ["website"] = Clip(request.Website, FieldMax["website"]),
            ["logo_path"] = Clip(request.LogoPath, FieldMax["logo_path"]),
            ["bank_name"] = Clip(request.BankName, FieldMax["bank_name"]),
            ["bank_iban"] = Clip(request.BankIban, FieldMax["bank_iban"]),
            ["legal_footer"] = Clip(request.LegalFooter, FieldMax["legal_footer"]),
        };

        var sets = new List<string>();
        var args = new List<object?>();
        foreach (var (column, value) in values)
        {
            if (posted is not null && !posted.Contains(column))
            {
                continue;
            }

            sets.Add("`" + column + "`=?");
            args.Add(value);
        }

        if (sets.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No company fields to save.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        sets.Add("`updated_at`=?");
        args.Add(now);
        args.Add(1);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_document_company` WHERE `id`=?"),
                cancellationToken, 1L).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Company profile is missing — schema-ensure stays Classic.");
            }

            var versioned = await TryBumpVersionAsync(connection, request.ExpectedVersion, cancellationToken).ConfigureAwait(false);
            if (!versioned.Succeeded)
            {
                return versioned;
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_document_company` SET " + string.Join(", ", sets) + " WHERE `id`=?"),
                cancellationToken,
                args.ToArray()).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Company profile saved", 1);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Document-control company table is missing — schema-ensure stays Classic.");
        }
    }

    public async Task<ErpSimpleWriteResult> SaveTemplateAsync(
        CpDocumentTemplateSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var code = NormalizeTemplateCode(request.Code);
        if (code.Length == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "Template code required");
        }

        if (!_connections.IsConfigured)
        {
            return ErpSimpleWriteResult.Fail("db", "TenantRegistry DB is not configured.");
        }

        var posted = request.PostedFields;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["title"] = Clip(request.Title, TemplateFieldMax["title"]),
            ["description"] = Clip(request.Description, TemplateFieldMax["description"]),
            ["header_html"] = ClipRaw(request.HeaderHtml, TemplateFieldMax["header_html"]),
            ["body_html"] = ClipRaw(request.BodyHtml, TemplateFieldMax["body_html"]),
            ["footer_html"] = ClipRaw(request.FooterHtml, TemplateFieldMax["footer_html"]),
            ["css_extra"] = ClipRaw(request.CssExtra, TemplateFieldMax["css_extra"]),
            ["active"] = request.Active ? 1 : 0,
        };

        var sets = new List<string>();
        var args = new List<object?>();
        foreach (var (column, value) in values)
        {
            if (posted is not null && !posted.Contains(column))
            {
                continue;
            }

            sets.Add("`" + column + "`=?");
            args.Add(value);
        }

        if (sets.Count == 0)
        {
            return ErpSimpleWriteResult.Fail("invalid", "No template fields to save.");
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        sets.Add("`updated_at`=?");
        args.Add(now);
        args.Add(code);

        try
        {
            await using var connection = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);
            var exists = await ErpDb.LongAsync(
                connection, null,
                ErpDb.Positional("SELECT COUNT(*) FROM `epc_document_templates` WHERE `code`=?"),
                cancellationToken, code).ConfigureAwait(false);
            if (exists <= 0)
            {
                return ErpSimpleWriteResult.Fail("not_found", "Template was not found — create stays Classic.");
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_document_templates` SET " + string.Join(", ", sets) + " WHERE `code`=?"),
                cancellationToken,
                args.ToArray()).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("Template saved", 0);
        }
        catch (DbException)
        {
            return ErpSimpleWriteResult.Fail("db", "Document-control template table is missing — schema-ensure stays Classic.");
        }
    }

    private static async Task<ErpSimpleWriteResult> TryBumpVersionAsync(
        DbConnection connection,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            if (expectedVersion > 0)
            {
                var n = await ErpDb.ExecuteAsync(
                    connection, null,
                    ErpDb.Positional("UPDATE `epc_document_company` SET `row_version`=`row_version`+1 WHERE `id`=? AND `row_version`=?"),
                    cancellationToken, 1L, expectedVersion).ConfigureAwait(false);
                if (n < 1)
                {
                    return ErpSimpleWriteResult.Fail(
                        "conflict",
                        "Version conflict — another user saved this record. Reload and re-apply your changes.");
                }

                return ErpSimpleWriteResult.Ok("ok", 1);
            }

            await ErpDb.ExecuteAsync(
                connection, null,
                ErpDb.Positional("UPDATE `epc_document_company` SET `row_version`=`row_version`+1 WHERE `id`=?"),
                cancellationToken, 1L).ConfigureAwait(false);
            return ErpSimpleWriteResult.Ok("ok", 1);
        }
        catch (DbException)
        {
            // row_version add-column stays Classic; PHP still writes the profile fields.
            return expectedVersion > 0
                ? ErpSimpleWriteResult.Fail("db", "Company row_version is missing — schema-ensure stays Classic.")
                : ErpSimpleWriteResult.Ok("ok", 1);
        }
    }
}
