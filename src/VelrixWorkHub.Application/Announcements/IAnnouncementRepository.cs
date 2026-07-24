using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Announcements;
public interface IAnnouncementRepository
{
    IReadOnlyList<Announcement> List();
    void Add(Announcement announcement);
    void Update(Announcement announcement);
    void Remove(Guid announcementId);
}
