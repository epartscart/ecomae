namespace EcomAE.Platform.Presentation;

/// <summary>
/// How to use every ERP menu area. Each step opens the page that does that work.
/// Built from <see cref="ErpProcessFlowCatalog"/> so a new menu area appears here too.
/// </summary>
public static class ErpAreaUseGuide
{
    public sealed record Step(int Number, string Label, string Icon, string Href);

    public sealed record Lane(
        string AreaId,
        string CategoryId,
        string Category,
        string Title,
        string Icon,
        string Use,
        IReadOnlyList<Step> Steps,
        IReadOnlyList<Step> JewellerySteps);

    public sealed record Board(string CategoryId, string Category, IReadOnlyList<Lane> Lanes);

    public const string Fallback = "Create the record, check it, approve it, post it, then open the report.";

    public static IReadOnlyList<Board> Boards { get; }

    public static IReadOnlyList<Lane> Lanes { get; }

    static ErpAreaUseGuide()
    {
        var boards = new List<Board>();
        var flat = new List<Lane>();
        foreach (var group in ErpProcessFlowCatalog.Groups)
        {
            var lanes = new List<Lane>();
            foreach (var area in group.Areas)
            {
                var lane = LaneFor(area);
                lanes.Add(lane);
                flat.Add(lane);
            }

            if (lanes.Count > 0)
            {
                boards.Add(new Board(group.Id, group.Label, lanes));
            }
        }

        Boards = boards;
        Lanes = flat;
    }

    public static Lane? Find(string? areaId)
    {
        if (string.IsNullOrWhiteSpace(areaId))
        {
            return null;
        }

        var key = areaId.Trim();
        foreach (var lane in Lanes)
        {
            if (lane.AreaId.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return lane;
            }
        }

        return null;
    }

    public static string UseFor(string? areaId)
        => Find(areaId)?.Use ?? Sentence(areaId);

    public static string Anchor(string? areaId)
    {
        var key = (areaId ?? string.Empty).Trim().ToLowerInvariant();
        var safe = new string(key.Where(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_').ToArray());
        return "area-" + (safe.Length == 0 ? "overview" : safe);
    }

    private static Lane LaneFor(ErpProcessFlowCatalog.AreaFlow area)
    {
        var number = 0;
        return new Lane(
            area.Id,
            area.CategoryId,
            area.CategoryLabel,
            area.Label,
            area.Icon,
            Sentence(area.Id),
            Number(area.Steps, ref number),
            Number(area.JewellerySteps, ref number));
    }

    private static IReadOnlyList<Step> Number(IReadOnlyList<ErpProcessFlowCatalog.Step> source, ref int number)
    {
        var list = new Step[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            number++;
            var step = source[i];
            list[i] = new Step(number, step.Label, step.Icon, step.Href);
        }

        return list;
    }

    private static string Sentence(string? areaId) => (areaId ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "overview" => "Open the dashboard, check what is waiting, approve it, then continue from favourites.",
        "finance" => "Open the chart of accounts, enter the journal, post it, then read profit and loss and the balance sheet.",
        "budgeting" => "Enter the budget, compare it with actuals, then adjust the plan.",
        "consolidations" => "Open the legal entities, consolidate them, remove the intercompany amounts, then read the group report.",
        "cost_acct" => "Enter the allocation, post it to the ledger, then read the cost report.",
        "audit_wb" => "Open the audit policy, review the trail, keep the proof, then close the period.",
        "fixed_assets" => "Register the asset, acquire it, run depreciation, then dispose of it when it leaves.",
        "purchasing" => "Add the supplier, raise the requisition and the order, receive the goods, match the bill, then pay it.",
        "ap" => "Set up the vendor, enter the bill, match it, pay it, then read the aging.",
        "landed_cost_area" => "Open the voyage, add the charges, allocate them, then read the inventory cost.",
        "expense" => "Enter the expense, approve it, reimburse it, then post it to the ledger.",
        "rebate_mgmt" => "Enter the rebate deal, accrue it, claim it, then settle it.",
        "sales" => "Add the prospect, raise the estimate and the order, deliver it, create the invoice, then receive the payment.",
        "ar" => "Set up the customer, create the invoice, receive the payment, then send the statement.",
        "credit_coll" => "Review the credit, collect what is due, read the aging, then send the statement.",
        "retail" => "Open the channel, record the daily sales, take the card payment, then read the takings.",
        "service_mgmt" => "Open the agreement, raise the service order, record the return, then invoice it.",
        "banking" => "Open the bank account, record the receipt or payment, reconcile it, then read the cash balance.",
        "inventory_mgmt" => "Add the item, receive or move the stock, then read the stock report.",
        "pim" => "Enter the product, add the variant and the barcode, then publish it.",
        "cost_mgmt" => "Set the cost model, read the inventory value, then post the difference to the ledger.",
        "production" => "Plan the order, make it, check the quality, then receive the finished goods.",
        "warehouse" => "Open the warehouse, do the work, release the wave, then confirm the stock.",
        "mhei" => "Register the equipment, do the work, confirm it, then read the stock.",
        "logistics" => "Arrange the shipment, clear customs, receive the inbound goods, then dispatch the outbound delivery.",
        "master_planning_area" => "Enter the forecast, run the plan, release the planned order, then raise the purchase order.",
        "asset_mgmt" => "Register the asset, record the maintenance, complete the work order, then read the history.",
        "transportation" => "Open the load, ship it, enter the freight bill, then confirm the delivery.",
        "people" => "Add the worker, recruit and review them, record the leave, then check compliance.",
        "payroll_area" => "Record the time, run the payroll, pay the employees and the liabilities, then keep the insurance proof.",
        "leave_abs" => "Set the leave plan, enter the request, approve it, then pass it to payroll.",
        "projects" => "Open the project or the property, record the cost or the time, invoice it, then read the result.",
        "time_att" => "Register the time, approve it, transfer it to payroll, then read attendance.",
        "tax" => "Follow the e-invoice guide, send the PINT-AE invoice through the accredited service provider, then prepare the VAT return, withholding, and the filing.",
        "risk" => "Set the policy, record the claim, watch document expiry, then review the trail.",
        "setup" => "Set the company, add the users, set security, then design the invoice, voucher, and posted report.",
        "enterprise" => "Set the business unit and the legal entity, file the contract, then keep the documents.",
        "common" => "Open the agenda and the contacts, then file the document and the attachment.",
        "questionnaire" => "Design the questionnaire, choose the contacts, send it, then read the results.",
        _ => Fallback,
    };
}
