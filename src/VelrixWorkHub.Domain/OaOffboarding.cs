namespace VelrixWorkHub.Domain;

public enum OaOffboardingStatus
{
    Pending,
    InProgress,
    Completed,
    Cancelled
}

public sealed class OaOffboardingRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid UserId { get; private set; }
    public DateOnly LastWorkDate { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public string? HandoverSummary { get; private set; }
    public bool HandoverCompleted { get; private set; }
    public bool AssetsReturned { get; private set; }
    public bool VehiclesReturned { get; private set; }
    public bool DocumentsReturned { get; private set; }
    public bool AccessRevocationRequested { get; private set; }
    public bool AccountDisabled { get; private set; }
    public DateTime? AccountDisabledAt { get; private set; }
    public string? AccountDisabledBy { get; private set; }
    public string? AccountDisableReason { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public OaOffboardingStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public OaOffboardingRecord(Guid userId, DateOnly lastWorkDate, string reason, string? handoverSummary, string? otherInfo, DateTime createdAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("员工用户不能为空。", nameof(userId));
        UserId = userId;
        CreatedAt = createdAt;
        Edit(lastWorkDate, reason, handoverSummary, otherInfo);
        Status = OaOffboardingStatus.Pending;
    }

    public void Edit(DateOnly lastWorkDate, string reason, string? handoverSummary, string? otherInfo)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("离职原因不能为空。", nameof(reason));
        Reason = reason.Trim();
        LastWorkDate = lastWorkDate;
        HandoverSummary = Clean(handoverSummary);
        OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void UpdateChecklist(bool handoverCompleted, bool assetsReturned, bool vehiclesReturned, bool documentsReturned, bool accessRevocationRequested)
    {
        EnsureNotCompleted();
        HandoverCompleted = handoverCompleted;
        AssetsReturned = assetsReturned;
        VehiclesReturned = vehiclesReturned;
        DocumentsReturned = documentsReturned;
        AccessRevocationRequested = accessRevocationRequested;
        if (Status == OaOffboardingStatus.Pending && (handoverCompleted || assetsReturned || vehiclesReturned || documentsReturned || accessRevocationRequested))
            Status = OaOffboardingStatus.InProgress;
    }

    public void Complete(DateTime completedAt)
    {
        EnsureNotCompleted();
        if (!HandoverCompleted || !AssetsReturned || !VehiclesReturned || !DocumentsReturned || !AccessRevocationRequested)
            throw new InvalidOperationException("离职清单未全部完成。");
        Status = OaOffboardingStatus.Completed;
        CompletedAt = completedAt;
    }

    public void MarkAccountDisabled(string actor, string reason, DateTime disabledAt)
    {
        EnsureNotCompleted();
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作者不能为空。", nameof(actor));
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("停用原因不能为空。", nameof(reason));
        AccountDisabled = true;
        AccountDisabledAt = disabledAt;
        AccountDisabledBy = actor.Trim();
        AccountDisableReason = reason.Trim();
    }

    private void EnsureNotCompleted()
    {
        if (Status == OaOffboardingStatus.Completed) throw new InvalidOperationException("已完成离职的记录不能再修改。");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
