using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

/// <summary>
/// Workflow 完成项目变更审批后的唯一应用层推进入口。
/// </summary>
public interface IPmsProjectChangeWorkflowApprover
{
    void ApplyApproval(PmsProjectChange item);
}
