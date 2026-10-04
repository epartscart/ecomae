using System.Text.Json.Nodes;
using EcomAE.Platform.Erp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class ErpWorkflowWriteServiceTests
{
    [Fact]
    public void NormalizeSteps_defaults_to_php_values()
    {
        var steps = ErpWorkflowWriteService.NormalizeSteps(
        [
            new ErpWorkflowStepInput(null, null, null, null, null, 0),
        ]);

        var step = Assert.Single(steps);
        Assert.Equal("action", step.StepType);
        Assert.Equal("", step.ActionType); // PHP: action_type ?? ''
        Assert.Equal("", step.Label);
        Assert.Equal("stop", step.OnFailure);
        Assert.Equal(0, step.RetryCount);
    }

    [Fact]
    public void NormalizeSteps_clips_unknown_enums_to_php_defaults()
    {
        var steps = ErpWorkflowWriteService.NormalizeSteps(
        [
            new ErpWorkflowStepInput("bogus", "bogus", "X", "{}", "bogus", 0),
        ]);

        var step = Assert.Single(steps);
        Assert.Equal("action", step.StepType);
        Assert.Equal("bogus", step.ActionType); // PHP keeps arbitrary action_type; run_action rejects at execution
        Assert.Equal("stop", step.OnFailure);
    }

    [Fact]
    public void NormalizeSteps_folds_label_into_config_only_when_absent()
    {
        var steps = ErpWorkflowWriteService.NormalizeSteps(
        [
            new ErpWorkflowStepInput("action", "send_email", "Email step", null, null, 0),
            new ErpWorkflowStepInput("action", "send_email", "Outer", """{"label":"Inner"}""", null, 0),
        ]);

        Assert.Equal("Email step", steps[0].Config["label"]?.GetValue<string>());
        Assert.Equal("Inner", steps[1].Config["label"]?.GetValue<string>());
    }

    [Fact]
    public void NormalizeSteps_malformed_config_json_becomes_empty_object_with_label()
    {
        var steps = ErpWorkflowWriteService.NormalizeSteps(
        [
            new ErpWorkflowStepInput("action", "send_notification", "S", "not-json{", null, 0),
        ]);

        var step = Assert.Single(steps);
        Assert.Equal("S", step.Config["label"]?.GetValue<string>());
        Assert.Single(step.Config);
    }

    [Theory]
    [InlineData("=", false)] // PHP only supports == and !=
    [InlineData("!=", false)]
    [InlineData("==", true)]
    [InlineData(">", false)]
    [InlineData("<=", true)]
    [InlineData("contains", true)]
    [InlineData("bogus", false)]
    public void EvaluateCondition_matches_php_operators(string op, bool expected)
    {
        Assert.Equal(expected, ErpWorkflowWriteService.EvaluateCondition(JsonValue.Create(10), op, JsonValue.Create(10)));
    }

    [Theory]
    [InlineData(10, ">", 5, true)]
    [InlineData(5, ">", 10, false)]
    [InlineData(7, ">=", 7, true)]
    [InlineData(7, "<", 3, false)]
    [InlineData("hello world", "contains", "world", true)]
    [InlineData("hello", "contains", "xyz", false)]
    public void EvaluateCondition_compares(object? actual, string op, object? compare, bool expected)
    {
        Assert.Equal(expected, ErpWorkflowWriteService.EvaluateCondition(JsonValue.Create(actual), op, JsonValue.Create(compare)));
    }

    [Fact]
    public void EvaluateCondition_null_actual_fails_all_operators()
    {
        Assert.False(ErpWorkflowWriteService.EvaluateCondition(null, "=", JsonValue.Create(1)));
    }

    [Fact]
    public void Interpolate_replaces_known_keys_and_keeps_unknown()
    {
        var data = new JsonObject
        {
            ["amount"] = JsonValue.Create(42),
            ["name"] = JsonValue.Create("Ali"),
        };

        var result = ErpWorkflowWriteService.Interpolate("Hi {{name}}, total {{amount}} -> {{missing}}", data);

        Assert.Equal("Hi Ali, total 42 -> {{missing}}", result);
    }

    [Fact]
    public void Interpolate_empty_data_and_template()
    {
        Assert.Equal("", ErpWorkflowWriteService.Interpolate("", new JsonObject()));
    }
}
