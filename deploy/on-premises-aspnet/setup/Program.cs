using System.Diagnostics;
using System.Text;

namespace EcomAE.OnPremSetup;

/// <summary>
/// Customer setup.exe / ecomae-setup. On-premises or hybrid.
/// Does not install leftover runtimes and will not invent a database password.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("ECOM AE setup");
        Console.WriteLine("On premises or hybrid. Windows: setup.exe. Linux: ecomae-setup.");
        Console.WriteLine();

        var root = FindPackRoot();
        var mode = Arg(args, "--mode") ?? InferMode(root) ?? Prompt("Mode (on-premises / hybrid)");
        mode = (mode ?? "").Trim().ToLowerInvariant();
        if (mode is "onprem" or "on-prem" or "premises")
        {
            mode = "on-premises";
        }

        if (mode == "hybrid")
        {
            return RunHybrid(root, args);
        }

        if (mode is "on-premises" or "")
        {
            return RunOnPremises(root, args);
        }

        Console.Error.WriteLine("Mode must be on-premises or hybrid.");
        return 1;
    }

    private static int RunOnPremises(string root, string[] args)
    {
        var host = Arg(args, "--host") ?? Arg(args, "--domain") ?? Prompt("Public host (example erp.company.local)");
        var password = Arg(args, "--db-password") ?? PromptSecret("MariaDB password (you choose)");
        var port = Arg(args, "--port") ?? "5100";
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Host and database password are required. The installer will not invent a password.");
            return 1;
        }

        if (OperatingSystem.IsWindows())
        {
            var ps1 = Path.Combine(root, "setup.ps1");
            if (File.Exists(ps1))
            {
                return Run("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{ps1}\" -HostName \"{host}\" -DbPassword \"{password}\" -Port {port}", root);
            }
        }

        var sh = Path.Combine(root, "ecomae-setup");
        if (!File.Exists(sh))
        {
            sh = Path.Combine(root, "install.sh");
        }

        if (!File.Exists(sh))
        {
            Console.Error.WriteLine("Unzip the setup pack and run setup.exe / ecomae-setup from that folder.");
            return 1;
        }

        return Run("/usr/bin/env", $"bash \"{sh}\" --host \"{host}\" --db-password \"{password}\" --port {port}", root);
    }

    private static int RunHybrid(string root, string[] args)
    {
        var site = Arg(args, "--site-key") ?? Prompt("Site key (from Super CP register)");
        var cloud = Arg(args, "--cloud-url") ?? Prompt("Cloud URL") ?? "https://www.ecomae.com";
        var enrollment = Arg(args, "--enrollment") ?? Prompt("Enrollment (enr_…)");
        var installation = Arg(args, "--installation") ?? Prompt("Installation (inst_…)");
        var password = Arg(args, "--db-password") ?? Arg(args, "--dsn") ?? "";
        if (string.IsNullOrWhiteSpace(site) || string.IsNullOrWhiteSpace(cloud)
            || string.IsNullOrWhiteSpace(enrollment) || string.IsNullOrWhiteSpace(installation))
        {
            Console.Error.WriteLine("Hybrid needs --site-key --cloud-url --enrollment --installation. Register first. Do not invent tokens.");
            return 1;
        }

        if (!cloud.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Cloud URL must be HTTPS. The agent is outbound only.");
            return 1;
        }

        if (OperatingSystem.IsWindows())
        {
            var ps1 = Path.Combine(root, "setup.ps1");
            if (File.Exists(ps1))
            {
                var extra = string.IsNullOrWhiteSpace(password) ? "" : $" -DbPassword \"{password}\"";
                return Run("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{ps1}\" -SiteKey \"{site}\" -CloudUrl \"{cloud}\" -Enrollment \"{enrollment}\" -Installation \"{installation}\"{extra}", root);
            }
        }

        var sh = Path.Combine(root, "ecomae-setup");
        if (!File.Exists(sh))
        {
            sh = Path.Combine(root, "install.sh");
        }

        if (!File.Exists(sh))
        {
            Console.Error.WriteLine("Unzip the hybrid pack and run setup.exe / ecomae-setup from that folder.");
            return 1;
        }

        var cmd = new StringBuilder();
        cmd.Append("bash \"").Append(sh).Append("\" --site-key \"").Append(site)
            .Append("\" --cloud-url \"").Append(cloud)
            .Append("\" --enrollment \"").Append(enrollment)
            .Append("\" --installation \"").Append(installation).Append('"');
        if (!string.IsNullOrWhiteSpace(password))
        {
            cmd.Append(" --dsn \"").Append(password).Append('"');
        }

        return Run("/usr/bin/env", cmd.ToString(), root);
    }

    private static string? InferMode(string root)
    {
        if (File.Exists(Path.Combine(root, "docker-compose.hybrid.yml"))
            || File.Exists(Path.Combine(root, "sync-agent", "sync-agent.py")))
        {
            return "hybrid";
        }

        if (File.Exists(Path.Combine(root, "docker-compose.yml")))
        {
            return "on-premises";
        }

        return null;
    }

    private static string FindPackRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "install.sh"))
                || File.Exists(Path.Combine(dir.FullName, "setup.ps1"))
                || File.Exists(Path.Combine(dir.FullName, "ecomae-setup")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static int Run(string file, string arguments, string workDir)
    {
        var start = new ProcessStartInfo(file, arguments)
        {
            WorkingDirectory = workDir,
            UseShellExecute = false,
        };
        using var process = Process.Start(start);
        process?.WaitForExit();
        return process?.ExitCode ?? 1;
    }

    private static string? Arg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static string Prompt(string label)
    {
        Console.Write(label + ": ");
        return Console.ReadLine()?.Trim() ?? "";
    }

    private static string PromptSecret(string label)
    {
        Console.Write(label + ": ");
        return Console.ReadLine()?.Trim() ?? "";
    }
}
