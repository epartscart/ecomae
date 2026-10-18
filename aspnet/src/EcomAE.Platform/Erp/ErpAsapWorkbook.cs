using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace EcomAE.Platform.Erp;

/// <summary>
/// ASAP master workbook. Headers match the reference CSVs supplied for this build.
/// Excel never writes a stock reservation.
/// </summary>
public static class ErpAsapWorkbook
{
    public static readonly string[] TemplateSheets =
    [
        "START_HERE", "Companies", "Business_Centres", "Profit_Centres", "Cost_Centres", "Branches",
        "Warehouses", "Brands", "GL_Accounts", "Suppliers", "Product_Master", "Product_Attributes",
        "OEM_References", "Vehicle_Fitment", "Cross_References", "Alternates_Kits", "Customers",
        "Customer_Price_List", "Users_Roles", "Purchase_Requisitions", "Purchase_Orders", "PO_Lines",
        "Supplier_Price_List", "Import_Shipments", "Import_Costs", "Goods_Receipts", "GRN_Lines",
        "Inventory_Opening", "Inventory_Movements", "Reorder_Policies", "Stock_Ageing", "Bins",
        "Warehouse_Transfers", "Pick_Waves", "Pick_Lines", "Sales_Orders", "SO_Lines", "Reservations",
        "Deliveries", "Sales_Invoices", "Sales_Invoice_Lines", "Returns_RMA", "Cycle_Counts",
        "Intercompany_Rules", "Intercompany_Transactions", "TP_Documentation", "Bank_Cheques",
        "Payments_Receipts", "Journal_Entries", "VAT_Customs", "Budgets", "Forecast_Demand",
        "AR_Opening", "AP_Opening", "Expenses", "Alerts", "Documents", "Import_Audit",
    ];

    public sealed record Sheet(
        string Name,
        IReadOnlyList<string> Headers,
        IReadOnlyList<IReadOnlyDictionary<string, string>> Records);

    public static byte[] Template()
    {
        var sheets = TemplateSheets.Select(name => (name, Headers(name), SheetRows(name))).ToArray();
        return Xlsx(sheets);
    }

    public static byte[] ExportPackage()
    {
        var book = Template();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("ASAP_AUTO_PARTS_MASTER_IMPORT_TEMPLATE.xlsx");
            using (var stream = entry.Open())
            {
                stream.Write(book, 0, book.Length);
            }

            var readme = zip.CreateEntry("README.txt");
            using var writer = new StreamWriter(readme.Open(), new UTF8Encoding(false));
            writer.Write("ASAP workbook: 57 reference sheets plus START_HERE. The Reservations sheet does not allocate live stock. A warehouse move inside one company is not an intercompany sale. A designated zone is not automatically VAT exempt. This package does not post reservations, invoices, or journals.");
        }

