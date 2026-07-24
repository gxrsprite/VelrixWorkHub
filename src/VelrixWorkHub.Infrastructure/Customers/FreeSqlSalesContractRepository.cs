using FreeSql;
using VelrixWorkHub.Application.Contracts;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public sealed class FreeSqlSalesContractRepository(IFreeSql fsql) : ISalesContractRepository
{
    public IReadOnlyList<SalesContract> List() => fsql.Select<SalesContractRecord>().OrderByDescending(item => item.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(SalesContract item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(SalesContract item)
    {
        var rows = fsql.Update<SalesContractRecord>().Set(record => record.CustomerId, item.CustomerId).Set(record => record.OpportunityId, item.OpportunityId).Set(record => record.ContractNo, item.ContractNo).Set(record => record.Title, item.Title).Set(record => record.Amount, item.Amount).Set(record => record.StartDate, item.StartDate.ToDateTime(TimeOnly.MinValue)).Set(record => record.EndDate, item.EndDate.ToDateTime(TimeOnly.MinValue)).Set(record => record.Status, item.Status).Set(record => record.ModifiedTime, DateTime.Now).Where(record => record.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("合同不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<SalesContractRecord>().Where(item => item.Id == id).ExecuteAffrows();
    private static SalesContract ToDomain(SalesContractRecord record) { var item = new SalesContract(record.CustomerId, record.OpportunityId, record.ContractNo, record.Title, record.Amount, DateOnly.FromDateTime(record.StartDate), DateOnly.FromDateTime(record.EndDate)) { Id = record.Id }; if (record.Status == ContractStatus.Active) item.Activate(); else if (record.Status == ContractStatus.Terminated) { item.Activate(); item.Terminate(); } return item; }
    private static SalesContractRecord ToRecord(SalesContract item, DateTime created, DateTime modified) => new() { Id = item.Id, CustomerId = item.CustomerId, OpportunityId = item.OpportunityId, ContractNo = item.ContractNo, Title = item.Title, Amount = item.Amount, StartDate = item.StartDate.ToDateTime(TimeOnly.MinValue), EndDate = item.EndDate.ToDateTime(TimeOnly.MinValue), Status = item.Status, CreatedTime = created, ModifiedTime = modified };
}
