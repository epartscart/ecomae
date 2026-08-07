namespace EcomAE.Platform.LifeOs.Part8;

/// <summary>
/// Part 8 — Client Applications, User Experience &amp; Cross-Platform Architecture (Ch.102–125).
/// Unified client UX scaffold (web console live; native clients roadmap).
/// </summary>
public interface ILifeOsClientExperience
{
    IReadOnlyList<LifeOsUxPrinciple> ExperiencePrinciples { get; }

    IReadOnlyList<LifeOsClientPlatform> ClientPlatforms { get; }

    IReadOnlyList<string> DesignPrinciples { get; }

    IReadOnlyList<LifeOsDesignComponent> DesignComponents { get; }

    IReadOnlyList<LifeOsNavMethod> NavigationMethods { get; }

    IReadOnlyList<LifeOsWorkspaceModule> AiWorkspaceModules { get; }

    IReadOnlyList<LifeOsSearchDomain> SearchDomains { get; }

    IReadOnlyList<LifeOsDashboardKind> DashboardKinds { get; }

    IReadOnlyList<string> VoiceCapabilities { get; }

    IReadOnlyList<string> ChatCapabilities { get; }

    IReadOnlyList<LifeOsWidget> SmartWidgets { get; }

    IReadOnlyList<LifeOsWorkspaceModule> ProductivityModules { get; }

    IReadOnlyList<string> MobileFeatures { get; }

    IReadOnlyList<string> DesktopFeatures { get; }

    IReadOnlyList<string> GlassesCapabilities { get; }

    IReadOnlyList<string> WearableDisplays { get; }

    IReadOnlyList<string> VehicleDashboardItems { get; }

    IReadOnlyList<string> ContinuityFlow { get; }

    IReadOnlyList<LifeOsAccessibilitySupport> AccessibilitySupports { get; }

    IReadOnlyList<LifeOsOfflineCapability> OfflineCapabilities { get; }

    IReadOnlyList<string> OfflineSyncFlow { get; }

    IReadOnlyList<LifeOsPersonalizationKnob> PersonalizationKnobs { get; }

    IReadOnlyList<string> AdaptiveLearningSignals { get; }

    IReadOnlyList<LifeOsFocusMode> FocusModes { get; }

    IReadOnlyList<string> NotificationPolicyFlow { get; }

    IReadOnlyList<LifeOsMultiUserProfile> MultiUserProfiles { get; }

    IReadOnlyList<string> DigitalTwinCapabilities { get; }

    IReadOnlyList<LifeOsUxMetric> ExperienceMetrics { get; }

    object ClientEcosystemDigest();

    object DesignAndNavigationDigest();

    object WorkspaceAndSearchDigest();

    object ModalityClientsDigest();

    object ContinuityAccessibilityOfflineDigest();

    object PersonalizationAndFocusDigest();

    object MetricsAndTwinDigest();

    object FullPart8Digest();
}
