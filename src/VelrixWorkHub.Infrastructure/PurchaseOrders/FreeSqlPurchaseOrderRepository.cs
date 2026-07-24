using FreeSql;
using VelrixWorkHub.Application.PurchaseOrders;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.PurchaseOrders;
public sealed class FreeSqlPurchaseOrderRepository(IFreeSql fsql) : IPurchaseOrderRepository
{
    public IReadOnlyList<PurchaseOrder> List() => fsql.Select<PurchaseOrderRecord>().OrderByDescending(x => x.OrderDate).ToList().Select(ToDomain).ToArray();
    public void Add(PurchaseOrder item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(PurchaseOrder item) { var rows = fsql.Update<PurchaseOrderRecord>().Set(x => x.Status, item.Status).Set(x => x.SourceKind, item.SourceKind).Set(x => x.SourceDocumentNo, item.SourceDocumentNo).Set(x => x.IsLocked, item.IsLocked).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("采购订单不存在或已被删除。"); }
    private static PurchaseOrder ToDomain(PurchaseOrderRecord x) => PurchaseOrder.Restore(x.Id, x.OrderNo, x.SupplierId, x.ProductId, DateOnly.FromDateTime(x.OrderDate), x.Quantity, x.UnitPrice, x.Status, x.SourceKind, x.SourceDocumentNo, x.IsLocked, x.DueDate is null ? null : DateOnly.FromDateTime(x.DueDate.Value), x.SourceLineId);
    private static PurchaseOrderRecord ToRecord(PurchaseOrder x) => new() { Id = x.Id, OrderNo = x.OrderNo, SupplierId = x.SupplierId, ProductId = x.ProductId, OrderDate = x.OrderDate.ToDateTime(TimeOnly.MinValue), Quantity = x.Quantity, UnitPrice = x.UnitPrice, Status = x.Status, SourceKind = x.SourceKind, SourceDocumentNo = x.SourceDocumentNo, IsLocked = x.IsLocked, DueDate = x.DueDate.ToDateTime(TimeOnly.MinValue), SourceLineId = x.SourceLineId };
}
