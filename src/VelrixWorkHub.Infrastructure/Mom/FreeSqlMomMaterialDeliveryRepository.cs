using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomMaterialDeliveryRepository(IFreeSql fsql) : IMomMaterialDeliveryRepository
{
    public IReadOnlyList<MomMaterialDelivery> List() => fsql.Select<MomMaterialDeliveryRecord>()
        .OrderBy(x => x.WorkOrderId).OrderByDescending(x => x.OccurredOn).ToList().Select(ToDomain).ToArray();

    public void Add(MomMaterialDelivery item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomMaterialDelivery ToDomain(MomMaterialDeliveryRecord x)
        => MomMaterialDelivery.Restore(x.Id, x.RequirementId, x.WorkOrderId, x.ProductId, x.WorkCenterId,
            x.Quantity, x.SourceNo, x.OccurredOn, x.Notes, x.OtherInfo, x.SourceWarehouseId, x.SourceLocationId, x.TargetWarehouseId, x.TargetLocationId,
            x.BatchNo, x.ExpiryDate, x.SerialNo);

    private static MomMaterialDeliveryRecord ToRecord(MomMaterialDelivery x) => new()
    {
        Id = x.Id, RequirementId = x.RequirementId, WorkOrderId = x.WorkOrderId, ProductId = x.ProductId,
        WorkCenterId = x.WorkCenterId, SourceWarehouseId = x.SourceWarehouseId, SourceLocationId = x.SourceLocationId,
        TargetWarehouseId = x.TargetWarehouseId, TargetLocationId = x.TargetLocationId, Quantity = x.Quantity, SourceNo = x.SourceNo,
        OccurredOn = x.OccurredOn, Notes = x.Notes, OtherInfo = x.OtherInfo, BatchNo = x.BatchNo, ExpiryDate = x.ExpiryDate, SerialNo = x.SerialNo
    };
}
