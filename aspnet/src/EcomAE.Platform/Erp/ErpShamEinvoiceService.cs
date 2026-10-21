using System.Globalization;
using System.Text;
using System.Xml;

namespace EcomAE.Platform.Erp;

/// <summary>
/// SHAM blueprint §11: UAE FTA e-invoicing. Renders an issued sales
/// invoice (<c>epc_erp_sham_sales_invoices</c>) as UBL 2.1 XML — the
/// document format Peppol/FTA accredited service providers accept.
/// Emits whatever the doc store holds (seller entity, buyer name,
/// totals, VAT); TRN fields are left for the tenant's entity registry
/// data and omitted when absent.
/// Grant: <c>einvoice-view</c>.
/// </summary>
public interface IErpShamEinvoiceService
{
    Task<ErpShamEinvoiceResult> RenderAsync(string entityCode, string invoiceRef, string userEmail, CancellationToken cancellationToken = default);
}

public sealed record ErpShamEinvoiceResult(bool Ok, string Message, string Xml, string InvoiceNumber);

public sealed class ErpShamEinvoiceService : IErpShamEinvoiceService
{
    private readonly IErpWriteConnectionFactory _connections;
    private readonly IErpShamFoundationService _foundation;

    public ErpShamEinvoiceService(IErpWriteConnectionFactory connections, IErpShamFoundationService foundation)
    {
        _connections = connections;
        _foundation = foundation;
    }

