using System.Globalization;

namespace EcomAE.Platform.Erp;

public sealed record ErpLandlordLine(
    long Id,
    string Code,
    long PropertyId,
    long UnitId,
    int Year,
    string Kind,
    string Category,
    decimal Amount,
    string Spread,
    long ParentId,
    string MovementDate,
    string Note,
    string Status,
    string PropertyCode,
    string PropertyName,
    string UnitCode,
    string UnitName,
    string CoaCode = "");

public sealed record ErpLandlordProperty(
    long Id,
    string Code,
    string Name,
    string Holder,
    int StartYear,
    int PlannedUnits,
    int ListedUnits,
    string Type);

public sealed record ErpLandlordUnit(long Id, long PropertyId, string Code, string Name);

public sealed record ErpLandlordTotals(
    decimal Income,
    decimal Expense,
    decimal ReturnAmount,
    decimal Recovery,
    decimal Investment,
    decimal Through2024,
    bool RecoveryMarked)
{
    public decimal? RecoveryPercent
        => Recovery > 0 && Expense > 0
            ? decimal.Round(Recovery / Expense * 100m, 2, MidpointRounding.AwayFromZero)
            : null;

    public decimal? ReturnOnInvestment
        => Investment > 0
            ? decimal.Round(ReturnAmount / Investment * 100m, 2, MidpointRounding.AwayFromZero)
            : null;
}

public sealed record ErpLandlordMeasure(int Year, decimal YearIncome, decimal YearExpense, decimal AllIncome, decimal AllExpense)
{
    public decimal YearReturn => YearIncome - YearExpense;

    public decimal AllReturn => AllIncome - AllExpense;

    public static ErpLandlordMeasure Empty { get; } = new(0, 0, 0, 0, 0);
}

public sealed record ErpLandlordRollup(
    long PropertyId,
    string Code,
    string Name,
    string Holder,
    int StartYear,
    int PlannedUnits,
    int ListedUnits,
    decimal Income,
    decimal Expense,
    decimal Recovery);

public static class ErpLandlordCalc
{
    public static decimal[] SplitEven(decimal amount, int parts)
    {
        if (parts <= 0)
        {
            return [];
        }

        var cents = (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);
        var baseCents = cents / parts;
        var remainder = cents % parts;
        var rows = new decimal[parts];
        for (var i = 0; i < parts; i++)
        {
            rows[i] = (baseCents + (i < remainder ? 1 : 0)) / 100m;
        }

        return rows;
    }

    public static bool CountsInPropertyTotal(ErpLandlordLine line)
        => line.ParentId == 0 && !string.Equals(line.Status, "void", StringComparison.OrdinalIgnoreCase);

