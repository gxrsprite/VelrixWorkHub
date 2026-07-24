using FreeSql;
using VelrixWorkHub.Application.CashAdvances;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.CashAdvances;

public sealed class FreeSqlCashAdvanceRepaymentRepository(IFreeSql fsql) : IOaCashAdvanceRepaymentRepository
{
    public IReadOnlyList<OaCashAdvanceRepayment> List(Guid? applicantUserId = null, Guid? cashAdvanceId = null)
    {
        var query = fsql.Select<OaCashAdvanceRepaymentRecord>();
        if (applicantUserId is Guid applicantId) query = query.Where(x => x.ApplicantUserId == applicantId);
        if (cashAdvanceId is Guid advanceId) query = query.Where(x => x.CashAdvanceId == advanceId);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaCashAdvanceRepayment? Get(Guid id)
        => fsql.Select<OaCashAdvanceRepaymentRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();

    public void Add(OaCashAdvanceRepayment item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(OaCashAdvanceRepayment item)
    {
        var rows = fsql.Update<OaCashAdvanceRepaymentRecord>()
            .Set(x => x.ApplicantName, item.ApplicantName).Set(x => x.DepartmentName, item.DepartmentName)
            .Set(x => x.LegalEntity, item.LegalEntity).Set(x => x.DocumentNo, item.DocumentNo).Set(x => x.Title, item.Title)
            .Set(x => x.Amount, item.Amount).Set(x => x.RepaymentDate, item.RepaymentDate.ToDateTime(TimeOnly.MinValue))
            .Set(x => x.RepaymentMethod, item.RepaymentMethod).Set(x => x.ReceiptReference, item.ReceiptReference)
            .Set(x => x.Notes, item.Notes).Set(x => x.OtherInfo, item.OtherInfo).Set(x => x.Status, item.Status)
            .Set(x => x.RejectionReason, item.RejectionReason).Set(x => x.SubmittedAt, item.SubmittedAt)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("还款不存在或已被删除。 ");
    }

    private static OaCashAdvanceRepayment ToDomain(OaCashAdvanceRepaymentRecord x)
    {
        var item = new OaCashAdvanceRepayment(x.CashAdvanceId, x.ApplicantUserId, x.ApplicantName, x.DepartmentName,
            x.LegalEntity, x.DocumentNo, x.Title, x.Amount, DateOnly.FromDateTime(x.RepaymentDate), x.RepaymentMethod,
            x.ReceiptReference, x.Notes, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        switch (x.Status)
        {
            case OaCashAdvanceRepaymentStatus.Submitted: item.Submit(x.SubmittedAt ?? x.CreatedAt); break;
            case OaCashAdvanceRepaymentStatus.Rejected: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Reject(x.RejectionReason); break;
            case OaCashAdvanceRepaymentStatus.Approved: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); break;
            case OaCashAdvanceRepaymentStatus.Cancelled: item.Cancel(); break;
        }
        return item;
    }

    private static OaCashAdvanceRepaymentRecord ToRecord(OaCashAdvanceRepayment x) => new()
    {
        Id = x.Id, CashAdvanceId = x.CashAdvanceId, ApplicantUserId = x.ApplicantUserId, ApplicantName = x.ApplicantName,
        DepartmentName = x.DepartmentName, LegalEntity = x.LegalEntity, DocumentNo = x.DocumentNo, Title = x.Title,
        Amount = x.Amount, RepaymentDate = x.RepaymentDate.ToDateTime(TimeOnly.MinValue), RepaymentMethod = x.RepaymentMethod,
        ReceiptReference = x.ReceiptReference, Notes = x.Notes, OtherInfo = x.OtherInfo, Status = x.Status,
        RejectionReason = x.RejectionReason, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt
    };
}
