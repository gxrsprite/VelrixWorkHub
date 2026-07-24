using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.FollowUps;
public interface ICustomerFollowUpRepository { IReadOnlyList<CustomerFollowUp> List(); void Add(CustomerFollowUp followUp); void Remove(Guid followUpId); }
