namespace EcomAE.Platform.LifeOs.Spec;

/// <summary>LifeOS™ Master Technical Specification v4.0 — Parts 1–10 registry + digests.</summary>
public interface ILifeOsMasterSpec
{
    string Version { get; }

    IReadOnlyList<LifeOsSpecPart> Parts { get; }

    IReadOnlyList<LifeOsModalityAdapter> MultimodalAdapters { get; }

    IReadOnlyList<LifeOsApiSurface> ApiSurfaces { get; }

    IReadOnlyList<LifeOsSecurityControl> SecurityControls { get; }

    IReadOnlyList<LifeOsClientSurface> Clients { get; }

    IReadOnlyList<LifeOsPluginDescriptor> Plugins { get; }

    /// <summary>
    /// Master Spec JSON envelope for <c>/lifeos/spec/json</c>.
    /// Pass rich Part 2–10 digests via <paramref name="runtime"/>; each lands at the top level
    /// (<c>part2</c>…<c>part10</c>), never nested under another part.
    /// </summary>
    object FullDigest(ILifeOsCognitiveEngines cognitive, LifeOsSpecRuntimeDigests? runtime = null);
}
