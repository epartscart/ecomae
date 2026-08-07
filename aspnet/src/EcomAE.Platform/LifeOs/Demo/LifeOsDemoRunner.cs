using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.Models;
using EcomAE.Platform.LifeOs.Orchestrator;
using EcomAE.Platform.LifeOs.Part3;
using EcomAE.Platform.LifeOs.Part4;

namespace EcomAE.Platform.LifeOs.Demo;

public sealed class LifeOsDemoRunner : ILifeOsDemoRunner
{
    private readonly ILifeOsOrchestrator _orch;
    private readonly ILifeOsAiCore _ai;
    private readonly ILifeOsMultimodalRuntime _runtime;
    private readonly ILifeOsMemorySystem _memory;
    private readonly ILifeOsAgentFramework _agents;
    private readonly ILifeOsPlanningEngine _planning;

    public LifeOsDemoRunner(
        ILifeOsOrchestrator orch,
        ILifeOsAiCore ai,
        ILifeOsMultimodalRuntime runtime,
        ILifeOsMemorySystem memory,
        ILifeOsAgentFramework agents,
        ILifeOsPlanningEngine planning)
    {
        _orch = orch;
        _ai = ai;
        _runtime = runtime;
        _memory = memory;
        _agents = agents;
        _planning = planning;
    }

    public IReadOnlyList<LifeOsDemoScenario> Scenarios { get; } =
    [
        new(
            "board-meeting",
            "Prepare tomorrow's board meeting",
            "Amina · VP Operations",
            "Prepare tomorrow's board meeting.",
            "Work",
            "Voice command on the commute. LifeOS gathers calendar, CRM, ERP flash, and drafts agenda + invites — waiting for human confirm before sending.",
            [
                "Calendar: Board Review · Tomorrow 10:00 · Conference A",
                "CRM: Acme Corp · Pipeline $420k · Next action: proposal",
                "ERP flash: Cash $1.2M · AR aging 12d · Inventory OK",
                "Email: 3 unread from CFO / Legal / Sales",
                "Device: Wearable · Focus score 78 · Commute mode",
            ]),
        new(
            "invoice-followup",
            "Follow up unpaid invoices",
            "Omar · Finance lead",
            "Follow up on unpaid invoices over 30 days.",
            "Wealth",
            "Finance agent + memory pull overdue AR. LifeOS proposes a polite chase sequence; ethics blocks mass-send without confirm.",
            [
                "AR: INV-1042 · $18,400 · 41 days",
                "AR: INV-1088 · $6,250 · 33 days",
                "Customer: Gulf Parts LLC · preferred channel email",
                "Policy: no irreversible send without confirm",
            ]),
        new(
            "health-focus",
            "Protect deep-work focus",
            "Sara · Product designer",
            "Block distractions and protect my focus for the next two hours.",
            "Health",
            "Wearable stress + calendar gaps → notification intelligence silences low-priority pings and schedules a focus window.",
            [
                "Wearable: HRV low · stress rising",
                "Calendar: 2h open until 15:00",
                "Notifications: 14 queued · 3 high priority",
                "Preference: deep work mornings",
            ]),
        new(
            "home-arrival",
            "Prepare home for arrival",
            "Layla · Founder",
            "I'm 20 minutes from home — prepare the house.",
            "Home",
            "Vehicle ETA + home bridge (scaffold): climate, lights, and evening family briefing.",
            [
                "Vehicle: ETA 18 min · traffic moderate",
                "Home: living room 27°C · front door locked",
                "Family calendar: dinner 19:30 · school pickup done",
                "Briefing: 2 package deliveries today",
            ]),
    ];

    public LifeOsDemoScenario DefaultScenario => Scenarios[0];

    public object CatalogDigest() => new
    {
        ok = true,
        product = "LifeOS™",
        title = "How LifeOS works — sample demo catalog",
        scaffold = true,
        loop = new[] { "Perceive", "Decide", "Act", "Learn" },
        scenarios = Scenarios,
        run = "GET /lifeos/demo?run=1  ·  POST /lifeos/demo/run  ·  UI /lifeos/demo-app",
        note = "Dry-run only — in-memory bus/memory; no irreversible side effects; live LLMs not claimed"
    };

