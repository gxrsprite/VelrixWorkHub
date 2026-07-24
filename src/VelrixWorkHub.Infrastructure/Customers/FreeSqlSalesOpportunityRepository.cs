using FreeSql;
using VelrixWorkHub.Application.Opportunities;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public sealed class FreeSqlSalesOpportunityRepository(IFreeSql fsql) : ISalesOpportunityRepository
{
    public IReadOnlyList<SalesOpportunity> List() => fsql.Select<SalesOpportunityRecord>().OrderByDescending(item => item.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(SalesOpportunity item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(SalesOpportunity item)
    {
        var rows = fsql.Update<SalesOpportunityRecord>().Set(record => record.CustomerId, item.CustomerId).Set(record => record.Title, item.Title).Set(record => record.Stage, item.Stage).Set(record => record.ExpectedAmount, item.ExpectedAmount).Set(record => record.ExpectedCloseDate, (DateTime?)item.ExpectedCloseDate?.ToDateTime(TimeOnly.MinValue)).Set(record => record.LostReason, item.LostReason).Set(record => record.ModifiedTime, DateTime.Now).Where(record => record.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("商机不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<SalesOpportunityRecord>().Where(item => item.Id == id).ExecuteAffrows();
    private static SalesOpportunity ToDomain(SalesOpportunityRecord record) { var item = new SalesOpportunity(record.CustomerId, record.Title, record.ExpectedAmount, record.ExpectedCloseDate is null ? null : DateOnly.FromDateTime(record.ExpectedCloseDate.Value)) { Id = record.Id }; if (record.Stage != OpportunityStage.Prospecting) item.MoveTo(record.Stage, record.LostReason); return item; }
    private static SalesOpportunityRecord ToRecord(SalesOpportunity item, DateTime created, DateTime modified) => new() { Id = item.Id, CustomerId = item.CustomerId, Title = item.Title, Stage = item.Stage, ExpectedAmount = item.ExpectedAmount, ExpectedCloseDate = item.ExpectedCloseDate?.ToDateTime(TimeOnly.MinValue), LostReason = item.LostReason, CreatedTime = created, ModifiedTime = modified };
}
