using FreeSql;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.PmsProjects;

public sealed class FreeSqlPmsProjectMeetingRepository(IFreeSql fsql) : IPmsProjectMeetingRepository
{
    public IReadOnlyList<PmsProjectMeeting> List(Guid? projectId = null)
    {
        var query = fsql.Select<PmsProjectMeetingRecord>();
        if (projectId is Guid id) query = query.Where(x => x.ProjectId == id);
        return query.OrderByDescending(x => x.StartsAt).ToList().Select(ToDomain).ToArray();
    }

    public void Add(PmsProjectMeeting item)
    {
        var now = DateTime.Now;
        fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows();
    }

    public void Update(PmsProjectMeeting item)
    {
        var rows = fsql.Update<PmsProjectMeetingRecord>()
            .SetSource(ToRecord(item, DateTime.MinValue, DateTime.Now))
            .IgnoreColumns(x => new { x.CreatedTime })
            .Where(x => x.Id == item.Id)
            .ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("会议不存在或已被删除。");
    }

    public void Remove(Guid id) => fsql.Delete<PmsProjectMeetingRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static PmsProjectMeeting ToDomain(PmsProjectMeetingRecord x) => PmsProjectMeeting.Restore(x.Id, x.ProjectId, x.Title, x.MeetingType, x.StartsAt, x.EndsAt, x.LocationOrMode, x.HostName, x.ParticipantNames, x.Minutes, x.Decisions, x.OtherInfo);
    private static PmsProjectMeetingRecord ToRecord(PmsProjectMeeting x, DateTime created, DateTime modified) => new() { Id = x.Id, ProjectId = x.ProjectId, Title = x.Title, MeetingType = x.MeetingType, StartsAt = x.StartsAt, EndsAt = x.EndsAt, LocationOrMode = x.LocationOrMode, HostName = x.HostName, ParticipantNames = x.ParticipantNames, Minutes = x.Minutes, Decisions = x.Decisions, OtherInfo = x.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}
