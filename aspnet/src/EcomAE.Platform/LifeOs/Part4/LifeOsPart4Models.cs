namespace EcomAE.Platform.LifeOs.Part4;

public enum LifeOsRuntimeState
{
    Initialize,
    Authenticate,
    LoadProfile,
    LoadMemory,
    MonitorEvents,
    AnalyzeContext,
    Reason,
    Execute,
    Respond,
    Learn,
    IdleMonitoring,
    Shutdown
}

public enum LifeOsWakeMode
{
    Passive,
    AlwaysListeningLocal,
    PushToTalk,
    WearableActivation,
    ScheduledListening,
    MeetingMode,
    DrivingMode,
    HandsFreeMode,
    EmergencyMode
}

public enum LifeOsNotificationPriority
{
    Critical,
    High,
    Normal,
    Low,
    Silent
}

public sealed record LifeOsDeviceDescriptor(
    string Key,
    string Title,
    string Category,
    string Status,
    IReadOnlyList<string> Capabilities);

public sealed record LifeOsRuntimeComponent(
    string Key,
    string Title,
    string Responsibility);

public sealed record LifeOsModalityPipeline(
    string Modality,
    IReadOnlyList<string> Stages,
    IReadOnlyList<string> Features);

public sealed record LifeOsPerformanceTarget(
    string Component,
    string Target,
    int BudgetMs);

public sealed record LifeOsNotificationDecision(
    string NotificationId,
    LifeOsNotificationPriority Priority,
    bool Interrupt,
    string Reason,
    string DeliveryChannel);

public sealed record LifeOsSyncSnapshot(
    string SessionId,
    IReadOnlyList<string> ConnectedDevices,
    string ActiveConversation,
    string CurrentTask,
    IReadOnlyList<string> RunningWorkflows,
    DateTimeOffset SyncedAt);

public sealed record LifeOsRuntimeTickResult(
    string TickId,
    LifeOsRuntimeState State,
    string Channel,
    string Summary,
    LifeOsNotificationDecision? Notification,
    LifeOsSyncSnapshot Sync,
    IReadOnlyList<string> Pipeline);
