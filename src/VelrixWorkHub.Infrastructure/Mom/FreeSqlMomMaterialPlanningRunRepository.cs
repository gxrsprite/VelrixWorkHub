using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomMaterialPlanningRunRepository(IFreeSql fsql) : IMomMaterialPlanningRunRepository
{
    public IReadOnlyList<MomMaterialPlanningRun> List() => fsql.Select<MomMaterialPlanningRunRecord>().OrderByDescending(x => x.ReferenceDate).OrderByDescending(x => x.PlanNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomMaterialPlanningRun item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomMaterialPlanningRun item)
    {
        var rows = fsql.Update<MomMaterialPlanningRunRecord>().Set(x => x.Status, item.Status).Set(x => x.OtherInfo, item.OtherInfo).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("MRP 计划批次不存在或已被删除。");
    }

    private static MomMaterialPlanningRun ToDomain(MomMaterialPlanningRunRecord x) => MomMaterialPlanningRun.Restore(x.Id, x.PlanNo, DateOnly.FromDateTime(x.ReferenceDate), DateOnly.FromDateTime(x.HorizonDate), x.Status, x.OtherInfo);
    private static MomMaterialPlanningRunRecord ToRecord(MomMaterialPlanningRun x) => new() { Id = x.Id, PlanNo = x.PlanNo, ReferenceDate = x.ReferenceDate.ToDateTime(TimeOnly.MinValue), HorizonDate = x.HorizonDate.ToDateTime(TimeOnly.MinValue), Status = x.Status, OtherInfo = x.OtherInfo };
}
