using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomMaterialDeliveryReversalRepository(IFreeSql fsql) : IMomMaterialDeliveryReversalRepository
{
    public IReadOnlyList<MomMaterialDeliveryReversal> List() => fsql.Select<MomMaterialDeliveryReversalRecord>()
        .OrderByDescending(x => x.OccurredOn).OrderByDescending(x => x.SourceNo).ToList().Select(ToDomain).ToArray();

    public void Add(MomMaterialDeliveryReversal item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomMaterialDeliveryReversal ToDomain(MomMaterialDeliveryReversalRecord x)
        => MomMaterialDeliveryReversal.Restore(x.Id, x.DeliveryId, x.RequirementId, x.WorkOrderId, x.ProductId,
            x.WorkCenterId, x.Quantity, x.SourceNo, x.OccurredOn, x.Notes, x.OtherInfo);

    private static MomMaterialDeliveryReversalRecord ToRecord(MomMaterialDeliveryReversal x) => new()
    {
        Id = x.Id, DeliveryId = x.DeliveryId, RequirementId = x.RequirementId, WorkOrderId = x.WorkOrderId,
        ProductId = x.ProductId, WorkCenterId = x.WorkCenterId, Quantity = x.Quantity, SourceNo = x.SourceNo,
        OccurredOn = x.OccurredOn, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
