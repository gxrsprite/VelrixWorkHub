using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Settlements;

public interface ISettlementRepository
{
    IReadOnlyList<ErpSettlement> List();
    void Add(ErpSettlement item);
    void Update(ErpSettlement item);
}
