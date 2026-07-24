using VelrixWorkHub.Application.Attachments;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

/// <summary>聚合许可证申请、审批实例和流程操作历史；不复制 Workflow 或 CRM 数据。</summary>
public sealed class LmsLicenseRequestDetailService(
    ILmsLicenseRepository licenses,
    WorkflowBindingService bindings,
    IWorkflowOperationRepository operations,
    AttachmentService? attachments = null,
    LmsLicenseAccessService? access = null)
{
    public LmsLicenseRequestDetail? Get(Guid requestId, string? actor = null, bool isAdministrator = false)
    {
        if (requestId == Guid.Empty) return null;
        var request = licenses.ListRequests().FirstOrDefault(x => x.Id == requestId);
        if (request is null) return null;
        if (access is not null && !access.CanReadRequest(requestId, actor, isAdministrator)) return null;
        var workflows = bindings.List(nameof(LmsLicenseRequest), requestId)
            .Where(x => x.DefinitionCode.Equals(WorkflowBindingCodes.LmsLicenseApproval, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.StartedAt)
            .ToArray();
        var history = operations.List(businessType: nameof(LmsLicenseRequest), businessId: requestId)
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .ToArray();
        var files = attachments?.List(nameof(LmsLicenseRequest), requestId) ?? [];
        return new LmsLicenseRequestDetail(request, workflows, history, files);
    }
}

public sealed record LmsLicenseRequestDetail(
    LmsLicenseRequest Request,
    IReadOnlyList<WorkflowInstance> Workflows,
    IReadOnlyList<WorkflowOperation> History,
    IReadOnlyList<BusinessAttachment> Attachments)
{
    public WorkflowInstance? LatestWorkflow => Workflows.FirstOrDefault();
}
