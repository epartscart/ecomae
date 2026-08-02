namespace EcomAE.Platform.Auth;

/// <summary>
/// Read-only module access entry for migration probes.
/// OpenAccess means the module has no modules_access rows (PHP: open to all).
/// Nested group inheritance expands user groups via groups.parent ancestry.
/// </summary>
public sealed record ModuleAclEntry(int ModuleId, string Caption, bool OpenAccess);
