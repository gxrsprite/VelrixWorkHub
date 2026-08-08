using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomManufacturingOperationStandardTests
{
    [Fact]
    public void DraftRouteSupportsStandardHoursAndCrossWorkCenterAssignments()
    {
        var version = new MomManufacturingVersion(Guid.CreateVersion7(), "V1", "版本", DateOnly.FromDateTime(DateTime.Today));
        var factory = new MomFactory("F-STD", "工厂");
        var assembly = new MomWorkCenter(factory.Id, "WC-A", "装配", MomWorkCenterType.Assembly, 8);
        var testing = new MomWorkCenter(factory.Id, "WC-T", "测试", MomWorkCenterType.Testing, 8);
        var repository = new InMemoryStandardRepository();
        var service = new MomManufacturingOperationStandardService(new InMemoryVersionRepository([version]), repository, new InMemoryWorkCenterRepository([assembly, testing]));

        var standard = service.Create(version.Id, 20, "TEST-020", "成品测试", testing.Id, 0.5m, 0.25m);

        Assert.Equal(testing.Id, standard.WorkCenterId);
        Assert.Equal(3m, standard.StandardHoursFor(10));
        Assert.Single(service.List(version.Id));
    }

    [Fact]
    public void DuplicateSequenceInactiveCenterAndReleasedVersionAreRejected()
    {
        var version = new MomManufacturingVersion(Guid.CreateVersion7(), "V1", "版本", DateOnly.FromDateTime(DateTime.Today));
        var factory = new MomFactory("F-STD-2", "工厂");
        var center = new MomWorkCenter(factory.Id, "WC-STD", "工作中心", MomWorkCenterType.Assembly, 8);
        var inactive = new MomWorkCenter(factory.Id, "WC-OFF", "停用中心", MomWorkCenterType.Testing, 8);
        inactive.SetActive(false);
        var repository = new InMemoryStandardRepository();
        var service = new MomManufacturingOperationStandardService(new InMemoryVersionRepository([version]), repository, new InMemoryWorkCenterRepository([center, inactive]));

        service.Create(version.Id, 10, "OP-010", "装配", center.Id, 1, 0);
        Assert.Throws<InvalidOperationException>(() => service.Create(version.Id, 10, "OP-011", "重复顺序", center.Id, 1, 0.1m));
        Assert.Throws<InvalidOperationException>(() => service.Create(version.Id, 20, "OP-020", "停用中心", inactive.Id, 1, 0.1m));
        version.Release();
        Assert.Throws<InvalidOperationException>(() => service.Create(version.Id, 20, "OP-020", "已发布", center.Id, 1, 0.1m));
    }

    private sealed class InMemoryStandardRepository : IMomManufacturingOperationStandardRepository
    {
        private readonly List<MomManufacturingOperationStandard> items = [];
        public IReadOnlyList<MomManufacturingOperationStandard> List() => items;
        public void Add(MomManufacturingOperationStandard item) => items.Add(item);
        public void Update(MomManufacturingOperationStandard item) { }
        public void Remove(Guid id) => items.RemoveAll(x => x.Id == id);
    }

    private sealed class InMemoryVersionRepository(IReadOnlyList<MomManufacturingVersion> seed) : IMomManufacturingVersionRepository
    {
        private readonly List<MomManufacturingVersion> items = seed.ToList();
        public IReadOnlyList<MomManufacturingVersion> List() => items;
        public void Add(MomManufacturingVersion item) => items.Add(item);
        public void Update(MomManufacturingVersion item) { }
    }

    private sealed class InMemoryWorkCenterRepository(IReadOnlyList<MomWorkCenter> seed) : IMomWorkCenterRepository
    {
        private readonly List<MomWorkCenter> items = seed.ToList();
        public IReadOnlyList<MomWorkCenter> List() => items;
        public void Add(MomWorkCenter item) => items.Add(item);
        public void Update(MomWorkCenter item) { }
    }
}
