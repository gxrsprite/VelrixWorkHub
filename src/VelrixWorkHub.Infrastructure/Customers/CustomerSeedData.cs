using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public static class CustomerSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<CustomerRecord>(); if (fsql.Select<CustomerRecord>().Any()) return;
        var item = new Customer("Aster 科技", "林经理", "13800001234", "lin@example.com", "重点客户，关注第二阶段报价。"); var now = DateTime.Now;
        fsql.Insert(new CustomerRecord { Id = item.Id, Name = item.Name, ContactName = item.ContactName, Phone = item.Phone, Email = item.Email, Notes = item.Notes, Status = item.Status, OtherInfo = item.OtherInfo, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
