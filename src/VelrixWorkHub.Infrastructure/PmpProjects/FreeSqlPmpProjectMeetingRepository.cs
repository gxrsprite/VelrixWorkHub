using FreeSql;
using VelrixWorkHub.Application.PmpProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmpProjects;

public sealed class FreeSqlPmpProjectMeetingRepository(IFreeSql fsql) : IPmpProjectMeetingRepository
{
    public IReadOnlyList<PmpProjectMeeting> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmpProjectMeetingRecord>();
        if (projectId is Guid id) query = query.Where(x => x.ProjectId == id);
        return query.OrderByDescending(x => x.StartsAt).ToList().Select(ToDomain).ToArray();
    }

    public void Add(PmpProjectMeeting item)
    {
        var now = DateTime.Now;
        fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows();
    }

    public void Update(PmpProjectMeeting item)
    {
        var rows = fsql.Update<PmpProjectMeetingRecord>()
            .SetSource(ToRecord(item, DateTime.MinValue, DateTime.Now))
            .IgnoreColumns(x => new { x.CreatedTime })
            .Where(x => x.Id == item.Id)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("会议不存在或已被删除。");
    }

    public void Remove(Guid id) => fsql.Delete<PmpProjectMeetingRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static PmpProjectMeeting ToDomain(PmpProjectMeetingRecord x) => PmpProjectMeeting.Restore(x.Id, x.ProjectId, x.Title, x.MeetingType, x.StartsAt, x.EndsAt, x.LocationOrMode, x.HostName, x.ParticipantNames, x.Minutes, x.Decisions, x.OtherInfo);
    private static PmpProjectMeetingRecord ToRecord(PmpProjectMeeting x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, Title = x.Title, MeetingType = x.MeetingType, StartsAt = x.StartsAt, EndsAt = x.EndsAt, LocationOrMode = x.LocationOrMode, HostName = x.HostName, ParticipantNames = x.ParticipantNames, Minutes = x.Minutes, Decisions = x.Decisions, OtherInfo = x.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}
