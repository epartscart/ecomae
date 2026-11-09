namespace EcomAE.Platform.Storefront;

/// <summary>
/// Core fallback when the Auto Parts package assembly is not shipped:
/// VIN decode answers "package_missing" instead of crashing resolution.
/// </summary>
public sealed class UnavailableLaximoVinDecodeService : ILaximoVinDecodeService
{
    public Task<LaximoVinDecodeResult> DecodeAsync(string? vinOrFrame, CancellationToken cancellationToken = default)
        => Task.FromResult(new LaximoVinDecodeResult(
            false, "package_missing", "VIN decode requires the Auto Parts package.",
            vinOrFrame ?? string.Empty, null, null, 0, []));
}
