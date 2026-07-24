using FreeSql;
using VelrixWorkHub.Application.Leave;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Leave;

public sealed class FreeSqlLeaveCalendarEntryRepository(IFreeSql fsql) : IOaLeaveCalendarEntryRepository
{
    public IReadOnlyList<OaLeaveCalendarEntry> List(Guid userId)
        => fsql.Select<OaLeaveCalendarEntryRecord>().Where(x => x.UserId == userId).OrderByDescending(x => x.StartAt).ToList().Select(x => ToDomain(x)!).ToArray();

    public OaLeaveCalendarEntry? GetByLeaveRequest(Guid leaveRequestId)
    {
        var item = fsql.Select<OaLeaveCalendarEntryRecord>().Where(x => x.LeaveRequestId == leaveRequestId).ToOne();
        return item is null ? null : ToDomain(item)!;
    }

    public void Add(OaLeaveCalendarEntry entry) => fsql.Insert(new OaLeaveCalendarEntryRecord
    {
        Id = entry.Id, LeaveRequestId = entry.LeaveRequestId, UserId = entry.UserId, LeaveType = entry.LeaveType,
        StartAt = entry.StartAt, EndAt = entry.EndAt, Reason = entry.Reason, CreatedAt = entry.CreatedAt
    }).ExecuteAffrows();

    private static OaLeaveCalendarEntry? ToDomain(OaLeaveCalendarEntryRecord? item)
        => item is null ? null : new OaLeaveCalendarEntry(item.LeaveRequestId, item.UserId, item.LeaveType, item.StartAt, item.EndAt, item.Reason, item.CreatedAt) { Id = item.Id };
}
