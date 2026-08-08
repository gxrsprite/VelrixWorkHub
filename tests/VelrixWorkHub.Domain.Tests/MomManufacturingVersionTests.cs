using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Application.Products;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomManufacturingVersionTests
{
    [Fact]
    public void DraftManufacturingVersionCanAddComponentAndRelease()
    {
        var parent = new Product("FG-001", "成品 A", "套", 100, null);
        var component = new Product("RM-001", "组件 A", "件", 10, null);
        var versionRepository = new InMemoryVersionRepository();
        var componentRepository = new InMemoryComponentRepository();
        var service = new MomManufacturingVersionService(versionRepository, componentRepository, new InMemoryProductRepository([parent, component]));

        var version = service.Create(parent.Id, "V1.0", "标准版本", DateOnly.FromDateTime(DateTime.Today), engineeringChangeReference: "ECR-001");
        service.AddComponent(version.Id, 10, component.Id, 2.5m, 3.5m, 20, "主装配件");
        service.Release(version);

        Assert.Equal(MomManufacturingVersionStatus.Released, version.Status);
        var saved = Assert.Single(service.ListComponents(version.Id));
        Assert.Equal(2.5m, saved.QuantityPer);
        Assert.Equal(3.5m, saved.ScrapRatePercent);
        Assert.Throws<InvalidOperationException>(() => service.AddComponent(version.Id, 20, component.Id, 1));
    }

    [Fact]
    public void ReleaseRequiresComponentsAndRejectsOverlappingReleasedVersions()
    {
        var parent = new Product("FG-002", "成品 B", "套", 100, null);
        var component = new Product("RM-002", "组件 B", "件", 10, null);
        var versionRepository = new InMemoryVersionRepository();
        var componentRepository = new InMemoryComponentRepository();
        var service = new MomManufacturingVersionService(versionRepository, componentRepository, new InMemoryProductRepository([parent, component]));
        var today = DateOnly.FromDateTime(DateTime.Today);

        var first = service.Create(parent.Id, "V1.0", "第一版", today, today.AddDays(10));
        Assert.Throws<InvalidOperationException>(() => service.Release(first));
        service.AddComponent(first.Id, 10, component.Id, 1);
        service.Release(first);

        var overlapping = service.Create(parent.Id, "V2.0", "重叠版", today.AddDays(5), today.AddDays(20));
        service.AddComponent(overlapping.Id, 10, component.Id, 1);
        var error = Assert.Throws<InvalidOperationException>(() => service.Release(overlapping));

        Assert.Contains("有效期重叠", error.Message);
        Assert.Equal(MomManufacturingVersionStatus.Draft, overlapping.Status);
    }

    [Fact]
    public void InactiveProductCannotBeUsedByManufacturingVersion()
    {
        var product = new Product("FG-003", "停用成品", "套", 100, null);
        product.SetActive(false);
        var service = new MomManufacturingVersionService(new InMemoryVersionRepository(), new InMemoryComponentRepository(), new InMemoryProductRepository([product]));

        var error = Assert.Throws<InvalidOperationException>(() => service.Create(product.Id, "V1.0", "版本", DateOnly.FromDateTime(DateTime.Today)));

        Assert.Contains("停用商品", error.Message);
    }

    [Fact]
    public void ReleaseRechecksComponentProductStatus()
    {
        var parent = new Product("FG-004", "成品 C", "套", 100, null);
        var component = new Product("RM-004", "组件 C", "件", 10, null);
        var products = new List<Product> { parent, component };
        var service = new MomManufacturingVersionService(new InMemoryVersionRepository(), new InMemoryComponentRepository(), new InMemoryProductRepository(products));
        var version = service.Create(parent.Id, "V1.0", "版本", DateOnly.FromDateTime(DateTime.Today));
        service.AddComponent(version.Id, 10, component.Id, 1);
        component.SetActive(false);

        var error = Assert.Throws<InvalidOperationException>(() => service.Release(version));

        Assert.Contains("停用商品", error.Message);
        Assert.Equal(MomManufacturingVersionStatus.Draft, version.Status);
    }

    private sealed class InMemoryVersionRepository : IMomManufacturingVersionRepository
    {
        private readonly List<MomManufacturingVersion> items = [];
        public IReadOnlyList<MomManufacturingVersion> List() => items;
        public void Add(MomManufacturingVersion item) => items.Add(item);
        public void Update(MomManufacturingVersion item) { }
    }

    private sealed class InMemoryComponentRepository : IMomManufacturingComponentRepository
    {
        private readonly List<MomManufacturingComponent> items = [];
        public IReadOnlyList<MomManufacturingComponent> List() => items;
        public void Add(MomManufacturingComponent item) => items.Add(item);
        public void Update(MomManufacturingComponent item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryProductRepository(List<Product> items) : IProductRepository
    {
        public IReadOnlyList<Product> List() => items;
        public void Add(Product item) => items.Add(item);
        public void Update(Product item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }
}
