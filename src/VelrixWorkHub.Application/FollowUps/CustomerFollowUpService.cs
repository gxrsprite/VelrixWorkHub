using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.FollowUps;
public sealed class CustomerFollowUpService(ICustomerFollowUpRepository repository)
{
    public IReadOnlyList<CustomerFollowUp> List(string? keyword = null, FollowUpFilter filter = FollowUpFilter.All)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim(); var today = DateOnly.FromDateTime(DateTime.Today);
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(item => item.Content.Contains(text, StringComparison.OrdinalIgnoreCase));
        query = filter switch { FollowUpFilter.Upcoming => query.Where(item => item.NextFollowUpDate >= today), FollowUpFilter.Overdue => query.Where(item => item.NextFollowUpDate < today), _ => query };
        return query.ToArray();
    }
    public int Count(FollowUpFilter filter) => List(filter: filter).Count;
    public CustomerFollowUp Create(Guid customerId, Guid? contactId, FollowUpType type, string content, DateOnly? nextDate) { var item = new CustomerFollowUp(customerId, contactId, type, content, nextDate); repository.Add(item); return item; }
    public void Remove(CustomerFollowUp item) => repository.Remove(item.Id);
}
