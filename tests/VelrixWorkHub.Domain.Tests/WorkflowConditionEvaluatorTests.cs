using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkflowConditionEvaluatorTests
{
    [Fact]
    public void SelectBranch_UsesFirstMatchingExpressionAndDefault()
    {
        const string config = "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 10000\"},{\"key\":\"normal\",\"expression\":\"amount <= 10000\"}],\"defaultKey\":\"normal\"}";

        Assert.Equal("high", WorkflowConditionEvaluator.SelectBranch(config, new Dictionary<string, object?> { ["amount"] = 12000m }));
        Assert.Equal("normal", WorkflowConditionEvaluator.SelectBranch(config, new Dictionary<string, object?> { ["amount"] = 100m }));
    }

    [Fact]
    public void Evaluate_SupportsLogicalComparisonsAndTextOperators()
    {
        var fields = new Dictionary<string, object?> { ["amount"] = 12000m, ["status"] = "Open", ["owner"] = "admin" };

        Assert.True(WorkflowConditionEvaluator.Evaluate("amount >= 10000 && status == 'Open'", fields));
        Assert.True(WorkflowConditionEvaluator.Evaluate("owner contains 'min' || status == 'Closed'", fields));
        Assert.False(WorkflowConditionEvaluator.Evaluate("amount < 10000 && status == 'Open'", fields));
    }

    [Fact]
    public void Evaluate_DoesNotRouteMissingValuesThroughOrderedOrTextComparisons()
    {
        var fields = new Dictionary<string, object?>();

        Assert.False(WorkflowConditionEvaluator.Evaluate("amount <= 100", fields));
        Assert.False(WorkflowConditionEvaluator.Evaluate("owner contains 'min'", fields));
        Assert.True(WorkflowConditionEvaluator.Evaluate("amount == null", fields));
        Assert.True(WorkflowConditionEvaluator.Evaluate("amount is null", fields));
    }

    [Fact]
    public void Instance_AdvancesConditionUsingSnapshotBranch()
    {
        var definition = new WorkflowDefinition("CONDITION_RUNTIME", "条件运行时");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额判断", configJson: "{\"branches\":[{\"key\":\"high\",\"expression\":\"amount > 10000\"},{\"key\":\"normal\",\"expression\":\"amount <= 10000\"}],\"defaultKey\":\"normal\"}");
        var high = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "高金额结束");
        var normal = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "普通结束");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, high.Id, "high");
        definition.Connect(condition.Id, normal.Id, "normal");
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(condition.Id);

        var transition = instance.AdvanceCondition(new Dictionary<string, object?> { ["amount"] = 15000m });

        Assert.Equal("high", transition.ConditionKey);
        Assert.Equal(high.Id, instance.CurrentNodeId);
    }

    [Fact]
    public void InvalidBranchConfiguration_IsRejectedBeforePublish()
    {
        var definition = new WorkflowDefinition("INVALID_CONDITION", "无效条件");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "条件", configJson: "{\"branches\":[{\"key\":\"same\",\"expression\":\"amount > 0\"},{\"key\":\"same\",\"expression\":\"amount <= 0\"}]}");
        var firstEnd = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束一");
        var secondEnd = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束二");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, firstEnd.Id, "same");
        definition.Connect(condition.Id, secondEnd.Id, "other");

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("条件配置无效"));
        Assert.Throws<InvalidOperationException>(() => definition.Publish());
    }
}
