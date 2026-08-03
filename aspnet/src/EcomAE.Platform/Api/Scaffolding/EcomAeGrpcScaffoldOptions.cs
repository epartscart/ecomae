namespace EcomAE.Platform.Api.Scaffolding;

/// <summary>
/// gRPC scaffolding options for internal service-to-service calls.
/// Not bound in <c>Program.cs</c>. Public APIs remain REST (GraphQL only when required).
/// </summary>
public sealed class EcomAeGrpcScaffoldOptions
{
    public const string SectionName = "EcomAe:Grpc";

    public int Port { get; set; } = 5200;

    public bool Enabled { get; set; }

    /// <summary>Always false until internal gRPC contracts are approved.</summary>
    public bool ExposePublicEndpoint { get; set; }
}
