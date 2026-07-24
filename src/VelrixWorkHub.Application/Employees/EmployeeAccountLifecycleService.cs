namespace VelrixWorkHub.Application.Employees;

/// <summary>
/// 平台账号生命周期边界。员工档案只保存业务身份，账号启停仍由平台账号用例负责。
/// </summary>
public interface IEmployeeAccountLifecycleService
{
    void Disable(Guid userId, string actor, string reason);
}
