using FreeSql;
using VelrixWorkHub.Application.Mom;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Mom;

public sealed class FreeSqlMomAcceptanceRepository(IFreeSql fsql) : IMomAcceptanceRepository
{
    public IReadOnlyList<MomAcceptance> List() => fsql.Select<MomAcceptanceRecord>().OrderByDescending(x => x.PlannedDate).OrderByDescending(x => x.AcceptanceNo).ToList().Select(ToDomain).ToArray();
    public void Add(MomAcceptance item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(MomAcceptance item) => fsql.Update<MomAcceptanceRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows();

    private static MomAcceptance ToDomain(MomAcceptanceRecord x) => MomAcceptance.Restore(x.Id, x.AcceptanceNo, x.AcceptanceType, x.Status, x.SalesOrderId, x.ShipmentId, x.PmsProjectId,
        x.CustomerId, x.ProductId, x.SerialNo, DateOnly.FromDateTime(x.PlannedDate), x.LocationOrMode, x.Participants, x.CreatedBy, x.CreatedOn,
        x.SubmittedBy, x.SubmittedOn, x.CompletedBy, x.CompletedOn, x.Conclusion, x.FailureReason, x.CancelledBy, x.CancelledOn, x.CancellationReason, x.Notes, x.OtherInfo);

    private static MomAcceptanceRecord ToRecord(MomAcceptance x) => new()
    {
        Id = x.Id, AcceptanceNo = x.AcceptanceNo, AcceptanceType = x.AcceptanceType, Status = x.Status, SalesOrderId = x.SalesOrderId,
        ShipmentId = x.ShipmentId, PmsProjectId = x.PmsProjectId, CustomerId = x.CustomerId, ProductId = x.ProductId, SerialNo = x.SerialNo,
        PlannedDate = x.PlannedDate.ToDateTime(TimeOnly.MinValue), LocationOrMode = x.LocationOrMode, Participants = x.Participants,
        CreatedBy = x.CreatedBy, CreatedOn = x.CreatedOn, SubmittedBy = x.SubmittedBy, SubmittedOn = x.SubmittedOn, CompletedBy = x.CompletedBy,
        CompletedOn = x.CompletedOn, Conclusion = x.Conclusion, FailureReason = x.FailureReason, CancelledBy = x.CancelledBy, CancelledOn = x.CancelledOn,
        CancellationReason = x.CancellationReason, Notes = x.Notes, OtherInfo = x.OtherInfo
    };
}
