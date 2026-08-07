using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.PmsProjects;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class PmsProjectWorkItemReminderServiceTests
{
    [Fact]
    public void Scan_NotifiesDueOpenItemsOnceAndSkipsTerminalOrUnassignedItems()
    {
        var now = new DateTime(2026, 7, 21, 10, 0, 0);
        var owner = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "OWNER", "负责人", null, null, true, null, null);
        var disabledOwner = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "disabled", "停用负责人", null, null, false, null, null);
        var items = new WorkItemRepository();
        var due = AddItem(items, "到期工作项", owner.UserId, now);
        AddItem(items, "未来工作项", owner.UserId, now.AddMinutes(1));
        var completed = AddItem(items, "已完成工作项", owner.UserId, now);
        completed.SetStatus(PmsProjectWorkItemStatus.InProgress, null, now.AddHours(-1));
        completed.SetStatus(PmsProjectWorkItemStatus.PendingApproval, "已完成", now.AddMinutes(-1));
        completed.ApproveCompletion(now);
        AddItem(items, "未指派工作项", null, now);
        AddItem(items, "停用负责人工作项", disabledOwner.UserId, now);
        var notifications = new NotificationRepository();
        var service = new PmsProjectWorkItemReminderService(
            items,
            new EmployeeDirectoryService(new DirectoryRepository(owner, disabledOwner)),
            new NotificationService(notifications));

        var first = service.Scan(now);
        var second = service.Scan(now);

        Assert.Equal(3, first.DueWorkItemCount);
        Assert.Equal(1, first.NotificationAttemptCount);
        Assert.Single(notifications.Items);
        Assert.Equal("owner", notifications.Items[0].Recipient);
        Assert.Equal(WorkNotificationKind.Reminder, notifications.Items[0].Kind);
        Assert.Contains(due.Title, notifications.Items[0].Content, StringComparison.Ordinal);
        Assert.Equal(1, second.NotificationAttemptCount);
        Assert.Single(notifications.Items);
    }

    [Fact]
    public void Scan_NotifiesOverdueItemsOnceAndSkipsTerminalOrUnavailableOwners()
    {
        var now = new DateTime(2026, 7, 21, 10, 0, 0);
        var owner = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "OWNER", "负责人", null, null, true, null, null);
        var disabledOwner = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "disabled", "停用负责人", null, null, false, null, null);
        var items = new WorkItemRepository();
        var open = AddItem(items, "逾期待处理", owner.UserId, reminderAt: null, plannedEndAt: now.AddMinutes(-1));
        var inApproval = AddItem(items, "逾期验收", owner.UserId, reminderAt: null, plannedEndAt: now.AddMinutes(-2));
        inApproval.SetStatus(PmsProjectWorkItemStatus.Open, null, now.AddHours(-2));
        inApproval.SetStatus(PmsProjectWorkItemStatus.InProgress, null, now.AddHours(-1));
        inApproval.SetStatus(PmsProjectWorkItemStatus.PendingApproval, "待验收", now.AddMinutes(-30));
        var completed = AddItem(items, "已完成逾期", owner.UserId, reminderAt: null, plannedEndAt: now.AddMinutes(-3));
        completed.SetStatus(PmsProjectWorkItemStatus.Open, null, now.AddHours(-2));
        completed.SetStatus(PmsProjectWorkItemStatus.InProgress, null, now.AddHours(-1));
        completed.SetStatus(PmsProjectWorkItemStatus.PendingApproval, "已完成", now.AddMinutes(-30));
        completed.ApproveCompletion(now.AddMinutes(-10));
        AddItem(items, "未指派逾期", null, reminderAt: null, plannedEndAt: now.AddMinutes(-4));
        AddItem(items, "停用负责人逾期", disabledOwner.UserId, reminderAt: null, plannedEndAt: now.AddMinutes(-5));
        AddItem(items, "边界时间", owner.UserId, reminderAt: null, plannedEndAt: now);
        var notifications = new NotificationRepository();
        var service = new PmsProjectWorkItemReminderService(items, new EmployeeDirectoryService(new DirectoryRepository(owner, disabledOwner)), new NotificationService(notifications));

        var first = service.Scan(now);
        var second = service.Scan(now);

        Assert.Equal(0, first.DueWorkItemCount);
        Assert.Equal(4, first.OverdueWorkItemCount);
        Assert.Equal(2, first.NotificationAttemptCount);
        Assert.Equal(2, notifications.Items.Count);
        Assert.All(notifications.Items, notification =>
        {
            Assert.Equal("项目工作项已逾期", notification.Title);
            Assert.StartsWith("pms-work-item-overdue:", notification.DedupeKey, StringComparison.Ordinal);
        });
        Assert.Contains(notifications.Items, x => x.Content.Contains(open.Title, StringComparison.Ordinal));
        Assert.Equal(2, second.NotificationAttemptCount);
        Assert.Equal(2, notifications.Items.Count);
    }

    [Fact]
    public void Scan_KeepsScheduledAndOverdueNotificationsSeparate()
    {
        var now = new DateTime(2026, 7, 21, 10, 0, 0);
        var owner = new EmployeeDirectoryEntry(Guid.CreateVersion7(), "OWNER", "负责人", null, null, true, null, null);
        var items = new WorkItemRepository();
        AddItem(items, "同时提醒和逾期", owner.UserId, now.AddMinutes(-10), now.AddMinutes(-5));
        var notifications = new NotificationRepository();
        var service = new PmsProjectWorkItemReminderService(items, new EmployeeDirectoryService(new DirectoryRepository(owner)), new NotificationService(notifications));

        var result = service.Scan(now);

        Assert.Equal(1, result.DueWorkItemCount);
        Assert.Equal(1, result.OverdueWorkItemCount);
        Assert.Equal(2, result.NotificationAttemptCount);
        Assert.Equal(2, notifications.Items.Count);
        Assert.Contains(notifications.Items, x => x.Title == "项目工作项提醒");
        Assert.Contains(notifications.Items, x => x.Title == "项目工作项已逾期");
        Assert.Equal(2, notifications.Items.Select(x => x.DedupeKey).Distinct().Count());
    }

    private static PmsProjectWorkItem AddItem(WorkItemRepository repository, string title, Guid? ownerId, DateTime? reminderAt, DateTime? plannedEndAt = null)
    {
        var item = new PmsProjectWorkItem(Guid.CreateVersion7(), null, null, null, title, null, ownerId is null ? null : "负责人", null, PmsProjectWorkItemPriority.Medium, null, plannedEndAt, "{}", ownerId, reminderAt);
        item.SetStatus(PmsProjectWorkItemStatus.Open, null, (reminderAt ?? plannedEndAt ?? DateTime.Now).AddHours(-1));
        repository.Add(item);
        return item;
    }

    private sealed class WorkItemRepository : IPmsProjectWorkItemRepository
    {
        private readonly List<PmsProjectWorkItem> data = [];
        public IReadOnlyList<PmsProjectWorkItem> List(Guid? projectId = null) => projectId is Guid id ? data.Where(x => x.ProjectId == id).ToArray() : data;
        public void Add(PmsProjectWorkItem item) => data.Add(item);
        public void Update(PmsProjectWorkItem item) { }
        public void Remove(Guid id) => data.RemoveAll(x => x.Id == id);
    }

    private sealed class DirectoryRepository(params EmployeeDirectoryEntry[] data) : IEmployeeDirectoryRepository
    {
        public IReadOnlyList<EmployeeDirectoryEntry> List() => data;
        public IReadOnlyList<EmployeeDirectoryOrganization> ListOrganizations() => [];
    }

    private sealed class NotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];
        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false) => Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase)).ToArray();
        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey) => Items.FirstOrDefault(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == dedupeKey);
        public void Add(WorkNotification notification) => Items.Add(notification);
        public bool TryAdd(WorkNotification notification) { if (FindByDedupeKey(notification.Recipient, notification.DedupeKey) is not null) return false; Items.Add(notification); return true; }
        public void Update(WorkNotification notification) { }
        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds) => 0;
    }
}
