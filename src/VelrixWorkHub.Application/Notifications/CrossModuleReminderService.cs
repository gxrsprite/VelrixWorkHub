using VelrixWorkHub.Application.Settlements;
using VelrixWorkHub.Application.WorkItems;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Notifications;

public sealed record CrossModuleReminderScanResult(
    int CandidateEventCount,
    int RecipientCount,
    int NotificationAttemptCount);

/// <summary>
/// 将统一待办中的跨模块风险投影为 OA 站内提醒。
/// 扫描只负责投递幂等通知，不改变合同、订单、库存或 PMP 主数据状态。
/// </summary>
public sealed class CrossModuleReminderService(
    IWorkNotificationRecipientProvider recipients,
    NotificationService notifications)
{
    public CrossModuleReminderScanResult Scan(
        DateTime now,
        IEnumerable<SalesContract> contracts,
        IEnumerable<SettlementOrderBalance> settlementBalances,
        IEnumerable<InventoryRiskTodo> inventoryRisks,
        IEnumerable<PmpProjectIssue> issues,
        IEnumerable<PmpProjectPhase> phases,
        int contractReminderDays = 30)
    {
        var today = DateOnly.FromDateTime(now);
        var allItems = UnifiedTodoService.Build(
            today,
            [],
            [],
            contracts,
            issues,
            contractReminderDays,
            settlementBalances,
            phases: phases,
            inventoryRisks: inventoryRisks);
        var events = allItems
            .Where(item => IsReminderEvent(item, today))
            .Select(item => new CrossModuleReminderEvent(item, ReminderTitle(item, today), ReminderContent(item, today), DedupeKey(item)))
            .ToArray();
        var recipientNames = recipients.ListRecipients()
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var attempts = 0;
        foreach (var recipient in recipientNames)
        {
            foreach (var reminder in events)
            {
                attempts++;
                notifications.Publish(
                    recipient,
                    WorkNotificationKind.Reminder,
                    reminder.Title,
                    reminder.Content,
                    reminder.Href,
                    reminder.DedupeKey,
                    now);
            }
        }

        return new(events.Length, recipientNames.Length, attempts);
    }

    private static bool IsReminderEvent(UnifiedTodoItem item, DateOnly today) => item.Source switch
    {
        UnifiedTodoSource.Contract => true,
        UnifiedTodoSource.Settlement => item.IsOverdue(today),
        UnifiedTodoSource.InventoryRisk => true,
        UnifiedTodoSource.ProjectPhase => true,
        UnifiedTodoSource.ProjectIssue => item.Priority is UnifiedTodoPriority.High or UnifiedTodoPriority.Critical,
        _ => false
    };

    private static string ReminderTitle(UnifiedTodoItem item, DateOnly today) => item.Source switch
    {
        UnifiedTodoSource.Contract => item.IsOverdue(today) ? "CRM 合同已到期" : "CRM 合同即将到期",
        UnifiedTodoSource.Settlement => item.Detail.Contains("客户应收", StringComparison.Ordinal) ? "ERP 客户应收逾期" : "ERP 供应商应付逾期",
        UnifiedTodoSource.InventoryRisk => "ERP 库存低于安全线",
        UnifiedTodoSource.ProjectPhase => "PMP 项目节点逾期",
        UnifiedTodoSource.ProjectIssue => "PMP 风险问题升级",
        _ => "跨模块业务提醒"
    };

    private static string ReminderContent(UnifiedTodoItem item, DateOnly today)
        => $"{item.Title}；{item.Detail}；{(item.IsOverdue(today) ? $"已逾期 {item.DueDate:yyyy-MM-dd}" : $"截止 {item.DueDate:yyyy-MM-dd}")}。";

    private static string DedupeKey(UnifiedTodoItem item)
        => $"cross-module-reminder:{item.Source}:{item.SourceId}:{item.DueDate:yyyyMMdd}:{item.Priority}";

    private sealed record CrossModuleReminderEvent(UnifiedTodoItem Item, string Title, string Content, string DedupeKey)
    {
        public string Href => Item.Href.StartsWith("/", StringComparison.Ordinal) ? Item.Href : $"/{Item.Href}";
    }
}
