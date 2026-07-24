using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.ExpenseReimbursements;

/// <summary>把已批准报销编排为员工付款申请，并保持报销与付款申请的一对一关系。</summary>
public sealed class ExpenseReimbursementPaymentService(
    ExpenseReimbursementService reimbursements,
    PaymentRequestService paymentRequests,
    IWorkflowTransactionBoundary? transactions = null)
{
    public OaPaymentRequest? GetPaymentRequest(OaExpenseReimbursement reimbursement)
        => FindActivePaymentRequest(reimbursement);

    public OaPaymentRequest CreateForApprovedReimbursement(
        Guid reimbursementId,
        string paymentDocumentNo,
        string accountReference,
        string bankName,
        string currency,
        DateOnly requestedPaymentDate,
        string? otherInfo,
        bool canCreate)
    {
        if (!canCreate) throw new UnauthorizedAccessException("当前用户没有为报销创建付款申请的权限。");
        var reimbursement = reimbursements.Get(reimbursementId) ?? throw new InvalidOperationException("报销单不存在或已被删除。");
        var existing = FindActivePaymentRequest(reimbursement);
        if (existing is not null)
        {
            EnsureMatches(reimbursement, existing);
            if (reimbursement.Status == OaExpenseReimbursementStatus.Approved)
            {
                reimbursements.MarkReimbursedForPayment(reimbursement);
                if (existing.Status == OaPaymentRequestStatus.Paid) reimbursements.MarkPaidForPayment(reimbursement);
            }
            return existing;
        }

        if (reimbursement.Status != OaExpenseReimbursementStatus.Approved)
            throw new InvalidOperationException("只有已批准且尚未生成付款申请的报销单才能创建员工付款申请。");
        if (string.IsNullOrWhiteSpace(paymentDocumentNo)) throw new ArgumentException("付款申请单号不能为空。", nameof(paymentDocumentNo));
        if (string.IsNullOrWhiteSpace(accountReference)) throw new ArgumentException("收款账户引用不能为空。", nameof(accountReference));
        if (string.IsNullOrWhiteSpace(bankName)) throw new ArgumentException("收款银行不能为空。", nameof(bankName));
        if (requestedPaymentDate == default) throw new ArgumentException("期望付款日期不能为空。", nameof(requestedPaymentDate));

        var previousStatus = reimbursement.Status;
        OaPaymentRequest? created = null;
        void Core()
        {
            created = paymentRequests.Create(
                reimbursement.ApplicantUserId,
                reimbursement.ApplicantName,
                reimbursement.DepartmentName,
                reimbursement.LegalEntity,
                paymentDocumentNo,
                reimbursement.ApplicantName,
                accountReference,
                bankName,
                currency,
                reimbursement.ActualAmount,
                OaPaymentRequestType.EmployeePayment,
                DateOnly.FromDateTime(DateTime.Today),
                requestedPaymentDate,
                reimbursement.DocumentNo,
                reimbursement.ProjectId,
                $"报销付款：{reimbursement.Title}",
                otherInfo);
            reimbursements.MarkReimbursedForPayment(reimbursement);
        }

        if (transactions is null) Core();
        else transactions.Execute(Core, _ => reimbursement.SetStatusForRecovery(previousStatus));
        return created!;
    }

    private OaPaymentRequest? FindActivePaymentRequest(OaExpenseReimbursement reimbursement)
    {
        var matches = paymentRequests.List()
            .Where(x => x.PaymentType == OaPaymentRequestType.EmployeePayment
                && x.Status != OaPaymentRequestStatus.Cancelled
                && string.Equals(x.PrecedingDocumentNo, reimbursement.DocumentNo, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length > 1) throw new InvalidOperationException("同一报销单存在多个有效员工付款申请，请先修复数据。");
        return matches.SingleOrDefault();
    }

    private static void EnsureMatches(OaExpenseReimbursement reimbursement, OaPaymentRequest payment)
    {
        if (payment.ApplicantUserId != reimbursement.ApplicantUserId)
            throw new InvalidOperationException("付款申请申请人与报销单申请人不一致。");
        if (payment.Amount != reimbursement.ActualAmount)
            throw new InvalidOperationException("付款申请金额与报销实报金额不一致。");
    }
}
