using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Opportunities;
public sealed class SalesOpportunityService(ISalesOpportunityRepository repository)
{
    public IReadOnlyList<SalesOpportunity> List(string? keyword = null, OpportunityFilter filter = OpportunityFilter.All)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(item => item.Title.Contains(text, StringComparison.OrdinalIgnoreCase));
        query = filter switch { OpportunityFilter.Open => query.Where(item => item.Stage is not OpportunityStage.Won and not OpportunityStage.Lost), OpportunityFilter.Won => query.Where(item => item.Stage == OpportunityStage.Won), OpportunityFilter.Lost => query.Where(item => item.Stage == OpportunityStage.Lost), _ => query };
        return query.ToArray();
    }
    public int Count(OpportunityFilter filter) => List(filter: filter).Count;
    public SalesOpportunity Create(Guid customerId, string title, decimal? amount, DateOnly? closeDate) { var item = new SalesOpportunity(customerId, title, amount, closeDate); repository.Add(item); return item; }
    public void Edit(SalesOpportunity item, Guid customerId, string title, decimal? amount, DateOnly? closeDate) { item.Edit(customerId, title, amount, closeDate); repository.Update(item); }
    public void MoveTo(SalesOpportunity item, OpportunityStage stage, string? lostReason = null) { item.MoveTo(stage, lostReason); repository.Update(item); }
    public void Remove(SalesOpportunity item) => repository.Remove(item.Id);
}
