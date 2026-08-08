using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomMaterialConsumptionReversalRepository(IFreeSql fsql) : IMomMaterialConsumptionReversalRepository
{
    public IReadOnlyList<MomMaterialConsumptionReversal> List() => fsql.Select<MomMaterialConsumptionReversalRecord>()
        .OrderBy(x => x.WorkOrderId).OrderByDescending(x => x.OccurredOn).ToList().Select(ToDomain).ToArray();

    public void Add(MomMaterialConsumptionReversal item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomMaterialConsumptionReversal ToDomain(MomMaterialConsumptionReversalRecord x)
        => MomMaterialConsumptionReversal.Restore(x.Id, x.ConsumptionId, x.DeliveryId, x.RequirementId, x.WorkOrderId, x.ProductId,
            x.WorkCenterId, x.Quantity, x.SourceNo, x.OccurredOn, x.BatchNo, x.ExpiryDate, x.SerialNo, x.Notes, x.OtherInfo);

    private static MomMaterialConsumptionReversalRecord ToRecord(MomMaterialConsumptionReversal x) => new()
    {
        Id = x.Id, ConsumptionId = x.ConsumptionId, DeliveryId = x.DeliveryId, RequirementId = x.RequirementId,
        WorkOrderId = x.WorkOrderId, ProductId = x.ProductId, WorkCenterId = x.WorkCenterId, Quantity = x.Quantity,
        BatchNo = x.BatchNo, ExpiryDate = x.ExpiryDate, SerialNo = x.SerialNo, SourceNo = x.SourceNo,
        OccurredOn = x.OccurredOn, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
