using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmpProjects;

public sealed class PmpProjectPhaseService(IPmpProjectPhaseRepository repository, IPmpProjectRepository projectRepository)
{
    public IReadOnlyList<PmpProjectPhase> List(Guid? projectId = null, string? keyword = null)
    {
        var query = repository.List(projectId).AsEnumerable();
        var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Name.Contains(text, StringComparison.OrdinalIgnoreCase));
        return query.OrderBy(x => x.ProjectId).ThenBy(x => x.Sequence).ToArray();
    }

    public PmpProjectPhase Create(Guid projectId, string name, PmpProjectPhaseKind kind, int sequence, DateOnly start, DateOnly end)
    {
        var project = EnsureProject(projectId);
        EnsureWithinProject(project, start, end);
        var item = new PmpProjectPhase(projectId, name, kind, sequence, start, end);
        EnsureUnique(item);
        repository.Add(item);
        return item;
    }

    public void Edit(PmpProjectPhase item, string name, PmpProjectPhaseKind kind, int sequence, DateOnly start, DateOnly end)
    {
        var project = EnsureProject(item.ProjectId);
        EnsureWithinProject(project, start, end);
        EnsureUnique(item.ProjectId, item.Id, name);
        item.Edit(item.ProjectId, name, kind, sequence, start, end);
        repository.Update(item);
    }

    public void SetStatus(PmpProjectPhase item, PmpProjectPhaseStatus status) { item.SetStatus(status); repository.Update(item); }
    public void SetPercentComplete(PmpProjectPhase item, int percent) { item.SetPercentComplete(percent); repository.Update(item); }
    public void Remove(PmpProjectPhase item) => repository.Remove(item.Id);

    private PmpProject EnsureProject(Guid projectId)
    {
        return projectRepository.List().FirstOrDefault(x => x.Id == projectId) ?? throw new InvalidOperationException("关联项目不存在。");
    }

    private void EnsureUnique(PmpProjectPhase item)
        => EnsureUnique(item.ProjectId, item.Id, item.Name);

    private void EnsureUnique(Guid projectId, Guid currentId, string name)
    {
        if (repository.List(projectId).Any(x => x.Id != currentId && x.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同一项目下阶段或里程碑名称已存在。");
    }

    private static void EnsureWithinProject(PmpProject project, DateOnly start, DateOnly end)
    {
        if (start < project.PlannedStart || end > project.PlannedEnd) throw new InvalidOperationException("阶段或里程碑计划日期必须落在项目计划周期内。");
    }
}
