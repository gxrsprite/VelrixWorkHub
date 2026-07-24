using FreeSql;
using VelrixWorkHub.Application.Offboarding;
using VelrixWorkHub.Domain;
using OffboardingDomain = VelrixWorkHub.Domain.OaOffboardingRecord;

namespace VelrixWorkHub.Infrastructure.Offboarding;

public sealed class FreeSqlOffboardingRepository(IFreeSql fsql) : IOaOffboardingRepository
{
    public IReadOnlyList<OffboardingDomain> List() => fsql.Select<OaOffboardingRecord>().ToList().Select(ToDomain).ToArray();
    public OffboardingDomain? Get(Guid id) => fsql.Select<OaOffboardingRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public OffboardingDomain? GetByUser(Guid userId) => fsql.Select<OaOffboardingRecord>().Where(item => item.UserId == userId).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OffboardingDomain record) => fsql.Insert(ToRecord(record)).ExecuteAffrows();

    public void Update(OffboardingDomain record)
    {
        var rows = fsql.Update<OaOffboardingRecord>()
            .Set(item => item.LastWorkDate, record.LastWorkDate.ToDateTime(TimeOnly.MinValue))
            .Set(item => item.Reason, record.Reason).Set(item => item.HandoverSummary, record.HandoverSummary)
            .Set(item => item.HandoverCompleted, record.HandoverCompleted).Set(item => item.AssetsReturned, record.AssetsReturned)
            .Set(item => item.VehiclesReturned, record.VehiclesReturned).Set(item => item.DocumentsReturned, record.DocumentsReturned)
            .Set(item => item.AccessRevocationRequested, record.AccessRevocationRequested).Set(item => item.AccountDisabled, record.AccountDisabled)
            .Set(item => item.AccountDisabledAt, record.AccountDisabledAt).Set(item => item.AccountDisabledBy, record.AccountDisabledBy)
            .Set(item => item.AccountDisableReason, record.AccountDisableReason).Set(item => item.OtherInfo, record.OtherInfo)
            .Set(item => item.Status, record.Status).Set(item => item.CompletedAt, record.CompletedAt)
            .Where(item => item.Id == record.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("离职办理记录不存在或已被删除。");
    }

    private static OffboardingDomain ToDomain(OaOffboardingRecord item)
    {
        var domain = new OffboardingDomain(item.UserId, DateOnly.FromDateTime(item.LastWorkDate), item.Reason, item.HandoverSummary, item.OtherInfo, item.CreatedAt) { Id = item.Id };
        domain.UpdateChecklist(item.HandoverCompleted, item.AssetsReturned, item.VehiclesReturned, item.DocumentsReturned, item.AccessRevocationRequested);
        if (item.AccountDisabled)
            domain.MarkAccountDisabled(item.AccountDisabledBy ?? "system", item.AccountDisableReason ?? "离职办理完成，回收平台登录权限。", item.AccountDisabledAt ?? item.CreatedAt);
        if (item.Status == OaOffboardingStatus.Completed) domain.Complete(item.CompletedAt ?? item.CreatedAt);
        return domain;
    }

    private static OaOffboardingRecord ToRecord(OffboardingDomain item) => new()
    {
        Id = item.Id, UserId = item.UserId, LastWorkDate = item.LastWorkDate.ToDateTime(TimeOnly.MinValue), Reason = item.Reason,
        HandoverSummary = item.HandoverSummary, HandoverCompleted = item.HandoverCompleted, AssetsReturned = item.AssetsReturned,
        VehiclesReturned = item.VehiclesReturned, DocumentsReturned = item.DocumentsReturned, AccessRevocationRequested = item.AccessRevocationRequested,
        AccountDisabled = item.AccountDisabled, AccountDisabledAt = item.AccountDisabledAt, AccountDisabledBy = item.AccountDisabledBy,
        AccountDisableReason = item.AccountDisableReason, OtherInfo = item.OtherInfo, Status = item.Status, CreatedAt = item.CreatedAt, CompletedAt = item.CompletedAt
    };
}
