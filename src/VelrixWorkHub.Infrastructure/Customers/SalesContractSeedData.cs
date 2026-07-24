using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public static class SalesContractSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<SalesContractRecord>(); if (fsql.Select<SalesContractRecord>().Any()) return;
        var customer = fsql.Select<CustomerRecord>().First(); if (customer is null) return; var item = new SalesContract(customer.Id, null, "CT-2026-0001", "数字化升级服务合同", 280000m, DateOnly.FromDateTime(DateTime.Today), DateOnly.FromDateTime(DateTime.Today.AddYears(1))); item.Activate(); var now = DateTime.Now;
        fsql.Insert(new SalesContractRecord { Id = item.Id, CustomerId = item.CustomerId, ContractNo = item.ContractNo, Title = item.Title, Amount = item.Amount, StartDate = item.StartDate.ToDateTime(TimeOnly.MinValue), EndDate = item.EndDate.ToDateTime(TimeOnly.MinValue), Status = item.Status, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
