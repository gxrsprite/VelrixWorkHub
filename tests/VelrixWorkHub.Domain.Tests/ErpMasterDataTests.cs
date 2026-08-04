using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class ErpMasterDataTests
{
    [Fact]
    public void Product_RequiresCoreFieldsAndNormalizesValues()
    {
        var item = new Product(" SKU-1 ", " 标准包 ", " 套 ", 12.5m, " 备注 ");
        Assert.Equal("SKU-1", item.Code);
        Assert.Equal("标准包", item.Name);
        Assert.Equal("套", item.Unit);
        Assert.Equal("备注", item.Notes);
        Assert.Throws<ArgumentException>(() => new Product("", "商品", "件", 1, null));
    }

    [Fact]
    public void Product_AllowsOptionalSafetyStockAndRejectsNegativeValue()
    {
        var item = new Product("SKU-SAFETY", "安全库存商品", "件", 10m, null, safetyStock: 5m);

        Assert.Equal(5m, item.SafetyStock);
        Assert.Throws<ArgumentException>(() => new Product("SKU-NEGATIVE", "错误商品", "件", 10m, null, safetyStock: -1m));
    }

    [Fact]
    public void Product_AllowsMaximumInventoryAndRejectsNonPositiveValue()
    {
        var item = new Product("SKU-MAX-INVENTORY", "最大库存商品", "件", 10m, null, maxInventoryQuantity: 20m);

        Assert.Equal(20m, item.MaxInventoryQuantity);
        Assert.Throws<ArgumentException>(() => new Product("SKU-ZERO-INVENTORY", "错误商品", "件", 10m, null, maxInventoryQuantity: 0m));
    }

    [Fact]
    public void MasterData_PreservesOtherInfoAndRequiresJsonObject()
    {
        var product = new Product("SKU-EXT", "扩展商品", "件", 10m, null, otherInfo: "{\"brand\":\"Velrix\"}");
        var supplier = new Supplier("SUP-EXT", "扩展供应商", null, null, null, "{\"tier\":\"A\"}");
        var warehouse = new Warehouse("WH-EXT", "扩展仓", null, "{\"region\":\"east\"}");

        Assert.Equal("{\"brand\":\"Velrix\"}", product.OtherInfo);
        Assert.Equal("{\"tier\":\"A\"}", supplier.OtherInfo);
        Assert.Equal("{\"region\":\"east\"}", warehouse.OtherInfo);
        Assert.Throws<ArgumentException>(() => new Product("SKU-ARRAY", "错误扩展", "件", 1m, null, otherInfo: "[]"));
        Assert.Throws<ArgumentException>(() => new Supplier("SUP-BAD", "错误扩展", null, null, null, "not-json"));
        Assert.Throws<ArgumentException>(() => new Warehouse("WH-BAD", "错误扩展", null, "null"));
    }

    [Fact]
    public void Warehouse_CanOwnLocationsAndRejectInvalidLocation()
    {
        var warehouse = new Warehouse("WH-1", "中心仓", null);
        var location = warehouse.AddLocation(" A-01 ", " 一层 ");
        Assert.Equal(warehouse.Id, location.WarehouseId);
        Assert.Equal("A-01", location.Code);
        location.SetProductCapacity(Guid.CreateVersion7(), 10m);
        Assert.Equal(10m, location.ProductCapacities.Single().MaxQuantity);
        Assert.Throws<ArgumentException>(() => warehouse.AddLocation("", "库位"));
        Assert.Throws<ArgumentOutOfRangeException>(() => location.SetProductCapacity(Guid.CreateVersion7(), 0m));
    }

    [Fact]
    public void Orders_DefaultDueDateToThirtyDaysAndRejectEarlierDate()
    {
        var today = new DateOnly(2026, 7, 19);
        var sales = new SalesOrder("SO-DUE", Guid.NewGuid(), Guid.NewGuid(), today, 1m, 10m);
        var purchase = new PurchaseOrder("PO-DUE", Guid.NewGuid(), Guid.NewGuid(), today, 1m, 10m);

        Assert.Equal(today.AddDays(30), sales.DueDate);
        Assert.Equal(today.AddDays(30), purchase.DueDate);
        Assert.Throws<ArgumentException>(() => new SalesOrder("SO-EARLY", Guid.NewGuid(), Guid.NewGuid(), today, 1m, 10m, dueDate: today.AddDays(-1)));
        Assert.Throws<ArgumentException>(() => new PurchaseOrder("PO-EARLY", Guid.NewGuid(), Guid.NewGuid(), today, 1m, 10m, dueDate: today.AddDays(-1)));
    }
}
