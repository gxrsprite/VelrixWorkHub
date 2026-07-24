using VelrixWorkHub.Application.CashAdvances;
using VelrixWorkHub.Application.ExpenseReimbursements;
using VelrixWorkHub.Application.Vehicles;
using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Offboarding;

public enum OaOffboardingRiskKind
{
    VehicleUse,
    CashAdvance,
    Reimbursement,
    Asset
}

public sealed record OaOffboardingRiskItem(
    OaOffboardingRiskKind Kind,
    Guid BusinessId,
    string Reference,
    string Summary,
    string Href);

public interface IOaOffboardingRiskProvider
{
    IReadOnlyList<OaOffboardingRiskItem> List(Guid userId);
}

/// <summary>
/// 通过各业务 Application 服务读取离职前的未结事项，不直接访问其他模块数据表。
/// </summary>
public sealed class OffboardingRiskService(
    VehicleService vehicles,
    CashAdvanceService cashAdvances,
    ExpenseReimbursementService reimbursements,
    AssetService assets) : IOaOffboardingRiskProvider
{
    public IReadOnlyList<OaOffboardingRiskItem> List(Guid userId)
    {
        if (userId == Guid.Empty) return [];
        var risks = new List<OaOffboardingRiskItem>();

        foreach (var request in vehicles.ListMine(userId).Where(item => item.Status is OaVehicleUseRequestStatus.Submitted or OaVehicleUseRequestStatus.Approved))
        {
            var vehicle = vehicles.GetVehicle(request.VehicleId);
            risks.Add(new(
                OaOffboardingRiskKind.VehicleUse,
                request.Id,
                vehicle?.PlateNumber ?? "车辆已删除",
                $"用车申请 {request.Status switch { OaVehicleUseRequestStatus.Submitted => "待审批", _ => "待归还" }} · {request.Destination}",
                $"/Oa/Vehicle?requestId={request.Id}"));
        }

        foreach (var advance in cashAdvances.ListMine(userId).Where(item => (item.Status is OaCashAdvanceStatus.Submitted or OaCashAdvanceStatus.Approved or OaCashAdvanceStatus.PartiallySettled) && item.RemainingAmount > 0))
        {
            risks.Add(new(
                OaOffboardingRiskKind.CashAdvance,
                advance.Id,
                advance.DocumentNo,
                $"借款/备用金未结清 · 余额 ¥{advance.RemainingAmount:N2}",
                $"/Oa/CashAdvance?cashAdvanceId={advance.Id}"));
        }

        foreach (var reimbursement in reimbursements.ListMine(userId).Where(item => item.Status is OaExpenseReimbursementStatus.Submitted or OaExpenseReimbursementStatus.Approved or OaExpenseReimbursementStatus.Reimbursed))
        {
            risks.Add(new(
                OaOffboardingRiskKind.Reimbursement,
                reimbursement.Id,
                reimbursement.DocumentNo,
                $"报销尚未完成付款 · ¥{reimbursement.ActualAmount:N2}",
                $"/Oa/ExpenseReimbursement?reimbursementId={reimbursement.Id}"));
        }

        foreach (var asset in assets.ListByUser(userId).Where(item => item.Status == OaAssetStatus.InUse))
        {
            risks.Add(new(
                OaOffboardingRiskKind.Asset,
                asset.Id,
                asset.AssetNo,
                $"资产尚未归还 · {asset.Name}",
                $"/Oa/Asset?assetId={asset.Id}"));
        }

        return risks.OrderBy(item => item.Kind).ThenBy(item => item.Reference).ToArray();
    }
}
