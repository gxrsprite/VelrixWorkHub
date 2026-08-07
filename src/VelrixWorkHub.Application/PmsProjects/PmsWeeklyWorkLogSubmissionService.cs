using System.Text.Json;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;

namespace VelrixWorkHub.Application.PmsProjects;

public interface IPmsWeeklyWorkLogSubmissionRepository
{
    IReadOnlyList<PmsWeeklyWorkLogSubmission> List(Guid? projectId = null);
    void Add(PmsWeeklyWorkLogSubmission item);
    void Update(PmsWeeklyWorkLogSubmission item);
    void Remove(Guid id);
}
public interface IPmsWeeklyWorkLogSubmissionWorkflowApprover { void ApplyApproval(PmsWeeklyWorkLogSubmission item); void ApplyRejection(PmsWeeklyWorkLogSubmission item, string? reason); void ApplyWithdrawal(PmsWeeklyWorkLogSubmission item); }

public sealed record PmsWeeklyWorkLogSnapshotItem(Guid? WbsTaskId, string? WbsTaskTitle, DateOnly WorkDate, decimal Hours, PmsWorkLogAttendanceStatus AttendanceStatus, string? Note);

public sealed class PmsWeeklyWorkLogSubmissionService(
    IPmsWeeklyWorkLogSubmissionRepository repository,
    PmsWorkLogService workLogs,
    IPmsProjectMemberRepository members,
    IPmsWbsTaskRepository? tasks = null,
    WorkflowBindingService? bindings = null,
    IWorkflowTransactionBoundary? transactions = null) : IPmsWeeklyWorkLogSubmissionWorkflowApprover
{
    public IReadOnlyList<PmsWeeklyWorkLogSubmission> List(Guid? projectId = null) => repository.List(projectId).OrderByDescending(x => x.WeekStart).ThenBy(x => x.MemberName).ToArray();
    public IReadOnlyList<PmsWeeklyWorkLogSubmission> ListForMember(Guid projectId, string memberName)
    {
        var name = memberName?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(name) ? [] : List(projectId).Where(x => x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
    }
    public IReadOnlyList<PmsWeeklyWorkLogSubmission> ListForProjectMember(Guid projectId, Guid userId)
    {
        var member = FindUniqueProjectMember(projectId, userId);
        return member is null ? [] : ListForMember(projectId, member.MemberName);
    }
    public IReadOnlyList<PmsWeeklyWorkLogSnapshotItem> GetSnapshot(PmsWeeklyWorkLogSubmission item)
        => JsonSerializer.Deserialize<List<PmsWeeklyWorkLogSnapshotItem>>(item.SnapshotJson, JsonSerializationDefaults.CreateWeb()) ?? [];

    /// <summary>供非管理员入口按稳定用户 ID 解析项目成员，不能由客户端指定成员名称。</summary>
    public PmsWeeklyWorkLogSubmission SubmitForProjectMember(Guid projectId, Guid userId, DateOnly weekStart, string submittedBy)
    {
        var member = FindUniqueProjectMember(projectId, userId);
        if (member is null) throw new UnauthorizedAccessException("当前用户不是该项目的受控唯一成员，不能提交周工时审批。");
        return Submit(projectId, member.MemberName, weekStart, submittedBy);
    }

    public PmsWeeklyWorkLogSubmission Submit(Guid projectId, string memberName, DateOnly weekStart, string submittedBy)
    {
        if (weekStart.DayOfWeek != DayOfWeek.Monday) throw new ArgumentException("工时周必须从周一开始。", nameof(weekStart));
        if (bindings is null) throw new InvalidOperationException("工时周报审批服务未配置。");
        var name = memberName?.Trim() ?? string.Empty;
        if (!members.List(projectId).Any(x => x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("工时成员必须是当前项目成员。");
        if (repository.List(projectId).Any(x => x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase) && x.WeekStart == weekStart && x.Status is PmsWeeklyWorkLogSubmissionStatus.Submitted or PmsWeeklyWorkLogSubmissionStatus.Approved)) throw new InvalidOperationException("该成员本周工时已提交审批或已批准。");
        var taskTitles = tasks?.List(projectId).ToDictionary(x => x.Id, x => x.Title) ?? [];
        var snapshot = workLogs.List(projectId).Where(x => x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase) && x.WorkDate >= weekStart && x.WorkDate <= weekStart.AddDays(6)).Select(x => new PmsWeeklyWorkLogSnapshotItem(x.WbsTaskId, x.WbsTaskId is Guid taskId && taskTitles.TryGetValue(taskId, out var title) ? title : null, x.WorkDate, x.Hours, x.AttendanceStatus, x.Note)).ToArray();
        var item = new PmsWeeklyWorkLogSubmission(projectId, name, weekStart, JsonSerializer.Serialize(snapshot, JsonSerializationDefaults.CreateWeb()), snapshot.Sum(x => x.Hours));
        void Save()
        {
            item.Submit(submittedBy, DateTime.Now);
            repository.Add(item);
            try
            {
                bindings.StartOrGet(WorkflowBindingCodes.PmsWeeklyWorkLogApproval, nameof(PmsWeeklyWorkLogSubmission), item.Id, startedBy: submittedBy);
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
    public void ApplyApproval(PmsWeeklyWorkLogSubmission item) { if (item.Status == PmsWeeklyWorkLogSubmissionStatus.Approved) return; item.Approve(); repository.Update(item); }
    public void ApplyRejection(PmsWeeklyWorkLogSubmission item, string? reason) { if (item.Status == PmsWeeklyWorkLogSubmissionStatus.Rejected) return; item.Reject(reason); repository.Update(item); }
    public void ApplyWithdrawal(PmsWeeklyWorkLogSubmission item) { if (item.Status == PmsWeeklyWorkLogSubmissionStatus.Withdrawn) return; item.Withdraw(); repository.Update(item); }
    public void Withdraw(PmsWeeklyWorkLogSubmission item, string actor)
    {
        if (item.Status != PmsWeeklyWorkLogSubmissionStatus.Submitted) throw new InvalidOperationException("当前工时周报没有可撤回的审批。");
        if (string.IsNullOrWhiteSpace(actor) || !string.Equals(item.SubmittedBy, actor.Trim(), StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("只有工时周报提交人可以撤回审批。");
        var running = bindings?.List(nameof(PmsWeeklyWorkLogSubmission), item.Id).SingleOrDefault(x => x.DefinitionCode == WorkflowBindingCodes.PmsWeeklyWorkLogApproval && x.Status == WorkflowInstanceStatus.Running)
            ?? throw new InvalidOperationException("未找到运行中的工时周报审批。");
        bindings!.Withdraw(running.Id, actor, "提交人撤回周工时审批");
    }

    private PmsProjectMember? FindUniqueProjectMember(Guid projectId, Guid userId)
    {
        if (userId == Guid.Empty) return null;
        var matches = members.List(projectId).Where(x => x.UserId == userId).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }
}
