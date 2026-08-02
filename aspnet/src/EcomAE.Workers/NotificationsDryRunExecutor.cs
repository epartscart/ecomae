namespace EcomAE.Workers;

/// <summary>
/// Safe dry-run validator for notification worker replacement. Send/writes are always blocked.
/// </summary>
public sealed class NotificationsDryRunExecutor : IMigrationWorkerJobDryRunExecutor
{
    public bool CanExecute(MigrationWorkerJob job) =>
        string.Equals(job.Key, "notifications", StringComparison.OrdinalIgnoreCase);

    public MigrationWorkerJobDryRunOutput Execute(MigrationWorkerJob job, MigrationWorkerJobRunRequest request)
    {
        var parameters = request.Parameters ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!parameters.TryGetValue("sample_recipients", out var sample) || string.IsNullOrWhiteSpace(sample))
        {
            const string warning = "Provide parameters.sample_recipients (comma/newline-separated emails) for dry-run validation.";
            return new MigrationWorkerJobDryRunOutput(
                job.Key,
                "dry-run-needs-sample",
                warning,
                new Dictionary<string, string>
                {
                    ["writes"] = "0",
                    ["sent"] = "0",
                    ["valid_recipients"] = "0",
                    ["invalid_recipients"] = "0"
                },
                [warning],
                WritesBlocked: true);
        }

        var recipients = sample.Split([',', '\n', '\r', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var valid = recipients.Count(IsLikelyEmail);
        var invalid = recipients.Length - valid;
        var status = valid > 0 ? "dry-run-validated" : "dry-run-invalid-sample";
        var summary = valid > 0
            ? $"Validated {valid} recipient(s); notification send blocked."
            : "No valid recipient emails found in sample_recipients.";

        return new MigrationWorkerJobDryRunOutput(
            job.Key,
            status,
            summary,
            new Dictionary<string, string>
            {
                ["writes"] = "0",
                ["sent"] = "0",
                ["valid_recipients"] = valid.ToString(),
                ["invalid_recipients"] = invalid.ToString()
            },
            invalid > 0 ? ["Some recipient values were invalid and skipped."] : [],
            WritesBlocked: true);
    }

    private static bool IsLikelyEmail(string value)
    {
        var at = value.IndexOf('@');
        return at > 0 && at < value.Length - 1 && value.Contains('.', StringComparison.Ordinal);
    }
}
