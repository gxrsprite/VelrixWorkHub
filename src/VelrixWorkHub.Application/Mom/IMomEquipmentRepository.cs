using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomEquipmentRepository
{
    IReadOnlyList<MomEquipment> List();
    void Add(MomEquipment item);
    void Update(MomEquipment item);
}
