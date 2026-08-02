using EcomAE.Platform.Services;

namespace EcomAE.Platform.Surfaces;

public interface ISurfaceShellCatalog
{
    SurfaceShellResponse Build(string surfaceKey, TenantContext? tenant);
}
