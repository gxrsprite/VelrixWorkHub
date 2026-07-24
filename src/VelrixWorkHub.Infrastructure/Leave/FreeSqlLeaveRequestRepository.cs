using FreeSql;
using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Domain;
using LeaveDomain = VelrixWorkHub.Domain.OaLeaveRequest;

namespace VelrixWorkHub.Infrastructure.Leave;

public sealed class FreeSqlLeaveRequestRepository(IFreeSql fsql) : IOaLeaveRequestRepository
{
    public IReadOnlyList<LeaveDomain> List(Guid? userId = null)
    {
        var query = fsql.Select<OaLeaveRequestRecord>();
        if (userId is Guid id) query = query.Where(item => item.UserId == id);
        return query.OrderByDescending(item => item.StartAt).ToList().Select(ToDomain).ToArray();
    }

    public LeaveDomain? Get(Guid id) => fsql.Select<OaLeaveRequestRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(LeaveDomain request) => fsql.Insert(ToRecord(request)).ExecuteAffrows();

    public void Update(LeaveDomain request)
    {
        var rows = fsql.Update<OaLeaveRequestRecord>()
            .Set(item => item.LeaveType, request.LeaveType).Set(item => item.StartAt, request.StartAt).Set(item => item.EndAt, request.EndAt)
            .Set(item => item.Reason, request.Reason).Set(item => item.OtherInfo, request.OtherInfo).Set(item => item.Status, request.Status)
            .Set(item => item.RejectionReason, request.RejectionReason).Set(item => item.SubmittedAt, request.SubmittedAt)
            .Where(item => item.Id == request.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("请假申请不存在或已被删除。");
    }

    private static LeaveDomain ToDomain(OaLeaveRequestRecord item)
    {
        var request = new LeaveDomain(item.UserId, item.LeaveType, item.StartAt, item.EndAt, item.Reason, item.OtherInfo, item.CreatedAt) { Id = item.Id };
        if (item.Status == OaLeaveRequestStatus.Submitted) request.Submit(item.SubmittedAt ?? item.CreatedAt);
        else if (item.Status == OaLeaveRequestStatus.Approved) { request.Submit(item.SubmittedAt ?? item.CreatedAt); request.Approve(); }
        else if (item.Status == OaLeaveRequestStatus.Rejected) { request.Submit(item.SubmittedAt ?? item.CreatedAt); request.Reject(item.RejectionReason); }
        else if (item.Status == OaLeaveRequestStatus.Cancelled) request.Cancel();
        return request;
    }

    private static OaLeaveRequestRecord ToRecord(LeaveDomain item) => new()
    {
        Id = item.Id, UserId = item.UserId, LeaveType = item.LeaveType, StartAt = item.StartAt, EndAt = item.EndAt,
        Reason = item.Reason, OtherInfo = item.OtherInfo, Status = item.Status, RejectionReason = item.RejectionReason,
        CreatedAt = item.CreatedAt, SubmittedAt = item.SubmittedAt
    };
}
