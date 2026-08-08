using FreeSql;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public static class MomMasterDataSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        var factory = fsql.Select<MomFactoryRecord>().Where(x => x.Code == "FACTORY-SH").First();
        if (factory is null)
        {
            factory = new MomFactoryRecord { Id = Guid.CreateVersion7(), Code = "FACTORY-SH", Name = "Velrix 上海工厂", Status = MomMasterDataStatus.Active, OtherInfo = "{}" };
            fsql.Insert(factory).ExecuteAffrows();
        }
        if (!fsql.Select<MomWorkCenterRecord>().Any(x => x.FactoryId == factory.Id && x.Code == "WC-ASSEMBLY-01"))
        {
            fsql.Insert(new MomWorkCenterRecord { Id = Guid.CreateVersion7(), FactoryId = factory.Id, Code = "WC-ASSEMBLY-01", Name = "总装一线", Type = MomWorkCenterType.Assembly, ProductionLineName = "总装线 A", StandardHoursPerDay = 8, Status = MomMasterDataStatus.Active, OtherInfo = "{}" }).ExecuteAffrows();
        }
    }
}
