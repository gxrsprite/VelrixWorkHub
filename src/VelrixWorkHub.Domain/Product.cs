namespace VelrixWorkHub.Domain;

public sealed class Product
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Unit { get; private set; } = string.Empty;
    public decimal? SalePrice { get; private set; }
    public decimal? MaxPurchaseQuantity { get; private set; }
    public decimal? SafetyStock { get; private set; }
    public string OtherInfo { get; private set; } = "{}";
    public ProductStatus Status { get; private set; }
    public string? Notes { get; private set; }

    public Product(string code, string name, string unit, decimal? salePrice, string? notes, decimal? maxPurchaseQuantity = null, decimal? safetyStock = null, string? otherInfo = null)
    { Edit(code, name, unit, salePrice, notes, maxPurchaseQuantity, safetyStock, otherInfo); Status = ProductStatus.Active; }

    public void Edit(string code, string name, string unit, decimal? salePrice, string? notes, decimal? maxPurchaseQuantity = null, decimal? safetyStock = null, string? otherInfo = null)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("商品编码不能为空。", nameof(code));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("商品名称不能为空。", nameof(name));
        if (string.IsNullOrWhiteSpace(unit)) throw new ArgumentException("计量单位不能为空。", nameof(unit));
        if (salePrice < 0) throw new ArgumentException("销售单价不能为负数。", nameof(salePrice));
        if (maxPurchaseQuantity <= 0) throw new ArgumentException("单次最大采购量必须大于 0。", nameof(maxPurchaseQuantity));
        if (safetyStock < 0) throw new ArgumentException("安全库存不能为负数。", nameof(safetyStock));
        Code = code.Trim(); Name = name.Trim(); Unit = unit.Trim(); SalePrice = salePrice; MaxPurchaseQuantity = maxPurchaseQuantity; SafetyStock = safetyStock; Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(); OtherInfo = JsonObjectValue.Normalize(otherInfo, nameof(otherInfo));
    }
    public void SetActive(bool active) => Status = active ? ProductStatus.Active : ProductStatus.Inactive;
}

public enum ProductStatus { Inactive, Active }
