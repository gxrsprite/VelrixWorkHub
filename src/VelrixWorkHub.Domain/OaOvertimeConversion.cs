namespace VelrixWorkHub.Domain;

public enum OaOvertimeConversionType
{
    CompensatoryLeave,
    FinanceManual
}

public enum OaOvertimeFinanceProcessingStatus
{
    Pending,
    Processed
}

/// <summary>已批准加班的唯一兑换记录；财务兑换只登记小时，金额由财务体系处理。</summary>
public sealed class OaOvertimeConversion
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid OvertimeRequestId { get; init; }
    public Guid UserId { get; init; }
    public OaOvertimeConversionType Type { get; init; }
    public decimal Hours { get; init; }
    public DateTime CreatedAt { get; init; }
    public OaOvertimeFinanceProcessingStatus FinanceProcessingStatus { get; private set; } = OaOvertimeFinanceProcessingStatus.Pending;
    public string? FinanceProcessedBy { get; private set; }
    public DateTime? FinanceProcessedAt { get; private set; }
    public string? FinanceProcessingNote { get; private set; }

    public OaOvertimeConversion(Guid overtimeRequestId, Guid userId, OaOvertimeConversionType type, decimal hours, DateTime createdAt)
    {
        if (overtimeRequestId == Guid.Empty) throw new ArgumentException("加班申请不能为空。", nameof(overtimeRequestId));
        if (userId == Guid.Empty) throw new ArgumentException("员工不能为空。", nameof(userId));
        if (hours <= 0) throw new ArgumentOutOfRangeException(nameof(hours), "兑换加班时长必须大于 0。");
        OvertimeRequestId = overtimeRequestId;
        UserId = userId;
        Type = type;
        Hours = decimal.Round(hours, 2);
        CreatedAt = createdAt;
    }

    public void MarkFinanceProcessed(string processedBy, string? note, DateTime? processedAt = null)
    {
        if (Type != OaOvertimeConversionType.FinanceManual) throw new InvalidOperationException("只有登记财务处理的加班记录可以完成处理。");
        if (FinanceProcessingStatus == OaOvertimeFinanceProcessingStatus.Processed) throw new InvalidOperationException("该加班费财务处理记录已完成。");
        if (string.IsNullOrWhiteSpace(processedBy)) throw new ArgumentException("处理人不能为空。", nameof(processedBy));
        FinanceProcessingStatus = OaOvertimeFinanceProcessingStatus.Processed;
        FinanceProcessedBy = processedBy.Trim();
        FinanceProcessedAt = processedAt ?? DateTime.Now;
        FinanceProcessingNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public void RestoreFinanceProcessingForRecovery(OaOvertimeFinanceProcessingStatus status, string? processedBy, DateTime? processedAt, string? note)
    {
        FinanceProcessingStatus = status;
        FinanceProcessedBy = processedBy;
        FinanceProcessedAt = processedAt;
        FinanceProcessingNote = note;
    }
}
