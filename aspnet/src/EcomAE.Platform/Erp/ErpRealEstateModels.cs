namespace EcomAE.Platform.Erp;

public sealed record ErpRealEstateWriteRequest(
    string Action,
    long Id = 0,
    long RelatedId = 0,
    long LegalEntityId = 0,
    long PropertyId = 0,
    long UnitId = 0,
    long TenantId = 0,
    long LeaseId = 0,
    long ProjectId = 0,
    long ContractId = 0,
    long OpportunityId = 0,
    string? Code = null,
    string? Name = null,
    string? Status = null,
    string? Category = null,
    string? Emirate = null,
    string? City = null,
    string? Address = null,
    string? TitleDeed = null,
    string? Tenure = null,
    string? Note = null,
    string? Counterparty = null,
    string? Frequency = null,
    string? ChequeNo = null,
    string? BankName = null,
    string? VatCode = null,
    decimal Amount = 0,
    decimal Amount2 = 0,
    decimal Amount3 = 0,
    decimal Percent = 0,
    decimal Area = 0,
    int Count = 0,
    int ActorId = 0,
    string? Date1 = null,
    string? Date2 = null,
    bool ConfirmWrites = false);

public sealed record ErpRealEstateRow(
    long Id,
    string Kind,
    string Code,
    string Name,
    string Status,
    decimal Amount,
    decimal Amount2,
    string Detail,
    bool OwnedAsset);

public sealed record ErpRealEstateAlert(
    long Id,
    string Kind,
    string Title,
    string DueDate,
    string Status,
    long RelatedId);

public sealed record ErpRealEstateKpis(
    int Opportunities,
    int OwnedProperties,
    int Units,
    int ActiveLeases,
    int OpenCheques,
    int Projects,
    decimal PortfolioCost,
    decimal PortfolioFair,
    decimal Unrealized,
    decimal AnnualRent,
    decimal GrossYield,
    decimal Occupancy,
    decimal Collection,
    decimal CostToComplete);

public sealed record ErpRealEstateSnapshot(
    ErpRealEstateKpis Kpis,
    IReadOnlyList<ErpRealEstateRow> Opportunities,
    IReadOnlyList<ErpRealEstateRow> Properties,
    IReadOnlyList<ErpRealEstateRow> Units,
    IReadOnlyList<ErpRealEstateRow> Acquisitions,
    IReadOnlyList<ErpRealEstateRow> Disposals,
    IReadOnlyList<ErpRealEstateRow> Valuations,
    IReadOnlyList<ErpRealEstateRow> Tenants,
    IReadOnlyList<ErpRealEstateRow> Leases,
    IReadOnlyList<ErpRealEstateRow> Cheques,
    IReadOnlyList<ErpRealEstateRow> Deposits,
    IReadOnlyList<ErpRealEstateRow> Maintenance,
    IReadOnlyList<ErpRealEstateRow> Projects,
    IReadOnlyList<ErpRealEstateRow> Sales,
    IReadOnlyList<ErpRealEstateRow> Contractors,
    IReadOnlyList<ErpRealEstateRow> Contracts,
    IReadOnlyList<ErpRealEstateRow> Certificates,
    IReadOnlyList<ErpRealEstateRow> Documents,
    IReadOnlyList<ErpRealEstateAlert> Alerts,
    IReadOnlyList<ErpRealEstateCalc.KpiDefinition> Definitions,
    string Message);

public interface IErpRealEstateWriteService
{
    Task<ErpSimpleWriteResult> SaveAsync(ErpRealEstateWriteRequest request, CancellationToken cancellationToken = default);
}

public interface IErpRealEstateReadService
{
    Task<ErpRealEstateSnapshot> LoadAsync(CancellationToken cancellationToken = default);
}
