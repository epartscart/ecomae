using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Orchestrator;
using EcomAE.Platform.LifeOs.Spec;
using EcomAE.Platform.Routing;

namespace EcomAE.Platform.Modules;

public sealed class LifeOsModule : ISurfaceModule
{
    public SurfaceModuleDescriptor Descriptor { get; } = new(
        "lifeos",
        "LifeOS™",
        EcomAeRoutes.LifeOs,
        "LifeOS Master Spec v4.0 — Parts 1–10 scaffold",
        "cognitive-scaffold",
        []);

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(EcomAeRoutes.LifeOsArchitecture, (ILifeOsOrchestrator orch) =>
            Results.Ok(orch.ArchitectureDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsEvents, (ILifeOsEventBus bus) =>
            Results.Ok(new
            {
                ok = true,
                eventTypes = Enum.GetNames<LifeOsEventType>(),
                recent = bus.Recent(50)
            }));

        endpoints.MapGet(EcomAeRoutes.LifeOsMemoryDigest, (ILifeOsMemorySystem memory) =>
        {
            memory.SeedDemoProject();
            return Results.Ok(new { ok = true, snapshot = memory.Snapshot() });
        });

        endpoints.MapGet(EcomAeRoutes.LifeOsAgentsDigest, (ILifeOsAgentFramework agents) =>
            Results.Ok(new { ok = true, count = agents.Catalog.Count, catalog = agents.Catalog }));

        endpoints.MapGet(EcomAeRoutes.LifeOsPlansDigest, (ILifeOsPlanningEngine planning) =>
            Results.Ok(new { ok = true, sample = planning.SampleLifeOsMvp() }));

        endpoints.MapGet(EcomAeRoutes.LifeOsContextDigest, (ILifeOsContextEngine context) =>
            Results.Ok(new { ok = true, sources = context.KnownSourceNames }));

        endpoints.MapGet(EcomAeRoutes.LifeOsCognitive, (ILifeOsCognitiveEngines cognitive) =>
            Results.Ok(cognitive.Digest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsMultimodal, (ILifeOsMasterSpec spec) =>
            Results.Ok(new { ok = true, part = 4, adapters = spec.MultimodalAdapters }));

        endpoints.MapGet(EcomAeRoutes.LifeOsSecurityDigest, (ILifeOsMasterSpec spec) =>
            Results.Ok(new { ok = true, part = 7, controls = spec.SecurityControls }));

        endpoints.MapGet(EcomAeRoutes.LifeOsClients, (ILifeOsMasterSpec spec) =>
            Results.Ok(new { ok = true, part = 8, clients = spec.Clients }));

        endpoints.MapGet(EcomAeRoutes.LifeOsPlugins, (ILifeOsMasterSpec spec) =>
            Results.Ok(new { ok = true, part = 9, plugins = spec.Plugins }));

        endpoints.MapGet(EcomAeRoutes.LifeOsRoadmap, (ILifeOsMasterSpec spec) =>
            Results.Ok(new
            {
                ok = true,
                part = 10,
                version = spec.Version,
                parts = spec.Parts.Select(p => new { p.Number, p.Title, p.Status }).ToList()
            }));

        endpoints.MapGet(EcomAeRoutes.LifeOsSpec, (
            ILifeOsMasterSpec spec,
            ILifeOsCognitiveEngines cognitive,
            ILifeOsOrchestrator orch) =>
            Results.Ok(spec.FullDigest(cognitive, orch.ArchitectureDigest())));

        endpoints.MapPost(EcomAeRoutes.LifeOsOrchestrate, async (
            LifeOsOrchestrateBody? body,
            ILifeOsOrchestrator orch,
            ILifeOsCognitiveEngines cognitive,
            CancellationToken cancellationToken) =>
        {
            body ??= new LifeOsOrchestrateBody(null, null, null);
            var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(body.Transcript))
            {
                payload["transcript"] = body.Transcript.Trim();
            }

            if (body.Confirm == true)
            {
                payload["confirm"] = "1";
            }

            var type = ParseType(body.EventType);
            var evt = payload.Count == 0 && type == LifeOsEventType.VoiceEvent
                ? LifeOsEventFactory.SampleVoice()
                : LifeOsEventFactory.Create(
                    type,
                    body.Source ?? "Console",
                    payload.Count == 0
                        ? new Dictionary<string, string> { ["note"] = "scaffold-orchestrate" }
                        : payload,
                    LifeOsEventPriority.High);

            var result = await orch.ProcessAsync(evt, cancellationToken).ConfigureAwait(false);
            var reasoning = cognitive.Reason(result.Intent, result.Context.Sources.Select(s => $"{s.Name}:{s.Confidence:0.00}").ToList());
            var decision = cognitive.Decide(reasoning, allowIrreversible: body.Confirm == true);
            cognitive.Learn(result.TraceId, "orchestrate-scaffold");

            return Results.Ok(new
            {
                ok = true,
                scaffold = true,
                result,
                cognitive = new { reasoning, decision }
            });
        });
    }

    private static LifeOsEventType ParseType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LifeOsEventType.VoiceEvent;
        }

        return Enum.TryParse<LifeOsEventType>(value, ignoreCase: true, out var t)
            ? t
            : LifeOsEventType.VoiceEvent;
    }

    public sealed record LifeOsOrchestrateBody(string? Transcript, string? EventType, string? Source, bool? Confirm = null);
}
