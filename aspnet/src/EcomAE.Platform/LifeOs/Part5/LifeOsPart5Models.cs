namespace EcomAE.Platform.LifeOs.Part5;

public sealed record LifeOsMicroservice(
    string Key,
    string Title,
    string Domain,
    string OwnsDatabase,
    string Status);

public sealed record LifeOsApiConvention(
    string Method,
    string PathExample,
    string Purpose);

public sealed record LifeOsApiEnvelope(
    bool Success,
    object? Data,
    object? Meta,
    IReadOnlyList<object> Errors,
    LifeOsApiError? Error = null);

public sealed record LifeOsApiError(string Code, string Message);

public sealed record LifeOsEventTopic(
    string Topic,
    string Description);

public sealed record LifeOsDataStore(
    string Key,
    string Technology,
    IReadOnlyList<string> Workloads,
    string Status);

public sealed record LifeOsWorkflowLifecycleStage(string Name, string Note);

public sealed record LifeOsPluginManifest(
    string Name,
    string Version,
    string Kind,
    IReadOnlyList<string> Permissions);

public sealed record LifeOsAgentSdkContract(
    string Field,
    string Requirement);

public sealed record LifeOsAiRoute(
    string Task,
    string PreferredModel,
    string Notes);

public sealed record LifeOsAuthMethod(string Key, string Title, string Status);

public sealed record LifeOsTenantKind(string Key, string Title);

public sealed record LifeOsObservabilitySignal(
    string Category,
    string Name,
    string Status);
