using EcomAE.Platform.Erp;
using EcomAE.Platform.Presentation;

namespace EcomAE.Platform.Migration;

public interface IErpD365WorkspaceDryRun
{
    ErpD365WorkspaceDryRunResult Evaluate(ErpD365WorkspaceWriteRequest request, bool confirmWrites);
}

public sealed class ErpD365WorkspaceDryRun : IErpD365WorkspaceDryRun
{
    public ErpD365WorkspaceDryRunResult Evaluate(ErpD365WorkspaceWriteRequest request, bool confirmWrites)
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

        var check = ErpD365WorkspaceWriteService.Validate(request);
        if (check is not null)
        {
            return new("dry-run-invalid", check.Code, check.Message, 0, false);
        }

        ErpD365Part3Catalog.TryGet(request.Tab, out var page);
        return new(
            "dry-run-validated",
            "ok",
            "Workspace row validated; write blocked until confirmWrites=true.",
            0,
            true,
            page?.Area ?? request.Area,
            page?.Tab ?? request.Tab);
    }
}

public sealed record ErpD365WorkspaceDryRunResult(
    string Status,
    string ValidationCode,
    string Detail,
    int Writes,
    bool WouldWrite,
    string? Area = null,
    string? Tab = null)
{
    public object ToPayload(object session) => new
    {
        ok = true,
        surface = "erp",
        status = Status,
        writes = Writes,
        writesBlocked = true,
        cutoverAllowed = false,
        phpAuthoritative = false,
        validation_code = ValidationCode,
        would_write = WouldWrite,
        intended = new { area = Area, tab = Tab },
        session,
        note = Detail,
    };
}
