using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.PmpProjects;
public sealed class PmpWorkLogService(
    IPmpWorkLogRepository repository,
    IPmpProjectRepository projectRepository,
    IPmpWbsTaskRepository taskRepository,
    IPmpProjectMemberRepository memberRepository)
{
    public IReadOnlyList<PmpWorkLog> List(Guid? projectId = null) => repository.List(projectId).OrderByDescending(x => x.WorkDate).ThenBy(x => x.MemberName).ToArray();
    public decimal TotalHours(Guid? projectId = null) => repository.List(projectId).Sum(x => x.Hours);
    public PmpWorkLog Create(Guid projectId, Guid? taskId, DateOnly date, string memberName, decimal hours, string? note)
    {
        var project = EnsureProject(projectId);
        if (date < project.PlannedStart || date > project.PlannedEnd) throw new InvalidOperationException("工时日期必须落在项目计划周期内。");
        if (!memberRepository.List(projectId).Any(x => x.MemberName.Equals(memberName?.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("工时成员必须是当前项目成员。");
        if (taskId is not null && !taskRepository.List(projectId).Any(x => x.Id == taskId)) throw new InvalidOperationException("WBS 任务不属于当前项目。");
        var item = new PmpWorkLog(projectId, taskId, date, memberName, hours, note); repository.Add(item); return item;
    }
    public PmpWorkLog? SaveCell(Guid projectId, Guid? taskId, DateOnly date, string memberName, decimal hours, PmpWorkLogAttendanceStatus attendanceStatus, string? note)
    {
        var project = EnsureProject(projectId);
        if (date < project.PlannedStart || date > project.PlannedEnd) throw new InvalidOperationException("工时日期必须落在项目计划周期内。");
        if (!memberRepository.List(projectId).Any(x => x.MemberName.Equals(memberName?.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("工时成员必须是当前项目成员。");
        if (taskId is not null && !taskRepository.List(projectId).Any(x => x.Id == taskId)) throw new InvalidOperationException("WBS 任务不属于当前项目。");
        var existing = repository.List(projectId).FirstOrDefault(x => x.WbsTaskId == taskId && x.WorkDate == date && x.MemberName.Equals(memberName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (hours <= 0)
        {
            if (existing is not null) repository.Remove(existing.Id);
            return null;
        }
        if (existing is not null)
        {
            if (hours is < 0.1m or > 24m) throw new ArgumentOutOfRangeException(nameof(hours), "工时必须在 0.1 到 24 小时之间。");
            existing.Edit(hours, note, attendanceStatus);
            repository.Update(existing);
            return existing;
        }
        var item = new PmpWorkLog(projectId, taskId, date, memberName, hours, note, attendanceStatus); repository.Add(item); return item;
    }
    private PmpProject EnsureProject(Guid id) => projectRepository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联项目不存在。");
}