    public async Task<ErpShamEinvoiceResult> RenderAsync(string entityCode, string invoiceRef, string userEmail, CancellationToken cancellationToken = default)
    {
        if (!await _foundation.AccessAllowedAsync(userEmail, entityCode, null, "einvoice-view", cancellationToken).ConfigureAwait(false))
        {
            return new(false, "einvoice-view grant required for this entity.", "", invoiceRef ?? "");
        }
        await using var conn = await _connections.OpenAsync(cancellationToken).ConfigureAwait(false);

        var entityId = 0; var entityName = ""; var entityCountry = ""; var entityCurrency = "";
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional("SELECT `id`,`name`,`country_code`,`currency_code` FROM `epc_erp_pm_legal_entities` WHERE `code`=? AND `active`=1 LIMIT 1");
            ErpDb.AddParameters(cmd, (entityCode ?? string.Empty).Trim());
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                entityId = reader.GetInt32(0);
                entityName = reader.GetString(1);
                entityCountry = reader.GetString(2);
                entityCurrency = reader.GetString(3);
            }
        }
        if (entityId <= 0)
        {
            return new(false, "Unknown entity code.", "", invoiceRef ?? "");
        }

        string number = "", customer = "", status = "";
        decimal subtotal = 0, vat = 0, total = 0, fx = 0;
        long issueDate = 0; string currency = entityCurrency;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = ErpDb.Positional(
                "SELECT `invoice_number`,`customer`,`subtotal`,`vat`,`total`,`issue_date`,`currency_code`,`exchange_rate`,`status`"
                + " FROM `epc_erp_sham_sales_invoices` WHERE `entity_id`=? AND (`invoice_number`=? OR `id`=?) LIMIT 1");
            ErpDb.AddParameters(cmd, entityId, (invoiceRef ?? string.Empty).Trim(), long.TryParse(invoiceRef, out var n) ? n : 0L);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return new(false, "No issued invoice " + invoiceRef + " on " + entityCode + ".", "", invoiceRef ?? "");
            }
            number = reader.GetString(0); customer = reader.GetString(1);
            subtotal = reader.GetDecimal(2); vat = reader.GetDecimal(3); total = reader.GetDecimal(4);
            issueDate = reader.GetInt64(5);
            var c = reader.GetString(6); if (c.Length > 0) currency = c;
            fx = reader.GetDecimal(7); status = reader.GetString(8);
        }

        var issueIso = issueDate > 0
            ? DateTimeOffset.FromUnixTimeSeconds(issueDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var vatRate = subtotal > 0 ? Math.Round(vat / subtotal * 100m, 2) : 0m;
        var f = (decimal d) => d.ToString("0.00", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        using (var xw = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false }))
        {
            xw.WriteStartElement("Invoice", "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2");
            xw.WriteAttributeString("xmlns", "cbc", null, "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2");
            xw.WriteAttributeString("xmlns", "cac", null, "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2");
            xw.WriteElementString("cbc", "UBLVersionID", null, "2.1");
            xw.WriteElementString("cbc", "CustomizationID", null, "urn:ae:fta:einvoice:1.0");
            xw.WriteElementString("cbc", "ID", null, number);
            xw.WriteElementString("cbc", "IssueDate", null, issueIso);
            xw.WriteElementString("cbc", "InvoiceTypeCode", null, "380");
            xw.WriteElementString("cbc", "DocumentCurrencyCode", null, currency);

            xw.WriteStartElement("cac", "AccountingSupplierParty");
            xw.WriteStartElement("cac", "Party");
            xw.WriteStartElement("cac", "PartyName"); xw.WriteElementString("cbc", "Name", null, entityName); xw.WriteEndElement();
            xw.WriteStartElement("cac", "PostalAddress");
            xw.WriteElementString("cbc", "CountrySubentity", null, entityCountry);
            xw.WriteStartElement("cac", "Country"); xw.WriteElementString("cbc", "IdentificationCode", null, entityCountry); xw.WriteEndElement();
            xw.WriteEndElement();
            xw.WriteStartElement("cac", "PartyLegalEntity");
            xw.WriteElementString("cbc", "RegistrationName", null, entityName);
            xw.WriteElementString("cbc", "CompanyID", null, entityCode);
            xw.WriteEndElement();
            xw.WriteEndElement();
            xw.WriteEndElement();

            xw.WriteStartElement("cac", "AccountingCustomerParty");
            xw.WriteStartElement("cac", "Party");
            xw.WriteStartElement("cac", "PartyName"); xw.WriteElementString("cbc", "Name", null, customer); xw.WriteEndElement();
            xw.WriteEndElement();
            xw.WriteEndElement();

            xw.WriteStartElement("cac", "TaxTotal");
            xw.WriteElementString("cbc", "TaxAmount", null, f(vat));
            xw.WriteStartElement("cac", "TaxSubtotal");
            xw.WriteElementString("cbc", "TaxableAmount", null, f(subtotal));
            xw.WriteElementString("cbc", "TaxAmount", null, f(vat));
            xw.WriteStartElement("cac", "TaxCategory");
            xw.WriteElementString("cbc", "ID", null, "S");
            xw.WriteElementString("cbc", "Percent", null, vatRate.ToString("0.##", CultureInfo.InvariantCulture));
            xw.WriteStartElement("cac", "TaxScheme"); xw.WriteElementString("cbc", "ID", null, "VAT"); xw.WriteEndElement();
            xw.WriteEndElement();
            xw.WriteEndElement();
            xw.WriteEndElement();

            xw.WriteStartElement("cac", "LegalMonetaryTotal");
            xw.WriteElementString("cbc", "LineExtensionAmount", null, f(subtotal));
            xw.WriteElementString("cbc", "TaxExclusiveAmount", null, f(subtotal));
            xw.WriteElementString("cbc", "TaxInclusiveAmount", null, f(total));
            xw.WriteElementString("cbc", "PayableAmount", null, f(total));
            xw.WriteEndElement();

            xw.WriteStartElement("cac", "InvoiceLine");
            xw.WriteElementString("cbc", "ID", null, "1");
            xw.WriteElementString("cbc", "InvoicedQuantity", null, "1");
            xw.WriteElementString("cbc", "LineExtensionAmount", null, f(subtotal));
            xw.WriteStartElement("cac", "Item"); xw.WriteElementString("cbc", "Name", null, "Sales invoice " + number + " (" + status + ")"); xw.WriteEndElement();
            xw.WriteStartElement("cac", "Price"); xw.WriteElementString("cbc", "PriceAmount", null, f(subtotal)); xw.WriteEndElement();
            xw.WriteEndElement();

            xw.WriteEndElement();
        }
        return new(true, "UBL e-invoice for " + number + ".", sb.ToString(), number);
    }
}
