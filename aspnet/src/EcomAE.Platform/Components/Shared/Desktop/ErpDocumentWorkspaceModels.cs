namespace EcomAE.Platform.Components.Shared.Desktop;

public sealed record ErpDocumentWorkspaceField(string Label, string Value, string Group = "General");

public sealed record ErpDocumentWorkspaceLine(
    string ItemCode,
    string Description,
    decimal Quantity,
    string Unit,
    string Site,
    string Warehouse,
    decimal UnitPrice,
    decimal Discount,
    decimal Tax,
    decimal LineAmount,
    decimal FulfilledQuantity = 0,
    string SourceReference = "");
