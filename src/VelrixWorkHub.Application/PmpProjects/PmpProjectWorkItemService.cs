using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpProjectWorkItemRepository
{
    IReadOnlyList<PmpProjectWorkItem> List(Guid? projectId = null);
    void Add(PmpProjectWorkItem item);
    void Update(PmpProjectWorkItem item);
    void Remove(Guid id);
}

public interface IPmpProjectWorkItemActivityRepository
{
    IReadOnlyList<PmpProjectWorkItemActivity> List(Guid workItemId);
    void Add(PmpProjectWorkItemActivity activity);
}

public interface IPmpProjectWorkItemWorkflowApprover
{
    void ApplyCompletionApproval(PmpProjectWorkItem item);
    void ApplyCompletionRejection(PmpProjectWorkItem item, string? reason);
}

public sealed class PmpProjectWorkItemService(IPmpProjectWorkItemRepository repository, IPmpProjectRepository projects, IPmpProjectWorkItemActivityRepository? activities = null, EmployeeDirectoryService? directory = null, WorkflowBindingService? bindings = null, IWorkflowTransactionBoundary? transactions = null, IPmpProjectMemberRepository? members = null) : IPmpProjectWorkItemWorkflowApprover
{
    public IReadOnlyList<PmpProjectWorkItem> List(Guid? projectId = null, string? keyword = null)
    {
        var text = keyword?.Trim();
        return repository.List(projectId).Where(x => string.IsNullOrWhiteSpace(text) || x.Title.Contains(text, StringComparison.OrdinalIgnoreCase) || (x.OwnerName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)).OrderBy(x => x.Status).ThenByDescending(x => x.Priority).ThenBy(x => x.PlannedEndAt).ToArray();
    }
    public IReadOnlyList<PmpProjectWorkItem> ListVisible(Guid userId, bool isAdministrator, Guid? projectId = null, string? keyword = null)
    {
        if (isAdministrator) return List(projectId, keyword);
        if (userId == Guid.Empty) return [];
        var memberProjectIds = members?.List().Where(x => x.UserId == userId).Select(x => x.ProjectId).ToHashSet() ?? [];
        var person = directory?.List(status: EmployeeDirectoryStatus.All).FirstOrDefault(x => x.UserId == userId);
        var organizationId = person?.OrganizationId;
        var roleIds = person?.Roles?.Select(x => x.Id).ToHashSet() ?? [];
        return List(projectId, keyword).Where(item => item.OwnerUserId == userId || item.ParticipantUserIds.Contains(userId) || memberProjectIds.Contains(item.ProjectId) || (organizationId is Guid id && item.VisibilityOrganizationIds.Contains(id)) || item.VisibilityRoleIds.Any(roleIds.Contains)).ToArray();
    }
    public PmpProjectWorkItem Create(Guid projectId, Guid? parentId, string? sourceType, Guid? sourceId, string title, string? description, string? ownerName, string? participantNames, PmpProjectWorkItemPriority priority, DateTime? plannedStartAt, DateTime? plannedEndAt, string? otherInfo, Guid? ownerUserId = null, DateTime? reminderAt = null, IReadOnlyCollection<Guid>? participantUserIds = null, IReadOnlyCollection<Guid>? visibilityOrganizationIds = null, IReadOnlyCollection<Guid>? visibilityRoleIds = null)
    {
        EnsureProject(projectId); EnsureParent(projectId, parentId, Guid.Empty); ValidateVisibilityOrganizations(visibilityOrganizationIds); ValidateVisibilityRoles(visibilityRoleIds);
        var item = new PmpProjectWorkItem(projectId, parentId, sourceType, sourceId, title, description, ownerName, participantNames, priority, plannedStartAt, plannedEndAt, otherInfo, ownerUserId, reminderAt, participantUserIds, visibilityOrganizationIds, visibilityRoleIds); repository.Add(item); activities?.Add(new PmpProjectWorkItemActivity(item.Id, PmpProjectWorkItemActivityType.Created, "创建工作项", "system", null, item.Status, DateTime.Now)); return item;
    }
    public PmpProjectWorkItem CreateForPeople(Guid projectId, Guid? parentId, string? sourceType, Guid? sourceId, string title, string? description, Guid? ownerId, IReadOnlyCollection<Guid>? participantIds, PmpProjectWorkItemPriority priority, DateTime? plannedStartAt, DateTime? plannedEndAt, string? otherInfo, DateTime? reminderAt = null, IReadOnlyCollection<Guid>? visibilityOrganizationIds = null, IReadOnlyCollection<Guid>? visibilityRoleIds = null)
    {
        var people = ResolvePeople(ownerId, participantIds);
        return Create(projectId, parentId, sourceType, sourceId, title, description, people.OwnerName, people.ParticipantNames, priority, plannedStartAt, plannedEndAt, otherInfo, people.OwnerUserId, reminderAt, people.ParticipantUserIds, visibilityOrganizationIds, visibilityRoleIds);
    }
    public void Edit(PmpProjectWorkItem item, Guid? parentId, string? sourceType, Guid? sourceId, string title, string? description, string? ownerName, string? participantNames, PmpProjectWorkItemPriority priority, DateTime? plannedStartAt, DateTime? plannedEndAt, string? otherInfo, Guid? ownerUserId = null, DateTime? reminderAt = null, IReadOnlyCollection<Guid>? participantUserIds = null, IReadOnlyCollection<Guid>? visibilityOrganizationIds = null, IReadOnlyCollection<Guid>? visibilityRoleIds = null)
    {
        if (item.Status is PmpProjectWorkItemStatus.PendingApproval or PmpProjectWorkItemStatus.Completed or PmpProjectWorkItemStatus.Cancelled) throw new InvalidOperationException("验收审批中、已完成或已取消工作项不能编辑。");
        EnsureProject(item.ProjectId); EnsureParent(item.ProjectId, parentId, item.Id); ValidateVisibilityOrganizations(visibilityOrganizationIds); ValidateVisibilityRoles(visibilityRoleIds);
        item.Edit(item.ProjectId, parentId, sourceType, sourceId, title, description, ownerName, participantNames, priority, plannedStartAt, plannedEndAt, otherInfo, ownerUserId, reminderAt, participantUserIds, visibilityOrganizationIds, visibilityRoleIds); repository.Update(item);
    }
    public void EditForPeople(PmpProjectWorkItem item, Guid? parentId, string? sourceType, Guid? sourceId, string title, string? description, Guid? ownerId, IReadOnlyCollection<Guid>? participantIds, PmpProjectWorkItemPriority priority, DateTime? plannedStartAt, DateTime? plannedEndAt, string? otherInfo, DateTime? reminderAt = null, IReadOnlyCollection<Guid>? visibilityOrganizationIds = null, IReadOnlyCollection<Guid>? visibilityRoleIds = null)
    {
        var people = ResolvePeople(ownerId, participantIds);
        Edit(item, parentId, sourceType, sourceId, title, description, people.OwnerName, people.ParticipantNames, priority, plannedStartAt, plannedEndAt, otherInfo, people.OwnerUserId, reminderAt, people.ParticipantUserIds, visibilityOrganizationIds, visibilityRoleIds);
    }
    public void SetStatus(PmpProjectWorkItem item, PmpProjectWorkItemStatus status, string? feedback, string? actorName = null)
    {
        EnsureStored(item); var previous = item.Status; item.SetStatus(status, feedback, DateTime.Now); repository.Update(item);
        if (previous != item.Status) activities?.Add(new PmpProjectWorkItemActivity(item.Id, PmpProjectWorkItemActivityType.StatusChanged, feedback, string.IsNullOrWhiteSpace(actorName) ? "system" : actorName, previous, item.Status, DateTime.Now));
    }
    public void SubmitCompletionAndStartWorkflow(PmpProjectWorkItem item, Guid actorUserId, string actorName, string feedback)
    {
        EnsureStored(item); EnsureOwner(item, actorUserId);
        if (bindings is null) throw new InvalidOperationException("工作项验收审批服务未配置。");
        var previous = item.Status;
        void Core()
        {
            item.SetStatus(PmpProjectWorkItemStatus.PendingApproval, feedback, DateTime.Now);
            repository.Update(item);
            activities?.Add(new PmpProjectWorkItemActivity(item.Id, PmpProjectWorkItemActivityType.StatusChanged, feedback, actorName, previous, item.Status, DateTime.Now));
            bindings.StartOrGet(WorkflowBindingCodes.PmpWorkItemCompletionApproval, nameof(PmpProjectWorkItem), item.Id, startedBy: actorName);
        }
        if (transactions is null)
        {
            try { Core(); }
            catch { item.RejectCompletion(null); throw; }
        }
        else transactions.Execute(Core, _ => item.RejectCompletion(null));
    }
    public void WithdrawCompletionApproval(PmpProjectWorkItem item, Guid actorUserId, string actorName)
    {
        EnsureStored(item); EnsureOwner(item, actorUserId);
        if (item.Status != PmpProjectWorkItemStatus.PendingApproval) throw new InvalidOperationException("当前工作项没有可撤回的验收审批。");
        var running = bindings?.List(nameof(PmpProjectWorkItem), item.Id).SingleOrDefault(x => x.DefinitionCode == WorkflowBindingCodes.PmpWorkItemCompletionApproval && x.Status == WorkflowInstanceStatus.Running)
            ?? throw new InvalidOperationException("未找到运行中的工作项验收审批。");
        bindings!.Withdraw(running.Id, actorName, "负责人撤回工作项验收");
    }
    public void ApplyCompletionApproval(PmpProjectWorkItem item)
    {
        if (item.Status == PmpProjectWorkItemStatus.Completed) return;
        var previous = item.Status;
        item.ApproveCompletion(DateTime.Now); repository.Update(item);
        activities?.Add(new PmpProjectWorkItemActivity(item.Id, PmpProjectWorkItemActivityType.StatusChanged, "验收审批通过", "workflow", previous, item.Status, DateTime.Now));
    }
    public void ApplyCompletionRejection(PmpProjectWorkItem item, string? reason)
    {
        if (item.Status == PmpProjectWorkItemStatus.InProgress) return;
        var previous = item.Status;
        item.RejectCompletion(reason); repository.Update(item);
        activities?.Add(new PmpProjectWorkItemActivity(item.Id, PmpProjectWorkItemActivityType.StatusChanged, reason, "workflow", previous, item.Status, DateTime.Now));
    }
    public IReadOnlyList<PmpProjectWorkItemActivity> ListActivities(Guid workItemId) => activities?.List(workItemId).OrderByDescending(x => x.OccurredAt).ToArray() ?? [];
    public void AddComment(PmpProjectWorkItem item, string content, string actorName)
    {
        EnsureStored(item); if (activities is null) throw new InvalidOperationException("工作项活动仓储未配置。");
        activities.Add(new PmpProjectWorkItemActivity(item.Id, PmpProjectWorkItemActivityType.Commented, content, actorName, null, null, DateTime.Now));
    }
    public void Remove(PmpProjectWorkItem item) { if (item.Status != PmpProjectWorkItemStatus.Draft) throw new InvalidOperationException("只有草稿工作项可以删除。"); if (repository.List(item.ProjectId).Any(x => x.ParentId == item.Id)) throw new InvalidOperationException("存在子工作项，不能删除。"); repository.Remove(item.Id); }
    private PmpProject EnsureProject(Guid id) => projects.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联项目不存在。");
    private void EnsureStored(PmpProjectWorkItem item) { if (!repository.List(item.ProjectId).Any(x => x.Id == item.Id)) throw new InvalidOperationException("工作项不存在或已被删除。"); }
    private static void EnsureOwner(PmpProjectWorkItem item, Guid actorUserId)
    {
        if (actorUserId == Guid.Empty || item.OwnerUserId != actorUserId) throw new UnauthorizedAccessException("只有工作项负责人可以提交或撤回验收审批。");
    }
    private (Guid? OwnerUserId, string? OwnerName, IReadOnlyCollection<Guid> ParticipantUserIds, string? ParticipantNames) ResolvePeople(Guid? ownerId, IReadOnlyCollection<Guid>? participantIds)
    {
        if (directory is null) throw new InvalidOperationException("人员目录服务未配置。");
        var ids = participantIds?.ToArray() ?? [];
        if (ids.Any(x => x == Guid.Empty) || ids.Distinct().Count() != ids.Length) throw new ArgumentException("参与人引用无效或重复。", nameof(participantIds));
        if (ownerId is Guid owner && ids.Contains(owner)) throw new ArgumentException("负责人无需重复作为参与人。", nameof(participantIds));
        var enabled = directory.List(status: EmployeeDirectoryStatus.Enabled).ToDictionary(x => x.UserId, x => x.DisplayName);
        string? ownerName = null;
        if (ownerId is Guid selectedOwner && !enabled.TryGetValue(selectedOwner, out ownerName)) throw new ArgumentException("负责人不存在或已停用。", nameof(ownerId));
        var participants = new List<string>();
        foreach (var id in ids)
        {
            if (!enabled.TryGetValue(id, out var name)) throw new ArgumentException("参与人不存在或已停用。", nameof(participantIds));
            participants.Add(name);
        }
        return (ownerId, ownerName, ids, participants.Count == 0 ? null : string.Join(", ", participants));
    }
    private void EnsureParent(Guid projectId, Guid? parentId, Guid selfId)
    {
        if (parentId is not Guid id) return;
        if (id == selfId) throw new InvalidOperationException("工作项不能引用自身为父项。");
        if (repository.List(projectId).FirstOrDefault(x => x.Id == id) is null) throw new InvalidOperationException("父工作项不存在或不属于当前项目。");
    }
    private void ValidateVisibilityOrganizations(IReadOnlyCollection<Guid>? organizationIds)
    {
        var selected = organizationIds?.Where(x => x != Guid.Empty).Distinct().ToArray() ?? [];
        if (selected.Length == 0) return;
        if (directory is null) throw new InvalidOperationException("人员目录服务未配置。");
        var existing = directory.ListOrganizations().Select(x => x.Id).ToHashSet();
        if (selected.Any(x => !existing.Contains(x))) throw new ArgumentException("可见部门不存在。", nameof(organizationIds));
    }
    private void ValidateVisibilityRoles(IReadOnlyCollection<Guid>? roleIds)
    {
        var selected = roleIds?.Where(x => x != Guid.Empty).Distinct().ToArray() ?? [];
        if (selected.Length == 0) return;
        if (directory is null) throw new InvalidOperationException("人员目录服务未配置。");
        var existing = directory.ListRoles().Select(x => x.Id).ToHashSet();
        if (selected.Any(x => !existing.Contains(x))) throw new ArgumentException("可见角色不存在。", nameof(roleIds));
    }
}
