using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;
namespace VelrixWorkHub.Application.PmsProjects;
public sealed class PmsProjectChangeService(IPmsProjectChangeRepository repository, IPmsProjectRepository projectRepository, WorkflowApprovalService? approval = null) : IPmsProjectChangeWorkflowApprover
{
    public IReadOnlyList<PmsProjectChange> List(Guid? projectId = null, string? keyword = null)
    {
        var query = repository.List(projectId).AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Reason.Contains(text, StringComparison.OrdinalIgnoreCase));
        return query.OrderByDescending(x => x.CreatedTime).ToArray();
    }
    public PmsProjectChange Create(Guid projectId, string title, string reason, string? impact, string? requesterName) { EnsureProject(projectId); var item = new PmsProjectChange(projectId, title, reason, impact, requesterName, DateTime.Now); repository.Add(item); return item; }
    public void SetStatus(PmsProjectChange item, PmsProjectChangeStatus status)
    {
        if (status == PmsProjectChangeStatus.Approved && item.Status == PmsProjectChangeStatus.Proposed)
            approval?.RequireCompleted(WorkflowBindingCodes.ProjectChangeApproval, nameof(PmsProjectChange), item.Id, "项目变更批准");
        if (status == PmsProjectChangeStatus.Applied && item.Status != PmsProjectChangeStatus.Approved)
            throw new InvalidOperationException("只有已批准的项目变更才能实施。");
        item.SetStatus(status); repository.Update(item);
    }
    public void ApplyApproval(PmsProjectChange item)
    {
        if (item.Status == PmsProjectChangeStatus.Approved) return;
        if (item.Status != PmsProjectChangeStatus.Proposed) throw new InvalidOperationException($"项目变更不能从“{item.Status}”通过审批。");
        item.SetStatus(PmsProjectChangeStatus.Approved);
        repository.Update(item);
    }
    private void EnsureProject(Guid id) { if (!projectRepository.List().Any(x => x.Id == id)) throw new InvalidOperationException("关联项目不存在。"); }
}
