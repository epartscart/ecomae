namespace EcomAE.Platform.Integrations.Scaffolding;

/// <summary>
/// Python FastAPI AI-sidecar scaffolding options.
/// Not bound in <c>Program.cs</c>. Python must stay AI-only — no business SoR writes.
/// </summary>
public sealed class EcomAeAiSidecarScaffoldOptions
{
    public const string SectionName = "EcomAe:AiSidecar";

    public string BaseUrl { get; set; } = "http://127.0.0.1:8100";

    public bool Enabled { get; set; }

    /// <summary>Always false — AI sidecars must not own business transactions/permissions/SoR.</summary>
    public bool AllowBusinessWrites { get; set; }
}
