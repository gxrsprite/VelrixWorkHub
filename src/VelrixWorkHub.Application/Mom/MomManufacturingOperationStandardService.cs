using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Mom;

public sealed class MomManufacturingOperationStandardService(
    IMomManufacturingVersionRepository versionRepository,
    IMomManufacturingOperationStandardRepository standardRepository,
    IMomWorkCenterRepository workCenterRepository)
{
    public IReadOnlyList<MomManufacturingOperationStandard> List(Guid? manufacturingVersionId = null)
    {
        var query = standardRepository.List().AsEnumerable();
        if (manufacturingVersionId is Guid id) query = query.Where(x => x.ManufacturingVersionId == id);
        return query.OrderBy(x => x.ManufacturingVersionId).ThenBy(x => x.OperationSequence).ToArray();
    }

    public MomManufacturingOperationStandard Create(Guid manufacturingVersionId, int operationSequence, string operationCode,
        string operationName, Guid workCenterId, decimal setupHours, decimal runHoursPerUnit, string? otherInfo = null)
    {
        var version = FindDraftVersion(manufacturingVersionId);
        EnsureActiveWorkCenter(workCenterId);
        var item = new MomManufacturingOperationStandard(version.Id, operationSequence, operationCode, operationName, workCenterId, setupHours, runHoursPerUnit, otherInfo);
        EnsureUnique(item);
        standardRepository.Add(item);
        return item;
    }

    public void Edit(MomManufacturingOperationStandard item, int operationSequence, string operationCode,
        string operationName, Guid workCenterId, decimal setupHours, decimal runHoursPerUnit, string? otherInfo = null)
    {
        FindDraftVersion(item.ManufacturingVersionId);
        EnsureActiveWorkCenter(workCenterId);
        item.Edit(item.ManufacturingVersionId, operationSequence, operationCode, operationName, workCenterId, setupHours, runHoursPerUnit, otherInfo);
        EnsureUnique(item);
        standardRepository.Update(item);
    }

    public void Remove(MomManufacturingOperationStandard item)
    {
        FindDraftVersion(item.ManufacturingVersionId);
        standardRepository.Remove(item.Id);
    }

    private MomManufacturingVersion FindDraftVersion(Guid id)
    {
        var version = versionRepository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("制造版本不存在。");
        if (version.Status != MomManufacturingVersionStatus.Draft) throw new InvalidOperationException("只有草稿制造版本可以维护工序标准。");
        return version;
    }

    private void EnsureActiveWorkCenter(Guid id)
    {
        var center = workCenterRepository.List().FirstOrDefault(x => x.Id == id) ?? throw new InvalidOperationException("工作中心不存在。");
        if (center.Status != MomMasterDataStatus.Active) throw new InvalidOperationException("工作中心已停用，不能用于工序标准。");
    }

    private void EnsureUnique(MomManufacturingOperationStandard item)
    {
        if (standardRepository.List().Any(x => x.Id != item.Id && x.ManufacturingVersionId == item.ManufacturingVersionId && x.OperationSequence == item.OperationSequence))
            throw new InvalidOperationException("同一制造版本的工序顺序已存在。");
        if (standardRepository.List().Any(x => x.Id != item.Id && x.ManufacturingVersionId == item.ManufacturingVersionId && x.OperationCode.Equals(item.OperationCode, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("同一制造版本的工序编码已存在。");
    }
}
