using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomFinishedGoodsShipmentAllocationRepository(IFreeSql fsql) : IMomFinishedGoodsShipmentAllocationRepository
{
    public IReadOnlyList<MomFinishedGoodsShipmentAllocation> List(Guid? shipmentId = null)
    {
        var query = fsql.Select<MomFinishedGoodsShipmentAllocationRecord>();
        if (shipmentId is Guid selected) query = query.Where(x => x.ShipmentId == selected);
        return query.OrderBy(x => x.SourceNo).ToList().Select(ToDomain).ToArray();
    }

    public void Add(MomFinishedGoodsShipmentAllocation item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomFinishedGoodsShipmentAllocation ToDomain(MomFinishedGoodsShipmentAllocationRecord x)
        => MomFinishedGoodsShipmentAllocation.Restore(x.Id, x.ShipmentId, x.FinishedGoodsReceiptId, x.ProductId,
            x.WarehouseId, x.LocationId, x.Quantity, x.SourceNo, DateOnly.FromDateTime(x.ShipmentDate), x.BatchNo,
            x.ExpiryDate is DateTime expiry ? DateOnly.FromDateTime(expiry) : null, x.SerialNo, x.OtherInfo);

    private static MomFinishedGoodsShipmentAllocationRecord ToRecord(MomFinishedGoodsShipmentAllocation x) => new()
    {
        Id = x.Id, ShipmentId = x.ShipmentId, FinishedGoodsReceiptId = x.FinishedGoodsReceiptId, ProductId = x.ProductId,
        WarehouseId = x.WarehouseId, LocationId = x.LocationId, Quantity = x.Quantity, SourceNo = x.SourceNo,
        ShipmentDate = x.ShipmentDate.ToDateTime(TimeOnly.MinValue), BatchNo = x.BatchNo,
        ExpiryDate = x.ExpiryDate?.ToDateTime(TimeOnly.MinValue), SerialNo = x.SerialNo, OtherInfo = x.OtherInfo
    };
}
