using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class MomEquipmentTests
{
    [Fact]
    public void EquipmentCodeIsUniqueWithinWorkCenter()
    {
        var factory = new MomFactory("FACT-EQ-001", "设备工厂");
        var center = new MomWorkCenter(factory.Id, "WC-EQ-001", "设备工作中心", MomWorkCenterType.Assembly, 8);
        var centers = new InMemoryWorkCenterRepository([center]);
        var repository = new InMemoryEquipmentRepository();
        var service = new MomEquipmentService(repository, centers);

        var first = service.Create(center.Id, "EQ-001", "装配台一", "M-100");
        Assert.Equal(MomMasterDataStatus.Active, first.Status);
        Assert.Throws<InvalidOperationException>(() => service.Create(center.Id, " eq-001 ", "重复设备"));
    }

    [Fact]
    public void EquipmentCannotBeCreatedOrReactivatedForInactiveWorkCenter()
    {
        var factory = new MomFactory("FACT-EQ-002", "设备工厂");
        var center = new MomWorkCenter(factory.Id, "WC-EQ-002", "设备工作中心", MomWorkCenterType.Testing, 8);
        var centers = new InMemoryWorkCenterRepository([center]);
        var repository = new InMemoryEquipmentRepository();
        var service = new MomEquipmentService(repository, centers);
        var equipment = service.Create(center.Id, "EQ-002", "测试台");

        service.SetActive(equipment, false);
        center.SetActive(false);
        Assert.Throws<InvalidOperationException>(() => service.Create(center.Id, "EQ-003", "停用中心设备"));
        Assert.Throws<InvalidOperationException>(() => service.SetActive(equipment, true));
    }

    private sealed class InMemoryEquipmentRepository : IMomEquipmentRepository
    {
        private readonly List<MomEquipment> items = [];
        public IReadOnlyList<MomEquipment> List() => items;
        public void Add(MomEquipment item) => items.Add(item);
        public void Update(MomEquipment item) { }
    }

    private sealed class InMemoryWorkCenterRepository(IReadOnlyList<MomWorkCenter> seed) : IMomWorkCenterRepository
    {
        private readonly List<MomWorkCenter> items = seed.ToList();
        public IReadOnlyList<MomWorkCenter> List() => items;
        public void Add(MomWorkCenter item) => items.Add(item);
        public void Update(MomWorkCenter item) { }
    }
}
