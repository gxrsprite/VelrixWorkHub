namespace VelrixWorkHub.Domain;

public enum PmsWorkLogAttendanceStatus { Normal, NoAttendance }

public sealed class PmsWorkLog
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public Guid? WbsTaskId { get; private set; }
    public DateOnly WorkDate { get; private set; }
    public string MemberName { get; private set; } = string.Empty;
    public decimal Hours { get; private set; }
    public string? Note { get; private set; }
    public PmsWorkLogAttendanceStatus AttendanceStatus { get; private set; }

    public PmsWorkLog(Guid projectId, Guid? wbsTaskId, DateOnly workDate, string memberName, decimal hours, string? note)
        : this(projectId, wbsTaskId, workDate, memberName, hours, note, PmsWorkLogAttendanceStatus.Normal)
    {
    }

    public PmsWorkLog(Guid projectId, Guid? wbsTaskId, DateOnly workDate, string memberName, decimal hours, string? note, PmsWorkLogAttendanceStatus attendanceStatus)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("项目不能为空。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(memberName)) throw new ArgumentException("成员不能为空。", nameof(memberName));
        if (hours is < 0.1m or > 24m) throw new ArgumentOutOfRangeException(nameof(hours), "工时必须在 0.1 到 24 小时之间。");
        ProjectId = projectId; WbsTaskId = wbsTaskId; WorkDate = workDate; MemberName = memberName.Trim(); Hours = decimal.Round(hours, 2); Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(); AttendanceStatus = attendanceStatus;
    }

    public void Edit(decimal hours, string? note, PmsWorkLogAttendanceStatus attendanceStatus)
    {
        if (hours is < 0.1m or > 24m) throw new ArgumentOutOfRangeException(nameof(hours), "工时必须在 0.1 到 24 小时之间。");
        Hours = decimal.Round(hours, 2);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        AttendanceStatus = attendanceStatus;
    }
}
