namespace EcomAE.Platform.Messaging;

/// <summary>
/// RabbitMQ 4 scaffolding options (documented Kafka alternative).
/// Not bound in <c>Program.cs</c>; Kafka remains the primary messaging target.
/// </summary>
public sealed class EcomAeRabbitMqScaffoldOptions
{
    public const string SectionName = "EcomAe:RabbitMq";

    public string HostName { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public bool Enabled { get; set; }

    /// <summary>Always false — Kafka is primary; RabbitMQ alternate must not publish yet.</summary>
    public bool AllowPublish { get; set; }
}
