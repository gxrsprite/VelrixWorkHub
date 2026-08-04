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
    public WarehouseLocation AddLocation(Guid id, string code, string name, IEnumerable<WarehouseLocationProductCapacity>? productCapacities = null)
    { var item = new WarehouseLocation(Id, code, name) { Id = id }; item.RestoreProductCapacities(productCapacities); _locations.Add(item); return item; }
}
public enum WarehouseStatus { Inactive, Active }

public sealed class WarehouseLocation
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid WarehouseId { get; init; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public IReadOnlyList<WarehouseLocationProductCapacity> ProductCapacities => _productCapacities;
    private readonly List<WarehouseLocationProductCapacity> _productCapacities = [];
    public WarehouseLocation(Guid warehouseId, string code, string name)
    {
        if (warehouseId == Guid.Empty) throw new ArgumentException("必须选择仓库。", nameof(warehouseId));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("库位编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("库位名称不能为空。", nameof(name));
        WarehouseId = warehouseId; Code = code.Trim(); Name = name.Trim();
    }
    public void SetProductCapacity(Guid productId, decimal maxQuantity)
    {
        if (productId == Guid.Empty) throw new ArgumentException("必须选择商品。", nameof(productId));
        if (maxQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(maxQuantity), "库位商品最大库存必须大于零。");
        var existing = _productCapacities.FirstOrDefault(x => x.ProductId == productId);
        if (existing is null) _productCapacities.Add(new WarehouseLocationProductCapacity(Id, productId, maxQuantity));
        else existing.SetMaxQuantity(maxQuantity);
    }
    public void RemoveProductCapacity(Guid productId) => _productCapacities.RemoveAll(x => x.ProductId == productId);
    internal void RestoreProductCapacities(IEnumerable<WarehouseLocationProductCapacity>? items)
    {
        _productCapacities.Clear();
        if (items is not null) _productCapacities.AddRange(items);
    }
}

public sealed class WarehouseLocationProductCapacity
{
    public Guid LocationId { get; init; }
    public Guid ProductId { get; init; }
    public decimal MaxQuantity { get; private set; }
    public WarehouseLocationProductCapacity(Guid locationId, Guid productId, decimal maxQuantity)
    {
        if (locationId == Guid.Empty) throw new ArgumentException("必须选择库位。", nameof(locationId));
        if (productId == Guid.Empty) throw new ArgumentException("必须选择商品。", nameof(productId));
        if (maxQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(maxQuantity), "库位商品最大库存必须大于零。");
        LocationId = locationId; ProductId = productId; MaxQuantity = maxQuantity;
    }
    public void SetMaxQuantity(decimal maxQuantity)
    {
        if (maxQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(maxQuantity), "库位商品最大库存必须大于零。");
        MaxQuantity = maxQuantity;
    }
}
