using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public static class SalesOpportunitySeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<SalesOpportunityRecord>(); if (fsql.Select<SalesOpportunityRecord>().Any()) return;
        var customer = fsql.Select<CustomerRecord>().First(); if (customer is null) return;
        var item = new SalesOpportunity(customer.Id, "第二阶段数字化项目", 280000m, DateOnly.FromDateTime(DateTime.Today.AddDays(30))); var now = DateTime.Now;
        fsql.Insert(new SalesOpportunityRecord { Id = item.Id, CustomerId = item.CustomerId, Title = item.Title, Stage = item.Stage, ExpectedAmount = item.ExpectedAmount, ExpectedCloseDate = item.ExpectedCloseDate?.ToDateTime(TimeOnly.MinValue), CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
