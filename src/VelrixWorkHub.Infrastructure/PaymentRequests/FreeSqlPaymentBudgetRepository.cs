using FreeSql;
using VelrixWorkHub.Application.PaymentRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PaymentRequests;

public sealed class FreeSqlPaymentBudgetRepository(IFreeSql fsql) : IOaPaymentBudgetRepository
{
    public IReadOnlyList<OaPaymentBudget> List() => fsql.Select<OaPaymentBudgetRecord>().ToList().Select(ToDomain).ToArray();
    public OaPaymentBudget? Get(Guid id) => fsql.Select<OaPaymentBudgetRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaPaymentBudget item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(OaPaymentBudget item)
    {
        if (fsql.Update<OaPaymentBudgetRecord>().Set(x => x.ReservedAmount, item.ReservedAmount).Set(x => x.ConsumedAmount, item.ConsumedAmount)
            .Set(x => x.Status, item.Status).Set(x => x.OtherInfo, item.OtherInfo).Where(x => x.Id == item.Id).ExecuteAffrows() == 0)
            throw new InvalidOperationException("付款预算不存在或已被删除。");
    }

    private static OaPaymentBudget ToDomain(OaPaymentBudgetRecord x) => OaPaymentBudget.Restore(x.Id, x.BudgetNo, x.LegalEntity, x.DepartmentName,
        x.Currency, x.TotalAmount, x.ReservedAmount, x.ConsumedAmount, x.Status, x.OtherInfo, x.CreatedAt);

    private static OaPaymentBudgetRecord ToRecord(OaPaymentBudget x) => new()
    {
        Id = x.Id, BudgetNo = x.BudgetNo, LegalEntity = x.LegalEntity, DepartmentName = x.DepartmentName, Currency = x.Currency,
        TotalAmount = x.TotalAmount, ReservedAmount = x.ReservedAmount, ConsumedAmount = x.ConsumedAmount, Status = x.Status,
        OtherInfo = x.OtherInfo, CreatedAt = x.CreatedAt
    };
}

public sealed class FreeSqlPaymentBudgetReservationRepository(IFreeSql fsql) : IOaPaymentBudgetReservationRepository
{
    public IReadOnlyList<OaPaymentBudgetReservation> List(Guid? budgetId = null)
    {
        var query = fsql.Select<OaPaymentBudgetReservationRecord>();
        if (budgetId is Guid id) query = query.Where(x => x.BudgetId == id);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaPaymentBudgetReservation? GetByPaymentRequest(Guid paymentRequestId)
        => fsql.Select<OaPaymentBudgetReservationRecord>().Where(x => x.PaymentRequestId == paymentRequestId).ToList().Select(ToDomain).FirstOrDefault();

    public void Add(OaPaymentBudgetReservation item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(OaPaymentBudgetReservation item)
    {
        if (fsql.Update<OaPaymentBudgetReservationRecord>().Set(x => x.Status, item.Status).Set(x => x.CompletedAt, item.CompletedAt)
            .Where(x => x.Id == item.Id).ExecuteAffrows() == 0) throw new InvalidOperationException("预算占用记录不存在或已被删除。");
    }

    private static OaPaymentBudgetReservation ToDomain(OaPaymentBudgetReservationRecord x) => OaPaymentBudgetReservation.Restore(x.Id, x.BudgetId,
        x.PaymentRequestId, x.Amount, x.Status, x.CreatedAt, x.CompletedAt);

    private static OaPaymentBudgetReservationRecord ToRecord(OaPaymentBudgetReservation x) => new()
    {
        Id = x.Id, BudgetId = x.BudgetId, PaymentRequestId = x.PaymentRequestId, Amount = x.Amount, Status = x.Status,
        CreatedAt = x.CreatedAt, CompletedAt = x.CompletedAt
    };
}