    public async Task<LifeOsDemoRunResult> RunAsync(
        string? scenarioKey = null,
        string? transcriptOverride = null,
        bool confirm = false,
        CancellationToken cancellationToken = default)
    {
        var scenario = Scenarios.FirstOrDefault(s =>
            string.Equals(s.Key, scenarioKey, StringComparison.OrdinalIgnoreCase))
            ?? DefaultScenario;

        var transcript = string.IsNullOrWhiteSpace(transcriptOverride)
            ? scenario.Transcript
            : transcriptOverride.Trim();

        _memory.SeedDemoProject();
        var evt = LifeOsEventFactory.SampleVoice(transcript);
        if (confirm)
        {
            var payload = new Dictionary<string, string>(evt.Payload, StringComparer.OrdinalIgnoreCase)
            {
                ["confirm"] = "1"
            };
            evt = LifeOsEventFactory.Create(evt.Type, evt.Source, payload, evt.Priority, evt.Timestamp);
        }

        var orch = await _orch.ProcessAsync(evt, cancellationToken).ConfigureAwait(false);
        var cycle = _ai.RunCycle(evt, userPermission: confirm);
        var tick = await _runtime.ProcessInputAsync(evt, deviceKey: "wearable-demo", cancellationToken)
            .ConfigureAwait(false);
        var memory = _memory.Snapshot();
        var plan = orch.Plan ?? _planning.SampleLifeOsMvp();

        var perceive = new
        {
            eventId = evt.EventId,
            source = evt.Source,
            type = evt.Type.ToString(),
            transcript,
            sampleContext = scenario.SampleContext,
            perception = new
            {
                cycle.Perception.PerceptionId,
                cycle.Perception.Modality,
                cycle.Perception.SemanticRepresentation,
                entities = cycle.Perception.ExtractedEntities
            },
            reality = new
            {
                cycle.Reality.Activity,
                cycle.Reality.Location,
                cycle.Reality.Device,
                cycle.Reality.FocusScore,
                cycle.Reality.CalendarEvent,
                cycle.Reality.EnergyLevel
            }
        };

        var decide = new
        {
            intent = orch.Intent,
            recommendation = cycle.Recommendation,
            decisionScore = cycle.DecisionScore,
            ethics = new
            {
                cycle.Ethics.Allowed,
                cycle.Ethics.Summary,
                checks = cycle.Ethics.Checks.Select(c => new { c.Name, c.Passed, c.Detail }).ToList()
            },
            riskLevel = orch.RiskLevel,
            permissionOk = orch.PermissionOk,
            reasoning = cycle.Reasoning.Select(r => new
            {
                method = r.Method.ToString(),
                r.Conclusion,
                r.Confidence
            }).ToList()
        };

        var act = new
        {
            executed = cycle.Executed,
            aggregatedResponse = orch.AggregatedResponse,
            selectedAgents = orch.SelectedAgents,
            agentResults = orch.AgentResults.Select(a => new
            {
                a.AgentKey,
                a.Ok,
                a.Summary,
                a.Confidence
            }).ToList(),
            plan = new
            {
                plan.PlanId,
                plan.Goal,
                tasks = plan.Tasks.Select(t => new
                {
                    t.TaskId,
                    t.Title,
                    t.Priority,
                    status = t.Status.ToString(),
                    t.DependsOn
                }).ToList()
            },
            runtime = new
            {
                tick.TickId,
                state = tick.State.ToString(),
                tick.Channel,
                tick.Summary,
                sync = new
                {
                    tick.Sync.SessionId,
                    devices = tick.Sync.ConnectedDevices,
                    tick.Sync.CurrentTask
                }
            },
            pipeline = orch.Pipeline
        };

        var learn = new
        {
            personalityMode = cycle.PersonalityMode,
            emotion = new { cycle.Emotion.State, cycle.Emotion.Confidence, cycle.Emotion.Signals },
            reflection = cycle.Reflection is null
                ? null
                : new
                {
                    cycle.Reflection.EfficiencyNote,
                    cycle.Reflection.PreferenceUpdates,
                    cycle.Reflection.GoalAchieved
                },
            predictions = cycle.Predictions.Select(p => new
            {
                p.EventKind,
                p.Forecast,
                p.Probability,
                p.Recommendation
            }).ToList(),
            memoryLayers = memory.CountsByLayer,
            recentMemory = memory.Recent.Take(6).Select(m => new
            {
                layer = m.Layer.ToString(),
                m.Key,
                m.Content
            }).ToList(),
            agentCatalogCount = _agents.Catalog.Count
        };

        var sampleData = new
        {
            scenario = new { scenario.Key, scenario.Title, scenario.Persona, scenario.Domain },
            contextCards = scenario.SampleContext,
            howConfirmWorks = confirm
                ? "confirm=true — ethics may allow execution scaffold"
                : "confirm=false — irreversible actions stay blocked (human control first)"
        };

        return new LifeOsDemoRunResult(
            scenario.Key,
            transcript,
            scenario.Story,
            perceive,
            decide,
            act,
            learn,
            sampleData,
            [
                "1. Perceive — voice event + sample context cards (calendar/CRM/ERP/devices)",
                "2. Decide — intent, multi-method reasoning, decision score, ethics gate",
                "3. Act — specialist agents + plan tasks + runtime tick (dry-run, no irreversible send)",
                "4. Learn — reflection, predictions, memory layers updated in scaffold store",
            ]);
    }
}
