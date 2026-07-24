using FreeSql;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

public static class WorkflowSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        var repository = new FreeSqlWorkflowDefinitionRepository(fsql);
        Ensure(repository, WorkflowBindingCodes.ContractApproval, "合同审批", "合同生效前审批");
        Ensure(repository, WorkflowBindingCodes.ProjectChangeApproval, "项目变更审批", "项目变更实施前审批");
        Ensure(repository, WorkflowBindingCodes.SettlementApproval, "核销审批", "收付款核销审批");
        Ensure(repository, WorkflowBindingCodes.PurchaseOrderApproval, "采购订单审批", "采购订单提交前审批");
        Ensure(repository, WorkflowBindingCodes.SalesOrderApproval, "销售订单审批", "销售订单提交前审批");
        Ensure(repository, WorkflowBindingCodes.LmsLicenseApproval, "许可证申请审批", "外部 License 登记前的许可证申请审批");
        Ensure(repository, WorkflowBindingCodes.LmsLicenseReplacementApproval, "许可证授权替代审批", "续期、重发或换机前审批");
        Ensure(repository, WorkflowBindingCodes.ExpenseReimbursementApproval, "费用报销审批", "OA 报销付款前审批");
        Ensure(repository, WorkflowBindingCodes.CashAdvanceApproval, "借款备用金审批", "OA 借款或备用金申请审批");
        Ensure(repository, WorkflowBindingCodes.CashAdvanceRepaymentApproval, "借款还款审批", "OA 借款余额的现金或转账还款登记审批");
        Ensure(repository, WorkflowBindingCodes.PaymentRequestApproval, "付款申请审批", "OA 付款申请审批");
        Ensure(repository, WorkflowBindingCodes.ProcurementRequestApproval, "采购申请审批", "OA 采购申请审批，通过后再生成 ERP 采购订单");
        Ensure(repository, WorkflowBindingCodes.LeaveApproval, "请假审批", "OA 请假申请审批");
        Ensure(repository, WorkflowBindingCodes.OvertimeApproval, "加班审批", "OA 加班申请审批");
        Ensure(repository, WorkflowBindingCodes.PmpWorkItemCompletionApproval, "项目工作项验收审批", "项目工作项完成前的验收审批");
        Ensure(repository, WorkflowBindingCodes.SimpleSealRequestApproval, "印章申请审批", "简单表单印章申请审批");
        Ensure(repository, WorkflowBindingCodes.VehicleUseApproval, "用车审批", "OA 用车申请审批");
        Ensure(repository, WorkflowBindingCodes.AssetRequestApproval, "资产领用审批", "OA 资产领用申请审批，通过后才锁定资产");
    }

    private static void Ensure(FreeSqlWorkflowDefinitionRepository repository, string code, string name, string description)
    {
        var existing = repository.List(code).OrderByDescending(x => x.VersionNumber).FirstOrDefault();
        var published = repository.List(code, WorkflowDefinitionStatus.Published).OrderByDescending(x => x.VersionNumber).FirstOrDefault();
        if (published is not null && (!RequiresStatusAction(code) || HasApprovedAction(published))) return;

        var version = existing is null ? 1 : existing.VersionNumber + 1;
        var definition = new WorkflowDefinition(code, name, version, description);
        var start = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Start, "开始", 80, 160);
        var approvalConfig = code switch
        {
            WorkflowBindingCodes.ContractApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Active\"}}",
            WorkflowBindingCodes.ProjectChangeApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"}}",
            WorkflowBindingCodes.SettlementApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Active\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.PurchaseOrderApproval or WorkflowBindingCodes.SalesOrderApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Submitted\"}}",
            WorkflowBindingCodes.LmsLicenseApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Withdrawn\"}}",
            WorkflowBindingCodes.LmsLicenseReplacementApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Withdrawn\"}}",
            WorkflowBindingCodes.ExpenseReimbursementApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.CashAdvanceApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.CashAdvanceRepaymentApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.PaymentRequestApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.ProcurementRequestApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.LeaveApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.OvertimeApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.PmpWorkItemCompletionApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Completed\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"InProgress\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"InProgress\"}}",
            WorkflowBindingCodes.SimpleSealRequestApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.VehicleUseApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"},\"onCancelled\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            WorkflowBindingCodes.AssetRequestApproval => "{\"approver\":\"admin\",\"onApproved\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Approved\"},\"onRejected\":{\"type\":\"SetField\",\"field\":\"Status\",\"value\":\"Rejected\"}}",
            _ => "{\"approver\":\"admin\"}"
        };
        var approval = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.Approval, "业务负责人审批", 320, 160, approvalConfig);
        var end = definition.AddNode(Guid.CreateVersion7(), WorkflowNodeType.End, "结束", 560, 160);
        definition.Connect(start.Id, approval.Id);
        definition.Connect(approval.Id, end.Id);
        definition.Publish();
        // 多个 Web 实例可能同时执行启动种子。定义版本唯一索引负责选出一个
        // 胜出者，竞争失败表示另一实例已经写入同一版本，按幂等启动处理。
        repository.TryAdd(definition);
    }

    private static bool RequiresStatusAction(string code)
        => code is WorkflowBindingCodes.ContractApproval or WorkflowBindingCodes.ProjectChangeApproval or WorkflowBindingCodes.SettlementApproval or WorkflowBindingCodes.PurchaseOrderApproval or WorkflowBindingCodes.SalesOrderApproval or WorkflowBindingCodes.LmsLicenseApproval or WorkflowBindingCodes.LmsLicenseReplacementApproval or WorkflowBindingCodes.ExpenseReimbursementApproval or WorkflowBindingCodes.CashAdvanceApproval or WorkflowBindingCodes.CashAdvanceRepaymentApproval or WorkflowBindingCodes.PaymentRequestApproval or WorkflowBindingCodes.ProcurementRequestApproval or WorkflowBindingCodes.LeaveApproval or WorkflowBindingCodes.OvertimeApproval or WorkflowBindingCodes.PmpWorkItemCompletionApproval or WorkflowBindingCodes.SimpleSealRequestApproval or WorkflowBindingCodes.VehicleUseApproval or WorkflowBindingCodes.AssetRequestApproval;

    private static bool HasApprovedAction(WorkflowDefinition definition)
        => definition.Nodes.Where(x => x.Type == WorkflowNodeType.Approval)
            .Select(x => WorkflowNodeActionConfiguration.Parse(x.ConfigJson).Get(WorkflowActionTrigger.Approved))
            .Any(x => x is not null);

}
