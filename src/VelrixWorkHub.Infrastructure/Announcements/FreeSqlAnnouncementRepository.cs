using FreeSql;
using VelrixWorkHub.Application.Announcements;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Announcements;
public sealed class FreeSqlAnnouncementRepository(IFreeSql fsql) : IAnnouncementRepository
{
    public IReadOnlyList<Announcement> List() => fsql.Select<AnnouncementRecord>().OrderByDescending(item => item.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(Announcement item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(Announcement item)
    {
        var rows = fsql.Update<AnnouncementRecord>().Set(record => record.Title, item.Title).Set(record => record.Content, item.Content)
            .Set(record => record.Status, item.Status).Set(record => record.PublishedTime, item.PublishedTime)
            .Set(record => record.ModifiedTime, DateTime.Now).Where(record => record.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("公告不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<AnnouncementRecord>().Where(item => item.Id == id).ExecuteAffrows();
    private static Announcement ToDomain(AnnouncementRecord record)
    {
        var item = new Announcement(record.Title, record.Content) { Id = record.Id };
        if (record.Status == AnnouncementStatus.Published) item.Publish();
        else if (record.Status == AnnouncementStatus.Archived) { item.Publish(); item.Archive(); }
        return item;
    }
    private static AnnouncementRecord ToRecord(Announcement item, DateTime created, DateTime modified) => new()
    {
        Id = item.Id, Title = item.Title, Content = item.Content, Status = item.Status,
        PublishedTime = item.PublishedTime, CreatedTime = created, ModifiedTime = modified
    };
}
