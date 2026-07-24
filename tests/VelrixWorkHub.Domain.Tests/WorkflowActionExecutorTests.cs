using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkflowActionExecutorTests
{
    [Fact]
    public void Execute_ReadsActionFromInstanceSnapshot_AndDispatchesTypedHandler()
    {
        var definition = CreateDefinition("ACTION_EXECUTE");
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var handler = new RecordingHandler("custom.document");

        new WorkflowActionExecutor([handler]).Execute(instance, definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id, WorkflowActionTrigger.Rejected, "金额不完整", "finance");

        var action = Assert.Single(handler.Actions);
        Assert.Equal(WorkflowActionType.SetField, action.Type);
        Assert.Equal("Status", action.Field);
        Assert.Equal("Rejected", action.Value);
        Assert.Equal("金额不完整", handler.Reason);
        Assert.Equal("finance", handler.Actor);
    }

    [Fact]
    public void Execute_RequiresExactlyOneHandlerForConfiguredBusinessAction()
    {
        var definition = CreateDefinition("ACTION_HANDLER");
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var nodeId = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Approval).Id;

        Assert.Throws<InvalidOperationException>(() => new WorkflowActionExecutor([]).Execute(instance, nodeId, WorkflowActionTrigger.Approved));
    }

    private static WorkflowDefinition CreateDefinition(string code)
    {
        var definition = new WorkflowDefinition(code, "动作测试");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        return definition;
    }

    private sealed class RecordingHandler(string businessType) : IWorkflowActionHandler
    {
        public List<WorkflowActionDefinition> Actions { get; } = [];
        public string? Reason { get; private set; }
        public string? Actor { get; private set; }

        public bool CanHandle(string requestedBusinessType) => requestedBusinessType == businessType;

        public void Execute(WorkflowActionContext context, WorkflowActionDefinition action)
        {
            Actions.Add(action);
            Reason = context.Reason;
            Actor = context.Actor;
        }
    }
}
