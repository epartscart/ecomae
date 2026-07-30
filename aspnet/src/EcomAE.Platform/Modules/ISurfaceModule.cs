namespace EcomAE.Platform.Modules;

public interface ISurfaceModule
{
    SurfaceModuleDescriptor Descriptor { get; }

    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
