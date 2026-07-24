using FreeSql;
using VelrixWorkHub.Domain;
using VelrixWorkHub.Infrastructure.Products;
using VelrixWorkHub.Infrastructure.Suppliers;
namespace VelrixWorkHub.Infrastructure.PurchaseOrders;
public static class PurchaseOrderSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<PurchaseOrderRecord>(); if (fsql.Select<PurchaseOrderRecord>().Any()) return; var supplier = fsql.Select<SupplierRecord>().First(); var product = fsql.Select<ProductRecord>().First(); if (supplier is null || product is null) return; var item = new PurchaseOrder("PO-20260712-001", supplier.Id, product.Id, DateOnly.FromDateTime(DateTime.Today), 10, product.SalePrice ?? 0); fsql.Insert(new PurchaseOrderRecord { Id = item.Id, OrderNo = item.OrderNo, SupplierId = item.SupplierId, ProductId = item.ProductId, OrderDate = item.OrderDate.ToDateTime(TimeOnly.MinValue), DueDate = item.DueDate.ToDateTime(TimeOnly.MinValue), Quantity = item.Quantity, UnitPrice = item.UnitPrice, Status = item.Status, SourceKind = item.SourceKind, SourceDocumentNo = item.SourceDocumentNo, IsLocked = item.IsLocked }).ExecuteAffrows();
    }
}
