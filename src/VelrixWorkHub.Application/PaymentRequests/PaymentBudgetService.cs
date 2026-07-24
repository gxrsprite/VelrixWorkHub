using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PaymentRequests;

public interface IOaPaymentBudgetRepository
{
    IReadOnlyList<OaPaymentBudget> List();
    OaPaymentBudget? Get(Guid id);
    void Add(OaPaymentBudget item);
    void Update(OaPaymentBudget item);
}

public interface IOaPaymentBudgetReservationRepository
{
    IReadOnlyList<OaPaymentBudgetReservation> List(Guid? budgetId = null);
    OaPaymentBudgetReservation? GetByPaymentRequest(Guid paymentRequestId);
    void Add(OaPaymentBudgetReservation item);
    void Update(OaPaymentBudgetReservation item);
}

public sealed class PaymentBudgetService(
    IOaPaymentBudgetRepository budgets,
    IOaPaymentBudgetReservationRepository reservations)
{
    public IReadOnlyList<OaPaymentBudget> List()
        => budgets.List().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.BudgetNo).ToArray();

    public IReadOnlyList<OaPaymentBudgetReservation> ListReservations(Guid budgetId)
        => reservations.List(budgetId).OrderByDescending(x => x.CreatedAt).ToArray();

    public OaPaymentBudget Create(string budgetNo, string legalEntity, string departmentName, string currency, decimal totalAmount,
        string? otherInfo, bool canManage)
    {
        EnsureManagePermission(canManage);
        if (budgets.List().Any(x => x.BudgetNo.Equals(budgetNo.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("预算编号已存在。");
        var item = new OaPaymentBudget(budgetNo, legalEntity, departmentName, currency, totalAmount, otherInfo, DateTime.Now);
        budgets.Add(item);
        return item;
    }

    public void Close(OaPaymentBudget item, bool canManage)
    {
        EnsureManagePermission(canManage);
        item.Close();
        budgets.Update(item);
    }

    public OaPaymentBudgetReservation? ReserveForSubmission(OaPaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BudgetReference)) return null;
        var existing = reservations.GetByPaymentRequest(request.Id);
        if (existing?.Status == OaPaymentBudgetReservationStatus.Reserved)
        {
            if (existing.Amount != request.Amount) throw new InvalidOperationException("付款申请金额已变化，请先释放原预算占用后再提交。");
            return existing;
        }
        if (existing?.Status == OaPaymentBudgetReservationStatus.Consumed)
            throw new InvalidOperationException("该付款申请预算已执行，不能再次提交。");

        var budget = FindBudget(request.BudgetReference);
        if (budget.Status != OaPaymentBudgetStatus.Active) throw new InvalidOperationException("预算已关闭，不能提交付款申请。");
        if (!budget.Currency.Equals(request.Currency, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("付款币种与预算币种不一致。");
        if (!budget.LegalEntity.Equals(request.LegalEntity, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("付款主体公司与预算主体公司不一致。");
        if (!budget.DepartmentName.Equals(request.DepartmentName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("付款部门与预算部门不一致。");

        budget.Reserve(request.Amount);
        if (existing?.Status == OaPaymentBudgetReservationStatus.Released)
        {
            existing.ReserveAgain(request.Amount);
            budgets.Update(budget);
            reservations.Update(existing);
            return existing;
        }
        var reservation = new OaPaymentBudgetReservation(budget.Id, request.Id, request.Amount, DateTime.Now);
        budgets.Update(budget);
        reservations.Add(reservation);
        return reservation;
    }

    public void ReleaseForRequest(OaPaymentRequest request)
    {
        var reservation = reservations.GetByPaymentRequest(request.Id);
        if (reservation is null || reservation.Status != OaPaymentBudgetReservationStatus.Reserved) return;
        var budget = budgets.Get(reservation.BudgetId) ?? throw new InvalidOperationException("预算不存在，不能释放付款申请占用。");
        budget.Release(reservation.Amount);
        reservation.Release();
        budgets.Update(budget);
        reservations.Update(reservation);
    }

    public void ConsumeForPayment(OaPaymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BudgetReference)) return;
        if (request.Status != OaPaymentRequestStatus.Approved || request.FinanceReviewStatus != OaPaymentFinanceReviewStatus.Approved)
            throw new InvalidOperationException("只有财务复核通过的已批准付款申请才能消耗预算。");
        var reservation = reservations.GetByPaymentRequest(request.Id) ?? throw new InvalidOperationException("付款申请尚未占用预算，不能登记实际付款。");
        if (reservation.Status == OaPaymentBudgetReservationStatus.Consumed) return;
        var budget = budgets.Get(reservation.BudgetId) ?? throw new InvalidOperationException("预算不存在，不能执行付款申请。");
        budget.Consume(reservation.Amount);
        reservation.Consume();
        budgets.Update(budget);
        reservations.Update(reservation);
    }

    private OaPaymentBudget FindBudget(string reference)
        => budgets.List().SingleOrDefault(x => x.BudgetNo.Equals(reference.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("付款申请引用的预算不存在。");

    private static void EnsureManagePermission(bool canManage)
    {
        if (!canManage) throw new UnauthorizedAccessException("当前用户没有维护付款预算的权限。");
    }
}
