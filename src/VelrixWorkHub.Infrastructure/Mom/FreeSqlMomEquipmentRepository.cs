using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomEquipmentRepository(IFreeSql fsql) : IMomEquipmentRepository
{
    public IReadOnlyList<MomEquipment> List() => fsql.Select<MomEquipmentRecord>().OrderBy(x => x.WorkCenterId).OrderBy(x => x.Code).ToList().Select(ToDomain).ToArray();
    public void Add(MomEquipment item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomEquipment item)
    {
        var rows = fsql.Update<MomEquipmentRecord>().Set(x => x.Status, item.Status).Set(x => x.OtherInfo, item.OtherInfo).Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("设备不存在或已被删除。");
    }

    private static MomEquipment ToDomain(MomEquipmentRecord x) => MomEquipment.Restore(x.Id, x.WorkCenterId, x.Code, x.Name, x.Model, x.Status, x.OtherInfo);
    private static MomEquipmentRecord ToRecord(MomEquipment x) => new()
    {
        Id = x.Id, WorkCenterId = x.WorkCenterId, Code = x.Code, Name = x.Name, Model = x.Model, Status = x.Status, OtherInfo = x.OtherInfo
    };
}
