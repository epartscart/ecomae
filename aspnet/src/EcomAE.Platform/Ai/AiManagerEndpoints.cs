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
        // Same surface as the page that submits: /cp/ for the CP screen, /en/ for the storefront /AI page.
        // Both prefixes are nginx-proxied to the app; a root-level path is not (it rewrites to /storefront/app).
        app.MapPost("/cp/ai-manager-action", HandleAsync);
        app.MapPost("/en/ai-manager-action", HandleAsync);
        // Public storefront assistant — no staff gate, rate-limited per IP.
        app.MapPost("/en/ai-guest-chat", GuestChatAsync);
        return app;
    }

    /// <summary>Cross-site write protection shared by the AI POST endpoints.</summary>
    private static bool CrossSite(HttpContext ctx)
    {
        var secSite = ctx.Request.Headers["Sec-Fetch-Site"].ToString();
        if (string.Equals(secSite, "cross-site", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var src = ctx.Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(src) || string.Equals(src, "null", StringComparison.OrdinalIgnoreCase))
        {
            src = ctx.Request.Headers.Referer.ToString();
        }
        return !string.IsNullOrWhiteSpace(src)
            && (!Uri.TryCreate(src, UriKind.Absolute, out var su) || !string.Equals(su.Host, ctx.Request.Host.Host, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Visitor chat on the tenant storefront — routes to the Support Agent (platform AI Manager on ecomae.com).</summary>
    private static async Task<IResult> GuestChatAsync(HttpContext ctx, IAiPlatform ai, IAiAgentRuntime runtime)
    {
        if (CrossSite(ctx))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted).ConfigureAwait(false);
        var text = (form["text"].ToString() ?? "").Trim();
        var back = "/en/AI";
        var referer = ctx.Request.Headers.Referer.ToString();
        if (referer.Contains("/AI", StringComparison.OrdinalIgnoreCase) && Uri.TryCreate(referer, UriKind.Absolute, out var ru))
        {
            back = ru.PathAndQuery.Split('?')[0];
        }
        if (text.Length == 0 || text.Length > 2000)
        {
            return Results.Redirect(back);
        }

        var tenant = ctx.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var isPlatform = tenant?.SiteKey == "platform" || tenant?.Mode == TenantMode.Platform;
        var key = isPlatform ? "platform" : (tenant?.SiteKey ?? tenant?.DatabaseName ?? ctx.Request.Host.Host);
        var ws = await ai.WorkspaceForAsync(key, tenant?.DisplayName ?? ctx.Request.Host.Host, ctx.RequestAborted).ConfigureAwait(false);

        // Rate limit: 10 guest messages per hour per IP per workspace.
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "?";
        var actor = "guest:" + ip;
        var recent = await ai.MessagesAsync(ws.Id, 300, ctx.RequestAborted).ConfigureAwait(false);
        var hourAgo = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 3600;
        if (recent.Count(m => m.FromActor == actor && m.Direction == "inbound" && m.CreatedAt >= hourAgo) >= 10)
        {
            return Results.Redirect(back + "?m=" + Uri.EscapeDataString("Please wait a little before sending more messages."));
        }

        var agents = await ai.AgentsAsync(ws.Id, ctx.RequestAborted).ConfigureAwait(false);
        var agent = isPlatform
            ? (agents.FirstOrDefault(a => a.TemplateKey == "platform_ai_manager") ?? agents.FirstOrDefault())
            : (agents.FirstOrDefault(a => a.TemplateKey == "support_agent") ?? agents.FirstOrDefault(a => a.IsManager) ?? agents.FirstOrDefault());
        var mid = await ai.PostMessageAsync(new AiMessage(0, ws.Id, "inbound", null, actor, agent?.Id, text, null, "open", "", 0), ctx.RequestAborted).ConfigureAwait(false);
        var flash = "Our assistant is unavailable right now — please try again shortly.";
        if (agent is not null)
        {
            var row = await runtime.RunAsync(ws.TenantKey, tenant?.DatabaseName, agent.Id, "guest", text, ctx.RequestAborted).ConfigureAwait(false);
            if (row.OutputSummary is not null)
            {
                await ai.PostMessageAsync(new AiMessage(0, ws.Id, "outbound", agent.Id, agent.Name, null, row.OutputSummary, mid, "answered", "", 0), ctx.RequestAborted).ConfigureAwait(false);
                flash = row.OutputSummary;
            }
            else if (row.Error is not null)
            {
                flash = "Assistant error: " + row.Error;
            }
        }
        return Results.Redirect(back + "?m=" + Uri.EscapeDataString(flash));
    }

    private static async Task<IResult> HandleAsync(HttpContext ctx, IAiPlatform ai, IAiAgentRuntime runtime, ILegacySessionValidator sessions)
    {
        // Cross-site write protection — this cookie-authenticated endpoint sits outside the admin-surface guard.
        if (CrossSite(ctx))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var session = await sessions.ValidateAsync(ctx, ctx.RequestAborted).ConfigureAwait(false);
        if (!PhpParityDumpCatalog.HasStaffAccess(session))
        {
            return Results.Redirect("/");
        }

        var form = await ctx.Request.ReadFormAsync(ctx.RequestAborted).ConfigureAwait(false);
        var tenant = ctx.Items[TenantResolutionMiddleware.HttpContextItemKey] as TenantContext;
        var isPlatform = tenant?.SiteKey == "platform" || tenant?.Mode == TenantMode.Platform;
        var key = isPlatform ? "platform" : (tenant?.SiteKey ?? tenant?.DatabaseName ?? ctx.Request.Host.Host);
        var ws = await ai.WorkspaceForAsync(key, tenant?.DisplayName ?? ctx.Request.Host.Host, ctx.RequestAborted).ConfigureAwait(false);
        var actor = "cp:" + session.UserId;
        var action = form["action"].ToString();
        var idStr = form["id"].ToString();
        var text = form["text"].ToString();
        var task = form["task"].ToString();
        long.TryParse(idStr, out var id);
        var back = "/cp/ai-manager-app";
        var agents = await ai.AgentsAsync(ws.Id, ctx.RequestAborted).ConfigureAwait(false);
        string flash;

        switch (action)
        {
            case "run":
            {
                if (agents.All(a => a.Id != id)) { flash = "agent not in workspace"; break; }
                var row = await runtime.RunAsync(ws.TenantKey, tenant?.DatabaseName, id, "manual", string.IsNullOrWhiteSpace(task) ? "Daily health check and improvement advice" : task, ctx.RequestAborted).ConfigureAwait(false);
                flash = row.Status + (row.Error is not null ? " — " + row.Error : " (" + (row.TokensIn + row.TokensOut) + " tokens, $" + row.CostUsd.ToString("F4") + ")");
                break;
            }
            case "suspend" or "activate":
            {
                if (agents.All(a => a.Id != id)) { flash = "agent not in workspace"; break; }
                await ai.SetAgentStatusAsync(id, action == "suspend" ? "suspended" : "active", actor, ctx.RequestAborted).ConfigureAwait(false);
                flash = "Agent " + (action == "suspend" ? "suspended" : "activated");
                break;
            }
            case "approve" or "reject":
            {
                var pending = await ai.PendingApprovalsAsync(ws.Id, ctx.RequestAborted).ConfigureAwait(false);
                if (pending.All(p => p.Id != id)) { flash = "approval not in workspace"; break; }
                await ai.DecideApprovalAsync(id, action, actor, ctx.RequestAborted).ConfigureAwait(false);
                flash = action == "approve" ? "Approved" : "Rejected";
                break;
            }
            case "chat" when !string.IsNullOrWhiteSpace(text):
            {
                var manager = agents.FirstOrDefault(a => a.IsManager) ?? agents.FirstOrDefault();
                var mid = await ai.PostMessageAsync(new AiMessage(0, ws.Id, "inbound", null, actor, manager?.Id, text, null, "open", "", 0), ctx.RequestAborted).ConfigureAwait(false);
                if (manager is not null)
                {
                    var row = await runtime.RunAsync(ws.TenantKey, tenant?.DatabaseName, manager.Id, "chat", text, ctx.RequestAborted).ConfigureAwait(false);
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
            back = Uri.TryCreate(referer, UriKind.Absolute, out var ru) ? ru.PathAndQuery.Split('?')[0] : back;
        }
        return Results.Redirect(back + "?m=" + Uri.EscapeDataString(flash));
    }
}
