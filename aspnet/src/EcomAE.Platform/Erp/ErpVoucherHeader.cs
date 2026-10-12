using System.Globalization;

namespace EcomAE.Platform.Erp;

/// <summary>
/// Shared ERP document identity: every voucher and invoice carries a date and a
/// voucher/invoice number. Number is allocated on post; the create form shows Auto.
/// </summary>
public static class ErpVoucherHeader
{
    public const string AutoNumber = "Auto";

    public static string TodayDate()
        => DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
