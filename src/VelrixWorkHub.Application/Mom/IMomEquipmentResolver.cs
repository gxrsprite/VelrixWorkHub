namespace VelrixWorkHub.Application.Mom;

public sealed record MomEquipmentOption(Guid Id, Guid WorkCenterId, string Code, string Name, string? Model);

public interface IMomEquipmentResolver
{
    IReadOnlyList<MomEquipmentOption> ListActive(Guid? workCenterId = null);
    MomEquipmentOption? GetActive(Guid equipmentId);
}
