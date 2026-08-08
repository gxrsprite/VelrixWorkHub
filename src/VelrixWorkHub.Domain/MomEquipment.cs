namespace VelrixWorkHub.Domain;

/// <summary>
/// MOM 工作中心设备主数据。设备不等同于工作中心，执行记录只保存稳定设备引用和名称快照。
/// </summary>
public sealed class MomEquipment
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid WorkCenterId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Model { get; private set; }
    public MomMasterDataStatus Status { get; private set; }
    public string OtherInfo { get; private set; } = "{}";

    public MomEquipment(Guid workCenterId, string code, string name, string? model = null, string? otherInfo = null)
    {
        Edit(workCenterId, code, name, model, otherInfo); Status = MomMasterDataStatus.Active;
    }

    public static MomEquipment Restore(Guid id, Guid workCenterId, string code, string name, string? model,
        MomMasterDataStatus status, string? otherInfo)
        => new(workCenterId, code, name, model, otherInfo) { Id = id, Status = status };

    public void Edit(Guid workCenterId, string code, string name, string? model = null, string? otherInfo = null)
    {
        if (workCenterId == Guid.Empty) throw new ArgumentException("设备必须归属工作中心。", nameof(workCenterId));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("设备编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("设备名称不能为空。", nameof(name));
        WorkCenterId = workCenterId; Code = code.Trim(); Name = name.Trim();
        Model = string.IsNullOrWhiteSpace(model) ? null : model.Trim(); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }

    public void SetActive(bool active) => Status = active ? MomMasterDataStatus.Active : MomMasterDataStatus.Inactive;
}
