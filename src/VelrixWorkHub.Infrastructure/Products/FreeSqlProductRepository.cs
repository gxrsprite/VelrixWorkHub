using FreeSql;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Products;
public sealed class FreeSqlProductRepository(IFreeSql fsql) : IProductRepository
{
    public IReadOnlyList<Product> List() => fsql.Select<ProductRecord>().OrderByDescending(x => x.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(Product item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(Product item) { var rows = fsql.Update<ProductRecord>().Set(x => x.Code, item.Code).Set(x => x.Name, item.Name).Set(x => x.Unit, item.Unit).Set(x => x.SalePrice, item.SalePrice).Set(x => x.Status, item.Status).Set(x => x.Notes, item.Notes).Set(x => x.MaxPurchaseQuantity, item.MaxPurchaseQuantity).Set(x => x.SafetyStock, item.SafetyStock).Set(x => x.OtherInfo, item.OtherInfo).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("商品不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<ProductRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static Product ToDomain(ProductRecord x) { var item = new Product(x.Code, x.Name, x.Unit, x.SalePrice, x.Notes, x.MaxPurchaseQuantity, x.SafetyStock, x.OtherInfo) { Id = x.Id }; item.SetActive(x.Status == ProductStatus.Active); return item; }
    private static ProductRecord ToRecord(Product x, DateTime created, DateTime modified) => new() { Id = x.Id, Code = x.Code, Name = x.Name, Unit = x.Unit, SalePrice = x.SalePrice, Status = x.Status, Notes = x.Notes, MaxPurchaseQuantity = x.MaxPurchaseQuantity, SafetyStock = x.SafetyStock, OtherInfo = x.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}
