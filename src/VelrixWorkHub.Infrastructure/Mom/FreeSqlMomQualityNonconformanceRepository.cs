using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomQualityNonconformanceRepository(IFreeSql fsql) : IMomQualityNonconformanceRepository
{
    public IReadOnlyList<MomQualityNonconformance> List() => fsql.Select<MomQualityNonconformanceRecord>().OrderByDescending(x => x.CreatedOn).OrderByDescending(x => x.NonconformanceNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomQualityNonconformance item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomQualityNonconformance item)
    {
        var rows = fsql.Update<MomQualityNonconformanceRecord>().Set(x => x.Status, item.Status).Set(x => x.DispositionId, item.DispositionId)
            .Set(x => x.ClosedOn, item.ClosedOn).Set(x => x.ClosedBy, item.ClosedBy).Set(x => x.ClosureNotes, item.ClosureNotes).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("不合格记录不存在或已被删除。");
    }

    private static MomQualityNonconformance ToDomain(MomQualityNonconformanceRecord x) => MomQualityNonconformance.Restore(x.Id, x.InspectionId, x.WorkOrderId, x.OperationId, x.ProductId,
        x.BatchNo, x.DefectCode, x.Description, x.Quantity, x.Severity, x.Status, x.DispositionId, x.CreatedOn, x.ClosedOn, x.ClosedBy, x.ClosureNotes, x.OtherInfo, x.NonconformanceNo);
    private static MomQualityNonconformanceRecord ToRecord(MomQualityNonconformance x) => new()
    {
        Id = x.Id, InspectionId = x.InspectionId, WorkOrderId = x.WorkOrderId, OperationId = x.OperationId, ProductId = x.ProductId, BatchNo = x.BatchNo,
        NonconformanceNo = x.NonconformanceNo, DefectCode = x.DefectCode, Description = x.Description, Quantity = x.Quantity, Severity = x.Severity,
        Status = x.Status, DispositionId = x.DispositionId, CreatedOn = x.CreatedOn, ClosedOn = x.ClosedOn, ClosedBy = x.ClosedBy, ClosureNotes = x.ClosureNotes, OtherInfo = x.OtherInfo
    };
}
