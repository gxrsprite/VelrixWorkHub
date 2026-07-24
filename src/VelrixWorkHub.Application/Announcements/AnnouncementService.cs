using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Announcements;
public sealed class AnnouncementService(IAnnouncementRepository repository)
{
    public IReadOnlyList<Announcement> List(AnnouncementFilter filter = AnnouncementFilter.All)
    {
        var items = repository.List();
        return filter switch
        {
            AnnouncementFilter.Draft => items.Where(item => item.Status == AnnouncementStatus.Draft).ToArray(),
            AnnouncementFilter.Published => items.Where(item => item.Status == AnnouncementStatus.Published).ToArray(),
            AnnouncementFilter.Archived => items.Where(item => item.Status == AnnouncementStatus.Archived).ToArray(),
            _ => items
        };
    }
    public int Count(AnnouncementFilter filter) => List(filter).Count;
    public Announcement Create(string title, string content) { var item = new Announcement(title, content); repository.Add(item); return item; }
    public void Edit(Announcement item, string title, string content) { item.Edit(title, content); repository.Update(item); }
    public void Publish(Announcement item) { item.Publish(); repository.Update(item); }
    public void Archive(Announcement item) { item.Archive(); repository.Update(item); }
    public void Remove(Announcement item) => repository.Remove(item.Id);
}
