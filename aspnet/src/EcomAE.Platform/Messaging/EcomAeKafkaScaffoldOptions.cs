namespace EcomAE.Platform.Messaging;

/// <summary>
/// Kafka 4 scaffolding options for future domain/integration events.
/// Not bound in <c>Program.cs</c>; workers remain write-blocked dry-run until job parity is approved.
/// </summary>
public sealed class EcomAeKafkaScaffoldOptions
{
    public const string SectionName = "EcomAe:Kafka";

    public string BootstrapServers { get; set; } = string.Empty;

    public string ClientId { get; set; } = "ecomae-platform";

    public bool Enabled { get; set; }

    /// <summary>Always false in scaffolding — no producer publish until dry-run parity evidence.</summary>
    public bool AllowPublish { get; set; }
}
