using EcomAE.Platform.Surfaces;

namespace EcomAE.Platform.Presentation;

public interface ILegacyHtmlShellRenderer
{
    string Render(
        string surfaceKey,
        SurfaceShellResponse shell,
        object? sessionPayload,
        string? note = null);
}
