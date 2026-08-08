using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomQualityInspectionRepository(IFreeSql fsql) : IMomQualityInspectionRepository
{
    public IReadOnlyList<MomQualityInspection> List() => fsql.Select<MomQualityInspectionRecord>()
        .OrderByDescending(x => x.CreatedOn).OrderByDescending(x => x.InspectionNo).ToList().Select(ToDomain).ToArray();

    public void Add(MomQualityInspection item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(MomQualityInspection item)
    {
        var rows = fsql.Update<MomQualityInspectionRecord>()
            .Set(x => x.AcceptedQuantity, item.AcceptedQuantity).Set(x => x.RejectedQuantity, item.RejectedQuantity)
            .Set(x => x.Status, item.Status).Set(x => x.Inspector, item.Inspector).Set(x => x.InspectedOn, item.InspectedOn)
            .Set(x => x.Notes, item.Notes).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("质量检验记录不存在或已被删除。");
    }

    private static MomQualityInspection ToDomain(MomQualityInspectionRecord x) => MomQualityInspection.Restore(
        x.Id, x.WorkOrderId, x.InspectionType, x.OperationId, x.ProductId, x.BatchNo, x.SerialNo, x.SampleQuantity,
        x.AcceptedQuantity, x.RejectedQuantity, x.Status, x.InspectionNo, x.Inspector, x.InspectedOn, x.CreatedOn, x.Notes, x.OtherInfo,
        x.StandardId, x.StandardCode, x.StandardVersion, x.StandardSnapshotJson);

    private static MomQualityInspectionRecord ToRecord(MomQualityInspection x) => new()
    {
        Id = x.Id, WorkOrderId = x.WorkOrderId, OperationId = x.OperationId, ProductId = x.ProductId, StandardId = x.StandardId,
        StandardCode = x.StandardCode, StandardVersion = x.StandardVersion, StandardSnapshotJson = x.StandardSnapshotJson,
        InspectionType = x.InspectionType, InspectionNo = x.InspectionNo, BatchNo = x.BatchNo, SerialNo = x.SerialNo,
        SampleQuantity = x.SampleQuantity, AcceptedQuantity = x.AcceptedQuantity, RejectedQuantity = x.RejectedQuantity,
        Status = x.Status, Inspector = x.Inspector, InspectedOn = x.InspectedOn, CreatedOn = x.CreatedOn,
        Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
