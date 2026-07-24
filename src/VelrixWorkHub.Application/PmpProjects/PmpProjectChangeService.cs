using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;
namespace VelrixWorkHub.Application.PmpProjects;
public sealed class PmpProjectChangeService(IPmpProjectChangeRepository repository, IPmpProjectRepository projectRepository, WorkflowApprovalService? approval = null) : IPmpProjectChangeWorkflowApprover
{
    public IReadOnlyList<PmpProjectChange> List(Guid? projectId = null, string? keyword = null)
    {
        var query = repository.List(projectId).AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || x.Reason.Contains(text, StringComparison.OrdinalIgnoreCase));
        return query.OrderByDescending(x => x.CreatedTime).ToArray();
    }
    public PmpProjectChange Create(Guid projectId, string title, string reason, string? impact, string? requesterName) { EnsureProject(projectId); var item = new PmpProjectChange(projectId, title, reason, impact, requesterName, DateTime.Now); repository.Add(item); return item; }
    public void SetStatus(PmpProjectChange item, PmpProjectChangeStatus status)
    {
        if (status == PmpProjectChangeStatus.Approved && item.Status == PmpProjectChangeStatus.Proposed)
            approval?.RequireCompleted(WorkflowBindingCodes.ProjectChangeApproval, nameof(PmpProjectChange), item.Id, "项目变更批准");
        if (status == PmpProjectChangeStatus.Applied && item.Status != PmpProjectChangeStatus.Approved)
            throw new InvalidOperationException("只有已批准的项目变更才能实施。");
        item.SetStatus(status); repository.Update(item);
    }
    public void ApplyApproval(PmpProjectChange item)
    {
        if (item.Status == PmpProjectChangeStatus.Approved) return;
        if (item.Status != PmpProjectChangeStatus.Proposed) throw new InvalidOperationException($"项目变更不能从“{item.Status}”通过审批。");
        item.SetStatus(PmpProjectChangeStatus.Approved);
        repository.Update(item);
    }
    private void EnsureProject(Guid id) { if (!projectRepository.List().Any(x => x.Id == id)) throw new InvalidOperationException("关联项目不存在。"); }
}
