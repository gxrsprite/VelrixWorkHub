using VelrixWorkHub.Application.Announcements;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class AnnouncementTests
{
    [Fact]
    public void NewAnnouncement_StartsAsDraft()
    {
        var item = new Announcement("系统通知", "请关注本周安排。");

        Assert.Equal(AnnouncementStatus.Draft, item.Status);
        Assert.Null(item.PublishedTime);
    }

    [Fact]
    public void Draft_CanPublishButCannotArchiveDirectly()
    {
        var item = new Announcement("系统通知", "请关注本周安排。");

        Assert.Throws<InvalidOperationException>(() => item.Archive());
        item.Publish();
        item.Archive();

        Assert.Equal(AnnouncementStatus.Archived, item.Status);
        Assert.NotNull(item.PublishedTime);
        Assert.Throws<InvalidOperationException>(() => item.Publish());
    }

    [Fact]
    public void BlankFields_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Announcement(" ", "内容"));
        Assert.Throws<ArgumentException>(() => new Announcement("标题", " "));
    }

    [Fact]
    public void ServiceFiltersAndUpdatesRepository()
    {
        var repository = new TestRepository();
        var service = new AnnouncementService(repository);
        var draft = service.Create("草稿", "内容");
        var published = service.Create("发布", "内容");
        service.Publish(published);

        Assert.Single(service.List(AnnouncementFilter.Draft));
        Assert.Single(service.List(AnnouncementFilter.Published));
        Assert.Equal(1, repository.UpdatedCount);
        service.Archive(published);
        Assert.Single(service.List(AnnouncementFilter.Archived));
        Assert.Equal(2, repository.UpdatedCount);
        Assert.Equal(2, service.List().Count);
        Assert.Equal(AnnouncementStatus.Draft, draft.Status);
    }

    private sealed class TestRepository : IAnnouncementRepository
    {
        private readonly List<Announcement> items = [];
        public int UpdatedCount { get; private set; }
        public IReadOnlyList<Announcement> List() => items;
        public void Add(Announcement announcement) => items.Add(announcement);
        public void Update(Announcement announcement) => UpdatedCount++;
        public void Remove(Guid announcementId) => items.RemoveAll(item => item.Id == announcementId);
    }
}
