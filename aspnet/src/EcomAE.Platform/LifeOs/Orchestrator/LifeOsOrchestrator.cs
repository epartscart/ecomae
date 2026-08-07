using EcomAE.Platform.LifeOs.Engines;
using EcomAE.Platform.LifeOs.EventBus;
using EcomAE.Platform.LifeOs.Models;

namespace EcomAE.Platform.LifeOs.Orchestrator;

public sealed class LifeOsOrchestrator : ILifeOsOrchestrator
{
    private readonly ILifeOsEventBus _bus;
    private readonly ILifeOsContextEngine _context;
    private readonly ILifeOsMemorySystem _memory;
    private readonly ILifeOsAgentFramework _agents;
    private readonly ILifeOsPlanningEngine _planning;

    public LifeOsOrchestrator(
        ILifeOsEventBus bus,
        ILifeOsContextEngine context,
        ILifeOsMemorySystem memory,
        ILifeOsAgentFramework agents,
        ILifeOsPlanningEngine planning)
    {
        _bus = bus;
        _context = context;
        _memory = memory;
        _agents = agents;
        _planning = planning;
    }

    public async Task<LifeOsOrchestrationResult> ProcessAsync(
        LifeOsEvent input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var pipeline = new List<string>
        {
            "EventNormalization",
            "ContextAggregation",
            "PrivacyValidator",
            "IntentClassifier",
            "PriorityScheduler",
            "MultiAgentRouter",
            "DecisionAggregator",
            "ResponseGenerator",
            "LearningFeedback"
        };

        await _bus.PublishAsync(input, cancellationToken).ConfigureAwait(false);

        // Permission / privacy gate (scaffold always allows non-destructive intents).
        var permissionOk = input.Priority != LifeOsEventPriority.Critical
            || input.Payload.GetValueOrDefault("confirm") == "1";
        var risk = input.Priority switch
        {
            LifeOsEventPriority.Critical => "high",
            LifeOsEventPriority.High => "elevated",
            _ => "low"
        };

        _memory.SeedDemoProject();
        var hints = _memory.Retrieve(query: "lifeos", take: 8);
        var context = _context.Build(input, hints);

        var intent = ClassifyIntent(input);
        _memory.Store(LifeOsMemoryLayer.Working, $"intent:{input.EventId}", intent);
        _memory.Store(LifeOsMemoryLayer.Conversation, $"turn:{input.EventId}",
            input.Payload.GetValueOrDefault("transcript") ?? input.Type.ToString());

        var selected = _agents.SelectAgents(intent, context);
        var results = new List<LifeOsAgentResult>();
        foreach (var key in selected)
        {
            results.Add(await _agents.InvokeAsync(key, input, context, cancellationToken).ConfigureAwait(false));
        }

        var plan = _planning.Decompose(intent.Contains("MVP", StringComparison.OrdinalIgnoreCase)
            ? "Launch LifeOS MVP"
            : intent);

        var response =
            $"LifeOS orchestrator ({input.EventId}): intent=\"{intent}\"; " +
            $"agents=[{string.Join(", ", selected)}]; " +
            $"contextConfidence={context.AggregateConfidence:0.00}; " +
            $"planTasks={plan.Tasks.Count}; risk={risk}. " +
            "Awaiting human confirmation for irreversible actions.";

        _memory.Store(LifeOsMemoryLayer.Experience, $"outcome:{input.EventId}", response);

        return new LifeOsOrchestrationResult(
            TraceId: $"TR-{input.EventId}",
            Input: input,
            Context: context,
            Intent: intent,
            SelectedAgents: selected,
            AgentResults: results,
            Plan: plan,
            AggregatedResponse: response,
            RiskLevel: risk,
            PermissionOk: permissionOk,
            Pipeline: pipeline);
    }

    public object ArchitectureDigest()
    {
        _memory.SeedDemoProject();
        var snap = _memory.Snapshot();
        return new
        {
            ok = true,
            surface = "lifeos",
            part = "2",
            status = "scaffold",
            chapters = new[]
            {
                "6 Orchestrator",
                "7 Event Bus",
                "8 Context Engine",
                "9 Memory System",
                "10 Multi-Agent Framework",
                "11 Planning Engine"
            },
            orchestrator = new
            {
                responsibilities = new[]
                {
                    "Intent Classification", "Context Aggregation", "Agent Selection",
                    "Task Scheduling", "Memory Retrieval", "Permission Validation",
                    "Risk Assessment", "Workflow Coordination", "Response Aggregation",
                    "Learning Feedback"
                },
                pipeline = new[]
                {
                    "INPUT EVENTS", "Event Normalization", "Context Aggregator",
                    "Privacy Validator", "Intent Classifier", "Priority Scheduler",
                    "Multi-Agent Router Engine", "Decision Aggregator", "Response Generator"
                }
            },
            eventBus = new
            {
                mode = "in-memory-scaffold",
                eventTypes = Enum.GetNames<LifeOsEventType>(),
                recent = _bus.Recent(10)
            },
            contextEngine = new
            {
                sources = _context.KnownSourceNames
            },
            memory = new
            {
                layers = Enum.GetNames<LifeOsMemoryLayer>(),
                snapshot = snap
            },
            agents = new
            {
                count = _agents.Catalog.Count,
                catalog = _agents.Catalog
            },
            planning = new
            {
                sample = _planning.SampleLifeOsMvp()
            },
            notClaimed = new[]
            {
                "Production multimodal perception",
                "Durable memory stores",
                "Live LLM reasoning",
                "Kafka/Rabbit bus wiring",
                "Native mobile/desktop clients"
            }
        };
    }

    private static string ClassifyIntent(LifeOsEvent input)
    {
        if (input.Payload.TryGetValue("transcript", out var t) && !string.IsNullOrWhiteSpace(t))
        {
            return t.Trim();
        }

        return input.Type switch
        {
            LifeOsEventType.CalendarEvent => "Schedule calendar action",
            LifeOsEventType.HealthEvent => "Review health signal",
            LifeOsEventType.AutomationEvent => "Run automation workflow",
            LifeOsEventType.WorkflowEvent => "Advance workflow",
            _ => $"Handle {input.Type}"
        };
    }
}
