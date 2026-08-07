using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Part4;

/// <summary>
/// Part 4 — Multimodal Runtime & Human Interaction (Ch.26–41).
/// Ambient input manager → event stream → context sync → cognitive handoff.
/// </summary>
public interface ILifeOsMultimodalRuntime
{
    IReadOnlyList<LifeOsRuntimeComponent> KernelComponents { get; }

    IReadOnlyList<LifeOsDeviceDescriptor> Devices { get; }

    IReadOnlyList<LifeOsModalityPipeline> ModalityPipelines { get; }

    IReadOnlyList<LifeOsPerformanceTarget> PerformanceTargets { get; }

    IReadOnlyList<string> InteractionModes { get; }

    LifeOsRuntimeState CurrentState { get; }

    LifeOsSyncSnapshot UnifiedSession { get; }

    Task<LifeOsRuntimeTickResult> ProcessInputAsync(LifeOsEvent input, string? deviceKey = null, CancellationToken cancellationToken = default);

    LifeOsNotificationDecision ClassifyNotification(
        string title,
        string sender,
        string activityContext);

    object FullPart4Digest();
}
