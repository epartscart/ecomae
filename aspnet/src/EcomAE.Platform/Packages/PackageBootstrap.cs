using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EcomAE.Platform.Packages;

/// <summary>
/// Phase-1 compile-time package bootstrapper: loads each deployed package's
/// well-known assembly and invokes its <c>Register(IServiceCollection,
/// IConfiguration)</c> entry point. Assembly names are allowlisted in
/// <see cref="AssemblyByPackage"/> — arbitrary assembly names are never loaded;
/// missing assemblies degrade gracefully (fail-closed service fallbacks).
/// </summary>
public static class PackageBootstrap
{
    /// <summary>package_id → assembly name. Compile-time allowlist; extend per package.</summary>
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
                // LoadFromAssemblyPath: package DLLs ship beside the app but are NOT in
                // deps.json — a plain Assembly.Load(name) cannot resolve them.
                var path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");
                var assembly = TryLoadAssembly(path);
                if (assembly is null)
                {
                    log?.LogWarning("package {Package} assembly {Assembly} not shipped", packageId, assemblyName);
                    continue;
                }

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
            catch (Exception ex)
            {
                log?.LogError(ex, "package {Package} registration failed", packageId);
            }
        }
    }

    /// <summary>Loads a package assembly from an exact path; null when absent.</summary>
    internal static Assembly? TryLoadAssembly(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    }
}
