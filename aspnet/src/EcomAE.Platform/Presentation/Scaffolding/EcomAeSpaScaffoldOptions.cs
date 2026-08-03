namespace EcomAE.Platform.Presentation.Scaffolding;

/// <summary>
/// Angular 20 / React 19 SPA target scaffolding options.
/// Not bound in <c>Program.cs</c>. Interim UI remains Blazor SSR hybrid on exact-routes only.
/// SPA must call ASP.NET Core APIs only — never PHP business backends.
/// </summary>
public sealed class EcomAeSpaScaffoldOptions
{
    public const string SectionName = "EcomAe:Spa";

    /// <summary>angular | react</summary>
    public string Framework { get; set; } = "angular";

    public string ApiBasePath { get; set; } = "/api/v1";

    public bool Enabled { get; set; }

    /// <summary>Always false until Blazor hybrid parity evidence exists.</summary>
    public bool ReplaceBlazorHybridPresentation { get; set; }
}
