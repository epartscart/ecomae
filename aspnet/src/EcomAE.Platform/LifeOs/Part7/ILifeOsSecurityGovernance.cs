namespace EcomAE.Platform.LifeOs.Part7;

/// <summary>
/// Part 7 — Enterprise Security, Privacy, Compliance &amp; AI Governance (Ch.82–101).
/// Trust framework scaffold (not a live SOC / IdP replacement).
/// </summary>
public interface ILifeOsSecurityGovernance
{
    IReadOnlyList<LifeOsSecurityPrinciple> SecurityPrinciples { get; }

    IReadOnlyList<string> ZeroTrustNeverTrust { get; }

    IReadOnlyList<string> ZeroTrustFlow { get; }

    IReadOnlyList<LifeOsIdentityType> IdentityTypes { get; }

    IReadOnlyList<LifeOsAuthMethod> AuthenticationMethods { get; }

    IReadOnlyList<LifeOsMfaFactor> MfaFactors { get; }

    IReadOnlyList<string> AdaptiveMfaTriggers { get; }

    IReadOnlyList<LifeOsRole> RbacRoles { get; }

    IReadOnlyList<LifeOsAbacAttribute> AbacAttributes { get; }

    IReadOnlyList<string> AgentIdentityFields { get; }

    LifeOsAgentPermissionSample SampleAgentPermissions { get; }

    IReadOnlyList<LifeOsDataClassLevel> DataClassificationLevels { get; }

    IReadOnlyList<LifeOsPermissionCategory> PermissionCategories { get; }

    IReadOnlyList<LifeOsConsentState> ConsentStates { get; }

    IReadOnlyList<LifeOsRetentionPolicy> RetentionPolicies { get; }

    IReadOnlyList<LifeOsAuditEventType> AuditEventTypes { get; }

    IReadOnlyList<string> AuditRecordFields { get; }

    IReadOnlyList<LifeOsComplianceFramework> ComplianceFrameworks { get; }

    IReadOnlyList<string> AiGovernanceFlow { get; }

    IReadOnlyList<string> AiGovernanceObjectives { get; }

    IReadOnlyList<string> SafetyEngineChecks { get; }

    IReadOnlyList<LifeOsSafetyDecision> SafetyDecisions { get; }

    IReadOnlyList<LifeOsThreatSignal> ThreatSignals { get; }

    IReadOnlyList<string> IncidentResponseWorkflow { get; }

    IReadOnlyList<LifeOsAdminConsoleModule> EnterpriseAdminModules { get; }

    IReadOnlyList<LifeOsResidencyOption> DataResidencyOptions { get; }

    IReadOnlyList<string> SocCapabilities { get; }

    IReadOnlyList<LifeOsDeploymentModel> DeploymentModels { get; }

    object ZeroTrustDigest();

    object IamDigest();

    object AuthorizationDigest();

    object EncryptionDigest();

    object PrivacyAndConsentDigest();

    object AiGovernanceDigest();

    object ThreatAndSocDigest();

    object EnterpriseAndDeploymentDigest();

    object FullPart7Digest();
}
