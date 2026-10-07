using System.Data.Common;
using System.Globalization;
using System.Text;
using EcomAE.Platform.Auth;
using EcomAE.Platform.Cp;
using EcomAE.Platform.Data;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Storefront;

/// <summary>One row of PHP <c>epc_payment_list_selectable()</c>.</summary>
public sealed record StorefrontPaymentMethod(int Id, string Handler, string? Name, int Active, string Region);

/// <summary>
/// PHP <c>content/shop/payments/epc_payment_method_picker.php</c> with <c>epc_payment_list_selectable()</c>: the enabled
/// gateways (<c>anable</c> = 1, non-empty handler, the active one first), named through <c>translate_str_by_id</c>, as the
/// "Pay with" select and <c>window.EPC_PAY_HANDLERS</c> that <c>my_order.php</c>, <c>my_order_not_authorized.php</c> and
/// <c>my_balance.php</c> send as <c>pay_handler</c> to <c>ajax_create_operation.php</c>.
/// </summary>
public static class StorefrontPaymentMethodPicker
{
    public static async Task<IReadOnlyList<StorefrontPaymentMethod>> ListSelectableAsync(
        DbConnection connection,
        StorefrontPhpTranslator translator,
        CancellationToken cancellationToken)
    {
        var rows = new List<(int Id, string Name, string Handler, int Active)>();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT `id`, `name`, `handler`, `active` FROM `shop_payment_systems` WHERE `anable` = 1 AND `handler` <> '' ORDER BY `active` DESC, `id` ASC";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                rows.Add((
                    Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture),
                    reader.IsDBNull(1) ? string.Empty : Convert.ToString(reader.GetValue(1), CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToString(reader.GetValue(2), CultureInfo.InvariantCulture) ?? string.Empty,
                    reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader.GetValue(3), CultureInfo.InvariantCulture)));
            }
        }
        catch (DbException)
        {
            return [];
        }

        var methods = new List<StorefrontPaymentMethod>(rows.Count);
        foreach (var row in rows)
        {
            methods.Add(new StorefrontPaymentMethod(
                row.Id,
                row.Handler,
                await translator.RawAsync(row.Name, cancellationToken).ConfigureAwait(false),
                row.Active,
                PhpPaymentGatewayCatalog.Region(row.Handler)));
        }

        return methods;
    }

    /// <summary>
    /// The picker options for the ASP.NET pay and balance pages: the tenant's enabled gateways with the active one
    /// selected, or the built-in demo list when the tenant database cannot be read.
    /// </summary>
    public static async Task<IReadOnlyList<(string Code, string Label, bool Selected)>> OptionsAsync(
        ITenantDbConnectionFactory? connections,
        string lang,
        CancellationToken cancellationToken)
    {
        if (connections is { IsConfigured: true })
        {
            try
            {
                await using var connection = await connections.OpenAsync(null, cancellationToken).ConfigureAwait(false);
                var methods = await ListSelectableAsync(connection, new StorefrontPhpTranslator(connection, lang), cancellationToken).ConfigureAwait(false);
                if (methods.Count > 0)
                {
                    return methods.Select(m => (m.Handler, Label(m), m.Active != 0)).ToList();
                }
            }
            catch (DbException)
            {
            }
        }

        return PhpCustomerWrites.PayGateways.Select(g => (g.Code, g.Label, false)).ToList();
    }

    /// <summary>The option text: the name, " · Crypto" for crypto rails, then the region label in brackets.</summary>
    public static string Label(StorefrontPaymentMethod method)
        => (method.Name ?? string.Empty)
            + (method.Region == PhpPaymentGatewayCatalog.RegionCrypto ? " · Crypto" : string.Empty)
            + " (" + PhpPaymentGatewayCatalog.RegionLabel(method.Region) + ")";

    /// <summary>The bytes the PHP picker include echoes.</summary>
    public static string Render(IReadOnlyList<StorefrontPaymentMethod> methods)
    {
        var html = new StringBuilder();
        html.Append("<div class=\"epc-pay-method-picker\" style=\"margin:12px 0 16px;padding:12px 14px;border:1px solid #e2e8f0;border-radius:10px;background:#f8fafc;\">\n")
            .Append("\t<label for=\"epc_pay_handler\" style=\"display:block;font-weight:700;font-size:13px;color:#0f172a;margin:0 0 8px;\">Pay with</label>\n")
            .Append("\t<select id=\"epc_pay_handler\" class=\"form-control\" style=\"max-width:420px;\">\n")
            .Append("\t\t");
        foreach (var method in methods)
        {
            var label = Label(method);
            html.Append("\t\t<option value=\"").Append(ErpDocumentControlRender.H(method.Handler)).Append("\" ")
                .Append(method.Active != 0 ? "selected" : string.Empty).Append(">\n")
                .Append("\t\t\t").Append(ErpDocumentControlRender.H(label))
                .Append("\t\t</option>\n")
                .Append("\t\t");
        }

        html.Append("\t</select>\n")
            .Append("\t<p style=\"margin:8px 0 0;font-size:12px;color:#64748b;\">Cards, BNPL, wallets, Pakistan methods, and cryptocurrency when enabled in CP.</p>\n")
            .Append("</div>\n")
            .Append("<script>\n")
            .Append("window.EPC_PAY_HANDLERS = ").Append(HandlersJson(methods)).Append(";\n")
            .Append("window.epcSelectedPayHandler = function () {\n")
            .Append("\tvar el = document.getElementById('epc_pay_handler');\n")
            .Append("\treturn el ? String(el.value || '') : '';\n")
            .Append("};\n")
            .Append("</script>\n");
        return html.ToString();
    }

    /// <summary>PHP <c>json_encode</c> of the handler/name/region/active list.</summary>
    public static string HandlersJson(IReadOnlyList<StorefrontPaymentMethod> methods)
    {
        var json = new StringBuilder("[");
        for (var i = 0; i < methods.Count; i++)
        {
            var method = methods[i];
            if (i > 0)
            {
                json.Append(',');
            }

            json.Append("{\"handler\":").Append(OAuthStart.PhpJsonString(method.Handler))
                .Append(",\"name\":").Append(method.Name is null ? "null" : OAuthStart.PhpJsonString(method.Name))
                .Append(",\"region\":").Append(OAuthStart.PhpJsonString(method.Region))
                .Append(",\"active\":").Append(method.Active.ToString(CultureInfo.InvariantCulture))
                .Append('}');
        }

        return json.Append(']').ToString();
    }
}
