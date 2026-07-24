using FreeSql;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Settlements;

public sealed class FreeSqlSettlementRepository(IFreeSql fsql) : ISettlementRepository
{
    public IReadOnlyList<ErpSettlement> List() => fsql.Select<SettlementRecord>().ToList().Select(x => ErpSettlement.Restore(x.Id, x.ReferenceNo, x.OrderId, x.PartyId, x.Kind, x.Amount, DateOnly.FromDateTime(x.OccurredOn), x.Notes, x.Status, x.VoidReason)).ToArray();
    public void Add(ErpSettlement item) => fsql.Insert(new SettlementRecord { Id = item.Id, ReferenceNo = item.ReferenceNo, OrderId = item.OrderId, PartyId = item.PartyId, Kind = item.Kind, Amount = item.Amount, OccurredOn = item.OccurredOn.ToDateTime(TimeOnly.MinValue), Notes = item.Notes, Status = item.Status, VoidReason = item.VoidReason }).ExecuteAffrows();
    public void Update(ErpSettlement item)
    {
        if (fsql.Update<SettlementRecord>().Set(x => x.Status, item.Status).Set(x => x.VoidReason, item.VoidReason).Where(x => x.Id == item.Id).ExecuteAffrows() == 0) throw new InvalidOperationException("核销流水不存在或已被删除。");
    }
}
