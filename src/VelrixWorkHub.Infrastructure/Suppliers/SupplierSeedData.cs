using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Suppliers;
public static class SupplierSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<SupplierRecord>(); if (fsql.Select<SupplierRecord>().Any()) return;
        var item = new Supplier("SUP-001", "华东供应链", "周经理", "13900001234", "标准服务包长期供应商。"); var now = DateTime.Now;
        fsql.Insert(new SupplierRecord { Id = item.Id, Code = item.Code, Name = item.Name, ContactName = item.ContactName, Phone = item.Phone, Notes = item.Notes, Status = item.Status, QualificationStatus = item.QualificationStatus, OtherInfo = item.OtherInfo, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
