namespace VelrixWorkHub.Domain;

public enum AnnouncementStatus { Draft, Published, Archived }

public sealed class Announcement
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public AnnouncementStatus Status { get; private set; }
    public DateTime? PublishedTime { get; private set; }

    public Announcement(string title, string content)
    {
        Edit(title, content);
        Status = AnnouncementStatus.Draft;
    }

    public void Edit(string title, string content)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("公告标题不能为空。", nameof(title));
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("公告内容不能为空。", nameof(content));
        Title = title.Trim();
        Content = content.Trim();
    }

    public void Publish()
    {
        if (Status == AnnouncementStatus.Archived) throw new InvalidOperationException("已归档公告不能直接发布。");
        Status = AnnouncementStatus.Published;
        PublishedTime ??= DateTime.Now;
    }

    public void Archive()
    {
        if (Status == AnnouncementStatus.Draft) throw new InvalidOperationException("草稿公告不能直接归档。");
        Status = AnnouncementStatus.Archived;
    }
}
