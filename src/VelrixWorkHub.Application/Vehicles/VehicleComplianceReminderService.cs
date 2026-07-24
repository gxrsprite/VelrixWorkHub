using VelrixWorkHub.Application.Employees;
using VelrixWorkHub.Application.Notifications;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Vehicles;

/// <summary>扫描车辆年检与保险到期风险，只向当前启用的台账负责人发送幂等提醒。</summary>
public sealed class VehicleComplianceReminderService(
    VehicleService vehicles,
    EmployeeDirectoryService directory,
    NotificationService notifications)
{
    public VehicleComplianceReminderScanResult Scan(DateOnly today, int warningDays = 30)
    {
        if (warningDays < 0) throw new ArgumentOutOfRangeException(nameof(warningDays));
        var deadline = today.AddDays(warningDays);
        var enabledUsers = directory.List(status: EmployeeDirectoryStatus.Enabled)
            .ToDictionary(item => item.UserId, item => item.Username);
        var inspection = 0;
        var insurance = 0;
        var delivered = 0;
        var skipped = 0;

        foreach (var vehicle in vehicles.ListVehicles())
        {
            if (vehicle.Status == OaVehicleStatus.Retired)
            {
                skipped++;
                continue;
            }

            if (vehicle.ResponsibleUserId is not Guid responsibleUserId || !enabledUsers.TryGetValue(responsibleUserId, out var username))
            {
                skipped++;
                continue;
            }

            if (vehicle.AnnualInspectionExpiresOn is DateOnly inspectionDate && inspectionDate <= deadline)
            {
                inspection++;
                notifications.Publish(username, WorkNotificationKind.Reminder, "车辆年检到期提醒",
                    $"车辆 {vehicle.PlateNumber} 的年检到期日为 {inspectionDate:yyyy-MM-dd}，请及时安排处理。",
                    "/Oa/Vehicle", $"vehicle-compliance:inspection:{vehicle.Id}:{inspectionDate:yyyyMMdd}");
                delivered++;
            }

            if (vehicle.InsuranceExpiresOn is DateOnly insuranceDate && insuranceDate <= deadline)
            {
                insurance++;
                notifications.Publish(username, WorkNotificationKind.Reminder, "车辆保险到期提醒",
                    $"车辆 {vehicle.PlateNumber} 的保险到期日为 {insuranceDate:yyyy-MM-dd}，请及时安排处理。",
                    "/Oa/Vehicle", $"vehicle-compliance:insurance:{vehicle.Id}:{insuranceDate:yyyyMMdd}");
                delivered++;
            }
        }

        return new VehicleComplianceReminderScanResult(inspection, insurance, delivered, skipped);
    }
}

public sealed record VehicleComplianceReminderScanResult(int InspectionDueCount, int InsuranceDueCount, int NotificationAttemptCount, int SkippedVehicleCount);
