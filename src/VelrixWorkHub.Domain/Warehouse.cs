namespace VelrixWorkHub.Domain;

public sealed class Warehouse
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public WarehouseStatus Status { get; private set; }
    public IReadOnlyList<WarehouseLocation> Locations => _locations;
    private readonly List<WarehouseLocation> _locations = [];

    public Warehouse(string code, string name, string? address, string? otherInfo = null)
    { Edit(code, name, address, otherInfo); Status = WarehouseStatus.Active; }
    public void Edit(string code, string name, string? address, string? otherInfo = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("仓库编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("仓库名称不能为空。", nameof(name));
        Code = code.Trim(); Name = name.Trim(); Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }
    public void SetActive(bool active) => Status = active ? WarehouseStatus.Active : WarehouseStatus.Inactive;
    public WarehouseLocation AddLocation(string code, string name)
    { var item = new WarehouseLocation(Id, code, name); _locations.Add(item); return item; }
    public WarehouseLocation AddLocation(Guid id, string code, string name)
    { var item = new WarehouseLocation(Id, code, name) { Id = id }; _locations.Add(item); return item; }
}
public enum WarehouseStatus { Inactive, Active }

public sealed class WarehouseLocation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid WarehouseId { get; init; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public WarehouseLocation(Guid warehouseId, string code, string name)
    {
        if (warehouseId == Guid.Empty) throw new ArgumentException("必须选择仓库。", nameof(warehouseId));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("库位编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("库位名称不能为空。", nameof(name));
        WarehouseId = warehouseId; Code = code.Trim(); Name = name.Trim();
    }
}
