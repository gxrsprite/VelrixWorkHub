using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Opportunities;
public interface ISalesOpportunityRepository { IReadOnlyList<SalesOpportunity> List(); void Add(SalesOpportunity opportunity); void Update(SalesOpportunity opportunity); void Remove(Guid opportunityId); }
