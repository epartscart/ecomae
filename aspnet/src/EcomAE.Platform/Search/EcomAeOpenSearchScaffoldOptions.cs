namespace EcomAE.Platform.Search;

/// <summary>
/// OpenSearch 3 scaffolding options for future enterprise search/logs.
/// Not bound in <c>Program.cs</c> and must not replace PHP/modex search until parity evidence exists.
/// </summary>
public sealed class EcomAeOpenSearchScaffoldOptions
{
    public const string SectionName = "EcomAe:OpenSearch";

    public string NodeUri { get; set; } = string.Empty;

    public string DefaultIndex { get; set; } = "ecomae";

    public bool Enabled { get; set; }

    /// <summary>Always false in scaffolding — catalog/storefront search remains PHP/UMAPI authoritative.</summary>
    public bool ReplacePhpSearch { get; set; }
}
