using FreeSql;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

public sealed class FreeSqlLmsLicenseReplacementRequestRepository(IFreeSql fsql) : ILmsLicenseReplacementRequestRepository
{
    public IReadOnlyList<LmsLicenseReplacementRequest> List() => fsql.Select<LmsLicenseReplacementRequestRecord>().ToList().Select(x =>
    {
        var item = new LmsLicenseReplacementRequest(x.RequestNo, x.OriginalAuthorizationId, x.Kind, x.TargetMachineId, x.LicenseNo, x.ExternalLicense, x.ExpiresAt, x.OtherInfo, x.Applicant, x.Reason, x.CreatedAt) { Id = x.Id };
        item.SetStatus(x.Status);
        return item;
    }).ToArray();

    public void Add(LmsLicenseReplacementRequest item) => fsql.Insert(new LmsLicenseReplacementRequestRecord
    {
        Id = item.Id, RequestNo = item.RequestNo, OriginalAuthorizationId = item.OriginalAuthorizationId, Kind = item.Kind, TargetMachineId = item.TargetMachineId,
        LicenseNo = item.LicenseNo, ExternalLicense = item.ExternalLicense, ExpiresAt = item.ExpiresAt, OtherInfo = item.OtherInfo,
        Applicant = item.Applicant, Reason = item.Reason, Status = item.Status, CreatedAt = item.CreatedAt
    }).ExecuteAffrows();

    public void Update(LmsLicenseReplacementRequest item)
    {
        try
        {
            if (fsql.Update<LmsLicenseReplacementRequestRecord>().Set(x => x.Status, item.Status).Where(x => x.Id == item.Id).ExecuteAffrows() == 0)
                throw new InvalidOperationException("授权替代申请不存在。");
        }
        catch (Exception exception) when (item.Status == LmsLicenseReplacementRequestStatus.Submitted && LmsLicenseReplacementRequestSchemaMigration.IsSubmittedRequestUniquenessViolation(exception))
        {
            throw new InvalidOperationException("该原授权已有审批中的替代申请。", exception);
        }
    }
}
