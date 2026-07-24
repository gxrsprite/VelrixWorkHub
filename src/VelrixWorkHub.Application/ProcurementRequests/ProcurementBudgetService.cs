using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.ProcurementRequests;

public interface IOaProcurementBudgetRepository
{
    IReadOnlyList<OaProcurementBudget> List();
    OaProcurementBudget? Get(Guid id);
    void Add(OaProcurementBudget item);
    void Update(OaProcurementBudget item);
}

public interface IOaProcurementBudgetReservationRepository
{
    IReadOnlyList<OaProcurementBudgetReservation> List(Guid? budgetId = null);
    OaProcurementBudgetReservation? GetByProcurementRequest(Guid procurementRequestId);
    void Add(OaProcurementBudgetReservation item);
    void Update(OaProcurementBudgetReservation item);
}

public sealed class ProcurementBudgetService(
    IOaProcurementBudgetRepository budgets,
    IOaProcurementBudgetReservationRepository reservations)
{
    public IReadOnlyList<OaProcurementBudget> List()
        => budgets.List().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.BudgetNo).ToArray();

    public IReadOnlyList<OaProcurementBudgetReservation> ListReservations(Guid budgetId)
        => reservations.List(budgetId).OrderByDescending(x => x.CreatedAt).ToArray();

    public OaProcurementBudget Create(string budgetNo, string legalEntity, string departmentName, decimal totalAmount,
        string? otherInfo, bool canManage)
    {
        EnsureManagePermission(canManage);
        if (budgets.List().Any(x => x.BudgetNo.Equals(budgetNo.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("采购预算编号已存在。");
        var item = new OaProcurementBudget(budgetNo, legalEntity, departmentName, totalAmount, otherInfo, DateTime.Now);
        budgets.Add(item);
        return item;
    }

    public void Close(OaProcurementBudget item, bool canManage)
    {
        EnsureManagePermission(canManage);
        item.Close();
        budgets.Update(item);
    }

    public OaProcurementBudgetReservation? ReserveForSubmission(OaProcurementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BudgetReference)) return null;
        if (request.EstimatedAmount <= 0) throw new InvalidOperationException("采购申请预计金额必须大于 0，不能占用预算。");
        var existing = reservations.GetByProcurementRequest(request.Id);
        if (existing?.Status == OaProcurementBudgetReservationStatus.Reserved)
        {
            if (existing.Amount != request.EstimatedAmount) throw new InvalidOperationException("采购申请金额已变化，请先释放原预算占用后再提交。");
            return existing;
        }
        if (existing?.Status == OaProcurementBudgetReservationStatus.Consumed)
            throw new InvalidOperationException("该采购申请预算已执行，不能再次提交。");

        var budget = FindBudget(request.BudgetReference);
        if (budget.Status != OaProcurementBudgetStatus.Active) throw new InvalidOperationException("采购预算已关闭，不能提交采购申请。");
        if (!budget.LegalEntity.Equals(request.LegalEntity, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("采购申请主体公司与预算主体公司不一致。");
        if (!budget.DepartmentName.Equals(request.DepartmentName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("采购申请部门与预算部门不一致。");

        budget.Reserve(request.EstimatedAmount);
        if (existing?.Status == OaProcurementBudgetReservationStatus.Released)
        {
            existing.ReserveAgain(request.EstimatedAmount);
            budgets.Update(budget);
            reservations.Update(existing);
            return existing;
        }
        var reservation = new OaProcurementBudgetReservation(budget.Id, request.Id, request.EstimatedAmount, DateTime.Now);
        budgets.Update(budget);
        reservations.Add(reservation);
        return reservation;
    }

    public void ReleaseForRequest(OaProcurementRequest request)
    {
        var reservation = reservations.GetByProcurementRequest(request.Id);
        if (reservation is null || reservation.Status != OaProcurementBudgetReservationStatus.Reserved) return;
        var budget = budgets.Get(reservation.BudgetId) ?? throw new InvalidOperationException("采购预算不存在，不能释放申请占用。");
        budget.Release(reservation.Amount);
        reservation.Release();
        budgets.Update(budget);
        reservations.Update(reservation);
    }

    public void ValidateForOrder(OaProcurementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BudgetReference)) return;
        if (request.Status != OaProcurementRequestStatus.Approved) throw new InvalidOperationException("只有已批准采购申请才能执行预算。");
        var reservation = reservations.GetByProcurementRequest(request.Id) ?? throw new InvalidOperationException("采购申请尚未占用预算，不能生成采购订单。");
        if (reservation.Status == OaProcurementBudgetReservationStatus.Consumed) return;
        if (reservation.Status != OaProcurementBudgetReservationStatus.Reserved) throw new InvalidOperationException("采购申请预算占用已释放，不能生成采购订单。");
        if (reservation.Amount != request.EstimatedAmount) throw new InvalidOperationException("采购申请金额与预算占用金额不一致，不能生成采购订单。");
        if (budgets.Get(reservation.BudgetId) is null) throw new InvalidOperationException("采购预算不存在，不能生成采购订单。");
    }

    public void PrepareForOrder(OaProcurementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BudgetReference)) return;
        var reservation = reservations.GetByProcurementRequest(request.Id);
        if (reservation?.Status != OaProcurementBudgetReservationStatus.Released)
        {
            ValidateForOrder(request);
            return;
        }
        if (request.Status != OaProcurementRequestStatus.Approved) throw new InvalidOperationException("只有已批准采购申请才能重新执行预算。");
        if (request.EstimatedAmount <= 0) throw new InvalidOperationException("采购申请预计金额必须大于 0，不能重新执行预算。");
        var budget = budgets.Get(reservation.BudgetId) ?? throw new InvalidOperationException("采购预算不存在，不能重新执行采购申请。");
        if (budget.Status != OaProcurementBudgetStatus.Active) throw new InvalidOperationException("采购预算已关闭，不能重新执行采购申请。");
        if (!budget.LegalEntity.Equals(request.LegalEntity, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("采购申请主体公司与预算主体公司不一致。");
        if (!budget.DepartmentName.Equals(request.DepartmentName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("采购申请部门与预算部门不一致。");
        budget.Reserve(request.EstimatedAmount);
        reservation.ReserveAgain(request.EstimatedAmount);
        budgets.Update(budget);
        reservations.Update(reservation);
    }

    public void ReleaseForCancelledOrder(OaProcurementRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BudgetReference)) return;
        var reservation = reservations.GetByProcurementRequest(request.Id);
        if (reservation is null) return;
        var budget = budgets.Get(reservation.BudgetId) ?? throw new InvalidOperationException("采购预算不存在，不能恢复取消订单的预算。");
        if (reservation.Status == OaProcurementBudgetReservationStatus.Reserved)
        {
            budget.Release(reservation.Amount);
            reservation.Release();
        }
        else if (reservation.Status == OaProcurementBudgetReservationStatus.Consumed)
        {
            budget.RestoreConsumed(reservation.Amount);
            reservation.ReleaseConsumed();
        }
        else return;
        budgets.Update(budget);
        reservations.Update(reservation);
    }

    public void ConsumeForOrder(OaProcurementRequest request)
    {
        ValidateForOrder(request);
        if (string.IsNullOrWhiteSpace(request.BudgetReference)) return;
        var reservation = reservations.GetByProcurementRequest(request.Id)!;
        if (reservation.Status == OaProcurementBudgetReservationStatus.Consumed) return;
        var budget = budgets.Get(reservation.BudgetId) ?? throw new InvalidOperationException("采购预算不存在，不能执行采购申请。");
        budget.Consume(reservation.Amount);
        reservation.Consume();
        budgets.Update(budget);
        reservations.Update(reservation);
    }

    private OaProcurementBudget FindBudget(string reference)
        => budgets.List().SingleOrDefault(x => x.BudgetNo.Equals(reference.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("采购申请引用的预算不存在。");

    private static void EnsureManagePermission(bool canManage)
    {
        if (!canManage) throw new UnauthorizedAccessException("当前用户没有维护采购预算的权限。");
    }
}
