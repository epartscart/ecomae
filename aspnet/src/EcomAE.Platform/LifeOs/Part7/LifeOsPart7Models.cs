namespace EcomAE.Platform.LifeOs.Part7;

public sealed record LifeOsSecurityPrinciple(string Key, string Title);

public sealed record LifeOsIdentityType(string Key, string Title);

public sealed record LifeOsAuthMethod(string Key, string Title, string Kind);

public sealed record LifeOsMfaFactor(string Key, string Title);

public sealed record LifeOsRole(string Key, string Title);

public sealed record LifeOsAbacAttribute(string Key, string Title);

public sealed record LifeOsDataClassLevel(
    string Key,
    string Title,
    IReadOnlyList<string> Examples);

public sealed record LifeOsPermissionCategory(string Key, string Title);

public sealed record LifeOsConsentState(string Key, string Title);

public sealed record LifeOsRetentionPolicy(string DataType, string DefaultRetention);

public sealed record LifeOsAuditEventType(string Key, string Title);

public sealed record LifeOsComplianceFramework(string Key, string Title, string Notes);

public sealed record LifeOsSafetyDecision(string Key, string Title);

public sealed record LifeOsThreatSignal(string Key, string Title);

public sealed record LifeOsAdminConsoleModule(string Key, string Title);

public sealed record LifeOsResidencyOption(string Key, string Title);

public sealed record LifeOsDeploymentModel(string Key, string Title, string Notes);

public sealed record LifeOsAgentPermissionSample(
    string Agent,
    IReadOnlyList<string> Permissions);
