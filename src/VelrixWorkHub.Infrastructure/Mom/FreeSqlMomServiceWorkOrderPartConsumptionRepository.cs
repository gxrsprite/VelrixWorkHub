using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomServiceWorkOrderPartConsumptionRepository(IFreeSql fsql)
    : IMomServiceWorkOrderPartConsumptionRepository
{
    public IReadOnlyList<MomServiceWorkOrderPartConsumption> List(Guid? serviceWorkOrderId = null)
    {
        var query = fsql.Select<MomServiceWorkOrderPartConsumptionRecord>();
        if (serviceWorkOrderId is Guid id) query = query.Where(x => x.ServiceWorkOrderId == id);
        return query.OrderByDescending(x => x.ConsumedOn).ToList().Select(ToDomain).OrderByDescending(x => x.ConsumedOn).ThenByDescending(x => x.SourceNo).ToArray();
    }

    public void Add(MomServiceWorkOrderPartConsumption item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomServiceWorkOrderPartConsumption ToDomain(MomServiceWorkOrderPartConsumptionRecord x)
        => MomServiceWorkOrderPartConsumption.Restore(x.Id, x.ServiceWorkOrderId, x.EquipmentId, x.ProductId,
            x.WarehouseId, x.LocationId, x.Quantity, x.SourceNo, DateOnly.FromDateTime(x.ConsumedOn), x.BatchNo,
            x.ExpiryDate is DateTime expiry ? DateOnly.FromDateTime(expiry) : null, x.SerialNo, x.Actor, x.Notes, x.OtherInfo);

    private static MomServiceWorkOrderPartConsumptionRecord ToRecord(MomServiceWorkOrderPartConsumption x) => new()
    {
        Id = x.Id, ServiceWorkOrderId = x.ServiceWorkOrderId, EquipmentId = x.EquipmentId, ProductId = x.ProductId,
        WarehouseId = x.WarehouseId, LocationId = x.LocationId, Quantity = x.Quantity, SourceNo = x.SourceNo,
        ConsumedOn = x.ConsumedOn.ToDateTime(TimeOnly.MinValue), BatchNo = x.BatchNo,
        ExpiryDate = x.ExpiryDate?.ToDateTime(TimeOnly.MinValue), SerialNo = x.SerialNo, Actor = x.Actor,
        Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
