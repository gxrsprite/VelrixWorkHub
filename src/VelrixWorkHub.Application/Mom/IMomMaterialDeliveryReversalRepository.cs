using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomMaterialDeliveryReversalRepository
{
    IReadOnlyList<MomMaterialDeliveryReversal> List();
    void Add(MomMaterialDeliveryReversal item);
}
