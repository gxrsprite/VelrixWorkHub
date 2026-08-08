using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomFinishedGoodsShipmentRepository(IFreeSql fsql) : IMomFinishedGoodsShipmentRepository
{
    public IReadOnlyList<MomFinishedGoodsShipment> List() => fsql.Select<MomFinishedGoodsShipmentRecord>()
        .OrderByDescending(x => x.ShipmentDate).OrderByDescending(x => x.SourceNo).ToList().Select(ToDomain).ToArray();

    public void Add(MomFinishedGoodsShipment item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomFinishedGoodsShipment ToDomain(MomFinishedGoodsShipmentRecord x) => MomFinishedGoodsShipment.Restore(x.Id,
        x.SalesOrderId, x.FinishedGoodsReceiptId, x.ProductId, x.WarehouseId, x.LocationId, x.Quantity, x.SourceNo,
        DateOnly.FromDateTime(x.ShipmentDate), x.BatchNo, x.ExpiryDate is DateTime expiry ? DateOnly.FromDateTime(expiry) : null,
        x.SerialNo, x.OtherInfo);

    private static MomFinishedGoodsShipmentRecord ToRecord(MomFinishedGoodsShipment x) => new()
    {
        Id = x.Id, SalesOrderId = x.SalesOrderId, FinishedGoodsReceiptId = x.FinishedGoodsReceiptId, ProductId = x.ProductId,
        WarehouseId = x.WarehouseId, LocationId = x.LocationId, Quantity = x.Quantity, SourceNo = x.SourceNo,
        ShipmentDate = x.ShipmentDate.ToDateTime(TimeOnly.MinValue), BatchNo = x.BatchNo,
        ExpiryDate = x.ExpiryDate?.ToDateTime(TimeOnly.MinValue), SerialNo = x.SerialNo, OtherInfo = x.OtherInfo
    };
}
