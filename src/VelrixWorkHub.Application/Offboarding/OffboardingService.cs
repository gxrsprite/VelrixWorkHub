using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Offboarding;

public interface IOaOffboardingRepository
{
    IReadOnlyList<OaOffboardingRecord> List();
    OaOffboardingRecord? Get(Guid id);
    OaOffboardingRecord? GetByUser(Guid userId);
    void Add(OaOffboardingRecord record);
    void Update(OaOffboardingRecord record);
}

public sealed class OffboardingService(
    IOaOffboardingRepository repository,
    EmployeeProfileService profiles,
    IEmployeeAccountLifecycleService? accounts = null,
    IWorkflowTransactionBoundary? transactions = null,
    IOaOffboardingRiskProvider? risks = null)
{
    public IReadOnlyList<OaOffboardingRecord> List() => repository.List().OrderByDescending(item => item.LastWorkDate).ThenBy(item => item.CreatedAt).ToArray();

    public OaOffboardingRecord Create(Guid userId, DateOnly lastWorkDate, string reason, string? handoverSummary, string? otherInfo)
    {
        var profile = profiles.Get(userId) ?? throw new InvalidOperationException("员工档案不存在，不能办理离职。");
        if (profile.Status is not (OaEmploymentStatus.Employed or OaEmploymentStatus.Suspended))
            throw new InvalidOperationException("只有在职或停职员工才能办理离职。");
        if (repository.GetByUser(userId) is not null) throw new InvalidOperationException("该员工已存在离职办理记录。");
        var record = new OaOffboardingRecord(userId, lastWorkDate, reason, handoverSummary, otherInfo, DateTime.Now);
        repository.Add(record);
        return record;
    }

    public void Edit(OaOffboardingRecord record, DateOnly lastWorkDate, string reason, string? handoverSummary, string? otherInfo)
    {
        if (record.Status == OaOffboardingStatus.Completed) throw new InvalidOperationException("已完成离职的记录不能再修改。");
        record.Edit(lastWorkDate, reason, handoverSummary, otherInfo);
        repository.Update(record);
    }

    public void UpdateChecklist(OaOffboardingRecord record, bool handoverCompleted, bool assetsReturned, bool vehiclesReturned, bool documentsReturned, bool accessRevocationRequested)
    {
        record.UpdateChecklist(handoverCompleted, assetsReturned, vehiclesReturned, documentsReturned, accessRevocationRequested);
        repository.Update(record);
    }

    public void Complete(OaOffboardingRecord record, string actor, bool canEdit)
    {
        if (!canEdit) throw new UnauthorizedAccessException("当前用户没有完成离职办理的权限。");
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("操作者不能为空。", nameof(actor));
        var profile = profiles.Get(record.UserId) ?? throw new InvalidOperationException("员工档案不存在，不能完成离职。");
        var pendingRisks = risks?.List(record.UserId) ?? [];
        if (pendingRisks.Count > 0)
            throw new InvalidOperationException($"存在未处理离职风险：{string.Join("；", pendingRisks.Select(item => $"{item.Reference} {item.Summary}"))}");
        var completedAt = DateTime.Now;

        void CompleteCore()
        {
            const string reason = "离职办理完成，回收平台登录权限。";
            if (accounts is not null)
            {
                accounts.Disable(record.UserId, actor, reason);
                record.MarkAccountDisabled(actor, reason, completedAt);
            }
            record.Complete(completedAt);
            profiles.Save(profile.UserId, profile.EmployeeNo, profile.Phone, profile.Email, profile.PositionTitle, profile.HireDate, OaEmploymentStatus.Resigned, profile.OtherInfo, actor, canEdit);
            repository.Update(record);
        }

        if (transactions is null) CompleteCore();
        else transactions.Execute(CompleteCore);
    }
}
