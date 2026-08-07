namespace EcomAE.Platform.LifeOs.Part8;

public sealed record LifeOsUxPrinciple(string Key, string Title);

public sealed record LifeOsClientPlatform(
    string Key,
    string Family,
    string Title,
    string Status);

public sealed record LifeOsDesignComponent(string Key, string Title);

public sealed record LifeOsNavMethod(string Key, string Title);

public sealed record LifeOsWorkspaceModule(string Key, string Title);

public sealed record LifeOsSearchDomain(string Key, string Title);

public sealed record LifeOsDashboardKind(
    string Key,
    string Title,
    IReadOnlyList<string> Panels);

public sealed record LifeOsWidget(string Key, string Title);

public sealed record LifeOsFocusMode(string Key, string Title);

public sealed record LifeOsAccessibilitySupport(string Key, string Title);

public sealed record LifeOsOfflineCapability(string Key, string Title);

public sealed record LifeOsPersonalizationKnob(string Key, string Title);

public sealed record LifeOsUxMetric(
    string Category,
    string Name,
    string Status);

public sealed record LifeOsMultiUserProfile(string Key, string Title);
