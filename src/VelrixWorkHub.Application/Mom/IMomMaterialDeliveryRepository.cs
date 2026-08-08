using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomMaterialDeliveryRepository
{
    IReadOnlyList<MomMaterialDelivery> List();
    void Add(MomMaterialDelivery item);
}
