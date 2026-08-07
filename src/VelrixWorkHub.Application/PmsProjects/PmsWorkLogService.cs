using VelrixWorkHub.Domain;
using VelrixWorkHub.Application.Workflow;
namespace VelrixWorkHub.Application.PmsProjects;
public sealed record PmsWorkLogCellSave(Guid? TaskId, DateOnly Date, string MemberName, decimal Hours, PmsWorkLogAttendanceStatus AttendanceStatus, string? Note);

public sealed class PmsWorkLogService(
    IPmsWorkLogRepository repository,
    IPmsProjectRepository projectRepository,
    IPmsWbsTaskRepository taskRepository,
    IPmsProjectMemberRepository memberRepository,
    IWorkflowTransactionBoundary? transactions = null)
{
    public IReadOnlyList<PmsWorkLog> List(Guid? projectId = null) => repository.List(projectId).OrderByDescending(x => x.WorkDate).ThenBy(x => x.MemberName).ToArray();
    public IReadOnlyList<PmsWorkLog> ListForProjectMember(Guid projectId, Guid userId)
    {
        var member = FindUniqueProjectMember(projectId, userId);
        return member is null
            ? []
            : repository.List(projectId)
                .Where(x => x.MemberName.Equals(member.MemberName, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.WorkDate)
                .ThenBy(x => x.MemberName)
                .ToArray();
    }
    public decimal TotalHours(Guid? projectId = null) => repository.List(projectId).Sum(x => x.Hours);
    public PmsWorkLog Create(Guid projectId, Guid? taskId, DateOnly date, string memberName, decimal hours, string? note)
    {
        var project = EnsureProject(projectId);
        if (date < project.PlannedStart || date > project.PlannedEnd) throw new InvalidOperationException("工时日期必须落在项目计划周期内。");
        if (!memberRepository.List(projectId).Any(x => x.MemberName.Equals(memberName?.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("工时成员必须是当前项目成员。");
        if (taskId is not null && !taskRepository.List(projectId).Any(x => x.Id == taskId)) throw new InvalidOperationException("WBS 任务不属于当前项目。");
        EnsureHours(hours);
        EnsureMemberDailyHours(projectId, date, memberName, hours);
        var item = new PmsWorkLog(projectId, taskId, date, memberName, hours, note); repository.Add(item); return item;
    }
    public PmsWorkLog? SaveCell(Guid projectId, Guid? taskId, DateOnly date, string memberName, decimal hours, PmsWorkLogAttendanceStatus attendanceStatus, string? note)
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
            EnsureHours(hours);
            EnsureMemberDailyHours(projectId, date, memberName, hours, existing.Id);
            existing.Edit(hours, note, attendanceStatus);
            repository.Update(existing);
            return existing;
        }
        EnsureHours(hours);
        EnsureMemberDailyHours(projectId, date, memberName, hours);
        var item = new PmsWorkLog(projectId, taskId, date, memberName, hours, note, attendanceStatus); repository.Add(item); return item;
    }

    public IReadOnlyList<PmsWorkLog?> SaveCells(Guid projectId, IReadOnlyCollection<PmsWorkLogCellSave> cells)
    {
        if (cells.Count == 0) return [];
        EnsureBatchCanBeSaved(projectId, cells);
        IReadOnlyList<PmsWorkLog?> saved = [];
        void Save() => saved = cells.Select(x => SaveCell(projectId, x.TaskId, x.Date, x.MemberName, x.Hours, x.AttendanceStatus, x.Note)).ToArray();
        if (transactions is null) Save();
        else transactions.Execute(Save);
        return saved;
    }

    public IReadOnlyList<PmsWorkLog?> SaveCellsForMember(Guid projectId, string memberName, IReadOnlyCollection<PmsWorkLogCellSave> cells)
    {
        var name = memberName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || cells.Any(x => !x.MemberName.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new UnauthorizedAccessException("当前用户只能维护自己的项目工时。");
        return SaveCells(projectId, cells);
    }
    public IReadOnlyList<PmsWorkLog?> SaveCellsForProjectMember(Guid projectId, Guid userId, IReadOnlyCollection<PmsWorkLogCellSave> cells)
    {
        var member = FindUniqueProjectMember(projectId, userId);
        if (member is null) throw new UnauthorizedAccessException("当前用户不是该项目的受控唯一成员，不能维护工时。");
        return SaveCellsForMember(projectId, member.MemberName, cells);
    }

    private void EnsureBatchCanBeSaved(Guid projectId, IReadOnlyCollection<PmsWorkLogCellSave> cells)
    {
        var project = EnsureProject(projectId);
        var members = memberRepository.List(projectId);
        var taskIds = taskRepository.List(projectId).Select(x => x.Id).ToHashSet();
        var existingLogs = repository.List(projectId).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totals = existingLogs
            .GroupBy(x => (x.WorkDate, x.MemberName.Trim()), new WorkLogDailyKeyComparer())
            .ToDictionary(x => x.Key, x => x.Sum(item => item.Hours), new WorkLogDailyKeyComparer());

        foreach (var cell in cells)
        {
            var memberName = cell.MemberName?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(memberName) || !members.Any(x => x.MemberName.Equals(memberName, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("工时成员必须是当前项目成员。");
            if (cell.Date < project.PlannedStart || cell.Date > project.PlannedEnd) throw new InvalidOperationException("工时日期必须落在项目计划周期内。");
            if (cell.TaskId is not null && !taskIds.Contains(cell.TaskId.Value)) throw new InvalidOperationException("WBS 任务不属于当前项目。");
            if (cell.Hours > 0) EnsureHours(cell.Hours);

            var cellKey = $"{cell.TaskId:N}|{cell.Date:yyyyMMdd}|{memberName}";
            if (!seen.Add(cellKey)) throw new InvalidOperationException("同一工时单元格不能在一次保存中重复提交。");

            var dailyKey = (cell.Date, memberName);
            var existing = existingLogs.FirstOrDefault(x => x.WbsTaskId == cell.TaskId && x.WorkDate == cell.Date && x.MemberName.Equals(memberName, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) totals[dailyKey] -= existing.Hours;
            if (cell.Hours > 0) totals[dailyKey] = totals.GetValueOrDefault(dailyKey) + cell.Hours;
            if (totals.GetValueOrDefault(dailyKey) > 24m) throw new InvalidOperationException("同一成员同一天的项目工时合计不能超过 24 小时。");
        }
    }

    private static void EnsureHours(decimal hours)
    {
        if (hours is < 0.1m or > 24m) throw new ArgumentOutOfRangeException(nameof(hours), "工时必须在 0.1 到 24 小时之间。");
    }

    private void EnsureMemberDailyHours(Guid projectId, DateOnly date, string memberName, decimal hours, Guid? excludingId = null)
    {
        var currentHours = repository.List(projectId)
            .Where(x => x.Id != excludingId && x.WorkDate == date && x.MemberName.Equals(memberName.Trim(), StringComparison.OrdinalIgnoreCase))
            .Sum(x => x.Hours);
        if (currentHours + hours > 24m) throw new InvalidOperationException("同一成员同一天的项目工时合计不能超过 24 小时。");
    }

    private PmsProjectMember? FindUniqueProjectMember(Guid projectId, Guid userId)
    {
        if (userId == Guid.Empty) return null;
        var matches = memberRepository.List(projectId).Where(x => x.UserId == userId).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private sealed class WorkLogDailyKeyComparer : IEqualityComparer<(DateOnly Date, string MemberName)>
    {
        public bool Equals((DateOnly Date, string MemberName) x, (DateOnly Date, string MemberName) y) => x.Date == y.Date && string.Equals(x.MemberName, y.MemberName, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((DateOnly Date, string MemberName) obj) => HashCode.Combine(obj.Date, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.MemberName));
    }

    private PmsProject EnsureProject(Guid id) => projectRepository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("关联项目不存在。");
}
