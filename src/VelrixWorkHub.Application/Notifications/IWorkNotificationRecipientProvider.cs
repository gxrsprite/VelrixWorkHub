namespace VelrixWorkHub.Application.Notifications;

/// <summary>提供跨模块系统提醒的接收人。组织/角色数据范围由后续平台切片收口。</summary>
public interface IWorkNotificationRecipientProvider
{
    IReadOnlyList<string> ListRecipients();
}
