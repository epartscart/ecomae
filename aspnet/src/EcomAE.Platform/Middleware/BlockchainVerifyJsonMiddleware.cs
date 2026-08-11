using EcomAE.Platform.Migration;
using System.Text.Json;

namespace EcomAE.Platform.Middleware;

/// <summary>
/// Serves <c>/blockchain/verify?format=json</c> (and Accept: application/json) from ASP.NET.
/// Replaces product <c>/epc-blockchain-verify.php</c> JSON — PHP stays under /php-reference only.
/// </summary>
public sealed class BlockchainVerifyJsonMiddleware
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly RequestDelegate _next;

    public BlockchainVerifyJsonMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ISurfaceDashboardSummaryReporter dashboards)
    {
        var path = context.Request.Path.Value ?? "";
        if (!path.Equals("/blockchain/verify", StringComparison.OrdinalIgnoreCase)
            && !path.Equals("/epc-blockchain-verify.php", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var format = context.Request.Query["format"].ToString();
        var accept = context.Request.Headers.Accept.ToString();
        var wantJson = format.Equals("json", StringComparison.OrdinalIgnoreCase)
            || context.Request.Query.ContainsKey("json")
            || (accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
                && !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase));

        if (!wantJson)
        {
            await _next(context);
            return;
        }

        var key = (context.Request.Query["proof"].FirstOrDefault()
            ?? context.Request.Query["hash"].FirstOrDefault()
            ?? context.Request.Query["id"].FirstOrDefault()
            ?? "").Trim();

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-EcomAE-Platform"] = "primary";

        if (string.IsNullOrEmpty(key))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                ok = false,
                error = "Provide ?proof=<proof_uid> or ?hash=<sha256>",
                product = "ECOM AE Blockchain BOS"
            }, JsonOpts));
            return;
        }

        var digest = await dashboards.BuildCpBlockchainProofsDigestAsync(500, context.RequestAborted);
        var row = digest.Proofs.FirstOrDefault(r =>
            r.ProofUid.Equals(key, StringComparison.OrdinalIgnoreCase)
            || r.PayloadHash.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (row is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                ok = false,
                valid = false,
                proof = (object?)null,
                error = "Proof not found",
                product = "ECOM AE Blockchain BOS",
                source = digest.Source
            }, JsonOpts));
            return;
        }

        var valid = row.Status.Equals("anchored", StringComparison.OrdinalIgnoreCase)
            || row.Status.Equals("confirmed", StringComparison.OrdinalIgnoreCase);

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            ok = true,
            valid,
            proof = new
            {
                proof_uid = row.ProofUid,
                tenant_key = row.TenantKey,
                record_type = row.RecordType,
                record_id = row.RecordId,
                payload_hash = row.PayloadHash,
                status = row.Status,
                anchor_ref = row.AnchorRef,
                batch_id = row.BatchId,
                created_at = row.CreatedAt
            },
            product = "ECOM AE Blockchain BOS",
            source = digest.Source
        }, JsonOpts));
    }
}
