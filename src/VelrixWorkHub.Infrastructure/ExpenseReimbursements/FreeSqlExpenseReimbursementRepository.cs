using FreeSql;
using VelrixWorkHub.Application.ExpenseReimbursements;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.ExpenseReimbursements;

public sealed class FreeSqlExpenseReimbursementRepository(IFreeSql fsql) : IOaExpenseReimbursementRepository
{
    public IReadOnlyList<OaExpenseReimbursement> List(Guid? applicantUserId = null)
    {
        var query = fsql.Select<OaExpenseReimbursementRecord>();
        if (applicantUserId is Guid id) query = query.Where(x => x.ApplicantUserId == id);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaExpenseReimbursement? Get(Guid id) => fsql.Select<OaExpenseReimbursementRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaExpenseReimbursement item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(OaExpenseReimbursement item)
    {
        var rows = fsql.Update<OaExpenseReimbursementRecord>()
            .Set(x => x.DocumentNo, item.DocumentNo).Set(x => x.ApplicantName, item.ApplicantName)
            .Set(x => x.DepartmentName, item.DepartmentName).Set(x => x.LegalEntity, item.LegalEntity)
            .Set(x => x.Title, item.Title).Set(x => x.ReimbursementDate, item.ReimbursementDate.ToDateTime(TimeOnly.MinValue))
            .Set(x => x.ReimbursementType, item.ReimbursementType).Set(x => x.ProjectId, item.ProjectId)
            .Set(x => x.IsEntrusted, item.IsEntrusted).Set(x => x.IsTeamBuilding, item.IsTeamBuilding)
            .Set(x => x.IsEntertainment, item.IsEntertainment).Set(x => x.ActualAmount, item.ActualAmount)
            .Set(x => x.Reason, item.Reason).Set(x => x.OtherInfo, item.OtherInfo).Set(x => x.Status, item.Status)
            .Set(x => x.RejectionReason, item.RejectionReason).Set(x => x.SubmittedAt, item.SubmittedAt)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("报销单不存在或已被删除。");
    }

    private static OaExpenseReimbursement ToDomain(OaExpenseReimbursementRecord x)
    {
        var item = new OaExpenseReimbursement(x.ApplicantUserId, x.ApplicantName, x.DepartmentName, x.LegalEntity, x.DocumentNo,
            x.Title, DateOnly.FromDateTime(x.ReimbursementDate), x.ReimbursementType, x.ProjectId, x.IsEntrusted,
            x.IsTeamBuilding, x.IsEntertainment, x.Reason, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        item.SetActualAmount(x.ActualAmount);
        switch (x.Status)
        {
            case OaExpenseReimbursementStatus.Submitted: item.Submit(x.SubmittedAt ?? x.CreatedAt); break;
            case OaExpenseReimbursementStatus.Rejected: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Reject(x.RejectionReason); break;
            case OaExpenseReimbursementStatus.Approved: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); break;
            case OaExpenseReimbursementStatus.Reimbursed: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); item.MarkReimbursed(); break;
            case OaExpenseReimbursementStatus.Paid: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); item.MarkReimbursed(); item.MarkPaid(); break;
            case OaExpenseReimbursementStatus.Cancelled: item.Cancel(); break;
        }
        return item;
    }

    private static OaExpenseReimbursementRecord ToRecord(OaExpenseReimbursement x) => new()
    {
        Id = x.Id, DocumentNo = x.DocumentNo, ApplicantUserId = x.ApplicantUserId, ApplicantName = x.ApplicantName,
        DepartmentName = x.DepartmentName, LegalEntity = x.LegalEntity, Title = x.Title,
        ReimbursementDate = x.ReimbursementDate.ToDateTime(TimeOnly.MinValue), ReimbursementType = x.ReimbursementType,
        ProjectId = x.ProjectId, IsEntrusted = x.IsEntrusted, IsTeamBuilding = x.IsTeamBuilding, IsEntertainment = x.IsEntertainment,
        ActualAmount = x.ActualAmount, Reason = x.Reason, OtherInfo = x.OtherInfo, Status = x.Status,
        RejectionReason = x.RejectionReason, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt
    };
}

public sealed class FreeSqlExpenseLineRepository(IFreeSql fsql) : IOaExpenseLineRepository
{
    public IReadOnlyList<OaExpenseLine> List(Guid? reimbursementId = null)
    {
        var query = fsql.Select<OaExpenseLineRecord>();
        if (reimbursementId is Guid id) query = query.Where(x => x.ReimbursementId == id);
        return query.OrderBy(x => x.BusinessDate).ToList().Select(ToDomain).ToArray();
    }

    public OaExpenseLine? Get(Guid id) => fsql.Select<OaExpenseLineRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaExpenseLine item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(OaExpenseLine item) { if (fsql.Update<OaExpenseLineRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows() == 0) throw new InvalidOperationException("费用明细不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<OaExpenseLineRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static OaExpenseLine ToDomain(OaExpenseLineRecord x) => new(x.ReimbursementId, x.ExpenseType, x.Description, x.InvoiceNo, x.PaymentFlowNo, DateOnly.FromDateTime(x.BusinessDate), x.Amount, x.ActualAmount, x.ProjectId, x.OtherInfo) { Id = x.Id };
    private static OaExpenseLineRecord ToRecord(OaExpenseLine x) => new()
    {
        Id = x.Id, ReimbursementId = x.ReimbursementId, ExpenseType = x.ExpenseType, Description = x.Description,
        InvoiceNo = x.InvoiceNo, PaymentFlowNo = x.PaymentFlowNo, BusinessDate = x.BusinessDate.ToDateTime(TimeOnly.MinValue),
        Amount = x.Amount, ActualAmount = x.ActualAmount, ProjectId = x.ProjectId, OtherInfo = x.OtherInfo
    };
}
