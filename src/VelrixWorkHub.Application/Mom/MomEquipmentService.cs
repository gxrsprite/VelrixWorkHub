using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public sealed class MomEquipmentService(IMomEquipmentRepository repository, IMomWorkCenterRepository workCenterRepository) : IMomEquipmentResolver
{
    public IReadOnlyList<MomEquipment> List(Guid? workCenterId = null, MomMasterDataStatus? status = null)
    {
        var query = repository.List().AsEnumerable();
        if (workCenterId is Guid selectedWorkCenterId) query = query.Where(x => x.WorkCenterId == selectedWorkCenterId);
        if (status is MomMasterDataStatus selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        return query.OrderBy(x => x.WorkCenterId).ThenBy(x => x.Code).ToArray();
    }

    public MomEquipment Create(Guid workCenterId, string code, string name, string? model = null, string? otherInfo = null)
    {
        EnsureWorkCenterActive(workCenterId);
        var item = new MomEquipment(workCenterId, code, name, model, otherInfo);
        if (repository.List().Any(x => x.WorkCenterId == workCenterId && x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("同一工作中心下设备编码已存在。");
        repository.Add(item); return item;
    }

    public void SetActive(MomEquipment item, bool active)
    {
        if (active) EnsureWorkCenterActive(item.WorkCenterId);
        item.SetActive(active); repository.Update(item);
    }

    public IReadOnlyList<MomEquipmentOption> ListActive(Guid? workCenterId = null)
    {
        var activeCenters = workCenterRepository.List().Where(x => x.Status == MomMasterDataStatus.Active).Select(x => x.Id).ToHashSet();
        return List(workCenterId, MomMasterDataStatus.Active)
            .Where(x => activeCenters.Contains(x.WorkCenterId))
            .Select(x => new MomEquipmentOption(x.Id, x.WorkCenterId, x.Code, x.Name, x.Model))
            .ToArray();
    }

    public MomEquipmentOption? GetActive(Guid equipmentId) => equipmentId == Guid.Empty ? null : ListActive().FirstOrDefault(x => x.Id == equipmentId);

    private void EnsureWorkCenterActive(Guid workCenterId)
    {
        var center = workCenterRepository.List().FirstOrDefault(x => x.Id == workCenterId) ?? throw new InvalidOperationException("工作中心不存在。");
        if (center.Status != MomMasterDataStatus.Active) throw new InvalidOperationException("工作中心已停用，不能使用设备主数据。");
    }
}
