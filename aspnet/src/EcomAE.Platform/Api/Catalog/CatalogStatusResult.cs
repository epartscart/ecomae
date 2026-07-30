namespace EcomAE.Platform.Api.Catalog;

public sealed record CatalogStatusResult(
    string Surface,
    string LegacyPhpRoute,
    string AspNetRoute,
    string MigrationStatus,
    string[] NextSteps);
