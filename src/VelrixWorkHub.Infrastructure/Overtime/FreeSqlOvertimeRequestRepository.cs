using FreeSql;
using VelrixWorkHub.Application.Overtime;
using VelrixWorkHub.Domain;
using OvertimeDomain = VelrixWorkHub.Domain.OaOvertimeRequest;

namespace VelrixWorkHub.Infrastructure.Overtime;

public sealed class FreeSqlOvertimeRequestRepository(IFreeSql fsql) : IOaOvertimeRequestRepository
{
    public IReadOnlyList<OvertimeDomain> List(Guid? userId = null)
    {
        var query = fsql.Select<OaOvertimeRequestRecord>();
        if (userId is Guid id) query = query.Where(item => item.UserId == id);
        return query.OrderByDescending(item => item.StartAt).ToList().Select(ToDomain).ToArray();
    }

    public OvertimeDomain? Get(Guid id) => fsql.Select<OaOvertimeRequestRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OvertimeDomain request) => fsql.Insert(ToRecord(request)).ExecuteAffrows();

    public void Update(OvertimeDomain request)
    {
        var rows = fsql.Update<OaOvertimeRequestRecord>()
            .Set(item => item.StartAt, request.StartAt).Set(item => item.EndAt, request.EndAt)
            .Set(item => item.Reason, request.Reason).Set(item => item.OtherInfo, request.OtherInfo).Set(item => item.Status, request.Status)
            .Set(item => item.RejectionReason, request.RejectionReason).Set(item => item.SubmittedAt, request.SubmittedAt)
            .Where(item => item.Id == request.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("加班申请不存在或已被删除。");
    }

    private static OvertimeDomain ToDomain(OaOvertimeRequestRecord item)
    {
        var request = new OvertimeDomain(item.UserId, item.StartAt, item.EndAt, item.Reason, item.OtherInfo, item.CreatedAt) { Id = item.Id };
        if (item.Status == OaOvertimeRequestStatus.Submitted) request.Submit(item.SubmittedAt ?? item.CreatedAt);
        else if (item.Status == OaOvertimeRequestStatus.Approved) { request.Submit(item.SubmittedAt ?? item.CreatedAt); request.Approve(); }
        else if (item.Status == OaOvertimeRequestStatus.Rejected) { request.Submit(item.SubmittedAt ?? item.CreatedAt); request.Reject(item.RejectionReason); }
        else if (item.Status == OaOvertimeRequestStatus.Cancelled) request.Cancel();
        return request;
    }

    private static OaOvertimeRequestRecord ToRecord(OvertimeDomain item) => new()
    {
        Id = item.Id, UserId = item.UserId, StartAt = item.StartAt, EndAt = item.EndAt, Reason = item.Reason,
        OtherInfo = item.OtherInfo, Status = item.Status, RejectionReason = item.RejectionReason,
        CreatedAt = item.CreatedAt, SubmittedAt = item.SubmittedAt
    };
}
