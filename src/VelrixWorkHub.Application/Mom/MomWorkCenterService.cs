using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public sealed class MomWorkCenterService(IMomWorkCenterRepository repository, IMomFactoryRepository factoryRepository)
{
    public IReadOnlyList<MomWorkCenter> List(Guid? factoryId = null, MomMasterDataStatus? status = null)
    {
        var query = repository.List().AsEnumerable(); if (factoryId is Guid selectedFactory) query = query.Where(x => x.FactoryId == selectedFactory); if (status is MomMasterDataStatus selectedStatus) query = query.Where(x => x.Status == selectedStatus);
        return query.OrderBy(x => x.FactoryId).ThenBy(x => x.Code).ToArray();
    }

    public MomWorkCenter Create(Guid factoryId, string code, string name, MomWorkCenterType type, decimal standardHoursPerDay, string? productionLineName = null, string? otherInfo = null)
    {
        EnsureFactoryActive(factoryId); var item = new MomWorkCenter(factoryId, code, name, type, standardHoursPerDay, productionLineName, otherInfo);
        if (repository.List().Any(x => x.FactoryId == factoryId && x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("同一工厂下工作中心编码已存在。");
        repository.Add(item); return item;
    }

    public void SetActive(MomWorkCenter item, bool active)
    {
        if (active) EnsureFactoryActive(item.FactoryId); item.SetActive(active); repository.Update(item);
    }

    public void EnsureExecutable(Guid workCenterId)
    {
        var item = repository.List().FirstOrDefault(x => x.Id == workCenterId) ?? throw new InvalidOperationException("工作中心不存在。");
        if (item.Status != MomMasterDataStatus.Active) throw new InvalidOperationException("工作中心已停用，不能下达制造工单。");
        EnsureFactoryActive(item.FactoryId);
    }

    private void EnsureFactoryActive(Guid factoryId)
    {
        var factory = factoryRepository.List().FirstOrDefault(x => x.Id == factoryId) ?? throw new InvalidOperationException("工厂不存在。");
        if (factory.Status != MomMasterDataStatus.Active) throw new InvalidOperationException("工厂已停用，不能使用制造主数据。");
    }
}
