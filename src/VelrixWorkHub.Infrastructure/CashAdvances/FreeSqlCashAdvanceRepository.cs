using FreeSql;
using VelrixWorkHub.Application.CashAdvances;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.CashAdvances;

public sealed class FreeSqlCashAdvanceRepository(IFreeSql fsql) : IOaCashAdvanceRepository
{
    public IReadOnlyList<OaCashAdvance> List(Guid? applicantUserId = null)
    {
        var query = fsql.Select<OaCashAdvanceRecord>();
        if (applicantUserId is Guid id) query = query.Where(x => x.ApplicantUserId == id);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaCashAdvance? Get(Guid id) => fsql.Select<OaCashAdvanceRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaCashAdvance item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(OaCashAdvance item)
    {
        var rows = fsql.Update<OaCashAdvanceRecord>()
            .Set(x => x.DocumentNo, item.DocumentNo).Set(x => x.ApplicantName, item.ApplicantName).Set(x => x.DepartmentName, item.DepartmentName)
            .Set(x => x.LegalEntity, item.LegalEntity).Set(x => x.Title, item.Title).Set(x => x.AdvanceType, item.AdvanceType)
            .Set(x => x.RequestDate, item.RequestDate.ToDateTime(TimeOnly.MinValue)).Set(x => x.ExpectedSettlementDate, item.ExpectedSettlementDate.ToDateTime(TimeOnly.MinValue))
            .Set(x => x.ProjectId, item.ProjectId).Set(x => x.Amount, item.Amount).Set(x => x.SettledAmount, item.SettledAmount)
            .Set(x => x.Purpose, item.Purpose).Set(x => x.OtherInfo, item.OtherInfo).Set(x => x.Status, item.Status)
            .Set(x => x.RejectionReason, item.RejectionReason).Set(x => x.SubmittedAt, item.SubmittedAt)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("借款不存在或已被删除。");
    }

    private static OaCashAdvance ToDomain(OaCashAdvanceRecord x)
    {
        var item = new OaCashAdvance(x.ApplicantUserId, x.ApplicantName, x.DepartmentName, x.LegalEntity, x.DocumentNo, x.Title,
            x.AdvanceType, DateOnly.FromDateTime(x.RequestDate), DateOnly.FromDateTime(x.ExpectedSettlementDate), x.ProjectId,
            x.Amount, x.Purpose, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        item.SetSettledAmount(x.SettledAmount);
        switch (x.Status)
        {
            case OaCashAdvanceStatus.Submitted: item.Submit(x.SubmittedAt ?? x.CreatedAt); break;
            case OaCashAdvanceStatus.Rejected: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Reject(x.RejectionReason); break;
            case OaCashAdvanceStatus.Approved: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); break;
            case OaCashAdvanceStatus.PartiallySettled: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); item.SetStatus(OaCashAdvanceStatus.PartiallySettled); break;
            case OaCashAdvanceStatus.Settled: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); item.SetStatus(OaCashAdvanceStatus.Settled); break;
            case OaCashAdvanceStatus.Cancelled: item.Cancel(); break;
        }
        return item;
    }

    private static OaCashAdvanceRecord ToRecord(OaCashAdvance x) => new()
    {
        Id = x.Id, DocumentNo = x.DocumentNo, ApplicantUserId = x.ApplicantUserId, ApplicantName = x.ApplicantName,
        DepartmentName = x.DepartmentName, LegalEntity = x.LegalEntity, Title = x.Title, AdvanceType = x.AdvanceType,
        RequestDate = x.RequestDate.ToDateTime(TimeOnly.MinValue), ExpectedSettlementDate = x.ExpectedSettlementDate.ToDateTime(TimeOnly.MinValue),
        ProjectId = x.ProjectId, Amount = x.Amount, SettledAmount = x.SettledAmount, Purpose = x.Purpose, OtherInfo = x.OtherInfo,
        Status = x.Status, RejectionReason = x.RejectionReason, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt
    };
}

public sealed class FreeSqlCashAdvanceOffsetRepository(IFreeSql fsql) : IOaCashAdvanceOffsetRepository
{
    public IReadOnlyList<OaCashAdvanceOffset> List(Guid? cashAdvanceId = null)
    {
        var query = fsql.Select<OaCashAdvanceOffsetRecord>();
        if (cashAdvanceId is Guid id) query = query.Where(x => x.CashAdvanceId == id);
        return query.OrderByDescending(x => x.OffsetDate).ToList().Select(ToDomain).ToArray();
    }

    public void Add(OaCashAdvanceOffset item) => fsql.Insert(new OaCashAdvanceOffsetRecord
    {
        Id = item.Id, CashAdvanceId = item.CashAdvanceId, ReimbursementId = item.ReimbursementId, Amount = item.Amount,
        OffsetDate = item.OffsetDate.ToDateTime(TimeOnly.MinValue), Notes = item.Notes, OtherInfo = item.OtherInfo
    }).ExecuteAffrows();

    private static OaCashAdvanceOffset ToDomain(OaCashAdvanceOffsetRecord x) => new(x.CashAdvanceId, x.ReimbursementId, x.Amount, DateOnly.FromDateTime(x.OffsetDate), x.Notes, x.OtherInfo) { Id = x.Id };
}
