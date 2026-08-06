using Xunit;

namespace EcomAE.Platform.Tests;

/// <summary>
/// Serializes tests that mutate <see cref="Presentation.StorefrontSurfaceLinks.PreferAspNetApps"/>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PreferAspNetAppsCollection : ICollectionFixture<object>
{
    public const string Name = "PreferAspNetApps";
}
