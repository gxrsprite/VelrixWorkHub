using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomMaterialConsumptionAllocationRepository(IFreeSql fsql) : IMomMaterialConsumptionAllocationRepository
{
    public IReadOnlyList<MomMaterialConsumptionAllocation> List() => fsql.Select<MomMaterialConsumptionAllocationRecord>()
        .OrderBy(x => x.WorkOrderId).OrderByDescending(x => x.OccurredOn).ToList().Select(ToDomain).ToArray();

    public void Add(MomMaterialConsumptionAllocation item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomMaterialConsumptionAllocation ToDomain(MomMaterialConsumptionAllocationRecord x)
        => MomMaterialConsumptionAllocation.Restore(x.Id, x.ConsumptionId, x.DeliveryId, x.RequirementId, x.WorkOrderId, x.ProductId,
            x.WorkCenterId, x.Quantity, x.SourceNo, x.OccurredOn, x.BatchNo, x.ExpiryDate, x.SerialNo, x.Notes, x.OtherInfo);

    private static MomMaterialConsumptionAllocationRecord ToRecord(MomMaterialConsumptionAllocation x) => new()
    {
        Id = x.Id, ConsumptionId = x.ConsumptionId, DeliveryId = x.DeliveryId, RequirementId = x.RequirementId,
        WorkOrderId = x.WorkOrderId, ProductId = x.ProductId, WorkCenterId = x.WorkCenterId, Quantity = x.Quantity,
        BatchNo = x.BatchNo, ExpiryDate = x.ExpiryDate, SerialNo = x.SerialNo, SourceNo = x.SourceNo,
        OccurredOn = x.OccurredOn, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
