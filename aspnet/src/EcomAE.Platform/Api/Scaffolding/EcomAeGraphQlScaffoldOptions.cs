namespace EcomAE.Platform.Api.Scaffolding;

/// <summary>
/// GraphQL scaffolding options (only when required by Enterprise BOS law).
/// Not bound in <c>Program.cs</c>. REST remains the default API surface.
/// </summary>
public sealed class EcomAeGraphQlScaffoldOptions
{
    public const string SectionName = "EcomAe:GraphQl";

    public string Path { get; set; } = "/graphql";

    public bool Enabled { get; set; }

    /// <summary>Always false until a concrete GraphQL use-case is approved.</summary>
    public bool ExposePublicEndpoint { get; set; }
}
