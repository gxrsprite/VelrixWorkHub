using System.Text.Json;

namespace VelrixWorkHub.Domain;

public enum PmsWeeklyWorkLogSubmissionStatus { Draft, Submitted, Approved, Rejected, Withdrawn }

/// <summary>成员按周提交的工时审批快照；审批期间不依赖可继续编辑的原始工时记录。</summary>
public sealed class PmsWeeklyWorkLogSubmission
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid ProjectId { get; private set; }
    public string MemberName { get; private set; } = string.Empty;
    public DateOnly WeekStart { get; private set; }
    public string SnapshotJson { get; private set; } = "[]";
    public decimal TotalHours { get; private set; }
    public PmsWeeklyWorkLogSubmissionStatus Status { get; private set; }
    public string? SubmittedBy { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public PmsWeeklyWorkLogSubmission(Guid projectId, string memberName, DateOnly weekStart, string snapshotJson, decimal totalHours)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("必须关联项目。", nameof(projectId));
        if (string.IsNullOrWhiteSpace(memberName)) throw new ArgumentException("成员不能为空。", nameof(memberName));
        if (weekStart.DayOfWeek != DayOfWeek.Monday) throw new ArgumentException("工时周必须从周一开始。", nameof(weekStart));
        if (string.IsNullOrWhiteSpace(snapshotJson) || snapshotJson.Trim() == "[]") throw new ArgumentException("提交工时不能为空。", nameof(snapshotJson));
        try { using var document = JsonDocument.Parse(snapshotJson); if (document.RootElement.ValueKind != JsonValueKind.Array) throw new ArgumentException("工时快照必须是 JSON 数组。", nameof(snapshotJson)); }
        catch (JsonException) { throw new ArgumentException("工时快照必须是有效 JSON。", nameof(snapshotJson)); }
        if (totalHours <= 0) throw new ArgumentOutOfRangeException(nameof(totalHours), "提交工时必须大于零。");
        ProjectId = projectId; MemberName = memberName.Trim(); WeekStart = weekStart; SnapshotJson = snapshotJson; TotalHours = decimal.Round(totalHours, 2); Status = PmsWeeklyWorkLogSubmissionStatus.Draft;
    }

    public static PmsWeeklyWorkLogSubmission Restore(Guid id, Guid projectId, string memberName, DateOnly weekStart, string snapshotJson, decimal totalHours, PmsWeeklyWorkLogSubmissionStatus status, string? submittedBy, DateTime? submittedAt, string? rejectionReason)
        => new(projectId, memberName, weekStart, snapshotJson, totalHours) { Id = id, Status = status, SubmittedBy = submittedBy, SubmittedAt = submittedAt, RejectionReason = rejectionReason };

    public void Submit(string submittedBy, DateTime now)
    {
        if (Status is not PmsWeeklyWorkLogSubmissionStatus.Draft and not PmsWeeklyWorkLogSubmissionStatus.Rejected and not PmsWeeklyWorkLogSubmissionStatus.Withdrawn) throw new InvalidOperationException("当前工时周报不能提交。" );
        if (string.IsNullOrWhiteSpace(submittedBy)) throw new ArgumentException("提交人不能为空。", nameof(submittedBy));
        Status = PmsWeeklyWorkLogSubmissionStatus.Submitted; SubmittedBy = submittedBy.Trim(); SubmittedAt = now; RejectionReason = null;
    }

    public void Approve()
    {
        if (Status == PmsWeeklyWorkLogSubmissionStatus.Approved) return;
        if (Status != PmsWeeklyWorkLogSubmissionStatus.Submitted) throw new InvalidOperationException("只有审批中的工时周报可以批准。");
        Status = PmsWeeklyWorkLogSubmissionStatus.Approved;
    }

    public void Reject(string? reason)
    {
        if (Status == PmsWeeklyWorkLogSubmissionStatus.Rejected) return;
        if (Status != PmsWeeklyWorkLogSubmissionStatus.Submitted) throw new InvalidOperationException("只有审批中的工时周报可以驳回。");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("驳回必须填写原因。", nameof(reason));
        Status = PmsWeeklyWorkLogSubmissionStatus.Rejected; RejectionReason = reason.Trim();
    }

    public void Withdraw()
    {
        if (Status != PmsWeeklyWorkLogSubmissionStatus.Submitted) throw new InvalidOperationException("当前工时周报没有可撤回的审批。");
        Status = PmsWeeklyWorkLogSubmissionStatus.Withdrawn;
    }
}
