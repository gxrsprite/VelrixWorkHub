using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomFactoryRepository(IFreeSql fsql) : IMomFactoryRepository
{
    public IReadOnlyList<MomFactory> List() => fsql.Select<MomFactoryRecord>().OrderBy(x => x.Code).ToList().Select(ToDomain).ToArray();
    public void Add(MomFactory item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomFactory item) { var rows = fsql.Update<MomFactoryRecord>().Set(x => x.Status, item.Status).Set(x => x.OtherInfo, item.OtherInfo).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("工厂不存在或已被删除。"); }
    private static MomFactory ToDomain(MomFactoryRecord x) => MomFactory.Restore(x.Id, x.Code, x.Name, x.Status, x.OtherInfo);
    private static MomFactoryRecord ToRecord(MomFactory x) => new() { Id = x.Id, Code = x.Code, Name = x.Name, Status = x.Status, OtherInfo = x.OtherInfo };
}
