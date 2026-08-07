using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

public enum PmsResourceLoadLevel { Normal, Attention, Overloaded }

public sealed record PmsResourceAllocationCell(DateOnly Date, int TaskCount, decimal LoggedHours, IReadOnlyList<string> TaskTitles, PmsResourceLoadLevel LoadLevel)
{
    public string LoadLabel => LoadLevel switch { PmsResourceLoadLevel.Attention => "注意", PmsResourceLoadLevel.Overloaded => "超负荷", _ => "正常" };
}

public sealed record PmsResourceAllocationRow(Guid ProjectId, string ProjectCode, string ProjectName, Guid MemberId, string MemberName, string RoleName, string? DepartmentName, IReadOnlyList<PmsResourceAllocationCell> Cells);

public sealed class PmsResourceAllocationService(
    IPmsProjectRepository projectRepository,
    IPmsProjectMemberRepository memberRepository,
    IPmsWbsTaskRepository taskRepository,
    IPmsWorkLogRepository workLogRepository)
{
    public IReadOnlyList<PmsResourceAllocationRow> List(DateOnly start, DateOnly end, Guid? projectId = null, PmsProjectStatus? projectStatus = null, PmsWbsTaskStatus? taskStatus = null, string? keyword = null, int attentionTaskThreshold = 2, int overloadedTaskThreshold = 3, decimal attentionHoursThreshold = 6m, decimal overloadedHoursThreshold = 8m)
    {
        if (end < start) throw new ArgumentException("资源视图结束日期不能早于开始日期。", nameof(end));
        if (end.DayNumber - start.DayNumber > 31) throw new ArgumentException("资源视图一次最多查看 32 天。", nameof(end));
        if (attentionTaskThreshold < 1 || overloadedTaskThreshold < attentionTaskThreshold) throw new ArgumentOutOfRangeException(nameof(attentionTaskThreshold), "任务负荷阈值必须为正数且超负荷阈值不低于注意阈值。");
        if (attentionHoursThreshold <= 0 || overloadedHoursThreshold < attentionHoursThreshold) throw new ArgumentOutOfRangeException(nameof(attentionHoursThreshold), "工时负荷阈值必须为正数且超负荷阈值不低于注意阈值。");

        var text = keyword?.Trim();
        var projects = projectRepository.List().Where(x => projectId is null || x.Id == projectId).Where(x => projectStatus is null || x.Status == projectStatus).ToArray();
        var rows = new List<PmsResourceAllocationRow>();
        foreach (var project in projects)
        {
            var tasks = taskRepository.List(project.Id).Where(x => taskStatus is null || x.Status == taskStatus).ToArray();
            var logs = workLogRepository.List(project.Id).Where(x => x.WorkDate >= start && x.WorkDate <= end).ToArray();
            foreach (var member in memberRepository.List(project.Id))
            {
                if (!string.IsNullOrWhiteSpace(text) && !member.MemberName.Contains(text, StringComparison.OrdinalIgnoreCase) && !member.RoleName.Contains(text, StringComparison.OrdinalIgnoreCase) && !(member.DepartmentName?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false) && !project.Code.Contains(text, StringComparison.OrdinalIgnoreCase) && !project.Name.Contains(text, StringComparison.OrdinalIgnoreCase)) continue;
                var cells = Enumerable.Range(0, end.DayNumber - start.DayNumber + 1).Select(offset =>
                {
                    var date = start.AddDays(offset);
                    var dayTasks = tasks.Where(x => x.AssigneeName?.Equals(member.MemberName, StringComparison.OrdinalIgnoreCase) == true && x.PlannedStart <= date && x.PlannedEnd >= date).ToArray();
                    var hours = logs.Where(x => x.WorkDate == date && x.MemberName.Equals(member.MemberName, StringComparison.OrdinalIgnoreCase)).Sum(x => x.Hours);
                    var loadLevel = hours >= overloadedHoursThreshold || dayTasks.Length >= overloadedTaskThreshold ? PmsResourceLoadLevel.Overloaded : hours >= attentionHoursThreshold || dayTasks.Length >= attentionTaskThreshold ? PmsResourceLoadLevel.Attention : PmsResourceLoadLevel.Normal;
                    return new PmsResourceAllocationCell(date, dayTasks.Length, hours, dayTasks.Select(x => x.Title).ToArray(), loadLevel);
                }).ToArray();
                rows.Add(new PmsResourceAllocationRow(project.Id, project.Code, project.Name, member.Id, member.MemberName, member.RoleName, member.DepartmentName, cells));
            }
        }
        return rows.OrderBy(x => x.DepartmentName).ThenBy(x => x.MemberName).ThenBy(x => x.ProjectCode).ToArray();
    }
}
