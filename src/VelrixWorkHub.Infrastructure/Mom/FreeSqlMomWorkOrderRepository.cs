using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomWorkOrderRepository(IFreeSql fsql) : IMomWorkOrderRepository
{
    public IReadOnlyList<MomWorkOrder> List() => fsql.Select<MomWorkOrderRecord>().OrderByDescending(x => x.PlannedStart).ToList().Select(ToDomain).ToArray();
    public void Add(MomWorkOrder item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomWorkOrder item)
    {
        var rows = fsql.Update<MomWorkOrderRecord>().Set(x => x.WorkCenterId, item.WorkCenterId).Set(x => x.CompletedQuantity, item.CompletedQuantity).Set(x => x.Status, item.Status).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("制造工单不存在或已被删除。");
    }

    private static MomWorkOrder ToDomain(MomWorkOrderRecord x) => MomWorkOrder.Restore(x.Id, x.WorkOrderNo, x.ProductId, DateOnly.FromDateTime(x.PlannedStart), DateOnly.FromDateTime(x.PlannedEnd), x.PlannedQuantity, x.CompletedQuantity, x.Status, x.SourceKind, x.SourceDocumentNo, x.SalesOrderId, x.PmsProjectId, x.WorkCenterId, x.OtherInfo);
    private static MomWorkOrderRecord ToRecord(MomWorkOrder x) => new() { Id = x.Id, WorkOrderNo = x.WorkOrderNo, ProductId = x.ProductId, WorkCenterId = x.WorkCenterId, SalesOrderId = x.SalesOrderId, PmsProjectId = x.PmsProjectId, PlannedStart = x.PlannedStart.ToDateTime(TimeOnly.MinValue), PlannedEnd = x.PlannedEnd.ToDateTime(TimeOnly.MinValue), PlannedQuantity = x.PlannedQuantity, CompletedQuantity = x.CompletedQuantity, Status = x.Status, SourceKind = x.SourceKind, SourceDocumentNo = x.SourceDocumentNo, OtherInfo = x.OtherInfo };
}
