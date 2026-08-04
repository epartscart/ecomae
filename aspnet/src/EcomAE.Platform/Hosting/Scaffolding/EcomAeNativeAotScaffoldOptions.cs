namespace EcomAE.Platform.Hosting.Scaffolding;

/// <summary>
/// Native AOT scaffolding options for isolated services only.
/// Not bound in <c>Program.cs</c>. Do not mandate AOT for the main platform foundation.
/// </summary>
public sealed class EcomAeNativeAotScaffoldOptions
{
    public const string SectionName = "EcomAe:NativeAot";

    public bool Enabled { get; set; }

    /// <summary>Always false for the modular monolith foundation path.</summary>
    public bool RequireForPlatformHost { get; set; }

    /// <summary>Evaluate only after trimming/reflection compatibility evidence.</summary>
    public bool AllowIsolatedServiceEvaluation { get; set; } = true;
}
