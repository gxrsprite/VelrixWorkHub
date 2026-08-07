using System.Globalization;
using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.PmsProjects;

/// <summary>投递人工提醒和计划已逾期工作项通知，不改变工作项生命周期。</summary>
public sealed class PmsProjectWorkItemReminderService(
    IPmsProjectWorkItemRepository repository,
    EmployeeDirectoryService directory,
    NotificationService notifications)
{
    public PmsProjectWorkItemReminderScanResult Scan(DateTime now)
    {
        var enabledUsers = directory.List(status: EmployeeDirectoryStatus.Enabled)
            .ToDictionary(x => x.UserId, x => x.Username);
        var due = 0;
        var overdue = 0;
        var delivered = 0;
        var skipped = 0;

        foreach (var item in repository.List())
        {
            if (item.Status is PmsProjectWorkItemStatus.Completed or PmsProjectWorkItemStatus.Cancelled)
            {
                skipped++;
                continue;
            }

            var reminderAt = item.ReminderAt is DateTime scheduledReminder && scheduledReminder <= now
                ? scheduledReminder
                : (DateTime?)null;
            var plannedEndAt = item.PlannedEndAt is DateTime plannedEnd && plannedEnd < now
                ? plannedEnd
                : (DateTime?)null;
            if (reminderAt is null && plannedEndAt is null)
            {
                skipped++;
                continue;
            }

            if (reminderAt is not null) due++;
            if (plannedEndAt is not null) overdue++;
            if (item.OwnerUserId is not Guid ownerId || !enabledUsers.TryGetValue(ownerId, out var username))
            {
                skipped++;
                continue;
            }

            if (reminderAt is DateTime dueAt)
            {
                notifications.Publish(
                    username,
                    WorkNotificationKind.Reminder,
                    "项目工作项提醒",
                    $"工作项“{item.Title}”的提醒时间为 {dueAt:yyyy-MM-dd HH:mm}，请及时处理。",
                    "/Pms/WorkItem",
                    ReminderDedupeKey(item, dueAt),
                    now);
                delivered++;
            }

            if (plannedEndAt is DateTime overdueAt)
            {
                notifications.Publish(
                    username,
                    WorkNotificationKind.Reminder,
                    "项目工作项已逾期",
                    $"工作项“{item.Title}”计划于 {overdueAt:yyyy-MM-dd HH:mm} 完成，当前仍未结束，请及时处理。",
                    "/Pms/WorkItem",
                    OverdueDedupeKey(item, overdueAt),
                    now);
                delivered++;
            }
        }

        return new PmsProjectWorkItemReminderScanResult(due, overdue, delivered, skipped);
    }

    private static string ReminderDedupeKey(PmsProjectWorkItem item, DateTime reminderAt) =>
        $"pms-work-item-reminder:{item.Id}:{reminderAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}";

    private static string OverdueDedupeKey(PmsProjectWorkItem item, DateTime plannedEndAt) =>
        $"pms-work-item-overdue:{item.Id}:{plannedEndAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)}";
}

public sealed record PmsProjectWorkItemReminderScanResult(int DueWorkItemCount, int OverdueWorkItemCount, int NotificationAttemptCount, int SkippedWorkItemCount);
