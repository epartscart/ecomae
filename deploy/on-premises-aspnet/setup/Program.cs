using System.Diagnostics;

namespace EcomAE.OnPremSetup;

/// <summary>
/// Windows / Linux setup.exe equivalent. Starts the ASP.NET compose stack.
/// Does not install PHP and will not invent a database password.
/// </summary>
internal static class Program
{
    public static int Main(string[] args)
    {
        var root = FindPackRoot();
        var host = Arg(args, "--host") ?? Prompt("Public host (example erp.company.local)");
        var password = Arg(args, "--db-password") ?? PromptSecret("MariaDB password (you choose)");
        var port = Arg(args, "--port") ?? "5100";
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Host and database password are required. The installer will not invent a password.");
            return 1;
        }

        var script = OperatingSystem.IsWindows()
            ? Path.Combine(root, "setup.ps1")
            : Path.Combine(root, "install.sh");
        if (!File.Exists(script))
        {
            Console.Error.WriteLine("Pack files missing next to setup.exe.");
            return 1;
        }

        var start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\" -HostName \"{host}\" -DbPassword \"{password}\" -Port {port}")
            : new ProcessStartInfo("/usr/bin/env", $"bash \"{script}\" --host \"{host}\" --db-password \"{password}\" --port {port}");
        start.WorkingDirectory = root;
        start.UseShellExecute = false;
        using var process = Process.Start(start);
        process?.WaitForExit();
        return process?.ExitCode ?? 1;
    }

    private static string FindPackRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docker-compose.yml"))
                && File.Exists(Path.Combine(dir.FullName, "install.sh")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
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
