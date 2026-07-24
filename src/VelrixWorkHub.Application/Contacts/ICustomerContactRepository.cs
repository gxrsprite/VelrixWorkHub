using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Contacts;
public interface ICustomerContactRepository { IReadOnlyList<CustomerContact> List(); void Add(CustomerContact contact); void Update(CustomerContact contact); void ClearPrimary(Guid customerId, Guid exceptId); void Remove(Guid contactId); }
