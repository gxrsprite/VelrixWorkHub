using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomFinishedGoodsReceiptRepository(IFreeSql fsql) : IMomFinishedGoodsReceiptRepository
{
    public IReadOnlyList<MomFinishedGoodsReceipt> List() => fsql.Select<MomFinishedGoodsReceiptRecord>()
        .OrderByDescending(x => x.ReceiptDate).OrderByDescending(x => x.SourceNo).ToList().Select(ToDomain).ToArray();

    public void Add(MomFinishedGoodsReceipt item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    private static MomFinishedGoodsReceipt ToDomain(MomFinishedGoodsReceiptRecord x) => MomFinishedGoodsReceipt.Restore(x.Id, x.WorkOrderId,
        x.ProductId, x.WarehouseId, x.LocationId, x.Quantity, x.SourceNo, DateOnly.FromDateTime(x.ReceiptDate), x.BatchNo,
        x.ExpiryDate is DateTime expiry ? DateOnly.FromDateTime(expiry) : null, x.SerialNo, x.OtherInfo);

    private static MomFinishedGoodsReceiptRecord ToRecord(MomFinishedGoodsReceipt x) => new()
    {
        Id = x.Id, WorkOrderId = x.WorkOrderId, ProductId = x.ProductId, WarehouseId = x.WarehouseId, LocationId = x.LocationId,
        Quantity = x.Quantity, SourceNo = x.SourceNo, ReceiptDate = x.ReceiptDate.ToDateTime(TimeOnly.MinValue), BatchNo = x.BatchNo,
        ExpiryDate = x.ExpiryDate?.ToDateTime(TimeOnly.MinValue), SerialNo = x.SerialNo, OtherInfo = x.OtherInfo
    };
}
