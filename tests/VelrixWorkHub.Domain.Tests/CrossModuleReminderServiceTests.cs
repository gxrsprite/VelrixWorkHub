using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.WorkItems;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class CrossModuleReminderServiceTests
{
    [Fact]
    public void Scan_ProjectsCrossModuleRisksAndSuppressesRepeatedNotifications()
    {
        var today = new DateOnly(2026, 7, 19);
        var customerId = Guid.CreateVersion7();
        var projectId = Guid.CreateVersion7();
        var contract = new SalesContract(customerId, null, "CT-REMINDER-001", "即将到期合同", 100m, today.AddDays(-30), today.AddDays(3));
        contract.Activate();
        var issue = new PmsProjectIssue(projectId, PmsProjectIssueKind.Risk, "上线风险", "需要升级", "项目经理", PmsProjectIssuePriority.Critical, today.AddDays(5));
        var phase = new PmsProjectPhase(projectId, "上线里程碑", PmsProjectPhaseKind.Milestone, 1, today.AddDays(-2), today.AddDays(-2));
        phase.SetStatus(PmsProjectPhaseStatus.Active);
        var settlement = new SettlementOrderBalance(Guid.CreateVersion7(), "SO-REMINDER-001", ErpSettlementKind.Receivable, 100m, 0m)
        {
            DueDate = today.AddDays(-1)
        };
        var inventoryRisk = new InventoryRiskTodo(Guid.CreateVersion7(), "关键商品", 10m, 3m);
        var repository = new InMemoryNotificationRepository();
        var service = new CrossModuleReminderService(
            new RecipientProvider(["ADMIN", "admin", ""]),
            new NotificationService(repository));

        var first = service.Scan(
            new DateTime(2026, 7, 19, 10, 0, 0),
            [contract], [settlement], [inventoryRisk], [issue], [phase]);

        Assert.Equal(5, first.CandidateEventCount);
        Assert.Equal(1, first.RecipientCount);
        Assert.Equal(5, first.NotificationAttemptCount);
        Assert.Equal(5, repository.Items.Count);
        Assert.All(repository.Items, item => Assert.Equal("admin", item.Recipient));
        Assert.Contains(repository.Items, item => item.Title == "CRM 合同即将到期");
        Assert.Contains(repository.Items, item => item.Title == "ERP 客户应收逾期" && item.Href!.Contains("Erp/Settlement", StringComparison.Ordinal));
        Assert.Contains(repository.Items, item => item.Title == "ERP 库存低于安全线");
        Assert.Contains(repository.Items, item => item.Title == "PMS 项目节点逾期");
        Assert.Contains(repository.Items, item => item.Title == "PMS 风险问题升级");

        var second = service.Scan(
            new DateTime(2026, 7, 19, 10, 0, 0),
            [contract], [settlement], [inventoryRisk], [issue], [phase]);

        Assert.Equal(5, second.NotificationAttemptCount);
        Assert.Equal(5, repository.Items.Count);
    }

    private sealed class RecipientProvider(IReadOnlyList<string> names) : IWorkNotificationRecipientProvider
    {
        public IReadOnlyList<string> ListRecipients() => names;
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        public List<WorkNotification> Items { get; } = [];

        public IReadOnlyList<WorkNotification> List(string recipient, bool unreadOnly = false)
            => Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase))
                .Where(x => !unreadOnly || !x.IsRead)
                .ToArray();

        public WorkNotification? FindByDedupeKey(string recipient, string dedupeKey)
            => Items.FirstOrDefault(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && x.DedupeKey == dedupeKey);

        public void Add(WorkNotification notification) => Items.Add(notification);

        public bool TryAdd(WorkNotification notification)
        {
            if (FindByDedupeKey(notification.Recipient, notification.DedupeKey) is not null) return false;
            Items.Add(notification);
            return true;
        }

        public void Update(WorkNotification notification) { }

        public int Delete(string recipient, IReadOnlyCollection<Guid> notificationIds)
        {
            var selected = Items.Where(x => x.Recipient.Equals(recipient, StringComparison.OrdinalIgnoreCase) && notificationIds.Contains(x.Id)).ToArray();
            foreach (var item in selected) Items.Remove(item);
            return selected.Length;
        }
    }
}
