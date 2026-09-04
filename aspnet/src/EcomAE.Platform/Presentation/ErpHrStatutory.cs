namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_erp_payroll_calc</c> + UAE <c>epc_hr_gratuity</c> / leave (AE default).</summary>
public static class ErpHrStatutory
{
    public const int StandardDays = 30;

    public static decimal EstPay(decimal basic, decimal allowances, decimal daysWorked, int standardDays = StandardDays)
    {
        var days = daysWorked > 0 ? daysWorked : standardDays;
        if (standardDays <= 0) return 0m;
        return Math.Round((basic + allowances) / standardDays * days, 2, MidpointRounding.AwayFromZero);
    }

    public static (bool Eligible, decimal Amount, decimal Days, string Note) GratuityAe(decimal basicSalary, double years)
    {
        if (years < 1.0)
            return (false, 0m, 0m, "Under 1 year: no gratuity (UAE).");

        var first = Math.Min(years, 5.0) * 21.0;
        var beyond = Math.Max(years - 5.0, 0.0) * 30.0;
        var days = first + beyond;
        var daily = basicSalary / StandardDays;
        var amount = (decimal)days * daily;
        var cap = 24m * basicSalary;
        if (amount > cap) amount = cap;
        return (true, Math.Round(amount, 2, MidpointRounding.AwayFromZero),
            (decimal)Math.Round(days, 2),
            "UAE Federal Decree-Law 33/2021: 21 days/yr first 5 yrs, 30 days/yr beyond; cap 2 years’ basic.");
    }

    public static (decimal AnnualDays, decimal AccruedDays) LeaveAe(double serviceMonths)
    {
        var annual = serviceMonths >= 6 ? 30m : 0m;
        var accrued = annual <= 0 || serviceMonths <= 0
            ? 0m
            : Math.Round(annual * (decimal)(serviceMonths / 12.0), 1, MidpointRounding.AwayFromZero);
        return (annual, accrued);
    }

    public static decimal LeaveSalary(decimal basicSalary, decimal accruedDays)
        => Math.Round(basicSalary / StandardDays * accruedDays, 2, MidpointRounding.AwayFromZero);

    public static double ServiceYears(long hireUnix, long nowUnix)
    {
        if (hireUnix <= 0 || nowUnix <= hireUnix) return 0;
        return (nowUnix - hireUnix) / 31557600.0;
    }
}
