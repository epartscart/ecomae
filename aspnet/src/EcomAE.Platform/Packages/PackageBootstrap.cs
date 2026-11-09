using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Packages;

/// <summary>
/// Phase-1 compile-time package bootstrapper: loads each deployed package's
/// well-known assembly and invokes its <c>Register(IServiceCollection,
/// IConfiguration)</c> entry point. Assembly names are hard-mapped here — no
/// dynamic probing; missing assemblies degrade gracefully.
/// </summary>
public static class PackageBootstrap
{
    /// <summary>package_id → assembly name. Compile-time known; extend per package.</summary>
    private static readonly IReadOnlyDictionary<string, string> AssemblyByPackage =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["industry.auto_parts"] = "EcomAE.Package.AutoParts",
            ["customer.auto247"] = "EcomAE.Customer.Auto247",
        };

    /// <summary>
    /// Loads each deployed package assembly and calls its <c>{Package}Package.Register</c>
    /// static method. Services register unconditionally; runtime on/off is the
    /// resolver's job (<see cref="IPackageGate"/>).
    /// </summary>
    public static void RegisterPackages(IServiceCollection services, IConfiguration configuration, ILogger? log = null)
    {
        foreach (var (packageId, assemblyName) in AssemblyByPackage)
        {
            try
            {
                var assembly = Assembly.Load(new AssemblyName(assemblyName));
                var entry = assembly.GetTypes()
                    .FirstOrDefault(t => t.IsSealed && t.IsAbstract && t.Name.EndsWith("Package", StringComparison.Ordinal));
                var register = entry?.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
                if (register is null)
                {
                    log?.LogWarning("package {Package} assembly {Assembly} has no static Register method", packageId, assemblyName);
                    continue;
                }

                register.Invoke(null, [services, configuration]);
            }
            catch (FileNotFoundException)
            {
                // Package assembly not shipped — registry resolution stays fail-closed.
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "package {Package} registration failed", packageId);
            }
        }
    }
}
