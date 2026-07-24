using FreeSql;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Announcements;
public static class AnnouncementSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        fsql.CodeFirst.SyncStructure<AnnouncementRecord>();
        if (fsql.Select<AnnouncementRecord>().Any()) return;
        var item = new Announcement("Velrix Work Hub 已上线", "欢迎使用统一工作台。公告、任务和客户经营能力会按业务闭环持续交付。\n\n如遇问题，请联系系统管理员。\n");
        item.Publish();
        var now = DateTime.Now;
        fsql.Insert(new AnnouncementRecord { Id = item.Id, Title = item.Title, Content = item.Content, Status = item.Status, PublishedTime = item.PublishedTime, CreatedTime = now, ModifiedTime = now }).ExecuteAffrows();
    }
}
