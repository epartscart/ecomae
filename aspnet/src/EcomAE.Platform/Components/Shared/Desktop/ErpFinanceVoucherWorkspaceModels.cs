namespace EcomAE.Platform.Components.Shared.Desktop;

public sealed record ErpFinanceVoucherField(string Label, string Value, string Group = "General");

public sealed record ErpFinanceVoucherLine(
    string Date,
    string Voucher,
    string Company,
    string AccountType,
    string Account,
    string AccountName,
    string Description,
    decimal Debit,
    decimal Credit,
    string Currency,
    string OffsetCompany = "",
    string OffsetAccountType = "",
    string OffsetAccount = "",
    string Invoice = "",
    string Tax = "",
    string Dimensions = "");

public sealed record ErpFinanceVoucherAction(string Label, string Icon, string Description, bool Enabled = false);
