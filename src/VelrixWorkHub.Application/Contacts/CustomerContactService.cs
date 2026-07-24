using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Contacts;
public sealed class CustomerContactService(ICustomerContactRepository repository)
{
    public IReadOnlyList<CustomerContact> List(string? keyword = null, Guid? customerId = null)
    {
        var query = repository.List().AsEnumerable(); var text = keyword?.Trim();
        if (customerId is not null && customerId != Guid.Empty) query = query.Where(item => item.CustomerId == customerId);
        if (!string.IsNullOrWhiteSpace(text)) query = query.Where(item => item.Name.Contains(text, StringComparison.OrdinalIgnoreCase) || (item.Phone?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        return query.ToArray();
    }
    public CustomerContact Create(Guid customerId, string name, string? position, string? phone, string? email, bool primary) { var item = new CustomerContact(customerId, name, position, phone, email, primary); if (primary) repository.ClearPrimary(customerId, item.Id); repository.Add(item); return item; }
    public void Edit(CustomerContact item, Guid customerId, string name, string? position, string? phone, string? email) { item.Edit(customerId, name, position, phone, email); repository.Update(item); }
    public void SetPrimary(CustomerContact item) { repository.ClearPrimary(item.CustomerId, item.Id); item.SetPrimary(true); repository.Update(item); }
    public void Remove(CustomerContact item) => repository.Remove(item.Id);
}
