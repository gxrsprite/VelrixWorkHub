using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmpProjects;
public sealed class PmpProjectIssueService(IPmpProjectIssueRepository repository, IPmpProjectRepository projectRepository)
{
    public IReadOnlyList<PmpProjectIssue> List(Guid? projectId = null, string? keyword = null)
    {
        var query = repository.List(projectId).AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.OwnerName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        return query.OrderBy(x => x.Status).ThenByDescending(x => x.Priority).ThenBy(x => x.DueDate).ToArray();
    }
    public PmpProjectIssue Create(Guid projectId, PmpProjectIssueKind kind, string title, string? description, string? ownerName, PmpProjectIssuePriority priority, DateOnly? dueDate)
    { var project = EnsureProject(projectId); EnsureDueDate(project, dueDate); var item = new PmpProjectIssue(projectId, kind, title, description, ownerName, priority, dueDate); EnsureUnique(item); repository.Add(item); return item; }
    public void Edit(PmpProjectIssue item, PmpProjectIssueKind kind, string title, string? description, string? ownerName, PmpProjectIssuePriority priority, DateOnly? dueDate) { var project = EnsureProject(item.ProjectId); EnsureDueDate(project, dueDate); EnsureUnique(item.ProjectId, item.Id, kind, title); item.Edit(item.ProjectId, kind, title, description, ownerName, priority, dueDate); repository.Update(item); }
    public void SetStatus(PmpProjectIssue item, PmpProjectIssueStatus status) { item.SetStatus(status); repository.Update(item); }
    public void Remove(PmpProjectIssue item) => repository.Remove(item.Id);
    private PmpProject EnsureProject(Guid id) => projectRepository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联项目不存在。");
    private static void EnsureDueDate(PmpProject project, DateOnly? dueDate)
    {
        if (dueDate is DateOnly date && (date < project.PlannedStart || date > project.PlannedEnd)) throw new InvalidOperationException("截止日期必须落在项目计划周期内。");
    }
    private void EnsureUnique(PmpProjectIssue item) => EnsureUnique(item.ProjectId, item.Id, item.Kind, item.Title);
    private void EnsureUnique(Guid projectId, Guid currentId, PmpProjectIssueKind kind, string title)
    {
        if (repository.List(projectId).Any(x => x.Id != currentId && x.Kind == kind && x.Title.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同一项目下同类型标题已存在。");
    }
}
