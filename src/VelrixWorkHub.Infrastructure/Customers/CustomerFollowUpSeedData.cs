using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public static class CustomerFollowUpSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<CustomerFollowUpRecord>(); if (fsql.Select<CustomerFollowUpRecord>().Any()) return;
        var customer = fsql.Select<CustomerRecord>().First(); if (customer is null) return;
        var contact = fsql.Select<CustomerContactRecord>().Where(item => item.CustomerId == customer.Id).First(); var item = new CustomerFollowUp(customer.Id, contact?.Id, FollowUpType.Phone, "确认第二阶段报价反馈，并约定下一次沟通时间。", DateOnly.FromDateTime(DateTime.Today.AddDays(2))); var now = DateTime.Now;
        fsql.Insert(new CustomerFollowUpRecord { Id = item.Id, CustomerId = item.CustomerId, ContactId = item.ContactId, Type = item.Type, Content = item.Content, NextFollowUpDate = item.NextFollowUpDate?.ToDateTime(TimeOnly.MinValue), CreatedTime = now }).ExecuteAffrows();
    }
}