    public static bool CountsInView(ErpLandlordLine line, long unitId)
    {
        if (string.Equals(line.Status, "void", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (unitId <= 0)
        {
            return line.ParentId == 0;
        }

        return line.UnitId == unitId
            && (line.ParentId != 0 || string.Equals(line.Spread, "flat", StringComparison.OrdinalIgnoreCase));
    }

    public static bool InYear(ErpLandlordLine line, int year)
        => year <= 0 || line.Year == year;

    public static ErpLandlordTotals Summarize(IEnumerable<ErpLandlordLine> lines, int year, long propertyId, long unitId)
    {
        var chosen = lines.Where(line =>
            CountsInView(line, unitId)
            && (propertyId <= 0 || line.PropertyId == propertyId)).ToArray();

        decimal Sum(string kind, bool ignoreYear = false)
            => chosen.Where(line => string.Equals(line.Kind, kind, StringComparison.OrdinalIgnoreCase)
                && (ignoreYear || InYear(line, year))).Sum(line => line.Amount);

        var income = Sum("income");
        var expense = Sum("expense");
        var recovery = Sum("recovery");
        var marked = chosen.Any(line => string.Equals(line.Kind, "recovery", StringComparison.OrdinalIgnoreCase) && InYear(line, year));
        return new ErpLandlordTotals(
            income,
            expense,
            income - expense,
            recovery,
            Sum("investment", ignoreYear: true),
            Sum("through_2024", ignoreYear: true),
            marked);
    }

    public static IReadOnlyList<ErpLandlordRollup> ByProperty(
        IEnumerable<ErpLandlordLine> lines,
        IEnumerable<ErpLandlordProperty> properties,
        int year)
    {
        var source = lines.Where(line => CountsInPropertyTotal(line) && InYear(line, year)).ToArray();
        return properties.Select(property =>
        {
            var rows = source.Where(line => line.PropertyId == property.Id).ToArray();
            return new ErpLandlordRollup(
                property.Id,
                property.Code,
                property.Name,
                property.Holder,
                property.StartYear,
                property.PlannedUnits,
                property.ListedUnits,
                rows.Where(line => line.Kind == "income").Sum(line => line.Amount),
                rows.Where(line => line.Kind == "expense").Sum(line => line.Amount),
                rows.Where(line => line.Kind == "recovery").Sum(line => line.Amount));
        }).Where(row => row.Income != 0 || row.Expense != 0 || row.Recovery != 0 || row.PlannedUnits > 0).ToArray();
    }
}

public static class ErpLandlordOpening
{
    public const string PortfolioCode = "REAL-HOLD";
    public const string PortfolioName = "Landlord portfolio";
    public const int SheetYear = 2025;

    public sealed record PropertySeed(
        string Code,
        string Name,
        string Type,
        string Holder,
        int StartYear,
        int PlannedUnits,
        string Emirate,
        string City,
        string Note);

    public sealed record LineSeed(string Code, string PropertyCode, string Kind, string Category, decimal Amount, string Note);

    public static IReadOnlyList<PropertySeed> Properties { get; } =
    [
        new("LC-126", "Labour Camp", "labour_camp", "Lutfi", 2011, 126, "Dubai", "Dubai", "126 rooms. Room list comes later."),
        new("DIC-135", "DIC 531-135", "residential", "R.A.S", 2017, 38, "Dubai", "DIC", "38 flats. Flat list comes later."),
        new("DIC-178", "DIC 531-178", "residential", "R.A.S", 2022, 99, "Dubai", "DIC", "99 flats. Flat list comes later."),
        new("MEY-B172", "Meydan B172", "villa", "Rashid", 2018, 1, "Dubai", "Meydan", "6 BHK."),
        new("DH-V68", "Dubai Hills V-68", "villa", "Ahmed", 2025, 1, "Dubai", "Dubai Hills", "5 bedroom."),
        new("DH-V69", "Dubai Hills V-69", "villa", "Shahid", 2025, 1, "Dubai", "Dubai Hills", "5 bedroom."),
        new("PEARL-1001", "AJM Pearl 1001", "apartment", "Haji Saheb", 2015, 1, "Ajman", "Ajman", "1 BHK."),
        new("RITAJ-530", "Ritaj A-530", "studio", "Haji Saheb", 2013, 1, "", "Ritaj", "Studio."),
        new("RITAJ-531", "Ritaj A-531", "studio", "Haji Saheb", 2015, 1, "", "Ritaj", "Studio."),
        new("V6869", "V-68/69 expense pool", "cost", "Ahmed/Shahid", 2025, 0, "Dubai", "Dubai Hills", "Sheet places villa 68 and 69 expenses here and also marks 5 bedroom from 2025. No rental income."),
        new("MD-V16", "MD Villa 16", "villa", "Ahmed", 0, 0, "", "", "Expense share of villa 16 and 19. No rental income on the sheet."),
        new("MD-V19", "MD Villa 19", "villa", "Shahid", 0, 0, "", "", "Expense share of villa 16 and 19. No rental income on the sheet."),
        new("DXB-SOUTH", "Dubai South", "cost", "", 0, 0, "Dubai", "Dubai South", "Legal case. No rental income on the sheet."),
        new("DIC-123", "DIC 531-123", "plot", "", 0, 0, "Dubai", "DIC", "New site plan. Not part of the 38 or 99 flats."),
        new("JAFZA", "Offshore Jafza", "cost", "", 0, 0, "Dubai", "Jafza", "Legal case file. No rental income on the sheet."),
        new("HOR-1410", "Horizon Tower A flat 1410", "apartment", "", 0, 1, "Dubai", "Horizon", "Community service only. No rental income on the sheet."),
    ];

    public static IReadOnlyList<LineSeed> Lines { get; } = BuildLines();

    private static LineSeed[] BuildLines()
    {
        var rows = new List<LineSeed>();
        void Income(string property, decimal amount)
            => rows.Add(new("Y2025-INC-" + property, property, "income", "rental", amount, "Rental income labelled December 2025."));
        void Expense(string property, string category, decimal amount)
            => rows.Add(new("Y2025-EXP-" + property + "-" + category.ToUpperInvariant(), property, "expense", category, amount, "Expense labelled December 2025."));
        void Recovered(string property)
            => rows.Add(new("Y2025-REC-" + property, property, "recovery", "recovered", 0, "Marked recovered on the December 2025 sheet. Amount not provided."));

        Income("LC-126", 691175m);
        Income("DIC-135", 1540783m);
        Income("DIC-178", 3298446m);
        Income("MEY-B172", 2016712m);
        Income("DH-V68", 87318m);
        Income("DH-V69", 87318m);
        Income("PEARL-1001", 285978m);
        Income("RITAJ-530", 38020m);
        Income("RITAJ-531", 30707m);

        Expense("LC-126", "bank_charges", 1395m);
        Expense("LC-126", "camp_head_lease", 106138m);
        Expense("LC-126", "vat", 7650m);

        Expense("DIC-135", "dcd_24x7", 3071m);
        Expense("DIC-135", "bank_charges", 119m);
        Expense("DIC-135", "dewa", 53185m);
        Expense("DIC-135", "ejari", 750m);
        Expense("DIC-135", "eid_allowance", 600m);
        Expense("DIC-135", "fire_alarm", 2100m);
        Expense("DIC-135", "pest_control", 1575m);
        Expense("DIC-135", "repairs", 83652m);
        Expense("DIC-135", "security", 46703m);
        Expense("DIC-135", "salary", 39706m);
        Expense("DIC-135", "garbage", 15627m);
        Expense("DIC-135", "water_tank", 800m);

        Expense("DIC-178", "dcd_24x7", 4677m);
        Expense("DIC-178", "dewa", 16914m);
        Expense("DIC-178", "eid_allowance", 419m);
        Expense("DIC-178", "fire_alarm", 9421m);
        Expense("DIC-178", "legal_case", 12806m);
        Expense("DIC-178", "cleaning", 69650m);
        Expense("DIC-178", "pest_control", 5770m);
        Expense("DIC-178", "repairs", 24712m);
        Expense("DIC-178", "security", 83168m);
        Expense("DIC-178", "security_refund", 2800m);
        Expense("DIC-178", "salary", 14000m);
        Expense("DIC-178", "sewerage", 24564m);
        Expense("DIC-178", "garbage", 12274m);
        Expense("DIC-178", "vat", 12975m);
        Expense("DIC-178", "water_tank", 2000m);

        Expense("MEY-B172", "community_service", 82456m);
        Expense("MEY-B172", "ejari", 220m);
        Expense("MEY-B172", "villa_expenses", 140537m);

        Expense("DH-V68", "community_service", 7020m);
        Expense("DH-V68", "dewa", 20336m);
        Expense("DH-V68", "repairs", 478m);

        Expense("DH-V69", "community_service", 7020m);
        Expense("DH-V69", "dewa", 17882m);

        Expense("PEARL-1001", "community_service", 3801m);
        Expense("PEARL-1001", "final_settlement", 451m);
        Expense("PEARL-1001", "title_deed_noc", 2100m);

        Expense("RITAJ-530", "community_service", 22772m);
        Expense("RITAJ-530", "dewa", 34m);

        Expense("RITAJ-531", "community_service", 22472m);
        Expense("V6869", "villa_68_69", 885492m);
        Expense("MD-V16", "villa_16_19", 794828m);
        Expense("MD-V19", "villa_16_19", 90000m);
        Expense("DXB-SOUTH", "legal_case", 5500m);
        Expense("DIC-123", "site_plan", 1020m);
        Expense("JAFZA", "legal_case", 27950m);
        Expense("HOR-1410", "community_service", 10437m);

        Recovered("LC-126");
        Recovered("PEARL-1001");
        return rows.ToArray();
    }
}
