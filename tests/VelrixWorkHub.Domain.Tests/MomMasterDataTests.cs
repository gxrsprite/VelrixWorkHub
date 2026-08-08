using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomMasterDataTests
{
    [Fact]
    public void WorkCenterRequiresValidStandardHours()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MomWorkCenter(Guid.CreateVersion7(), "WC-001", "装配一线", MomWorkCenterType.Assembly, 24.1m));
        var center = new MomWorkCenter(Guid.CreateVersion7(), "WC-002", "装配二线", MomWorkCenterType.Assembly, 8m, "总装线 B");
        Assert.Equal("总装线 B", center.ProductionLineName);
    }

    [Fact]
    public void InactiveFactoryCannotCreateWorkCenter()
    {
        var factory = new MomFactory("F-001", "测试工厂"); factory.SetActive(false);
        var factories = new InMemoryFactoryRepository([factory]);
        var centers = new InMemoryWorkCenterRepository();
        var service = new MomWorkCenterService(centers, factories);

        var error = Assert.Throws<InvalidOperationException>(() => service.Create(factory.Id, "WC-001", "装配一线", MomWorkCenterType.Assembly, 8));

        Assert.Contains("工厂已停用", error.Message);
        Assert.Empty(centers.List());
    }

    private sealed class InMemoryFactoryRepository(List<MomFactory> items) : IMomFactoryRepository
    {
        public IReadOnlyList<MomFactory> List() => items;
        public void Add(MomFactory item) => items.Add(item);
        public void Update(MomFactory item) { }
    }

    private sealed class InMemoryWorkCenterRepository : IMomWorkCenterRepository
    {
        private readonly List<MomWorkCenter> items = [];
        public IReadOnlyList<MomWorkCenter> List() => items;
        public void Add(MomWorkCenter item) => items.Add(item);
        public void Update(MomWorkCenter item) { }
    }
}
