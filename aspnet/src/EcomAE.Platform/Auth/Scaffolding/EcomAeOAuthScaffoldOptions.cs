namespace EcomAE.Platform.Auth.Scaffolding;

/// <summary>
/// OAuth 2.1 / MFA / modern identity scaffolding options.
/// Not bound in <c>Program.cs</c>. PHP cookie bridge + API keys remain authoritative.
/// </summary>
public sealed class EcomAeOAuthScaffoldOptions
{
    public const string SectionName = "EcomAe:OAuth";

    public string Authority { get; set; } = string.Empty;

    public string Audience { get; set; } = "ecomae-platform";

    public bool Enabled { get; set; }

    public bool RequireMfa { get; set; }

    /// <summary>Always false until modern-identity parity evidence exists.</summary>
    public bool ReplacePhpCookieBridge { get; set; }
}
