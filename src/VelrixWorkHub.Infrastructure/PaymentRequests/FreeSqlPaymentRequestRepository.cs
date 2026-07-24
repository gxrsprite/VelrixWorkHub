using FreeSql;
using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

public sealed class FreeSqlPaymentRequestRepository(IFreeSql fsql) : IOaPaymentRequestRepository
{
    public IReadOnlyList<OaPaymentRequest> List(Guid? applicantUserId = null)
    {
        var query = fsql.Select<OaPaymentRequestRecord>();
        if (applicantUserId is Guid id) query = query.Where(x => x.ApplicantUserId == id);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaPaymentRequest? Get(Guid id) => fsql.Select<OaPaymentRequestRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaPaymentRequest item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(OaPaymentRequest item)
    {
        var rows = fsql.Update<OaPaymentRequestRecord>()
            .Set(x => x.DocumentNo, item.DocumentNo).Set(x => x.ApplicantName, item.ApplicantName).Set(x => x.DepartmentName, item.DepartmentName)
            .Set(x => x.LegalEntity, item.LegalEntity).Set(x => x.PayeeName, item.PayeeName).Set(x => x.PayeeAccountReference, item.PayeeAccountReference)
            .Set(x => x.PaymentBankName, item.PaymentBankName).Set(x => x.Currency, item.Currency).Set(x => x.Amount, item.Amount)
            .Set(x => x.PaymentType, item.PaymentType).Set(x => x.RequestDate, item.RequestDate.ToDateTime(TimeOnly.MinValue))
            .Set(x => x.RequestedPaymentDate, item.RequestedPaymentDate.ToDateTime(TimeOnly.MinValue)).Set(x => x.PrecedingDocumentNo, item.PrecedingDocumentNo)
            .Set(x => x.ProjectId, item.ProjectId).Set(x => x.Purpose, item.Purpose).Set(x => x.OtherInfo, item.OtherInfo)
            .Set(x => x.Status, item.Status).Set(x => x.RejectionReason, item.RejectionReason).Set(x => x.SubmittedAt, item.SubmittedAt)
            .Set(x => x.FinanceReviewStatus, item.FinanceReviewStatus).Set(x => x.FinanceReviewReason, item.FinanceReviewReason)
            .Set(x => x.FinanceReviewer, item.FinanceReviewer).Set(x => x.FinanceReviewedAt, item.FinanceReviewedAt).Set(x => x.BudgetReference, item.BudgetReference)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("付款申请不存在或已被删除。");
    }

    private static OaPaymentRequest ToDomain(OaPaymentRequestRecord x)
    {
        var item = new OaPaymentRequest(x.ApplicantUserId, x.ApplicantName, x.DepartmentName, x.LegalEntity, x.DocumentNo, x.PayeeName,
            x.PayeeAccountReference, x.PaymentBankName, x.Currency, x.Amount, x.PaymentType, DateOnly.FromDateTime(x.RequestDate),
            DateOnly.FromDateTime(x.RequestedPaymentDate), x.PrecedingDocumentNo, x.ProjectId, x.Purpose, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        item.SetBudgetReference(x.BudgetReference);
        if (x.Status != OaPaymentRequestStatus.Draft) item.Submit(x.SubmittedAt ?? x.CreatedAt);
        if (x.Status is OaPaymentRequestStatus.Approved or OaPaymentRequestStatus.Paid || x.FinanceReviewStatus != OaPaymentFinanceReviewStatus.Pending)
            item.Approve();
        if (x.FinanceReviewStatus != OaPaymentFinanceReviewStatus.Pending)
            item.ReviewFinance(x.FinanceReviewer ?? "历史财务复核", x.FinanceReviewStatus == OaPaymentFinanceReviewStatus.Approved, x.FinanceReviewReason, x.FinanceReviewedAt);
        if (x.Status == OaPaymentRequestStatus.Rejected)
        {
            if (x.FinanceReviewStatus == OaPaymentFinanceReviewStatus.Pending) item.Reject(x.RejectionReason);
            else item.SetStatus(OaPaymentRequestStatus.Rejected);
        }
        else if (x.Status == OaPaymentRequestStatus.Cancelled) item.Cancel();
        if (x.Status == OaPaymentRequestStatus.Paid)
        {
            if (x.FinanceReviewStatus == OaPaymentFinanceReviewStatus.Pending) item.SetStatus(OaPaymentRequestStatus.Paid);
            else item.MarkPaid();
        }
        return item;
    }

    private static OaPaymentRequestRecord ToRecord(OaPaymentRequest x) => new()
    {
        Id = x.Id, DocumentNo = x.DocumentNo, ApplicantUserId = x.ApplicantUserId, ApplicantName = x.ApplicantName,
        DepartmentName = x.DepartmentName, LegalEntity = x.LegalEntity, PayeeName = x.PayeeName,
        PayeeAccountReference = x.PayeeAccountReference, PaymentBankName = x.PaymentBankName, Currency = x.Currency,
        Amount = x.Amount, PaymentType = x.PaymentType, RequestDate = x.RequestDate.ToDateTime(TimeOnly.MinValue),
        RequestedPaymentDate = x.RequestedPaymentDate.ToDateTime(TimeOnly.MinValue), PrecedingDocumentNo = x.PrecedingDocumentNo,
        ProjectId = x.ProjectId, Purpose = x.Purpose, OtherInfo = x.OtherInfo, Status = x.Status,
        RejectionReason = x.RejectionReason, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt,
        FinanceReviewStatus = x.FinanceReviewStatus, FinanceReviewReason = x.FinanceReviewReason,
        FinanceReviewer = x.FinanceReviewer, FinanceReviewedAt = x.FinanceReviewedAt, BudgetReference = x.BudgetReference
    };
}
