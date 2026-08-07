using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public sealed class PmsWbsTaskService(IPmsWbsTaskRepository repository, IPmsProjectRepository projectRepository)
{
    public IReadOnlyList<PmsWbsTask> List(Guid? projectId = null, string? keyword = null)
    {
        var query = repository.List(projectId).AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(x => x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.AssigneeName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        return query.OrderBy(x => x.ProjectId).ThenBy(x => x.Sequence).ToArray();
    }

    public PmsWbsTask Create(Guid projectId, Guid? parentId, string title, string? assignee, int sequence, DateOnly start, DateOnly end, bool milestone)
    {
        var project = EnsureProject(projectId); EnsureWithinProject(project, start, end); EnsureParent(projectId, parentId, null, start, end);
        var item = new PmsWbsTask(projectId, parentId, title, assignee, sequence, start, end, milestone); EnsureUnique(item); repository.Add(item); return item;
    }

    public void Edit(PmsWbsTask item, Guid? parentId, string title, string? assignee, int sequence, DateOnly start, DateOnly end, bool milestone)
    {
        var project = EnsureProject(item.ProjectId); EnsureWithinProject(project, start, end); EnsureParent(item.ProjectId, parentId, item.Id, start, end); EnsureChildrenWithinRange(item, start, end); EnsureUnique(item.ProjectId, item.Id, parentId, title);
        item.Edit(item.ProjectId, parentId, title, assignee, sequence, start, end, milestone); repository.Update(item);
    }

    public void SetStatus(PmsWbsTask item, PmsWbsTaskStatus status) { item.SetStatus(status); repository.Update(item); }
    public void SetPercentComplete(PmsWbsTask item, int percent) { item.SetPercentComplete(percent); repository.Update(item); }
    public void Remove(PmsWbsTask item)
    {
        foreach (var child in repository.List(item.ProjectId).Where(x => x.ParentId == item.Id).ToArray()) Remove(child);
        repository.Remove(item.Id);
    }

    private PmsProject EnsureProject(Guid projectId) => projectRepository.List().FirstOrDefault(x => x.Id == projectId) ?? throw new InvalidOperationException("关联项目不存在。");

    private void EnsureParent(Guid projectId, Guid? parentId, Guid? currentId, DateOnly start, DateOnly end)
    {
        if (parentId is null) return;
        var all = repository.List(projectId);
        var parent = all.FirstOrDefault(x => x.Id == parentId && x.Id != currentId) ?? throw new InvalidOperationException("父任务不存在或不属于当前项目。");
        if (start < parent.PlannedStart || end > parent.PlannedEnd) throw new InvalidOperationException("子任务计划日期必须落在父任务计划周期内。");
        var cursor = parent;
        var visited = new HashSet<Guid>();
        while (cursor.ParentId is Guid ancestorId && visited.Add(cursor.Id))
        {
            if (ancestorId == currentId) throw new InvalidOperationException("不能将任务移动到自己的子任务下。");
            var ancestor = all.FirstOrDefault(x => x.Id == ancestorId);
            if (ancestor is null) break;
            cursor = ancestor;
        }
    }

    private void EnsureChildrenWithinRange(PmsWbsTask item, DateOnly start, DateOnly end)
    {
        if (repository.List(item.ProjectId).Any(x => x.ParentId == item.Id && (x.PlannedStart < start || x.PlannedEnd > end))) throw new InvalidOperationException("父任务计划周期不能缩短到子任务范围之外。");
    }

    private static void EnsureWithinProject(PmsProject project, DateOnly start, DateOnly end)
    {
        if (start < project.PlannedStart || end > project.PlannedEnd) throw new InvalidOperationException("WBS 任务计划日期必须落在项目计划周期内。");
    }

    private void EnsureUnique(PmsWbsTask item) => EnsureUnique(item.ProjectId, item.Id, item.ParentId, item.Title);
    private void EnsureUnique(Guid projectId, Guid currentId, Guid? parentId, string title)
    {
        if (repository.List(projectId).Any(x => x.Id != currentId && x.ParentId == parentId && x.Title.Equals(title.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同一父任务下任务名称已存在。");
    }
}
