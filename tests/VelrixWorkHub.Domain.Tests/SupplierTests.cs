using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class SupplierTests
{
    [Fact]
    public void Supplier_NormalizesValuesAndRequiresCodeAndName()
    {
        var item = new Supplier(" SUP-1 ", " 供应商 ", " 周经理 ", " 13900000000 ", " 备注 ");
        Assert.Equal("SUP-1", item.Code);
        Assert.Equal("供应商", item.Name);
        Assert.Equal("周经理", item.ContactName);
        Assert.Throws<ArgumentException>(() => new Supplier("", "供应商", null, null, null));
        Assert.Throws<ArgumentException>(() => new Supplier("SUP-2", "", null, null, null));
    }

    [Fact]
    public void Supplier_CanBeDisabled()
    {
        var item = new Supplier("SUP-1", "供应商", null, null, null);
        item.SetActive(false);
        Assert.Equal(SupplierStatus.Inactive, item.Status);
    }
}
