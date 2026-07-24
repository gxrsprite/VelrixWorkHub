namespace VelrixWorkHub.Domain;

public enum ExternalNotificationChannel
{
    Email,
    Sms,
    WeCom,
    DingTalk
}

public enum ExternalNotificationDeliveryStatus
{
    Pending,
    Delivered
}
