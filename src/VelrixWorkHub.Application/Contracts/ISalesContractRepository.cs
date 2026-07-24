using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Contracts;
public interface ISalesContractRepository { IReadOnlyList<SalesContract> List(); void Add(SalesContract contract); void Update(SalesContract contract); void Remove(Guid contractId); }
