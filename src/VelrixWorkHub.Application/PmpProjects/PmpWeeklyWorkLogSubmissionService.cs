using System.Text.Json;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Application.PmpProjects;

public interface IPmpWeeklyWorkLogSubmissionRepository
{
    IReadOnlyList<PmpWeeklyWorkLogSubmission> List(Guid? projectId = null);
    void Add(PmpWeeklyWorkLogSubmission item);
    void Update(PmpWeeklyWorkLogSubmission item);
    void Remove(Guid id);
}
public interface IPmpWeeklyWorkLogSubmissionWorkflowApprover { void ApplyApproval(PmpWeeklyWorkLogSubmission item); void ApplyRejection(PmpWeeklyWorkLogSubmission item, string? reason); void ApplyWithdrawal(PmpWeeklyWorkLogSubmission item); }

public sealed record PmpWeeklyWorkLogSnapshotItem(Guid? WbsTaskId, string? WbsTaskTitle, DateOnly WorkDate, decimal Hours, PmpWorkLogAttendanceStatus AttendanceStatus, string? Note);

public sealed class PmpWeeklyWorkLogSubmissionService(
    IPmpWeeklyWorkLogSubmissionRepository repository,
    PmpWorkLogService workLogs,
    IPmpProjectMemberRepository members,
    IPmpWbsTaskRepository? tasks = null,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null) : IPmpWeeklyWorkLogSubmissionWorkflowApprover
{
    public IReadOnlyList<PmpWeeklyWorkLogSubmission> List(Guid? projectId = null) => repository.List(projectId).OrderByDescending(x => x.WeekStart).ThenBy(x => x.MemberName).ToArray();
    public IReadOnlyList<PmpWeeklyWorkLogSubmission> ListForMember(Guid projectId, string memberName)
    {
        var name = memberName?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(name) ? [] : List(projectId).Where(x => x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    public IReadOnlyList<PmpWeeklyWorkLogSubmission> ListForProjectMember(Guid projectId, Guid userId)
    {
        var member = FindUniqueProjectMember(projectId, userId);
        return member is null ? [] : ListForMember(projectId, member.MemberName);
    }
    public IReadOnlyList<PmpWeeklyWorkLogSnapshotItem> GetSnapshot(PmpWeeklyWorkLogSubmission item)
        => JsonSerializer.Deserialize<List<PmpWeeklyWorkLogSnapshotItem>>(item.SnapshotJson, JsonSerializationDefaults.CreateWeb()) ?? [];

    /// <summary>供非管理员入口按稳定用户 ID 解析项目成员，不能由客户端指定成员名称。</summary>
    public PmpWeeklyWorkLogSubmission SubmitForProjectMember(Guid projectId, Guid userId, DateOnly weekStart, string submittedBy)
    {
        var member = FindUniqueProjectMember(projectId, userId);
        if (member is null) throw new UnauthorizedAccessException("当前用户不是该项目的受控唯一成员，不能提交周工时审批。");
        return Submit(projectId, member.MemberName, weekStart, submittedBy);
    }

    public PmpWeeklyWorkLogSubmission Submit(Guid projectId, string memberName, DateOnly weekStart, string submittedBy)
    {
        if (weekStart.DayOfWeek != DayOfWeek.Monday) throw new ArgumentException("工时周必须从周一开始。", nameof(weekStart));
        if (bindings is null) throw new InvalidOperationException("工时周报审批服务未配置。");
        var name = memberName?.Trim() ?? string.Empty;
        if (!members.List(projectId).Any(x => x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("工时成员必须是当前项目成员。");
        if (repository.List(projectId).Any(x => x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase) && x.WeekStart == weekStart && x.Status is PmpWeeklyWorkLogSubmissionStatus.Submitted or PmpWeeklyWorkLogSubmissionStatus.Approved)) throw new InvalidOperationException("该成员本周工时已提交审批或已批准。");
        var taskTitles = tasks?.List(projectId).ToDictionary(x => x.Id, x => x.Title) ?? [];
        var snapshot = workLogs.List(projectId).Where(x => x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase) && x.WorkDate >= weekStart && x.WorkDate <= weekStart.AddDays(6)).Select(x => new PmpWeeklyWorkLogSnapshotItem(x.WbsTaskId, x.WbsTaskId is Guid taskId && taskTitles.TryGetValue(taskId, out var title) ? title : null, x.WorkDate, x.Hours, x.AttendanceStatus, x.Note)).ToArray();
        var item = new PmpWeeklyWorkLogSubmission(projectId, name, weekStart, JsonSerializer.Serialize(snapshot, JsonSerializationDefaults.CreateWeb()), snapshot.Sum(x => x.Hours));
        void Save()
        {
            item.Submit(submittedBy, DateTime.Now);
            repository.Add(item);
            try
            {
                bindings.StartOrGet(WorkflowBindingCodes.PmpWeeklyWorkLogApproval, nameof(PmpWeeklyWorkLogSubmission), item.Id, startedBy: submittedBy);
            }
            catch
            {
                // 没有事务边界的轻量宿主也不能因流程定义缺失留下孤立的“审批中”周报。
                repository.Remove(item.Id);
                throw;
            }
        }
        if (transactions is null) Save(); else transactions.Execute(Save);
        return item;
    }
    public void ApplyApproval(PmpWeeklyWorkLogSubmission item) { if (item.Status == PmpWeeklyWorkLogSubmissionStatus.Approved) return; item.Approve(); repository.Update(item); }
    public void ApplyRejection(PmpWeeklyWorkLogSubmission item, string? reason) { if (item.Status == PmpWeeklyWorkLogSubmissionStatus.Rejected) return; item.Reject(reason); repository.Update(item); }
    public void ApplyWithdrawal(PmpWeeklyWorkLogSubmission item) { if (item.Status == PmpWeeklyWorkLogSubmissionStatus.Withdrawn) return; item.Withdraw(); repository.Update(item); }
    public void Withdraw(PmpWeeklyWorkLogSubmission item, string actor)
    {
        if (item.Status != PmpWeeklyWorkLogSubmissionStatus.Submitted) throw new InvalidOperationException("当前工时周报没有可撤回的审批。");
        if (string.IsNullOrWhiteSpace(actor) || !string.Equals(item.SubmittedBy, actor.Trim(), StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("只有工时周报提交人可以撤回审批。");
        var running = bindings?.List(nameof(PmpWeeklyWorkLogSubmission), item.Id).SingleOrDefault(x => x.DefinitionCode == WorkflowBindingCodes.PmpWeeklyWorkLogApproval && x.Status == WorkflowInstanceStatus.Running)
            ?? throw new InvalidOperationException("未找到运行中的工时周报审批。");
        bindings!.Withdraw(running.Id, actor, "提交人撤回周工时审批");
    }

    private PmpProjectMember? FindUniqueProjectMember(Guid projectId, Guid userId)
    {
        if (userId == Guid.Empty) return null;
        var matches = members.List(projectId).Where(x => x.UserId == userId).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
