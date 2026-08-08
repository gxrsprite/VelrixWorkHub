using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomManufacturingOperationStandardRepository(IFreeSql fsql) : IMomManufacturingOperationStandardRepository
{
    public IReadOnlyList<MomManufacturingOperationStandard> List() => fsql.Select<MomManufacturingOperationStandardRecord>()
        .OrderBy(x => x.ManufacturingVersionId).OrderBy(x => x.OperationSequence).ToList().Select(ToDomain).ToArray();
    public void Add(MomManufacturingOperationStandard item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomManufacturingOperationStandard item)
    {
        var rows = fsql.Update<MomManufacturingOperationStandardRecord>()
            .Set(x => x.OperationSequence, item.OperationSequence).Set(x => x.OperationCode, item.OperationCode).Set(x => x.OperationName, item.OperationName)
            .Set(x => x.WorkCenterId, item.WorkCenterId).Set(x => x.SetupHours, item.SetupHours).Set(x => x.RunHoursPerUnit, item.RunHoursPerUnit)
            .Set(x => x.OtherInfo, item.OtherInfo).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("工序标准不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<MomManufacturingOperationStandardRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static MomManufacturingOperationStandard ToDomain(MomManufacturingOperationStandardRecord x) => MomManufacturingOperationStandard.Restore(x.Id, x.ManufacturingVersionId, x.OperationSequence, x.OperationCode, x.OperationName, x.WorkCenterId, x.SetupHours, x.RunHoursPerUnit, x.OtherInfo);
    private static MomManufacturingOperationStandardRecord ToRecord(MomManufacturingOperationStandard x) => new() { Id = x.Id, ManufacturingVersionId = x.ManufacturingVersionId, OperationSequence = x.OperationSequence, OperationCode = x.OperationCode, OperationName = x.OperationName, WorkCenterId = x.WorkCenterId, SetupHours = x.SetupHours, RunHoursPerUnit = x.RunHoursPerUnit, OtherInfo = x.OtherInfo };
}
