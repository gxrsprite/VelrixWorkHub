namespace VelrixWorkHub.Domain;

public enum MomMasterDataStatus { Inactive, Active }
public enum MomWorkCenterType { ProductionLine, Assembly, Machining, Testing, Warehouse, Outsourced }

public sealed class MomFactory
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public MomMasterDataStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomFactory(string code, string name, string? otherInfo = null)
    {
        Edit(code, name, otherInfo); Status = MomMasterDataStatus.Active;
    }

    public static MomFactory Restore(Guid id, string code, string name, MomMasterDataStatus status, string? otherInfo)
    {
        var item = new MomFactory(code, name, otherInfo) { Id = id, Status = status }; return item;
    }

    public void Edit(string code, string name, string? otherInfo = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("工厂编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("工厂名称不能为空。", nameof(name));
        Code = code.Trim(); Name = name.Trim(); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetActive(bool active) => Status = active ? MomMasterDataStatus.Active : MomMasterDataStatus.Inactive;
}

public sealed class MomWorkCenter
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid FactoryId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public MomWorkCenterType Type { get; private set; }
    public string? ProductionLineName { get; private set; }
    public decimal StandardHoursPerDay { get; private set; }
    public MomMasterDataStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomWorkCenter(Guid factoryId, string code, string name, MomWorkCenterType type, decimal standardHoursPerDay, string? productionLineName = null, string? otherInfo = null)
    {
        Edit(factoryId, code, name, type, standardHoursPerDay, productionLineName, otherInfo); Status = MomMasterDataStatus.Active;
    }

    public static MomWorkCenter Restore(Guid id, Guid factoryId, string code, string name, MomWorkCenterType type, decimal standardHoursPerDay, string? productionLineName, MomMasterDataStatus status, string? otherInfo)
    {
        var item = new MomWorkCenter(factoryId, code, name, type, standardHoursPerDay, productionLineName, otherInfo) { Id = id, Status = status }; return item;
    }

    public void Edit(Guid factoryId, string code, string name, MomWorkCenterType type, decimal standardHoursPerDay, string? productionLineName = null, string? otherInfo = null)
    {
        if (factoryId == Guid.Empty) throw new ArgumentException("工作中心必须归属工厂。", nameof(factoryId));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("工作中心编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("工作中心名称不能为空。", nameof(name));
        if (standardHoursPerDay <= 0 || standardHoursPerDay > 24) throw new ArgumentOutOfRangeException(nameof(standardHoursPerDay), "标准日工时必须大于 0 且不超过 24 小时。");
        FactoryId = factoryId; Code = code.Trim(); Name = name.Trim(); Type = type; StandardHoursPerDay = standardHoursPerDay;
        ProductionLineName = string.IsNullOrWhiteSpace(productionLineName) ? null : productionLineName.Trim(); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetActive(bool active) => Status = active ? MomMasterDataStatus.Active : MomMasterDataStatus.Inactive;
}
