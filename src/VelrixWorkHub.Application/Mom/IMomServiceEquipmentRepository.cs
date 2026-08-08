using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public interface IMomServiceEquipmentRepository
{
    IReadOnlyList<MomServiceEquipment> List();
    void Add(MomServiceEquipment item);
    void Update(MomServiceEquipment item);
}

public interface IMomServiceEquipmentLifecycleRepository
{
    IReadOnlyList<MomServiceEquipmentLifecycleEntry> List(Guid? equipmentId = null);
    void Add(MomServiceEquipmentLifecycleEntry item);
}
