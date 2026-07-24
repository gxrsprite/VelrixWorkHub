using FreeSql;
using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.ProcurementRequests;

public sealed class FreeSqlProcurementBudgetRepository(IFreeSql fsql) : IOaProcurementBudgetRepository
{
    public IReadOnlyList<OaProcurementBudget> List() => fsql.Select<OaProcurementBudgetRecord>().ToList().Select(ToDomain).ToArray();
    public OaProcurementBudget? Get(Guid id) => fsql.Select<OaProcurementBudgetRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaProcurementBudget item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(OaProcurementBudget item) => fsql.Update<OaProcurementBudgetRecord>().SetSource(ToRecord(item)).ExecuteAffrows();

    private static OaProcurementBudget ToDomain(OaProcurementBudgetRecord x) => OaProcurementBudget.Restore(x.Id, x.BudgetNo, x.LegalEntity,
        x.DepartmentName, x.TotalAmount, x.ReservedAmount, x.ConsumedAmount, x.Status, x.OtherInfo, x.CreatedAt);

    private static OaProcurementBudgetRecord ToRecord(OaProcurementBudget x) => new()
    {
        Id = x.Id, BudgetNo = x.BudgetNo, LegalEntity = x.LegalEntity, DepartmentName = x.DepartmentName,
        TotalAmount = x.TotalAmount, ReservedAmount = x.ReservedAmount, ConsumedAmount = x.ConsumedAmount,
        Status = x.Status, OtherInfo = x.OtherInfo, CreatedAt = x.CreatedAt
    };
}

public sealed class FreeSqlProcurementBudgetReservationRepository(IFreeSql fsql) : IOaProcurementBudgetReservationRepository
{
    public IReadOnlyList<OaProcurementBudgetReservation> List(Guid? budgetId = null)
    {
        var query = fsql.Select<OaProcurementBudgetReservationRecord>();
        if (budgetId is not null) query = query.Where(x => x.BudgetId == budgetId.Value);
        return query.ToList().Select(ToDomain).ToArray();
    }

    public OaProcurementBudgetReservation? GetByProcurementRequest(Guid procurementRequestId)
        => fsql.Select<OaProcurementBudgetReservationRecord>().Where(x => x.ProcurementRequestId == procurementRequestId).ToList().Select(ToDomain).FirstOrDefault();

    public void Add(OaProcurementBudgetReservation item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(OaProcurementBudgetReservation item) => fsql.Update<OaProcurementBudgetReservationRecord>().SetSource(ToRecord(item)).ExecuteAffrows();

    private static OaProcurementBudgetReservation ToDomain(OaProcurementBudgetReservationRecord x)
        => OaProcurementBudgetReservation.Restore(x.Id, x.BudgetId, x.ProcurementRequestId, x.Amount, x.Status, x.CreatedAt, x.CompletedAt);

    private static OaProcurementBudgetReservationRecord ToRecord(OaProcurementBudgetReservation x) => new()
    {
        Id = x.Id, BudgetId = x.BudgetId, ProcurementRequestId = x.ProcurementRequestId, Amount = x.Amount,
        Status = x.Status, CreatedAt = x.CreatedAt, CompletedAt = x.CompletedAt
    };
}
