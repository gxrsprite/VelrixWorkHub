using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Customers;
public interface ICustomerRepository { IReadOnlyList<Customer> List(); void Add(Customer customer); void Update(Customer customer); void Remove(Guid customerId); }
