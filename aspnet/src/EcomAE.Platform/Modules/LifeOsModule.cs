using EcomAE.Platform.LifeOs.Cinematic;
using EcomAE.Platform.LifeOs.Demo;
using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Orchestrator;
using EcomAE.Platform.LifeOs.Part3;
using EcomAE.Platform.LifeOs.Part4;
using EcomAE.Platform.LifeOs.Part5;
using EcomAE.Platform.LifeOs.Part6;
using EcomAE.Platform.LifeOs.Part7;
using EcomAE.Platform.LifeOs.Part8;
using EcomAE.Platform.LifeOs.Part9;
using EcomAE.Platform.LifeOs.Part10;
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

        endpoints.MapGet(EcomAeRoutes.LifeOsPlatform, (ILifeOsPlatformEngineering platform) =>
            Results.Ok(platform.FullPart5Digest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsServices, (ILifeOsPlatformEngineering platform) =>
            Results.Ok(new { ok = true, services = platform.Microservices }));

        endpoints.MapGet(EcomAeRoutes.LifeOsApiCatalog, (ILifeOsPlatformEngineering platform) =>
            Results.Ok(new
            {
                ok = true,
                conventions = platform.RestConventions,
                success = platform.Ok(new { id = "demo" }, new { requestId = "req_scaffold" }),
                error = platform.Fail("TASK_NOT_FOUND", "Task not found"),
                websockets = platform.WebSocketChannels
            }));

        endpoints.MapGet(EcomAeRoutes.LifeOsEventTopics, (ILifeOsPlatformEngineering platform) =>
            Results.Ok(new { ok = true, topics = platform.EventTopics, brokers = new[] { "Kafka", "NATS", "RabbitMQ" } }));

        endpoints.MapGet(EcomAeRoutes.LifeOsDataStores, (ILifeOsPlatformEngineering platform) =>
            Results.Ok(new { ok = true, stores = platform.DataStores, memoryLayers = platform.MemoryLayers }));

        endpoints.MapGet(EcomAeRoutes.LifeOsKnowledgeGraph, (ILifeOsPlatformEngineering platform) =>
            Results.Ok(platform.KnowledgeGraphSample()));

        endpoints.MapGet(EcomAeRoutes.LifeOsAgentSdk, (ILifeOsPlatformEngineering platform) =>
            Results.Ok(new { ok = true, contract = platform.AgentSdkContract, plugins = platform.SamplePlugins }));

        endpoints.MapGet(EcomAeRoutes.LifeOsAiGateway, (ILifeOsPlatformEngineering platform) =>
            Results.Ok(new { ok = true, routes = platform.AiGatewayRoutes }));

        // ── Part 6 digests (Ch.61–81 Cloud / DevOps / SRE) ────────────────────
        endpoints.MapGet(EcomAeRoutes.LifeOsInfra, (ILifeOsCloudOperations ops) =>
            Results.Ok(ops.FullPart6Digest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsKubernetes, (ILifeOsCloudOperations ops) =>
            Results.Ok(ops.KubernetesDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsCiCd, (ILifeOsCloudOperations ops) =>
            Results.Ok(ops.CiCdDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsGpu, (ILifeOsCloudOperations ops) =>
            Results.Ok(ops.GpuAndModelServingDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsBackupDr, (ILifeOsCloudOperations ops) =>
            Results.Ok(ops.BackupAndDrDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsObservability, (ILifeOsCloudOperations ops) =>
            Results.Ok(ops.ObservabilityDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsSre, (ILifeOsCloudOperations ops) =>
            Results.Ok(ops.SreDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsReadiness, (ILifeOsCloudOperations ops) =>
            Results.Ok(new { ok = true, checklist = ops.ProductionReadinessChecklist, targets = ops.PerformanceTargets }));

        // ── Part 7 digests (Ch.82–101 Security / Privacy / Governance) ────────
        endpoints.MapGet(EcomAeRoutes.LifeOsSecurityDigest, (
            ILifeOsSecurityGovernance gov,
            ILifeOsMasterSpec spec) =>
            Results.Ok(new
            {
                ok = true,
                part = 7,
                digest = gov.FullPart7Digest(),
                legacyControls = spec.SecurityControls
            }));

        endpoints.MapGet(EcomAeRoutes.LifeOsZeroTrust, (ILifeOsSecurityGovernance gov) =>
            Results.Ok(gov.ZeroTrustDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsIam, (ILifeOsSecurityGovernance gov) =>
            Results.Ok(gov.IamDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsAuthorization, (ILifeOsSecurityGovernance gov) =>
            Results.Ok(gov.AuthorizationDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsEncryption, (ILifeOsSecurityGovernance gov) =>
            Results.Ok(gov.EncryptionDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsPrivacy, (ILifeOsSecurityGovernance gov) =>
            Results.Ok(gov.PrivacyAndConsentDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsAiGovernance, (ILifeOsSecurityGovernance gov) =>
            Results.Ok(gov.AiGovernanceDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsThreatSoc, (ILifeOsSecurityGovernance gov) =>
            Results.Ok(gov.ThreatAndSocDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsEnterpriseDeploy, (ILifeOsSecurityGovernance gov) =>
            Results.Ok(gov.EnterpriseAndDeploymentDigest()));

        // ── Part 8 digests (Ch.102–125 Client UX / Cross-Platform) ────────────
        endpoints.MapGet(EcomAeRoutes.LifeOsClients, (
            ILifeOsClientExperience ux,
            ILifeOsMasterSpec spec) =>
            Results.Ok(new
            {
                ok = true,
                part = 8,
                digest = ux.FullPart8Digest(),
                legacyClients = spec.Clients
            }));

        endpoints.MapGet(EcomAeRoutes.LifeOsDesignSystem, (ILifeOsClientExperience ux) =>
            Results.Ok(ux.DesignAndNavigationDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsWorkspaceUx, (ILifeOsClientExperience ux) =>
            Results.Ok(ux.WorkspaceAndSearchDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsModalityClients, (ILifeOsClientExperience ux) =>
            Results.Ok(ux.ModalityClientsDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsContinuity, (ILifeOsClientExperience ux) =>
            Results.Ok(ux.ContinuityAccessibilityOfflineDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsPersonalization, (ILifeOsClientExperience ux) =>
            Results.Ok(ux.PersonalizationAndFocusDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsUxMetrics, (ILifeOsClientExperience ux) =>
            Results.Ok(ux.MetricsAndTwinDigest()));

        // ── Part 9 digests (Ch.126–150 Ecosystem / Marketplace / Dev Platform) ─
        endpoints.MapGet(EcomAeRoutes.LifeOsPlugins, (
            ILifeOsEcosystemPlatform eco,
            ILifeOsMasterSpec spec,
            ILifeOsPlatformEngineering platform) =>
            Results.Ok(new
            {
                ok = true,
                part = 9,
                digest = eco.FullPart9Digest(),
                legacyPlugins = spec.Plugins,
                samples = platform.SamplePlugins
            }));

        endpoints.MapGet(EcomAeRoutes.LifeOsMarketplace, (ILifeOsEcosystemPlatform eco) =>
            Results.Ok(eco.MarketplaceDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsAgentStore, (ILifeOsEcosystemPlatform eco) =>
            Results.Ok(eco.AgentAndPluginDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsDeveloperPortal, (ILifeOsEcosystemPlatform eco) =>
            Results.Ok(eco.DeveloperPlatformDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsBillingLicensing, (ILifeOsEcosystemPlatform eco) =>
            Results.Ok(eco.BillingLicensingDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsPartners, (ILifeOsEcosystemPlatform eco) =>
            Results.Ok(eco.PartnersCommunityGovernanceDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsEcosystemRoadmap, (ILifeOsEcosystemPlatform eco) =>
            Results.Ok(eco.RoadmapAndAnalyticsDigest()));

        endpoints.MapGet(EcomAeRoutes.LifeOsRoadmap, (
            ILifeOsExecutionStrategy exec,
            ILifeOsMasterSpec spec) =>
            Results.Ok(new
            {
                ok = true,
                part = 10,
                version = spec.Version,
                partMeta = spec.Parts.FirstOrDefault(p => p.Number == 10),
                digest = exec.FullPart10Digest()
            }));

        // JSON digest moved off /lifeos/spec so browsers get the Spec UI (Blazor @page "/lifeos/spec").
        endpoints.MapGet(EcomAeRoutes.LifeOsSpecJson, (
            ILifeOsMasterSpec spec,
            ILifeOsCognitiveEngines cognitive,
            ILifeOsOrchestrator orch,
            ILifeOsAiCore ai,
            ILifeOsMultimodalRuntime runtime,
            ILifeOsPlatformEngineering platform,
            ILifeOsCloudOperations ops,
            ILifeOsSecurityGovernance gov,
            ILifeOsClientExperience ux,
            ILifeOsEcosystemPlatform eco,
            ILifeOsExecutionStrategy exec) =>
            Results.Ok(spec.FullDigest(cognitive, new
            {
                part2 = orch.ArchitectureDigest(),
                part3 = ai.FullPart3Digest(),
                part4 = runtime.FullPart4Digest(),
                part5 = platform.FullPart5Digest(),
                part6 = ops.FullPart6Digest(),
                part7 = gov.FullPart7Digest(),
                part8 = ux.FullPart8Digest(),
                part9 = eco.FullPart9Digest(),
                part10 = exec.FullPart10Digest()
            })));

        // ── How-it-works sample demo (Perceive→Decide→Act→Learn) ───────────
        endpoints.MapGet(EcomAeRoutes.LifeOsDemo, async (
            ILifeOsDemoRunner demo,
            string? run,
            string? scenario,
            CancellationToken cancellationToken) =>
        {
            var shouldRun = string.Equals(run, "1", StringComparison.Ordinal)
                || string.Equals(run, "true", StringComparison.OrdinalIgnoreCase);
            if (shouldRun)
            {
                var result = await demo.RunAsync(scenario, null, confirm: false, cancellationToken)
                    .ConfigureAwait(false);
                return Results.Ok(new { ok = true, scaffold = true, catalog = demo.CatalogDigest(), result });
            }

            return Results.Ok(demo.CatalogDigest());
        });

        endpoints.MapPost(EcomAeRoutes.LifeOsDemoRun, async (
            LifeOsDemoRunBody? body,
            ILifeOsDemoRunner demo,
            CancellationToken cancellationToken) =>
        {
            body ??= new LifeOsDemoRunBody(null, null, null);
            var result = await demo.RunAsync(body.ScenarioKey, body.Transcript, body.Confirm == true, cancellationToken)
                .ConfigureAwait(false);
            return Results.Ok(new { ok = true, scaffold = true, result });
        });

        // ── Cinematic launch film (3:00 storyboard + master prompt) ────────
        endpoints.MapGet(EcomAeRoutes.LifeOsCinematic, (ILifeOsCinematicFilm film) =>
            Results.Ok(new { ok = true, scaffold = true, film = film.Digest() }));

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

    public sealed record LifeOsDemoRunBody(string? ScenarioKey, string? Transcript, bool? Confirm);
}
