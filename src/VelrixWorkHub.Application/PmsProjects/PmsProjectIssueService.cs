using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmsProjects;
public sealed class PmsProjectIssueService(IPmsProjectIssueRepository repository, IPmsProjectRepository projectRepository)
{
    public IReadOnlyList<PmsProjectIssue> List(Guid? projectId = null, string? keyword = null)
    {
        var query = repository.List(projectId).AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.OwnerName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        return query.OrderBy(x => x.Status).ThenByDescending(x => x.Priority).ThenBy(x => x.DueDate).ToArray();
    }
    public PmsProjectIssue Create(Guid projectId, PmsProjectIssueKind kind, string title, string? description, string? ownerName, PmsProjectIssuePriority priority, DateOnly? dueDate)
    { var project = EnsureProject(projectId); EnsureDueDate(project, dueDate); var item = new PmsProjectIssue(projectId, kind, title, description, ownerName, priority, dueDate); EnsureUnique(item); repository.Add(item); return item; }
    public void Edit(PmsProjectIssue item, PmsProjectIssueKind kind, string title, string? description, string? ownerName, PmsProjectIssuePriority priority, DateOnly? dueDate) { var project = EnsureProject(item.ProjectId); EnsureDueDate(project, dueDate); EnsureUnique(item.ProjectId, item.Id, kind, title); item.Edit(item.ProjectId, kind, title, description, ownerName, priority, dueDate); repository.Update(item); }
    public void SetStatus(PmsProjectIssue item, PmsProjectIssueStatus status) { item.SetStatus(status); repository.Update(item); }
    public void Remove(PmsProjectIssue item) => repository.Remove(item.Id);
    private PmsProject EnsureProject(Guid id) => projectRepository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联项目不存在。");
    private static void EnsureDueDate(PmsProject project, DateOnly? dueDate)
    {
        if (dueDate is DateOnly date && (date < project.PlannedStart || date > project.PlannedEnd)) throw new InvalidOperationException("截止日期必须落在项目计划周期内。");
    }
    private void EnsureUnique(PmsProjectIssue item) => EnsureUnique(item.ProjectId, item.Id, item.Kind, item.Title);
    private void EnsureUnique(Guid projectId, Guid currentId, PmsProjectIssueKind kind, string title)
    {
        if (repository.List(projectId).Any(x => x.Id != currentId && x.Kind == kind && x.Title.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同一项目下同类型标题已存在。");
    }
}
