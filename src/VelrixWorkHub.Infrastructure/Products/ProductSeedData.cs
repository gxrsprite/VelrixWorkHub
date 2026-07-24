using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Products;
public static class ProductSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<ProductRecord>(); if (fsql.Select<ProductRecord>().Any()) return;
        var item = new Product("SKU-1001", "标准服务包", "套", 1280m, "ERP 商品主数据示例"); var now = DateTime.Now;
        fsql.Insert(new ProductRecord { Id = item.Id, Code = item.Code, Name = item.Name, Unit = item.Unit, SalePrice = item.SalePrice, Status = item.Status, Notes = item.Notes, MaxPurchaseQuantity = item.MaxPurchaseQuantity, SafetyStock = item.SafetyStock, OtherInfo = item.OtherInfo, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