        return ms.ToArray();
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<Sheet> sheets)
    {
        var errors = new List<string>();
        var companies = Ids(sheets, "Companies", "company_id");
        foreach (var sheet in sheets)
        {
            if (sheet.Name.Equals("START_HERE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rowNo = 1;
            foreach (var row in sheet.Records)
            {
                rowNo++;
                if (IsBlank(row) || IsSample(row))
                {
                    continue;
                }

                switch (sheet.Name)
                {
                    case "Companies":
                        Need(errors, sheet.Name, rowNo, row, "company_id");
                        break;
                    case "Branches":
                        Need(errors, sheet.Name, rowNo, row, "branch_id");
                        Known(errors, sheet.Name, rowNo, row, "company_id", companies);
                        break;
                    case "Cross_References":
                        Need(errors, sheet.Name, rowNo, row, "sku");
                        Need(errors, sheet.Name, rowNo, row, "reference_part_number");
                        var fitment = Cell(row, "fitment_verified");
                        if (fitment.Length > 0 && fitment is not ("verified" or "unverified" or "conditional"))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": fitment_verified must be verified, unverified, or conditional.");
                        }

                        break;
                    case "Alternates_Kits":
                        Need(errors, sheet.Name, rowNo, row, "parent_sku");
                        if (Num(row, "quantity") <= 0m)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": quantity must be greater than zero.");
                        }

                        break;
                    case "Customers":
                        Need(errors, sheet.Name, rowNo, row, "customer_id");
                        if (Num(row, "credit_limit") < 0m)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": credit_limit cannot be negative.");
                        }

                        break;
                    case "Forecast_Demand":
                        if (Num(row, "forecast_qty") < 0m)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": forecast_qty cannot be negative.");
                        }

                        break;
                    case "Cycle_Counts":
                        if (Num(row, "counted_qty") < 0m)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": counted_qty cannot be negative.");
                        }

                        break;
                    case "Bank_Cheques":
                        if (Cell(row, "status").Equals("cleared", StringComparison.OrdinalIgnoreCase)
                            && Cell(row, "cleared_date").Length == 0)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": a cleared cheque needs a cleared date.");
                        }

                        break;
                    case "Documents":
                        Need(errors, sheet.Name, rowNo, row, "document_id");
                        Need(errors, sheet.Name, rowNo, row, "file_name");
                        break;
                    case "Product_Master":
                        Need(errors, sheet.Name, rowNo, row, "sku");
                        break;
                    case "OEM_References":
                        Need(errors, sheet.Name, rowNo, row, "sku");
                        Need(errors, sheet.Name, rowNo, row, "oem_part_number");
                        var verification = Cell(row, "verification_status");
                        if (verification.Length > 0 && verification is not ("verified" or "unverified" or "conditional"))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": verification_status must be verified, unverified, or conditional.");
                        }

                        break;
                    case "PO_Lines":
                        if (Num(row, "ordered_qty") <= 0m)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": ordered_qty must be greater than zero.");
                        }

                        break;
                    case "GRN_Lines":
                        if (Num(row, "accepted_qty") + Num(row, "rejected_qty") > Num(row, "received_qty"))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": accepted and rejected quantities cannot exceed the received quantity.");
                        }

                        break;
                    case "Pick_Lines":
                        if (Num(row, "qty_picked") > Num(row, "qty_to_pick"))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": qty_picked cannot exceed qty_to_pick.");
                        }

                        break;
                    case "Import_Costs":
                        if (Cell(row, "cost_type").Equals("vat", StringComparison.OrdinalIgnoreCase)
                            && Cell(row, "capitalizable") is "yes" or "true" or "1")
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": recoverable VAT is not inventory cost.");
                        }

                        break;
                    case "Intercompany_Rules":
                        if (Cell(row, "selling_company").Length > 0
                            && Cell(row, "selling_company").Equals(Cell(row, "buying_company"), StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": selling and buying companies must differ.");
                        }

                        break;
                    case "Journal_Entries":
                        if (Num(row, "debit") > 0m && Num(row, "credit") > 0m)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": a journal line is either a debit or a credit.");
                        }

                        break;
                    case "Inventory_Opening":
                        if (Num(row, "opening_qty") < 0m)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": opening_qty cannot be negative.");
                        }

                        break;
                    case "Reservations":
                        if (Cell(row, "status").Equals("confirmed", StringComparison.OrdinalIgnoreCase)
                            && Cell(row, "expires_at").Length > 0)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": a confirmed reservation does not expire.");
                        }

                        break;
                    case "Warehouse_Transfers":
                        if (Cell(row, "transfer_type").Equals("warehouse", StringComparison.OrdinalIgnoreCase)
                            && Cell(row, "from_company").Length > 0
                            && !Cell(row, "from_company").Equals(Cell(row, "to_company"), StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": a warehouse move does not change the owning company.");
                        }

                        break;
                    case "VAT_Customs":
                        var zone = Cell(row, "customs_zone");
                        if (Cell(row, "vat_treatment").Equals("exempt", StringComparison.OrdinalIgnoreCase)
                            && (zone.Contains("designated", StringComparison.OrdinalIgnoreCase)
                                || zone.Contains("jafza", StringComparison.OrdinalIgnoreCase)
                                || zone.Contains("free", StringComparison.OrdinalIgnoreCase)))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": a designated zone is not automatically VAT exempt.");
                        }

                        break;
                    case "Vehicle_Fitment":
                        Need(errors, sheet.Name, rowNo, row, "verification_source");
                        if (Num(row, "year_from") > 0m && Num(row, "year_to") > 0m && Num(row, "year_to") < Num(row, "year_from"))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": year_to is before year_from.");
                        }

                        break;
                    case "Reorder_Policies":
                        if (Num(row, "max_qty") > 0m && Num(row, "min_qty") > Num(row, "max_qty"))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": min_qty cannot exceed max_qty.");
                        }

                        break;
                    case "Sales_Invoices":
                        if (Num(row, "subtotal") + Num(row, "vat_amount") != Num(row, "total")
                            && (Num(row, "subtotal") > 0m || Num(row, "vat_amount") > 0m || Num(row, "total") > 0m))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": total must equal subtotal plus VAT.");
                        }

                        break;
                    case "SO_Lines":
                        if (Num(row, "qty") <= 0m)
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": qty must be greater than zero.");
                        }

                        break;
                    case "TP_Documentation":
                        Need(errors, sheet.Name, rowNo, row, "method");
                        break;
                    case "Users_Roles":
                        if (Cell(row, "can_override_reservation").Equals("yes", StringComparison.OrdinalIgnoreCase)
                            && Cell(row, "can_reserve").Equals("no", StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add(sheet.Name + " row " + rowNo + ": a user who cannot reserve cannot override a reservation.");
                        }

                        break;
                }
            }
        }

        var refs = sheets.FirstOrDefault(sheet => sheet.Name == "Cross_References");
        if (refs is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in refs.Records)
            {
                if (IsBlank(row) || IsSample(row))
                {
                    continue;
                }

                var key = Cell(row, "sku") + "|" + Cell(row, "reference_brand") + "|" + ErpAsapStock.NormalizeReference(Cell(row, "reference_part_number"));
                if (!seen.Add(key))
                {
                    errors.Add("Cross_References duplicate normalized reference " + key + ".");
                }
            }
        }

        var oem = sheets.FirstOrDefault(sheet => sheet.Name == "OEM_References");
        if (oem is not null)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in oem.Records)
            {
                if (IsBlank(row) || IsSample(row))
                {
                    continue;
                }

                var key = Cell(row, "sku") + "|" + ErpAsapStock.NormalizeReference(Cell(row, "oem_part_number"));
                if (!seen.Add(key))
                {
                    errors.Add("OEM_References duplicate normalized part " + key + ".");
                }
            }
        }

        return errors;
    }

    public static IReadOnlyList<Sheet> Read(byte[] xlsx)
    {
        using var ms = new MemoryStream(xlsx);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        var workbook = Load(zip, "xl/workbook.xml");
        var rels = Load(zip, "xl/_rels/workbook.xml.rels");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        XNamespace rNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var targets = rels.Root!.Elements(relNs + "Relationship")
            .ToDictionary(el => (string?)el.Attribute("Id") ?? "", el => (string?)el.Attribute("Target") ?? "", StringComparer.Ordinal);
        var sheets = new List<Sheet>();
        foreach (var sheet in workbook.Root!.Element(ns + "sheets")!.Elements(ns + "sheet"))
        {
            var name = (string?)sheet.Attribute("name") ?? "";
            var id = (string?)sheet.Attribute(rNs + "id") ?? "";
            if (!targets.TryGetValue(id, out var target))
            {
                continue;
            }

            var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
            var rows = Rows(Load(zip, path));
            if (rows.Count == 0)
            {
                sheets.Add(new Sheet(name, [], []));
                continue;
            }

            var headers = rows[0];
            var records = new List<IReadOnlyDictionary<string, string>>();
            for (var i = 1; i < rows.Count; i++)
            {
                var map = new Dictionary<string, string>(StringComparer.Ordinal);
                for (var c = 0; c < headers.Count; c++)
                {
                    map[headers[c]] = c < rows[i].Count ? rows[i][c] : "";
                }

                records.Add(map);
            }

            sheets.Add(new Sheet(name, headers, records));
        }

        return sheets;
    }

    public static string[] Headers(string sheet) => sheet switch
    {
        "START_HERE" => ["ASAP — AUTOMOTIVE AFTERMARKET ERP IMPORT REFERENCE", ""],
        "Companies" => ["company_id", "legal_name", "trade_license", "trn", "ct_trn", "entity_type", "country", "base_currency", "related_party_group"],
        "Business_Centres" => ["business_centre_id", "company_id", "name", "manager", "status"],
        "Cost_Centres" => ["cost_centre_id", "company_id", "profit_centre_id", "name", "cost_category"],
        "Branches" => ["branch_id", "company_id", "branch_name", "emirate", "location", "profit_centre_id", "warehouse_id"],
        "Brands" => ["brand_id", "brand_name", "brand_type", "country", "principal_supplier_id"],
        "Bins" => ["bin_id", "warehouse_id", "zone", "aisle", "rack", "shelf", "bin_type"],
        "Customers" => ["customer_id", "company_name", "customer_type", "trn", "country", "credit_limit", "credit_days", "price_group", "salesperson_id"],
        "Customer_Price_List" => ["customer_id", "price_group", "sku", "currency", "unit_price", "discount_pct", "valid_from", "valid_to"],
        "Cross_References" => ["cross_ref_id", "sku", "reference_brand", "reference_part_number", "relationship_type", "fitment_verified", "source", "priority"],
        "Alternates_Kits" => ["parent_sku", "component_sku", "quantity", "relationship_type", "substitution_approved"],
        "Deliveries" => ["delivery_id", "so_id", "warehouse_id", "dispatch_date", "carrier", "tracking_no", "proof_of_delivery", "status"],
        "Cycle_Counts" => ["count_id", "warehouse_id", "bin_id", "sku", "system_qty", "counted_qty", "count_date", "approved_by"],
        "Bank_Cheques" => ["cheque_id", "company_id", "party_id", "bank", "cheque_number", "cheque_date", "amount", "currency", "receipt_date", "deposit_date", "cleared_date", "status"],
        "Budgets" => ["budget_id", "company_id", "financial_year", "cost_centre_id", "profit_centre_id", "gl_code", "month", "budget_amount"],
        "Forecast_Demand" => ["sku", "warehouse_id", "forecast_month", "forecast_qty", "method", "reviewed_by"],
        "AR_Opening" => ["company_id", "customer_id", "invoice_id", "invoice_date", "due_date", "currency", "outstanding_amount"],
        "AP_Opening" => ["company_id", "supplier_id", "invoice_id", "invoice_date", "due_date", "currency", "outstanding_amount"],
        "Expenses" => ["expense_id", "company_id", "cost_centre_id", "profit_centre_id", "warehouse_id", "supplier_id", "expense_date", "category", "net_amount", "vat_amount"],
        "Alerts" => ["alert_id", "company_id", "alert_type", "source_entity", "source_id", "due_date", "severity", "owner", "status"],
        "Documents" => ["document_id", "entity_type", "entity_id", "document_category", "file_name", "relative_zip_path", "expiry_date", "version", "access_class"],
        "Profit_Centres" => ["profit_centre_id", "company_id", "business_centre_id", "name", "manager"],
        "Purchase_Orders" => ["po_id", "company_id", "supplier_id", "currency", "order_date", "expected_date", "incoterm", "status"],
        "GL_Accounts" => ["gl_code", "company_id", "account_name", "account_type", "parent_gl", "is_active"],
        "Goods_Receipts" => ["grn_id", "po_id", "warehouse_id", "receipt_date", "supplier_invoice", "received_by", "status"],
        "GRN_Lines" => ["grn_id", "line_no", "sku", "received_qty", "accepted_qty", "rejected_qty", "lot_no", "unit_cost"],
        "Import_Audit" => ["batch_id", "template_version", "imported_by", "imported_at", "sheet_name", "rows_accepted", "rows_rejected", "status"],
        "Import_Costs" => ["shipment_id", "cost_id", "cost_type", "supplier_id", "currency", "amount", "allocation_method", "capitalizable"],
        "Import_Shipments" => ["shipment_id", "company_id", "po_id", "supplier_id", "origin_country", "port_of_loading", "port_of_arrival", "incoterm", "bl_awb", "eta", "customs_declaration"],
        "Intercompany_Rules" => ["rule_id", "selling_company", "buying_company", "sku_category", "method", "markup_pct", "effective_from", "effective_to", "approval_id"],
        "Intercompany_Transactions" => ["ic_id", "seller_company", "buyer_company", "source_order", "source_invoice", "mirror_po", "mirror_invoice", "sku", "qty", "transfer_price", "elimination_group"],
        "Inventory_Movements" => ["movement_id", "company_id", "warehouse_id", "bin_id", "sku", "movement_type", "qty", "unit_cost", "source_doc", "posted_at"],
        "Inventory_Opening" => ["company_id", "warehouse_id", "bin_id", "sku", "lot_no", "opening_qty", "unit_cost", "as_of_date"],
        "Journal_Entries" => ["journal_id", "company_id", "posting_date", "document_ref", "gl_code", "debit", "credit", "cost_centre_id", "profit_centre_id"],
        "OEM_References" => ["reference_id", "sku", "oem_make", "oem_part_number", "reference_type", "verification_status", "source"],
        "Payments_Receipts" => ["payment_id", "company_id", "party_id", "document_id", "payment_date", "method", "currency", "amount", "bank_account"],
        "Pick_Lines" => ["wave_id", "pick_line_id", "reservation_id", "sku", "bin_id", "qty_to_pick", "qty_picked", "confirmed_at"],
        "Pick_Waves" => ["wave_id", "warehouse_id", "created_at", "priority", "status", "assigned_picker"],
        "PO_Lines" => ["po_id", "line_no", "sku", "ordered_qty", "unit_price", "discount_pct", "warehouse_id", "expected_date"],
        "Product_Attributes" => ["sku", "attribute_name", "attribute_value"],
        "Product_Master" => ["sku", "brand_id", "part_number", "description", "category_id", "origin_country", "hs_code", "uom", "pack_qty", "weight_kg", "dimensions", "active"],
        "Reorder_Policies" => ["sku", "warehouse_id", "min_qty", "max_qty", "safety_stock", "lead_time_days", "reorder_point", "order_multiple"],
        "Reservations" => ["reservation_id", "so_id", "line_no", "sku", "warehouse_id", "qty", "created_at", "expires_at", "status", "allocation_sequence"],
        "Returns_RMA" => ["rma_id", "original_invoice", "customer_id", "sku", "qty", "reason", "inspection_status", "credit_note_id"],
        "Sales_Invoice_Lines" => ["invoice_id", "line_no", "sku", "qty", "unit_price", "discount", "vat_code", "cost_centre_id", "profit_centre_id"],
        "Sales_Invoices" => ["invoice_id", "company_id", "customer_id", "so_id", "invoice_date", "currency", "subtotal", "vat_amount", "total", "status"],
        "Sales_Orders" => ["so_id", "company_id", "branch_id", "customer_id", "order_date", "channel", "currency", "priority", "status"],
        "SO_Lines" => ["so_id", "line_no", "sku", "qty", "unit_price", "source_warehouse", "profit_centre_id", "requested_date"],
        "Stock_Ageing" => ["company_id", "warehouse_id", "sku", "lot_no", "receipt_date", "qty_on_hand", "unit_cost", "as_of_date"],
        "Supplier_Price_List" => ["supplier_id", "sku", "supplier_part_number", "currency", "unit_price", "moq", "available_qty", "valid_from", "valid_to"],
        "Suppliers" => ["supplier_id", "legal_name", "country", "currency", "trade_terms", "lead_time_days", "trn", "credit_days", "bank_ref"],
        "TP_Documentation" => ["tp_doc_id", "rule_id", "method", "benchmark_source", "benchmark_date", "arm_length_range", "approval_by", "document_ref"],
        "Users_Roles" => ["user_id", "company_id", "branch_id", "role_id", "can_reserve", "can_override_reservation", "approval_limit"],
        "VAT_Customs" => ["tax_code", "transaction_type", "origin_country", "destination_country", "customs_zone", "vat_treatment", "document_required"],
        "Vehicle_Fitment" => ["fitment_id", "sku", "vehicle_make", "model", "year_from", "year_to", "engine", "vin_range", "position", "verification_source"],
        "Warehouse_Transfers" => ["transfer_id", "from_company", "from_warehouse", "to_company", "to_warehouse", "sku", "qty", "transfer_type", "status"],
        "Warehouses" => ["warehouse_id", "company_id", "name", "type", "emirate", "customs_status", "central_fulfilment", "allow_reservation"],
        "Purchase_Requisitions" => ["pr_id", "company_id", "warehouse_id", "sku", "qty", "required_date", "requester", "status"],
        _ => throw new ArgumentOutOfRangeException(nameof(sheet), sheet, "Unknown ASAP sheet."),
    };

    private static string[][] SheetRows(string sheet)
    {
        if (sheet == "START_HERE")
        {
            return
            [
                ["Purpose", "Automotive aftermarket ERP for multi-company wholesale, retail, import and central warehousing"],
                ["Sheets", "57 reference sheets from the supplied CSVs, plus this instruction sheet. Purchase_Orders is included once"],
                ["Reservation", "Excel does not reserve stock. The Reservations sheet is a reference only. A confirmed reservation has no expiry. A pick line uses that reservation and does not reserve again"],
                ["Transfer", "A warehouse move inside one company does not change ownership and is not an intercompany sale"],
                ["Tax", "A designated zone or JAFZA movement is not automatically VAT exempt"],
                ["Fitment", "Vehicle fitment keeps its source. This workbook does not invent compatibility"],
                ["Journals", "A posted journal is reversed. It is not overwritten"],
                ["Landed cost", "Freight and duty can be capitalized. Recoverable VAT stays out of inventory cost"],
                ["Intercompany", "The selling company and the buying company are different legal entities"],
                ["Ownership", "Warehouse location and the owning company are separate. Another company's stock needs an intercompany fulfilment"],
                ["Cross reference", "verified means interchangeable for the stated fitment. unverified and conditional stay as search hits only"],
                ["Normalize", "Search ignores spaces and hyphens. The original part number is kept"],
                ["Cheques", "A cleared cheque needs a cleared date before it counts as cash"],
                ["IDs", "Keep company_id, sku and cross_ref_id stable on re-import"],
            ];
        }

        if (sheet == "Cross_References")
        {
            return
            [
                ["XR-OEM-1", "FILTER-001", "OEM", "OEM-12345", "oem", "verified", "catalogue", "1"],
                ["XR-ALT-1", "FILTER-001", "BRAND-A", "A100", "aftermarket", "conditional", "supplier", "2"],
            ];
        }

        if (sheet == "Product_Master")
        {
            return [["FILTER-001", "BR-OEM", "A100", "Oil filter", "filters", "JP", "842123", "pcs", "1", "0.200", "8x8x8 cm", "yes"]];
        }

        if (sheet == "OEM_References")
        {
            return [["OEM-1", "FILTER-001", "Toyota", "OEM-12345", "oem", "verified", "catalogue"]];
        }

        if (sheet == "Intercompany_Rules")
        {
            return [["IC-1", "CO-A", "CO-B", "filters", "comparable_uncontrolled_price", "0", "2026-01-01", "2026-12-31", "APR-1"]];
        }

        if (sheet == "Import_Costs")
        {
            return [["SH-1", "COST-1", "freight", "SUP-1", "USD", "100", "value", "yes"]];
        }

        if (sheet == "Reservations")
        {
            return [["RSV-1", "SO-1", "1", "FILTER-001", "WH-CENTRAL", "6", "2026-10-15", "", "confirmed", "1"]];
        }

        if (sheet == "Warehouse_Transfers")
        {
            return [["TR-1", "CO-A", "WH-1", "CO-A", "WH-2", "FILTER-001", "2", "warehouse", "planned"]];
        }

        if (sheet == "VAT_Customs")
        {
            return [["VAT-DOM", "domestic", "AE", "AE", "mainland", "standard", "tax_invoice"]];
        }

        if (sheet == "Vehicle_Fitment")
        {
            return [["FIT-1", "FILTER-001", "Toyota", "Corolla", "2018", "2022", "1.8", "", "front", "catalogue"]];
        }

        if (sheet == "Sales_Invoices")
        {
            return [["INV-1", "CO-A", "CUST-1", "SO-1", "2026-10-15", "AED", "100", "5", "105", "draft"]];
        }

        if (sheet == "SO_Lines")
        {
            return [["SO-1", "1", "FILTER-001", "6", "10", "WH-CENTRAL", "PC-1", "2026-10-15"]];
        }

        if (sheet == "TP_Documentation")
        {
            return [["TP-1", "IC-1", "comparable_uncontrolled_price", "supplier quotes", "2026-01-01", "documented range", "CFO", "DOC-1"]];
        }

        if (sheet == "Pick_Lines")
        {
            return [["WAVE-1", "PICK-1", "RSV-1", "FILTER-001", "BIN-1", "4", "4", "2026-10-15"]];
        }

        if (sheet == "Companies")
        {
            return
            [
                ["CO-A", "Company A Spare Parts LLC", "TL-A", "100000000000003", "CT-A", "mainland", "AE", "AED", "ASAP"],
                ["CO-B", "Company B Trading LLC", "TL-B", "100000000000004", "CT-B", "mainland", "AE", "AED", "ASAP"],
            ];
        }

        return [Sample(sheet)];
    }

    private static string[] Sample(string sheet)
    {
        var headers = Headers(sheet);
        return headers.Select(header => header.EndsWith("_qty", StringComparison.Ordinal) || header is "quantity" or "qty" or "credit_limit" or "credit_days" or "unit_price" or "discount_pct" or "amount" or "net_amount" or "vat_amount" or "budget_amount" or "outstanding_amount" or "system_qty" or "counted_qty" or "priority" or "version" or "debit" or "credit" or "pack_qty" or "weight_kg" or "markup_pct" or "transfer_price" or "unit_cost" or "rows_accepted" or "rows_rejected" or "line_no" or "safety_stock" or "lead_time_days" or "reorder_point" or "order_multiple" or "subtotal" or "total" or "discount" or "year_from" or "year_to" or "approval_limit" or "moq" or "allocation_sequence"
            ? "0"
            : header.EndsWith("_date", StringComparison.Ordinal) || header.EndsWith("_at", StringComparison.Ordinal) || header is "forecast_month" or "valid_from" or "valid_to" or "month" or "eta" or "effective_from" or "effective_to"
                ? "2026-10-15"
                : header.EndsWith("_id", StringComparison.Ordinal) || header is "sku" || header is "parent_sku" || header is "component_sku"
                    ? "SAMPLE-001"
                    : "SAMPLE").ToArray();
    }

    private static HashSet<string> Ids(IReadOnlyList<Sheet> sheets, string name, string column)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sheet = sheets.FirstOrDefault(item => item.Name == name);
        if (sheet is null)
        {
            return set;
        }

        foreach (var row in sheet.Records)
        {
            var id = Cell(row, column);
            if (id.Length > 0 && !IsSample(row))
            {
                set.Add(id);
            }
        }

        return set;
    }

    private static void Need(List<string> errors, string sheet, int rowNo, IReadOnlyDictionary<string, string> row, string column)
    {
        if (Cell(row, column).Length == 0)
        {
            errors.Add(sheet + " row " + rowNo + ": " + column + " is required.");
        }
    }

    private static void Known(List<string> errors, string sheet, int rowNo, IReadOnlyDictionary<string, string> row, string column, HashSet<string> known)
    {
        var value = Cell(row, column);
        if (value.Length > 0 && known.Count > 0 && !known.Contains(value))
        {
            errors.Add(sheet + " row " + rowNo + ": " + column + " " + value + " is not on Companies.");
        }
    }

    private static bool IsSample(IReadOnlyDictionary<string, string> row)
        => row.Values.Any(value => value.Trim().StartsWith("SAMPLE", StringComparison.OrdinalIgnoreCase)
            || value.Trim().StartsWith("EXAMPLE", StringComparison.OrdinalIgnoreCase));

    private static bool IsBlank(IReadOnlyDictionary<string, string> row)
        => row.Values.All(value => string.IsNullOrWhiteSpace(value));

    private static string Cell(IReadOnlyDictionary<string, string> row, string key)
        => row.TryGetValue(key, out var value) ? value.Trim() : "";

    private static decimal Num(IReadOnlyDictionary<string, string> row, string key)
        => decimal.TryParse(Cell(row, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static XDocument Load(ZipArchive zip, string path)
    {
        var entry = zip.GetEntry(path) ?? throw new InvalidDataException("Workbook is missing " + path + ".");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private static List<List<string>> Rows(XDocument sheet)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var rows = new List<List<string>>();
        foreach (var row in sheet.Descendants(ns + "row"))
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                var reference = (string?)cell.Attribute("r") ?? "A";
                var column = 0;
                foreach (var ch in reference)
                {
                    if (!char.IsLetter(ch))
                    {
                        break;
                    }

                    column = (column * 26) + (char.ToUpperInvariant(ch) - 'A' + 1);
                }

                while (cells.Count < column - 1)
                {
                    cells.Add("");
                }

                cells.Add(cell.Descendants(ns + "t").FirstOrDefault()?.Value ?? "");
            }

            rows.Add(cells);
        }

        return rows;
    }

    private static byte[] Xlsx(IReadOnlyList<(string Name, string[] Headers, string[][] Rows)> sheets)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var overrides = new StringBuilder();
            var sheetRefs = new StringBuilder();
            var rels = new StringBuilder();
            for (var s = 1; s <= sheets.Count; s++)
            {
                overrides.Append("<Override PartName=\"/xl/worksheets/sheet").Append(s)
                    .Append(".xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
                sheetRefs.Append("<sheet name=\"").Append(Xml(sheets[s - 1].Name)).Append("\" sheetId=\"").Append(s)
                    .Append("\" r:id=\"rId").Append(s).Append("\"/>");
                rels.Append("<Relationship Id=\"rId").Append(s)
                    .Append("\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet")
                    .Append(s).Append(".xml\"/>");
            }

            WriteEntry(archive, "[Content_Types].xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" + overrides + "</Types>");
            WriteEntry(archive, "_rels/.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
            WriteEntry(archive, "xl/workbook.xml",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" + sheetRefs + "</sheets></workbook>");
            WriteEntry(archive, "xl/_rels/workbook.xml.rels",
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + rels + "</Relationships>");
            for (var s = 0; s < sheets.Count; s++)
            {
                var data = new StringBuilder();
                var allRows = new List<string[]> { sheets[s].Headers };
                allRows.AddRange(sheets[s].Rows);
                for (var r = 0; r < allRows.Count; r++)
                {
                    data.Append("<row r=\"").Append(r + 1).Append("\">");
                    for (var c = 0; c < allRows[r].Length && c < 26; c++)
                    {
                        var value = allRows[r][c] ?? "";
                        if (value.Length == 0 && r > 0)
                        {
                            continue;
                        }

                        data.Append("<c r=\"").Append((char)('A' + c)).Append(r + 1).Append("\" t=\"inlineStr\"><is><t>")
                            .Append(Xml(value)).Append("</t></is></c>");
                    }

                    data.Append("</row>");
                }

                WriteEntry(archive, "xl/worksheets/sheet" + (s + 1) + ".xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>" + data + "</sheetData></worksheet>");
            }
        }

        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string Xml(string value) => System.Security.SecurityElement.Escape(value) ?? "";
}
