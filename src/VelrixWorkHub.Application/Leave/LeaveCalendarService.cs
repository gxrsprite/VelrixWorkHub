using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Leave;

public interface IOaLeaveCalendarEntryRepository
{
    IReadOnlyList<OaLeaveCalendarEntry> List(Guid userId);
    OaLeaveCalendarEntry? GetByLeaveRequest(Guid leaveRequestId);
    void Add(OaLeaveCalendarEntry entry);
}

public sealed class LeaveCalendarService(IOaLeaveCalendarEntryRepository entries)
{
    public IReadOnlyList<OaLeaveCalendarEntry> ListMine(Guid userId)
        => userId == Guid.Empty ? [] : entries.List(userId).OrderByDescending(x => x.StartAt).ToArray();

    public OaLeaveCalendarEntry CreateForApproval(OaLeaveRequest request)
    {
        if (request.Status != OaLeaveRequestStatus.Submitted)
            throw new InvalidOperationException("只有审批中的请假申请才能写入请假日历。");
        var existing = entries.GetByLeaveRequest(request.Id);
        if (existing is not null) return existing;

        var entry = new OaLeaveCalendarEntry(request.Id, request.UserId, request.LeaveType, request.StartAt, request.EndAt, request.Reason, DateTime.Now);
        entries.Add(entry);
        return entry;
    }
}
