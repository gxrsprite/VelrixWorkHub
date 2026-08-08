namespace VelrixWorkHub.Application.Mom;

public sealed record MomOperator(Guid UserId, string Username, string DisplayName);

/// <summary>
/// MOM 只通过该 Application 边界读取可登记工时的员工，不直接访问 OA 或平台用户表。
/// </summary>
public interface IMomOperatorResolver
{
    IReadOnlyList<MomOperator> ListActive();
    MomOperator? GetActive(Guid userId);
}
