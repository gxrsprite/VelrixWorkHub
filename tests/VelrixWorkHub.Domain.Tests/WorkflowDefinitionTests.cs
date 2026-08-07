using System.Text.Json.Nodes;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class WorkflowDefinitionTests
{
    [Fact]
    public void Publish_ValidDefinitionWithApprovalAndCondition()
    {
        var definition = new WorkflowDefinition("CONTRACT_APPROVAL", "合同审批");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"department-manager\"}");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额判断", configJson: "{\"expression\":\"amount > 10000\",\"trueKey\":\"approved\",\"falseKey\":\"rejected\"}");
        var approved = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "通过");
        var rejected = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "驳回");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, condition.Id);
        definition.Connect(condition.Id, approved.Id, "approved");
        definition.Connect(condition.Id, rejected.Id, "rejected");

        var validation = definition.Validate();
        definition.Publish(new DateTime(2026, 7, 14, 8, 0, 0));

        Assert.True(validation.IsValid);
        Assert.Equal(WorkflowDefinitionStatus.Published, definition.Status);
        Assert.Equal(5, definition.Nodes.Count);
        Assert.Equal(4, definition.Connections.Count);
        Assert.Equal(new DateTime(2026, 7, 14, 8, 0, 0), definition.PublishedAt);
    }

    [Fact]
    public void Validate_ReportsMissingStartEndAndApprovalConfiguration()
    {
        var definition = new WorkflowDefinition("INVALID", "无效流程");
        definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批");

        var result = definition.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("开始节点"));
        Assert.Contains(result.Errors, x => x.Contains("结束节点"));
        Assert.Contains(result.Errors, x => x.Contains("approver"));
    }

    [Fact]
    public void Publish_AllowsApprovalConfiguredByBusinessField()
    {
        var definition = new WorkflowDefinition("BUSINESS_FIELD_APPROVER", "业务字段审批人");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "申请人审批", configJson: "{\"approverBusinessFields\":[\"RequesterName\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);

        definition.Publish();

        Assert.Equal(WorkflowDefinitionStatus.Published, definition.Status);
    }

    [Fact]
    public void Validate_ReportsUnknownConnectionWithoutThrowing()
    {
        var definition = new WorkflowDefinition("UNKNOWN_CONNECTION", "未知连线");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, end.Id);
        definition.Connect(Guid.CreateVersion7(), end.Id);

        var result = definition.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("不存在的节点"));
        Assert.Throws<InvalidOperationException>(() => definition.Publish());
    }

    [Fact]
    public void Validate_ReportsUnreachableNodeAndCycle()
    {
        var definition = new WorkflowDefinition("CYCLE", "循环流程");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"owner\"}");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "条件", configJson: "{\"expression\":\"ok\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        var orphan = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "孤立通知", configJson: "{\"recipients\":\"finance\"}");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, condition.Id);
        definition.Connect(condition.Id, end.Id);
        definition.Connect(condition.Id, approval.Id, "retry");

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("孤立通知"));
        Assert.Contains(result.Errors, x => x.Contains("循环"));
        Assert.Throws<InvalidOperationException>(() => definition.Publish());
    }

    [Fact]
    public void Validate_ReportsIncompleteConditionBranches()
    {
        var definition = new WorkflowDefinition("BRANCH", "分支流程");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "条件", configJson: "{\"expression\":\"amount > 0\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, end.Id, "default");

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("至少需要两个"));
    }

    [Fact]
    public void Validate_RejectsAmbiguousConditionBranchAndNonExecutableStart()
    {
        var definition = new WorkflowDefinition("AMBIGUOUS_BRANCH", "歧义分支");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "条件", configJson: "{\"expression\":\"amount > 0\",\"trueKey\":\"yes\",\"falseKey\":\"no\"}");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "通过一");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "通过二");
        var rejected = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "拒绝");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, first.Id, "yes");
        definition.Connect(condition.Id, second.Id, "yes");
        definition.Connect(condition.Id, rejected.Id, "no");

        var ambiguous = definition.Validate();
        Assert.Contains(ambiguous.Errors, x => x.Contains("分支键不能重复"));

        var noStartExit = new WorkflowDefinition("NO_START_EXIT", "开始无出口");
        noStartExit.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        noStartExit.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");

        var missingExit = noStartExit.Validate();
        Assert.Contains(missingExit.Errors, x => x.Contains("开始") && x.Contains("缺少出边"));
        Assert.Contains(missingExit.Errors, x => x.Contains("开始") && x.Contains("必须只有一条"));
        Assert.Throws<InvalidOperationException>(() => noStartExit.Publish());
    }

    [Fact]
    public void Validate_AcceptsMajorityApprovalMode()
    {
        var definition = new WorkflowDefinition("INVALID_APPROVAL_MODE", "无效审批策略");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\",\"approvalMode\":\"Majority\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);

        var result = definition.Validate();

        Assert.True(result.IsValid);
        definition.Publish();
    }

    [Fact]
    public void Validate_RejectsQuorumApprovalModeWithoutPositiveRequiredApprovals()
    {
        var definition = new WorkflowDefinition("INVALID_QUORUM", "无效法定人数");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\",\"approvalMode\":\"Quorum\",\"requiredApprovals\":0}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);

        Assert.Contains(definition.Validate().Errors, x => x.Contains("审批策略") && x.Contains("requiredApprovals"));
    }

    [Fact]
    public void Validate_RejectsParallelSplitDirectlyIntoJoin()
    {
        var definition = new WorkflowDefinition("INVALID_DIRECT_SPLIT_JOIN", "拆分直接汇聚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, join.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(join.Id, end.Id);

        var result = definition.Validate();

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.Contains("不能直接连接并行汇聚", StringComparison.Ordinal));
    }

    [Fact]
    public void AdvanceActiveNode_RejectsDirectParallelJoinBypass()
    {
        var definition = new WorkflowDefinition("JOIN_BYPASS", "汇聚绕过保护");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(split.Id);
        instance.SplitParallel(split.Id);

        var error = Assert.Throws<InvalidOperationException>(() => instance.AdvanceActiveNode(first.Id, join.Id));

        Assert.Contains("ArriveAtParallelJoin", error.Message);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(x => x), instance.ActiveNodeIds.OrderBy(x => x));
    }

    [Fact]
    public void AdvanceActiveNode_RejectsParallelBranchEndingBeforeOtherBranches()
    {
        var definition = new WorkflowDefinition("EARLY_PARALLEL_END", "并行提前结束");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, end.Id);
        definition.Connect(second.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(split.Id);
        instance.SplitParallel(split.Id);

        var error = Assert.Throws<InvalidOperationException>(() => instance.AdvanceActiveNode(first.Id, end.Id));

        Assert.Contains("ParallelJoin", error.Message);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(x => x), instance.ActiveNodeIds.OrderBy(x => x));
    }

    [Fact]
    public void Validate_RejectsInvalidReturnTarget()
    {
        var definition = new WorkflowDefinition("INVALID_RETURN_TARGET", "无效回退目标");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\",\"returnTargets\":[\"not-a-guid\"]}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("回退配置"));
        Assert.Throws<InvalidOperationException>(() => definition.Publish());
    }

    [Fact]
    public void ParallelSplitAndJoin_TrackAllActiveBranchesBeforeContinuing()
    {
        var definition = new WorkflowDefinition("PARALLEL_RUNTIME", "并行运行时");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();

        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        instance.AdvanceTo(split.Id);
        instance.SplitParallel(split.Id);
        Assert.Equal(new[] { first.Id, second.Id }.OrderBy(x => x), instance.ActiveNodeIds.OrderBy(x => x));

        Assert.False(instance.ArriveAtParallelJoin(first.Id, join.Id));
        Assert.Equal([second.Id], instance.ActiveNodeIds);
        Assert.True(instance.ArriveAtParallelJoin(second.Id, join.Id));
        Assert.Equal([join.Id], instance.ActiveNodeIds);
        instance.AdvanceTo(end.Id);
        Assert.Equal(end.Id, instance.CurrentNodeId);
    }

    [Fact]
    public void Validate_RejectsMalformedParallelSplitAndJoin()
    {
        var definition = new WorkflowDefinition("INVALID_PARALLEL", "无效并行");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(join.Id, end.Id);

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("并行拆分"));
        Assert.Contains(result.Errors, x => x.Contains("并行汇聚") && x.Contains("入边"));
        Assert.Throws<InvalidOperationException>(() => definition.Publish());
    }

    [Fact]
    public void Validate_RejectsParallelJoinWithoutCommonUpstreamSplit()
    {
        var definition = new WorkflowDefinition("UNSTRUCTURED_JOIN", "非结构化汇聚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "互斥条件", configJson: "{\"expression\":\"amount > 0\",\"trueKey\":\"yes\",\"falseKey\":\"no\"}");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "条件审批一", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "条件审批二", configJson: "{\"approver\":\"finance\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "错误汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, first.Id, "yes");
        definition.Connect(condition.Id, second.Id, "no");
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("上游并行拆分范围"));
        Assert.Throws<InvalidOperationException>(() => definition.Publish());
    }

    [Fact]
    public void Validate_AllowsConditionSourceToJoinInsideCommonParallelSplit()
    {
        var definition = new WorkflowDefinition("STRUCTURED_CONDITION_JOIN", "结构化条件汇聚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "人工审批", configJson: "{\"approver\":\"admin\"}");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "金额条件", configJson: "{\"expression\":\"amount > 0\",\"trueKey\":\"yes\",\"falseKey\":\"no\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, condition.Id);
        definition.Connect(approval.Id, join.Id);
        definition.Connect(condition.Id, join.Id, "yes");
        definition.Connect(condition.Id, join.Id, "no");
        definition.Connect(join.Id, end.Id);

        Assert.True(definition.Validate().IsValid);
        definition.Publish();
    }

    [Fact]
    public void Validate_RejectsConditionFanOutToMultipleJoinSourcesWithoutNestedSplit()
    {
        var definition = new WorkflowDefinition("CONDITION_FANOUT_JOIN", "条件扇出汇聚");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "外层拆分");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "互斥条件", configJson: "{\"expression\":\"amount > 0\",\"trueKey\":\"yes\",\"falseKey\":\"no\"}");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "条件审批一", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "条件审批二", configJson: "{\"approver\":\"finance\"}");
        var notification = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "并行通知", configJson: "{\"recipients\":\"legal\",\"content\":\"请关注\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "错误汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, condition.Id);
        definition.Connect(split.Id, notification.Id);
        definition.Connect(condition.Id, first.Id, "yes");
        definition.Connect(condition.Id, second.Id, "no");
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(notification.Id, join.Id);
        definition.Connect(join.Id, end.Id);

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("互斥分支会导致 Join 永久等待"));
        Assert.Throws<InvalidOperationException>(() => definition.Publish());
    }

    [Fact]
    public void Validate_AllowsConditionFanOutWhenNestedSplitCreatesJoinSources()
    {
        var definition = new WorkflowDefinition("CONDITION_NESTED_SPLIT", "条件后嵌套并行");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "条件", configJson: "{\"expression\":\"amount > 0\",\"trueKey\":\"yes\",\"falseKey\":\"no\"}");
        var nestedSplit = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "条件后拆分");
        var bypass = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "默认通知", configJson: "{\"recipients\":\"legal\",\"content\":\"默认路径\"}");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var nestedJoin = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "条件后汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, nestedSplit.Id, "yes");
        definition.Connect(condition.Id, bypass.Id, "no");
        definition.Connect(nestedSplit.Id, first.Id);
        definition.Connect(nestedSplit.Id, second.Id);
        definition.Connect(first.Id, nestedJoin.Id);
        definition.Connect(second.Id, nestedJoin.Id);
        definition.Connect(nestedJoin.Id, end.Id);
        definition.Connect(bypass.Id, end.Id);

        Assert.True(definition.Validate().IsValid);
        definition.Publish();
    }

    [Fact]
    public void Validate_RejectsParallelSplitDirectlyToEnd()
    {
        var definition = new WorkflowDefinition("INVALID_PARALLEL_END", "并行直接结束");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, approval.Id);
        definition.Connect(split.Id, end.Id);
        definition.Connect(approval.Id, end.Id);

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("并行拆分分支"));
        Assert.Throws<InvalidOperationException>(() => definition.Publish());
    }

    [Fact]
    public void Validate_ReportsBranchThatCannotReachAnEnd()
    {
        var definition = new WorkflowDefinition("DEAD_BRANCH", "无出口分支");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var condition = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "条件", configJson: "{\"expression\":\"ok\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        var dead = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Notification, "无出口通知", configJson: "{\"recipients\":\"owner\"}");
        definition.Connect(start.Id, condition.Id);
        definition.Connect(condition.Id, end.Id, "pass");
        definition.Connect(condition.Id, dead.Id, "notify");

        var result = definition.Validate();

        Assert.Contains(result.Errors, x => x.Contains("无出口通知") && x.Contains("结束节点"));
    }

    [Fact]
    public void PublishedDefinition_CannotBeModified()
    {
        var definition = CreateLinearDefinition("IMMUTABLE");
        definition.Publish();

        Assert.Throws<InvalidOperationException>(() => definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "新结束"));
        Assert.Throws<InvalidOperationException>(() => definition.Connect(Guid.CreateVersion7(), Guid.CreateVersion7()));
        definition.Archive();
        Assert.Equal(WorkflowDefinitionStatus.Archived, definition.Status);
    }

    [Fact]
    public void WorkflowService_IncrementsDraftVersionPerCode()
    {
        var repository = new WorkflowRepository();
        var service = new WorkflowDefinitionService(repository);

        var first = service.CreateDraft("ERP_ORDER", "订单审批");
        var second = service.CreateDraft("ERP_ORDER", "订单审批");

        Assert.Equal(1, first.VersionNumber);
        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(2, service.List("ERP_ORDER").Count);
    }

    [Fact]
    public void WorkflowService_RetriesWhenAnotherProcessWinsDefinitionVersion()
    {
        var repository = new ConflictInjectingDefinitionRepository();
        var service = new WorkflowDefinitionService(repository);

        var created = service.CreateDraft("CONCURRENT_FLOW", "并发版本");

        Assert.Equal(2, created.VersionNumber);
        Assert.Equal(new[] { 2, 1 }, service.List("CONCURRENT_FLOW").Select(x => x.VersionNumber));
    }

    [Fact]
    public void WorkflowDefinitionAndInstance_NormalizeCodesWhileAcceptingLegacySnapshotCase()
    {
        var definition = CreateLinearDefinition("  mixed_case_flow  ");

        Assert.Equal("MIXED_CASE_FLOW", definition.Code);

        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "crm.contract", Guid.CreateVersion7());

        Assert.Equal("MIXED_CASE_FLOW", instance.DefinitionCode);

        var restored = WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, "mixed_case_flow", instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision);

        Assert.Equal("MIXED_CASE_FLOW", restored.DefinitionCode);
        Assert.Equal(instance.DefinitionSnapshotJson, restored.DefinitionSnapshotJson);
    }

    [Fact]
    public void WorkflowService_CachesPublishedDefinitionsUntilVersionChanges()
    {
        var repository = new WorkflowRepository();
        var service = new WorkflowDefinitionService(repository);
        var definition = CreateLinearDefinition("CACHE_FLOW");
        definition.Publish();
        repository.Add(definition);

        var first = service.List("CACHE_FLOW", WorkflowDefinitionStatus.Published);
        var callsAfterFirstRead = repository.ListCallCount;
        var second = service.List("CACHE_FLOW", WorkflowDefinitionStatus.Published);

        Assert.Same(first[0], second[0]);
        Assert.Equal(callsAfterFirstRead, repository.ListCallCount);

        var next = service.CreateDraft("CACHE_FLOW", "缓存流程 V2");
        var nextStart = next.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var nextEnd = next.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        next.Connect(nextStart.Id, nextEnd.Id);
        service.Publish(next);

        Assert.Equal(2, service.List("CACHE_FLOW", WorkflowDefinitionStatus.Published).Count);
        Assert.True(repository.ListCallCount > callsAfterFirstRead);
    }

    [Fact]
    public void WorkflowService_SavesDraftGraphAfterAddingNodes()
    {
        var repository = new WorkflowRepository();
        var service = new WorkflowDefinitionService(repository);
        var definition = service.CreateDraft("SAVE_DRAFT", "保存草稿");

        definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        service.SaveDraft(definition);

        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void WorkflowService_DeletesDraftButProtectsPublishedVersion()
    {
        var repository = new WorkflowRepository();
        var service = new WorkflowDefinitionService(repository);
        var draft = service.CreateDraft("DELETE_DRAFT", "待删除草稿");

        service.DeleteDraft(draft);

        Assert.Empty(service.List("DELETE_DRAFT"));
        var published = CreateLinearDefinition("KEEP_PUBLISHED");
        published.Publish();
        repository.Add(published);
        Assert.Throws<InvalidOperationException>(() => service.DeleteDraft(published));
    }

    [Fact]
    public void Definition_RejectsInvalidJsonAndDuplicateConnection()
    {
        var definition = new WorkflowDefinition("VALIDATE", "校验");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");

        Assert.Throws<ArgumentException>(() => definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Condition, "条件", configJson: "not-json"));
        definition.Connect(start.Id, end.Id);
        Assert.Throws<InvalidOperationException>(() => definition.Connect(start.Id, end.Id));
    }

    [Fact]
    public void WorkflowDocument_RoundTripsCanvasJsonAndPublishedStatus()
    {
        var definition = CreateLinearDefinition("JSON_ROUNDTRIP");
        definition.Publish(new DateTime(2026, 7, 14, 8, 30, 0));

        var json = WorkflowDefinitionDocument.FromDomain(definition).ToJson();
        var restored = WorkflowDefinitionDocument.FromJson(json).ToDomain();

        Assert.Contains("线性流程", json);
        Assert.Equal(WorkflowDefinitionStatus.Published, restored.Status);
        Assert.Equal(definition.Code, restored.Code);
        Assert.Equal(definition.VersionNumber, restored.VersionNumber);
        Assert.Equal(definition.Nodes.Select(x => x.Id), restored.Nodes.Select(x => x.Id));
        Assert.Equal(definition.Connections, restored.Connections);
        Assert.Equal(definition.PublishedAt, restored.PublishedAt);
    }

    [Fact]
    public void WorkflowInstance_StartsOnlyFromPublishedVersionAndKeepsSnapshot()
    {
        var definition = CreateLinearDefinition("INSTANCE");
        var businessId = Guid.CreateVersion7();
        Assert.Throws<InvalidOperationException>(() => WorkflowInstance.Start(definition, "crm.contract", businessId));

        definition.Publish(new DateTime(2026, 7, 14, 9, 30, 0));
        var instance = WorkflowInstance.Start(definition, "crm.contract", businessId, new DateTime(2026, 7, 14, 9, 31, 0));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal("system", instance.StartedBy);
        Assert.Equal(1, instance.DefinitionVersion);
        Assert.Contains("INSTANCE", instance.DefinitionSnapshotJson);
        Assert.Equal(businessId, instance.BusinessId);
    }

    [Fact]
    public void WorkflowInstance_AdvancesOnlyAlongSnapshotConnections()
    {
        var definition = new WorkflowDefinition("ADVANCE", "节点推进");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());

        Assert.Equal(start.Id, instance.CurrentNodeId);
        Assert.Single(instance.GetOutgoingTransitions());
        Assert.Throws<InvalidOperationException>(() => instance.AdvanceTo(end.Id));

        instance.AdvanceTo(approval.Id);
        Assert.Equal(approval.Id, instance.CurrentNodeId);
        instance.AdvanceTo(end.Id);
        Assert.Equal(end.Id, instance.CurrentNodeId);
    }

    [Fact]
    public void WorkflowInstance_ReadsLegacyNumericNodeTypesWithoutChangingNewJsonContract()
    {
        var definition = new WorkflowDefinition("LEGACY_SNAPSHOT", "旧快照兼容");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "legacy.snapshot", Guid.CreateVersion7());
        var legacySnapshot = instance.DefinitionSnapshotJson
            .Replace("\"Type\":\"Start\"", "\"Type\":0", StringComparison.Ordinal)
            .Replace("\"Type\":\"End\"", "\"Type\":5", StringComparison.Ordinal);

        var rehydrated = WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, legacySnapshot, instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt, instance.PreviousInstanceId, instance.Revision);

        Assert.Equal(WorkflowNodeType.Start, rehydrated.GetNodeType(start.Id));
        Assert.Equal(WorkflowNodeType.End, rehydrated.GetNodeType(end.Id));
    }

    [Fact]
    public void WorkflowInstance_ReadsActionFromImmutableSnapshot()
    {
        var definition = new WorkflowDefinition("ACTION_SNAPSHOT", "动作快照");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();

        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());

        var action = instance.GetNodeAction(approval.Id, WorkflowActionTrigger.Approved);

        Assert.NotNull(action);
        Assert.Equal("Submitted", action.Value);
    }

    [Fact]
    public void WorkflowInstance_CannotFinishTwice()
    {
        var definition = CreateLinearDefinition("INSTANCE_STATUS");
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "pms.change", Guid.CreateVersion7());

        instance.Complete(new DateTime(2026, 7, 14, 10, 0, 0));

        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.Throws<InvalidOperationException>(() => instance.Reject());
    }

    [Fact]
    public void WorkflowInstanceService_StartsAndPersistsTerminalTransition()
    {
        var definition = CreateLinearDefinition("INSTANCE_SERVICE");
        definition.Publish();
        var repository = new WorkflowInstanceRepository();
        var service = new WorkflowInstanceService(repository);

        var instance = service.Start(definition, "erp.purchase-order", Guid.CreateVersion7());
        service.Complete(instance);

        Assert.Equal(WorkflowInstanceStatus.Completed, Assert.Single(service.List(status: WorkflowInstanceStatus.Completed)).Status);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(1, repository.UpdateCount);
    }

    [Fact]
    public void WorkflowInstanceService_WithoutTransaction_RestoresStateWhenTerminalPersistenceFails()
    {
        var definition = CreateLinearDefinition("INSTANCE_SERVICE_ROLLBACK");
        definition.Publish();
        var repository = new ThrowingTerminalInstanceRepository();
        var service = new WorkflowInstanceService(repository);
        var instance = service.Start(definition, "custom.document", Guid.CreateVersion7());

        Assert.Throws<InvalidOperationException>(() => service.Complete(instance));

        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(definition.Nodes.Single(x => x.Type == WorkflowNodeType.Start).Id, instance.CurrentNodeId);
        Assert.Equal(1, instance.Revision);
        Assert.Contains(instance.CurrentNodeId, instance.ActiveNodeIds);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsInvalidSnapshot()
    {
        Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(Guid.CreateVersion7(), Guid.CreateVersion7(), "FLOW", 1, "crm.contract", Guid.CreateVersion7(), null, "not-json", WorkflowInstanceStatus.Running, Guid.CreateVersion7(), DateTime.Now, null));
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsUnknownCurrentNodeWithLegacyEmptyActiveNodes()
    {
        var definition = CreateLinearDefinition("UNKNOWN_CURRENT_NODE");
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, Guid.CreateVersion7(), instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, "[]", instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson));

        Assert.Contains("流程实例活动节点快照无效", error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsDuplicateSnapshotConnection()
    {
        var definition = CreateLinearDefinition("DUPLICATE_SNAPSHOT_CONNECTION");
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var document = JsonNode.Parse(instance.DefinitionSnapshotJson)!.AsObject();
        var connections = document["connections"]!.AsArray();
        connections.Add(connections[0]!.DeepClone());

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, document.ToJsonString(), instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson));

        Assert.Contains("流程实例快照包含重复连线", error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsSnapshotThatBypassesPublishedNodeValidation()
    {
        var definition = new WorkflowDefinition("INVALID_SNAPSHOT_NODE_CONFIG", "快照节点配置");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approvalNode = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approvalNode.Id);
        definition.Connect(approvalNode.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var document = JsonNode.Parse(instance.DefinitionSnapshotJson)!.AsObject();
        var approval = document["nodes"]!.AsArray().Single(node =>
            string.Equals(node!["type"]!.GetValue<string>(), nameof(WorkflowNodeType.Approval), StringComparison.Ordinal));
        approval!["configJson"] = "{}";

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, document.ToJsonString(), instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson));

        Assert.Contains("流程实例快照图校验失败", error.Message);
        Assert.Contains("缺少配置“approver”", error.Message);
    }

    [Theory]
    [InlineData("id", "流程实例快照与实例定义 ID 不一致")]
    [InlineData("code", "流程实例快照与实例定义编码不一致")]
    [InlineData("version", "流程实例快照与实例定义版本不一致")]
    public void WorkflowInstance_RehydrateRejectsSnapshotWithMismatchedDefinitionMetadata(string mismatch, string expectedMessage)
    {
        var definition = CreateLinearDefinition("MISMATCHED_SNAPSHOT_VERSION");
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var document = JsonNode.Parse(instance.DefinitionSnapshotJson)!.AsObject();
        switch (mismatch)
        {
            case "id": document["id"] = Guid.CreateVersion7().ToString(); break;
            case "code": document["code"] = "OTHER_DEFINITION"; break;
            default: document["versionNumber"] = instance.DefinitionVersion + 1; break;
        }

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, document.ToJsonString(), instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson));

        Assert.Contains(expectedMessage, error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsInvalidParallelJoinArrivalSource()
    {
        var definition = new WorkflowDefinition("INVALID_JOIN_ARRIVAL", "无效汇聚到达快照");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var invalidArrivals = $"{{\"{join.Id}\":[\"{start.Id}\"]}}";

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, invalidArrivals, instance.LoopIterationsJson));

        Assert.Contains("并行汇聚快照无效", error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsParallelJoinArrivalWhoseSourceIsStillActive()
    {
        var definition = CreateParallelApprovalDefinition("ACTIVE_JOIN_ARRIVAL");
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var join = definition.Nodes.Single(x => x.Type == WorkflowNodeType.ParallelJoin);
        var source = definition.Nodes.First(x => x.Type == WorkflowNodeType.Approval);
        var invalidArrivals = $"{{\"{join.Id}\":[\"{source.Id}\"]}}";
        var activeNodes = $"[\"{source.Id}\"]";

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, source.Id, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, activeNodes, invalidArrivals, instance.LoopIterationsJson));

        Assert.Contains("并行汇聚快照无效", error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsParallelJoinArrivalThatAlreadyContainsAllSources()
    {
        var definition = CreateParallelApprovalDefinition("COMPLETE_JOIN_ARRIVAL");
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var join = definition.Nodes.Single(x => x.Type == WorkflowNodeType.ParallelJoin);
        var sources = definition.Connections.Where(x => x.TargetNodeId == join.Id).Select(x => x.SourceNodeId);
        var invalidArrivals = $"{{\"{join.Id}\":[{string.Join(',', sources.Select(x => $"\"{x}\""))}]}}";
        var start = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Start);
        var activeNodes = $"[\"{start.Id}\"]";

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, start.Id, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, activeNodes, invalidArrivals, instance.LoopIterationsJson));

        Assert.Contains("并行汇聚快照无效", error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsEndAlongsideOtherActiveNode()
    {
        var definition = new WorkflowDefinition("INVALID_ACTIVE_END", "无效活动结束节点快照");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var invalidActiveNodes = $"[\"{end.Id}\",\"{approval.Id}\"]";

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, end.Id, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, invalidActiveNodes, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson));

        Assert.Contains("流程实例活动节点快照无效", error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsLoopCounterOnNonLoopNode()
    {
        var definition = CreateLinearDefinition("INVALID_LOOP_COUNTER");
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var start = definition.Nodes.Single(x => x.Type == WorkflowNodeType.Start);
        var invalidCounters = $"{{\"{start.Id}\":1}}";

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, invalidCounters));

        Assert.Contains("循环计数包含无效节点或次数", error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsInvalidApprovalAssigneeSnapshot()
    {
        var definition = new WorkflowDefinition("INVALID_APPROVAL_SNAPSHOT", "无效审批人快照");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var invalidAssignees = $"{{\"{start.Id}\":[\"admin\"]}}";

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson, invalidAssignees));

        Assert.Contains("审批人快照无效", error.Message);
    }

    [Fact]
    public void WorkflowInstance_RehydrateRejectsEmptyApprovalAssigneeSnapshot()
    {
        var definition = new WorkflowDefinition("EMPTY_APPROVAL_SNAPSHOT", "空审批人快照");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "审批", configJson: "{\"approver\":\"admin\"}");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        var instance = WorkflowInstance.Start(definition, "custom.document", Guid.CreateVersion7());
        var invalidAssignees = $"{{\"{approval.Id}\":[]}}";

        var error = Assert.Throws<ArgumentException>(() => WorkflowInstance.Rehydrate(
            instance.Id, instance.DefinitionId, instance.DefinitionCode, instance.DefinitionVersion, instance.BusinessType, instance.BusinessId,
            instance.StartedBy, instance.DefinitionSnapshotJson, instance.Status, instance.CurrentNodeId, instance.StartedAt, instance.CompletedAt,
            instance.PreviousInstanceId, instance.Revision, instance.ActiveNodeIdsJson, instance.ParallelJoinArrivalsJson, instance.LoopIterationsJson, invalidAssignees));

        Assert.Contains("审批人快照无效", error.Message);
    }

    [Fact]
    public void WorkflowDocument_RejectsMissingGraphCollections()
    {
        var document = WorkflowDefinitionDocument.FromJson("{\"code\":\"FLOW\",\"name\":\"流程\",\"description\":\"\",\"versionNumber\":1,\"status\":\"Draft\",\"createdAt\":\"2026-07-14T10:00:00\",\"nodes\":null,\"connections\":null}");

        Assert.Throws<InvalidOperationException>(() => document.ToDomain());
    }

    private static WorkflowDefinition CreateParallelApprovalDefinition(string code)
    {
        var definition = new WorkflowDefinition(code, "并行审批流程");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var split = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelSplit, "并行拆分");
        var first = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "部门审批", configJson: "{\"approver\":\"admin\"}");
        var second = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "财务审批", configJson: "{\"approver\":\"finance\"}");
        var join = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.ParallelJoin, "并行汇聚");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, split.Id);
        definition.Connect(split.Id, first.Id);
        definition.Connect(split.Id, second.Id);
        definition.Connect(first.Id, join.Id);
        definition.Connect(second.Id, join.Id);
        definition.Connect(join.Id, end.Id);
        return definition;
    }

    private static WorkflowDefinition CreateLinearDefinition(string code)
    {
        var definition = new WorkflowDefinition(code, "线性流程");
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始");
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, end.Id);
        return definition;
    }

    private sealed class WorkflowRepository : IWorkflowDefinitionRepository
    {
        private readonly List<WorkflowDefinition> items = [];
        public int UpdateCount { get; private set; }
        public int ListCallCount { get; private set; }
        public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null)
        {
            ListCallCount++;
            return items.Where(x => (code is null || x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) && (status is null || x.Status == status)).ToArray();
        }
        public void Add(WorkflowDefinition definition) => items.Add(definition);
        public bool TryAdd(WorkflowDefinition definition)
        {
            if (items.Any(x => x.Id == definition.Id || (x.Code.Equals(definition.Code, StringComparison.OrdinalIgnoreCase) && x.VersionNumber == definition.VersionNumber))) return false;
            Add(definition);
            return true;
        }
        public void Update(WorkflowDefinition definition) => UpdateCount++;
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class ConflictInjectingDefinitionRepository : IWorkflowDefinitionRepository
    {
        private readonly List<WorkflowDefinition> items = [];
        private bool injected;

        public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null)
            => items.Where(x => (code is null || x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) && (status is null || x.Status == status)).ToArray();

        public void Add(WorkflowDefinition definition) => items.Add(definition);

        public bool TryAdd(WorkflowDefinition definition)
        {
            if (!injected)
            {
                injected = true;
                Add(new WorkflowDefinition(definition.Code, "并发胜出版本", versionNumber: definition.VersionNumber));
                return false;
            }

            Add(definition);
            return true;
        }

        public void Update(WorkflowDefinition definition) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class WorkflowInstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public int AddCount { get; private set; }
        public int UpdateCount { get; private set; }
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => items.Where(x => (businessType is null || x.BusinessType == businessType) && (businessId is null || x.BusinessId == businessId) && (status is null || x.Status == status)).ToArray();
        public void Add(WorkflowInstance instance) { items.Add(instance); AddCount++; }
        public bool TryAdd(WorkflowInstance instance) { Add(instance); return true; }
        public void Update(WorkflowInstance instance) { UpdateCount++; }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }

    private sealed class ThrowingTerminalInstanceRepository : IWorkflowInstanceRepository
    {
        private readonly List<WorkflowInstance> items = [];
        public IReadOnlyList<WorkflowInstance> List(string? businessType = null, Guid? businessId = null, WorkflowInstanceStatus? status = null) => items.ToArray();
        public void Add(WorkflowInstance instance) => items.Add(instance);
        public bool TryAdd(WorkflowInstance instance) { Add(instance); return true; }
        public void Update(WorkflowInstance instance)
        {
            if (instance.Status == WorkflowInstanceStatus.Completed)
                throw new InvalidOperationException("模拟实例完成持久化失败");
        }
        public bool TryUpdate(WorkflowInstance instance) { var nextRevision = checked(instance.Revision + 1); Update(instance); instance.MarkPersistedRevision(nextRevision); return true; }
    }
}
