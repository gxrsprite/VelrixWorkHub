using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Application.ExpenseReimbursements;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PaymentRequests;

public interface IOaPaymentExecutionRepository
{
    IReadOnlyList<OaPaymentExecution> List();
    OaPaymentExecution? Get(Guid id);
    OaPaymentExecution? GetByPaymentRequest(Guid paymentRequestId);
    void Add(OaPaymentExecution item);
}

/// <summary>
/// 付款申请的实际付款边界。供应商付款若前置单据是采购订单，必须同时生成 ERP 应付核销；
/// 员工/其他付款只保留外部流水引用，不伪造 ERP 订单。
/// </summary>
public sealed class PaymentExecutionService(
    IOaPaymentExecutionRepository repository,
    PaymentRequestService paymentRequests,
    IPurchaseOrderRepository purchaseOrders,
    ISupplierRepository suppliers,
    SettlementService settlements,
    IWorkflowTransactionBoundary? transactions = null,
    ExpenseReimbursementService? reimbursements = null)
{
    public IReadOnlyList<OaPaymentExecution> List() => repository.List().OrderByDescending(x => x.PaidOn).ThenByDescending(x => x.CreatedAt).ToArray();

    public OaPaymentExecution? GetByPaymentRequest(Guid paymentRequestId) => repository.GetByPaymentRequest(paymentRequestId);

    public IReadOnlyList<OaPaymentRequest> ListPending(IEnumerable<OaPaymentRequest> requests, bool canRegister)
    {
        if (!canRegister) return [];
        var executed = repository.List().Select(x => x.PaymentRequestId).ToHashSet();
        return requests.Where(x => x.Status == OaPaymentRequestStatus.Approved
                && x.FinanceReviewStatus == OaPaymentFinanceReviewStatus.Approved
                && !executed.Contains(x.Id))
            .OrderBy(x => x.RequestedPaymentDate).ThenBy(x => x.CreatedAt).ToArray();
    }

    public OaPaymentExecution Register(OaPaymentRequest request, string executionNo, DateOnly paidOn, OaPaymentExecutionChannel channel,
        string externalReference, string? notes, string @operator, bool canRegister)
    {
        if (!canRegister) throw new UnauthorizedAccessException("当前用户没有登记实际付款的权限。");
        var existing = repository.GetByPaymentRequest(request.Id);
        if (existing is not null)
        {
            if (request.Status != OaPaymentRequestStatus.Paid) throw new InvalidOperationException("付款申请已有实际付款记录但状态不一致，请先修复数据。");
            return existing;
        }
        if (request.Status != OaPaymentRequestStatus.Approved) throw new InvalidOperationException("只有已批准的付款申请才能登记实际付款。");
        if (request.FinanceReviewStatus != OaPaymentFinanceReviewStatus.Approved) throw new InvalidOperationException("只有财务复核通过的付款申请才能登记实际付款。");
        if (string.IsNullOrWhiteSpace(@operator)) throw new ArgumentException("付款登记人不能为空。", nameof(@operator));
        if (string.IsNullOrWhiteSpace(executionNo)) throw new ArgumentException("实际付款流水号不能为空。", nameof(executionNo));
        if (repository.List().Any(x => x.ExecutionNo.Equals(executionNo.Trim(), StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("实际付款流水号已存在。");

        var purchaseOrder = ResolveSupplierOrder(request);
        var reimbursement = ResolveReimbursement(request);
        OaPaymentExecution? created = null;
        var previousStatus = request.Status;
        var previousReimbursementStatus = reimbursement?.Status;
        void Core()
        {
            var erpSettlementId = purchaseOrder is null
                ? (Guid?)null
                : settlements.Create(ErpSettlementKind.Payable, purchaseOrder.Id, request.Amount, executionNo.Trim(), paidOn, $"OA 付款申请 {request.DocumentNo}").Id;
            created = new OaPaymentExecution(request.Id, executionNo, paidOn, request.Amount, request.Currency, channel,
                externalReference, notes, erpSettlementId, @operator, DateTime.Now);
            repository.Add(created);
            paymentRequests.MarkPaid(request, @operator);
            if (reimbursement is not null) reimbursements!.MarkPaidForPayment(reimbursement);
        }
        if (transactions is null) Core();
        else transactions.Execute(Core, _ =>
        {
            request.SetStatus(previousStatus);
            if (reimbursement is not null && previousReimbursementStatus is OaExpenseReimbursementStatus status)
                reimbursement.SetStatusForRecovery(status);
        });
        return created!;
    }

    private PurchaseOrder? ResolveSupplierOrder(OaPaymentRequest request)
    {
        if (request.PaymentType != OaPaymentRequestType.SupplierPayment) return null;
        if (string.IsNullOrWhiteSpace(request.PrecedingDocumentNo)) throw new InvalidOperationException("供应商付款必须关联采购订单号，才能登记 ERP 应付核销。");
        var order = purchaseOrders.List().SingleOrDefault(x => x.OrderNo.Equals(request.PrecedingDocumentNo, StringComparison.OrdinalIgnoreCase));
        if (order is null) throw new InvalidOperationException("前置采购订单不存在，不能登记 ERP 应付核销。");
        if (order.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled) throw new InvalidOperationException("只有已提交、已收货或已关闭的采购订单才能登记应付核销。");
        var supplier = suppliers.List().FirstOrDefault(x => x.Id == order.SupplierId);
        if (supplier is null || !supplier.Name.Equals(request.PayeeName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("付款收款方与前置采购订单供应商不一致。");
        return order;
    }

    private OaExpenseReimbursement? ResolveReimbursement(OaPaymentRequest request)
    {
        if (reimbursements is null || request.PaymentType != OaPaymentRequestType.EmployeePayment || string.IsNullOrWhiteSpace(request.PrecedingDocumentNo)) return null;
        var reimbursement = reimbursements.GetByDocumentNo(request.PrecedingDocumentNo);
        if (reimbursement is null) return null;
        if (reimbursement.ApplicantUserId != request.ApplicantUserId) throw new InvalidOperationException("付款申请申请人与关联报销单申请人不一致。");
        if (reimbursement.ActualAmount != request.Amount) throw new InvalidOperationException("付款申请金额与关联报销实报金额不一致。");
        if (reimbursement.Status is not (OaExpenseReimbursementStatus.Reimbursed or OaExpenseReimbursementStatus.Paid))
            throw new InvalidOperationException("关联报销单必须先完成报销登记，才能登记实际付款。");
        return reimbursement;
    }
}
