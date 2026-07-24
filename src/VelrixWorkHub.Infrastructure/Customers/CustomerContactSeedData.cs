using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public static class CustomerContactSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<CustomerContactRecord>(); if (fsql.Select<CustomerContactRecord>().Any()) return;
        var customer = fsql.Select<CustomerRecord>().First(); if (customer is null) return;
        var item = new CustomerContact(customer.Id, "林经理", "采购负责人", "13800001234", "lin@example.com", true); var now = DateTime.Now;
        fsql.Insert(new CustomerContactRecord { Id = item.Id, CustomerId = item.CustomerId, Name = item.Name, Position = item.Position, Phone = item.Phone, Email = item.Email, IsPrimary = true, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
