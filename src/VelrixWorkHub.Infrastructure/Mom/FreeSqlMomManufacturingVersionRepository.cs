using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomManufacturingVersionRepository(IFreeSql fsql) : IMomManufacturingVersionRepository
{
    public IReadOnlyList<MomManufacturingVersion> List() => fsql.Select<MomManufacturingVersionRecord>().OrderBy(x => x.ProductId).OrderByDescending(x => x.EffectiveFrom).ToList().Select(ToDomain).ToArray();
    public void Add(MomManufacturingVersion item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomManufacturingVersion item)
    {
        var rows = fsql.Update<MomManufacturingVersionRecord>()
            .Set(x => x.ProductId, item.ProductId)
            .Set(x => x.VersionCode, item.VersionCode)
            .Set(x => x.Name, item.Name)
            .Set(x => x.EffectiveFrom, item.EffectiveFrom.ToDateTime(TimeOnly.MinValue))
            .Set(x => x.EffectiveTo, item.EffectiveTo?.ToDateTime(TimeOnly.MinValue))
            .Set(x => x.EngineeringChangeReference, item.EngineeringChangeReference)
            .Set(x => x.Status, item.Status)
            .Set(x => x.OtherInfo, item.OtherInfo)
            .Where(x => x.Id == item.Id)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("制造版本不存在或已被删除。");
    }

    private static MomManufacturingVersion ToDomain(MomManufacturingVersionRecord x) => MomManufacturingVersion.Restore(x.Id, x.ProductId, x.VersionCode, x.Name, DateOnly.FromDateTime(x.EffectiveFrom), x.EffectiveTo is null ? null : DateOnly.FromDateTime(x.EffectiveTo.Value), x.EngineeringChangeReference, x.Status, x.OtherInfo);
    private static MomManufacturingVersionRecord ToRecord(MomManufacturingVersion x) => new() { Id = x.Id, ProductId = x.ProductId, VersionCode = x.VersionCode, Name = x.Name, EffectiveFrom = x.EffectiveFrom.ToDateTime(TimeOnly.MinValue), EffectiveTo = x.EffectiveTo?.ToDateTime(TimeOnly.MinValue), EngineeringChangeReference = x.EngineeringChangeReference, Status = x.Status, OtherInfo = x.OtherInfo };
}
