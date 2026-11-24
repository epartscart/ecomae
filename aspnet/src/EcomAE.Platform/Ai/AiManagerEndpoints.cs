using EcomAE.Platform.Auth;
using EcomAE.Platform.Erp;
using EcomAE.Platform.Middleware;
using EcomAE.Platform.Presentation;
using EcomAE.Platform.Services;

namespace EcomAE.Platform.Ai;

/// <summary>POST actions for the AI Manager screen (static SSR — plain forms).</summary>
public static class AiManagerEndpoints
{
    public static WebApplication MapAiManagerEndpoints(this WebApplication app)
    {
        app.MapPost("/cp/ai-manager-action", HandleAsync);
        return app;
    }

    private static async Task<IResult> HandleAsync(HttpContext ctx, IAiPlatform ai, IAiAgentRuntime runtime, ILegacySessionValidator sessions)
    {
        var session = await sessions.ValidateAsync(ctx, ctx.RequestAborted).ConfigureAwait(false);
        if (!PhpParityDumpCatalog.HasStaffAccess(session))
        {
            return Results.Redirect("/");
        }

        var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted).ConfigureAwait(false);
        var tenant = ctx.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var isPlatform = tenant?.Mode == TenantMode.Platform;
        var key = isPlatform ? "platform" : (tenant?.SiteKey ?? tenant?.DatabaseName ?? ctx.Request.Host.Host);
        var ws = await ai.WorkspaceForAsync(key, tenant?.DisplayName ?? ctx.Request.Host.Host, ctx.RequestAborted).ConfigureAwait(false);
        var actor = "cp:" + session.UserId;
        var action = form["action"].ToString();
        var idStr = form["id"].ToString();
        var text = form["text"].ToString();
        var task = form["task"].ToString();
        long.TryParse(idStr, out var id);
        var back = "/cp/ai-manager-app";
        string flash;

        switch (action)
        {
            case "run":
            {
                var row = await runtime.RunAsync(ws.TenantKey, id, "manual", string.IsNullOrWhiteSpace(task) ? "Daily health check and improvement advice" : task, ctx.RequestAborted).ConfigureAwait(false);
                flash = row.Status + (row.Error is not null ? " — " + row.Error : " (" + (row.TokensIn + row.TokensOut) + " tokens, $" + row.CostUsd.ToString("F4") + ")");
                break;
            }
            case "suspend": await ai.SetAgentStatusAsync(id, "suspended", actor, ctx.RequestAborted).ConfigureAwait(false); flash = "Agent suspended"; break;
            case "activate": await ai.SetAgentStatusAsync(id, "active", actor, ctx.RequestAborted).ConfigureAwait(false); flash = "Agent activated"; break;
            case "approve": await ai.DecideApprovalAsync(id, "approve", actor, ctx.RequestAborted).ConfigureAwait(false); flash = "Approved"; break;
            case "reject": await ai.DecideApprovalAsync(id, "reject", actor, ctx.RequestAborted).ConfigureAwait(false); flash = "Rejected"; break;
            case "chat" when !string.IsNullOrWhiteSpace(text):
            {
                var agents = await ai.AgentsAsync(ws.Id, ctx.RequestAborted).ConfigureAwait(false);
                var manager = agents.FirstOrDefault(a => a.IsManager) ?? agents.FirstOrDefault();
                var mid = await ai.PostMessageAsync(new AiMessage(0, ws.Id, "inbound", null, actor, manager?.Id, text, null, "open", "", 0), ctx.RequestAborted).ConfigureAwait(false);
                if (manager is not null)
                {
                    var row = await runtime.RunAsync(ws.TenantKey, manager.Id, "chat", text, ctx.RequestAborted).ConfigureAwait(false);
                    if (row.OutputSummary is not null)
                    {
                        await ai.PostMessageAsync(new AiMessage(0, ws.Id, "outbound", manager.Id, manager.Name, null, row.OutputSummary, mid, "answered", "", 0), ctx.RequestAborted).ConfigureAwait(false);
                    }
                    flash = row.Status;
                }
                else { flash = "no manager agent"; }
                break;
            }
            case "escalate" when !string.IsNullOrWhiteSpace(text) && !isPlatform:
            {
                var platform = await ai.WorkspaceForAsync("platform", "Ecomae Platform", ctx.RequestAborted).ConfigureAwait(false);
                var body = "[" + ws.TenantKey + "] " + text;
                await ai.PostMessageAsync(new AiMessage(0, platform.Id, "escalate", null, actor + "@" + ws.TenantKey, null, body, null, "open", "", 0), ctx.RequestAborted).ConfigureAwait(false);
                await ai.PostMessageAsync(new AiMessage(0, ws.Id, "escalate", null, actor, null, body, null, "open", "", 0), ctx.RequestAborted).ConfigureAwait(false);
                flash = "Escalated to Ecomae platform AI";
                break;
            }
            default:
                flash = "ignored";
                break;
        }

        // Non-CP surfaces (/AI) return to the storefront page; keep the flash message.
        var referer = ctx.Request.Headers.Referer.ToString();
        if (referer.Contains("/AI", StringComparison.OrdinalIgnoreCase) && !referer.Contains("/cp/"))
        {
            back = new Uri(referer, UriKind.Absolute).PathAndQuery.Split('?')[0];
        }
        return Results.Redirect(back + "?m=" + Uri.EscapeDataString(flash));
    }
}
