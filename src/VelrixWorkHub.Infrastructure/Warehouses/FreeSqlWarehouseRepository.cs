using FreeSql;
using VelrixWorkHub.Application.Warehouses;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Warehouses;
public sealed class FreeSqlWarehouseRepository(IFreeSql fsql) : IWarehouseRepository
{
    public IReadOnlyList<Warehouse> List()
    {
        var locations = fsql.Select<WarehouseLocationRecord>().ToList().GroupBy(x => x.WarehouseId).ToDictionary(x => x.Key, x => x.ToArray());
        return fsql.Select<WarehouseRecord>().OrderByDescending(x => x.CreatedTime).ToList().Select(x => ToDomain(x, locations.GetValueOrDefault(x.Id) ?? [])).ToArray();
    }
    public void Add(Warehouse item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(Warehouse item) { var rows = fsql.Update<WarehouseRecord>().Set(x => x.Code, item.Code).Set(x => x.Name, item.Name).Set(x => x.Address, item.Address).Set(x => x.Status, item.Status).Set(x => x.OtherInfo, item.OtherInfo).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("仓库不存在或已被删除。"); }
    public void Remove(Guid id) { fsql.Delete<WarehouseLocationRecord>().Where(x => x.WarehouseId == id).ExecuteAffrows(); fsql.Delete<WarehouseRecord>().Where(x => x.Id == id).ExecuteAffrows(); }
    public void AddLocation(WarehouseLocation item) => fsql.Insert(new WarehouseLocationRecord { Id = item.Id, WarehouseId = item.WarehouseId, Code = item.Code, Name = item.Name }).ExecuteAffrows();
    public void RemoveLocation(Guid id) => fsql.Delete<WarehouseLocationRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static Warehouse ToDomain(WarehouseRecord x, IEnumerable<WarehouseLocationRecord> locations) { var item = new Warehouse(x.Code, x.Name, x.Address, x.OtherInfo) { Id = x.Id }; item.SetActive(x.Status == WarehouseStatus.Active); foreach (var location in locations) item.AddLocation(location.Id, location.Code, location.Name); return item; }
    private static WarehouseRecord ToRecord(Warehouse x, DateTime created, DateTime modified) => new() { Id = x.Id, Code = x.Code, Name = x.Name, Address = x.Address, Status = x.Status, OtherInfo = x.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}
