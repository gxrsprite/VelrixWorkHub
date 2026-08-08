using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomWorkCenterRepository(IFreeSql fsql) : IMomWorkCenterRepository
{
    public IReadOnlyList<MomWorkCenter> List() => fsql.Select<MomWorkCenterRecord>().OrderBy(x => x.Code).ToList().Select(ToDomain).ToArray();
    public void Add(MomWorkCenter item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomWorkCenter item) { var rows = fsql.Update<MomWorkCenterRecord>().Set(x => x.Status, item.Status).Set(x => x.OtherInfo, item.OtherInfo).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("工作中心不存在或已被删除。"); }
    private static MomWorkCenter ToDomain(MomWorkCenterRecord x) => MomWorkCenter.Restore(x.Id, x.FactoryId, x.Code, x.Name, x.Type, x.StandardHoursPerDay, x.ProductionLineName, x.Status, x.OtherInfo);
    private static MomWorkCenterRecord ToRecord(MomWorkCenter x) => new() { Id = x.Id, FactoryId = x.FactoryId, Code = x.Code, Name = x.Name, Type = x.Type, ProductionLineName = x.ProductionLineName, StandardHoursPerDay = x.StandardHoursPerDay, Status = x.Status, OtherInfo = x.OtherInfo };
}
