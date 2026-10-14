using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Migration;

public interface IErpD365ParameterDryRun
{
    ErpD365WorkspaceDryRunResult Evaluate(ErpD365ParameterWriteRequest request, bool confirmWrites);
}

public sealed class ErpD365ParameterDryRun : IErpD365ParameterDryRun
{
    public ErpD365WorkspaceDryRunResult Evaluate(ErpD365ParameterWriteRequest request, bool confirmWrites)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (confirmWrites)
        {
            return new(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write.",
                0,
                false);
        }

        var check = ErpD365ParameterWriteService.Validate(request);
        if (check is not null)
        {
            return new("dry-run-invalid", check.Code, check.Message, 0, false);
        }

        ErpD365ProcessCatalog.TryGetParameter(request.Area, request.Key, out var parameter);
        return new(
            "dry-run-validated",
            "ok",
            "Official parameter validated; write blocked until confirmWrites=true.",
            0,
            true,
            parameter.Area,
            parameter.Key);
    }
}

public interface IErpD365LinkDryRun
{
    ErpD365WorkspaceDryRunResult Evaluate(ErpD365LinkWriteRequest request, bool confirmWrites);
}

public sealed class ErpD365LinkDryRun : IErpD365LinkDryRun
{
    public ErpD365WorkspaceDryRunResult Evaluate(ErpD365LinkWriteRequest request, bool confirmWrites)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (confirmWrites)
        {
            return new(
                "dry-run-confirm-refused",
                "confirm_writes_refused",
                "confirm_writes refused on the dry-run path; POST confirmWrites=true to write.",
                0,
                false);
        }

        var check = ErpD365LinkWriteService.Validate(request);
        if (check is not null)
        {
            return new("dry-run-invalid", check.Code, check.Message, 0, false);
        }

        ErpD365ProcessCatalog.TryGetEdge(request.FromTab, request.ToTab, out var edge);
        return new(
            "dry-run-validated",
            "ok",
            "Official interlink validated; write blocked until confirmWrites=true.",
            0,
            true,
            edge.FromTab,
            edge.ToTab);
    }
}
