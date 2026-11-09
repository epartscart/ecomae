namespace EcomAE.Platform.Storefront;

/// <summary>
/// VIN decode extension point. The Auto Parts package implements this
/// (Guayaquil <c>FindVehicleByVIN</c>/<c>FindVehicleByFrameNo</c> twin);
/// Standard Core holds only the contract and result records.
/// </summary>
public interface ILaximoVinDecodeService
{
    Task<LaximoVinDecodeResult> DecodeAsync(string? vinOrFrame, CancellationToken cancellationToken = default);
}

public sealed record LaximoVehicleRow(string Brand, string Name, string Catalog, string Ssd);

public sealed record LaximoVinDecodeResult(
    bool Ok,
    string Code,
    string Message,
    string Vin,
    string? Manufacturer,
    string? ModelLabel,
    int VehicleCount,
    IReadOnlyList<LaximoVehicleRow> Vehicles)
{
    public object ToPayload(object session) => new
    {
        ok = Ok,
        surface = "storefront",
        writes = 0,
        phpAuthoritative = false,
        validation_code = Code,
        message = Message,
        vin = Vin,
        manufacturer = Manufacturer,
        model_label = ModelLabel,
        vehicle_count = VehicleCount,
        vehicles = Vehicles.Select(v => new { brand = v.Brand, name = v.Name, catalog = v.Catalog, ssd = v.Ssd }),
        session
    };
}
