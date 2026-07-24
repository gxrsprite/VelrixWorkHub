using FreeSql;
using VelrixWorkHub.Application.Assets;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Assets;

public sealed class FreeSqlAssetRequestRepository(IFreeSql fsql) : IOaAssetRequestRepository
{
    public IReadOnlyList<OaAssetRequest> List(Guid? applicantUserId = null, Guid? assetId = null)
    {
        var query = fsql.Select<OaAssetRequestRecord>();
        if (applicantUserId is Guid applicant) query = query.Where(item => item.ApplicantUserId == applicant);
        if (assetId is Guid asset) query = query.Where(item => item.AssetId == asset);
        return query.OrderByDescending(item => item.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaAssetRequest? Get(Guid id) => fsql.Select<OaAssetRequestRecord>().Where(item => item.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaAssetRequest request) => fsql.Insert(ToRecord(request)).ExecuteAffrows();

    public void Update(OaAssetRequest request)
    {
        var rows = fsql.Update<OaAssetRequestRecord>().SetSource(ToRecord(request)).Where(item => item.Id == request.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("资产申请不存在或已被删除。");
    }

    private static OaAssetRequest ToDomain(OaAssetRequestRecord item)
    {
        var request = new OaAssetRequest(item.AssetId, item.ApplicantUserId, item.ApplicantName, item.Reason, item.OtherInfo, item.CreatedAt) { Id = item.Id };
        switch (item.Status)
        {
            case OaAssetRequestStatus.Submitted:
                request.Submit(item.SubmittedAt ?? item.CreatedAt);
                break;
            case OaAssetRequestStatus.Approved:
                request.Submit(item.SubmittedAt ?? item.CreatedAt);
                request.Approve(item.AssignmentId ?? throw new InvalidOperationException("已批准资产申请缺少领用记录。"), item.ApprovedAt ?? item.CreatedAt);
                break;
            case OaAssetRequestStatus.Rejected:
                request.Submit(item.SubmittedAt ?? item.CreatedAt);
                request.Reject(item.RejectionReason);
                break;
            case OaAssetRequestStatus.Withdrawn:
                request.Submit(item.SubmittedAt ?? item.CreatedAt);
                request.Cancel();
                break;
            case OaAssetRequestStatus.Cancelled:
                request.Cancel();
                break;
        }
        return request;
    }

    private static OaAssetRequestRecord ToRecord(OaAssetRequest item) => new()
    {
        Id = item.Id, AssetId = item.AssetId, ApplicantUserId = item.ApplicantUserId, ApplicantName = item.ApplicantName,
        Reason = item.Reason, OtherInfo = item.OtherInfo, Status = item.Status, RejectionReason = item.RejectionReason,
        AssignmentId = item.AssignmentId, CreatedAt = item.CreatedAt, SubmittedAt = item.SubmittedAt, ApprovedAt = item.ApprovedAt
    };
}
