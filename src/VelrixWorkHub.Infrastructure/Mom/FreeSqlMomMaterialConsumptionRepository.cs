using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomMaterialConsumptionRepository(IFreeSql fsql) : IMomMaterialConsumptionRepository
{
    public IReadOnlyList<MomMaterialConsumption> List() => fsql.Select<MomMaterialConsumptionRecord>()
        .OrderBy(x => x.WorkOrderId).OrderByDescending(x => x.OccurredOn).ToList().Select(ToDomain).ToArray();

    public void Add(MomMaterialConsumption item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomMaterialConsumption ToDomain(MomMaterialConsumptionRecord x)
        => MomMaterialConsumption.Restore(x.Id, x.RequirementId, x.WorkOrderId, x.ProductId, x.WorkCenterId,
            x.Quantity, x.SourceNo, x.OccurredOn, x.Notes, x.OtherInfo, x.DeliveryId);

    private static MomMaterialConsumptionRecord ToRecord(MomMaterialConsumption x) => new()
    {
        Id = x.Id, RequirementId = x.RequirementId, DeliveryId = x.DeliveryId, WorkOrderId = x.WorkOrderId, ProductId = x.ProductId,
        WorkCenterId = x.WorkCenterId, Quantity = x.Quantity, SourceNo = x.SourceNo, OccurredOn = x.OccurredOn,
        Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
