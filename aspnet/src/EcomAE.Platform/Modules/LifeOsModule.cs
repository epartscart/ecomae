using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Orchestrator;
using EcomAE.Platform.LifeOs.Part3;
using EcomAE.Platform.LifeOs.Part4;
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

        endpoints.MapGet(EcomAeRoutes.LifeOsCognitive, (ILifeOsAiCore ai) =>
            Results.Ok(ai.FullPart3Digest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsPerception, (ILifeOsPerceptionEngine perception) =>
            Results.Ok(perception.Digest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsPrediction, (ILifeOsPredictionEngine prediction) =>
            Results.Ok(prediction.Digest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsEthics, (ILifeOsEthicalAiLayer ethics) =>
            Results.Ok(ethics.Digest()));

        endpoints.MapPost(EcomAeRoutes.LifeOsCognitiveCycle, (
            LifeOsOrchestrateBody? body,
            ILifeOsAiCore ai) =>
        {
            body ??= new LifeOsOrchestrateBody(null, null, null);
            var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(body.Transcript))
            {
                payload["transcript"] = body.Transcript.Trim();
            }

            var evt = payload.Count == 0
                ? LifeOsEventFactory.SampleVoice()
                : LifeOsEventFactory.Create(
                    ParseType(body.EventType),
                    body.Source ?? "Console",
                    payload,
                    LifeOsEventPriority.High);

            var cycle = ai.RunCycle(evt, userPermission: body.Confirm == true);
            return Results.Ok(new { ok = true, scaffold = true, cycle });
        });

        endpoints.MapGet(EcomAeRoutes.LifeOsMultimodal, (ILifeOsMultimodalRuntime runtime) =>
            Results.Ok(runtime.FullPart4Digest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsDevices, (ILifeOsMultimodalRuntime runtime) =>
            Results.Ok(new { ok = true, devices = runtime.Devices, kernel = runtime.KernelComponents }));

        endpoints.MapGet(EcomAeRoutes.LifeOsSync, (ILifeOsMultimodalRuntime runtime) =>
            Results.Ok(new { ok = true, sync = runtime.UnifiedSession, state = runtime.CurrentState.ToString() }));

        endpoints.MapGet(EcomAeRoutes.LifeOsPerformance, (ILifeOsMultimodalRuntime runtime) =>
            Results.Ok(new { ok = true, targets = runtime.PerformanceTargets }));

        endpoints.MapPost(EcomAeRoutes.LifeOsNotifications, (
            LifeOsNotificationBody? body,
            ILifeOsMultimodalRuntime runtime) =>
        {
            body ??= new("Alert", "system", "working");
            var decision = runtime.ClassifyNotification(
                body.Title ?? "Alert",
                body.Sender ?? "system",
                body.Activity ?? "working");
            return Results.Ok(new { ok = true, decision });
        });

        endpoints.MapPost(EcomAeRoutes.LifeOsRuntimeTick, async (
            LifeOsOrchestrateBody? body,
            ILifeOsMultimodalRuntime runtime,
            CancellationToken cancellationToken) =>
        {
            body ??= new LifeOsOrchestrateBody(null, null, null);
            var payload = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(body.Transcript))
            {
                payload["transcript"] = body.Transcript.Trim();
            }

            var evt = payload.Count == 0
                ? LifeOsEventFactory.SampleVoice()
                : LifeOsEventFactory.Create(
                    ParseType(body.EventType),
                    body.Source ?? "Wearable",
                    payload,
                    LifeOsEventPriority.High);

            var tick = await runtime.ProcessInputAsync(evt, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(new { ok = true, scaffold = true, tick });
        });

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
            ILifeOsOrchestrator orch,
            ILifeOsAiCore ai,
            ILifeOsMultimodalRuntime runtime) =>
            Results.Ok(spec.FullDigest(cognitive, new
            {
                part2 = orch.ArchitectureDigest(),
                part3 = ai.FullPart3Digest(),
                part4 = runtime.FullPart4Digest()
            })));

        endpoints.MapPost(EcomAeRoutes.LifeOsOrchestrate, async (
            LifeOsOrchestrateBody? body,
            ILifeOsOrchestrator orch,
            ILifeOsAiCore ai,
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
            var cycle = ai.RunCycle(evt, userPermission: body.Confirm == true);

            return Results.Ok(new
            {
                ok = true,
                scaffold = true,
                result,
                cognitiveCycle = cycle
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

    public sealed record LifeOsNotificationBody(string? Title, string? Sender, string? Activity);
}
