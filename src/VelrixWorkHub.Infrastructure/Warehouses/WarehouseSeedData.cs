using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Warehouses;
public static class WarehouseSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<WarehouseRecord>(); fsql.CodeFirst.SyncStructure<WarehouseLocationRecord>(); if (fsql.Select<WarehouseRecord>().Any()) return;
        var item = new Warehouse("WH-001", "华东中心仓", "上海市浦东新区"); var location = item.AddLocation("A-01-01", "标准货架一层"); var now = DateTime.Now;
        fsql.Insert(new WarehouseRecord { Id = item.Id, Code = item.Code, Name = item.Name, Address = item.Address, Status = item.Status, OtherInfo = item.OtherInfo, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
        fsql.Insert(new WarehouseLocationRecord { Id = location.Id, WarehouseId = location.WarehouseId, Code = location.Code, Name = location.Name }).ExecuteAffrows();
    }
}
