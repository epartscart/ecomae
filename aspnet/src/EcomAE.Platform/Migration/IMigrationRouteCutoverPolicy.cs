using EcomAE.Platform.Services;

namespace EcomAE.Platform.Migration;

public interface IMigrationRouteCutoverPolicy
{
    MigrationRouteCutoverDecision Decide(TenantContext tenant);
}
