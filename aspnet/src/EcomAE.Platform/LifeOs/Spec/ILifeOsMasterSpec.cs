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

    object FullDigest(ILifeOsCognitiveEngines cognitive, object? part2Architecture);
}
