using FreeSql;
using VelrixWorkHub.Application.ProcurementRequests;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.ProcurementRequests;

public sealed class FreeSqlProcurementRequestRepository(IFreeSql fsql) : IOaProcurementRequestRepository
{
    public IReadOnlyList<OaProcurementRequest> List(Guid? applicantUserId = null)
    {
        var query = fsql.Select<OaProcurementRequestRecord>();
        if (applicantUserId is Guid id) query = query.Where(x => x.ApplicantUserId == id);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaProcurementRequest? Get(Guid id) => fsql.Select<OaProcurementRequestRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaProcurementRequest item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();

    public void Update(OaProcurementRequest item)
    {
        var rows = fsql.Update<OaProcurementRequestRecord>()
            .Set(x => x.DocumentNo, item.DocumentNo).Set(x => x.ApplicantName, item.ApplicantName).Set(x => x.DepartmentName, item.DepartmentName)
            .Set(x => x.LegalEntity, item.LegalEntity).Set(x => x.RequestType, item.RequestType).Set(x => x.RequestDate, item.RequestDate.ToDateTime(TimeOnly.MinValue))
            .Set(x => x.RequiredDate, item.RequiredDate.ToDateTime(TimeOnly.MinValue)).Set(x => x.ProjectId, item.ProjectId).Set(x => x.BudgetReference, item.BudgetReference)
            .Set(x => x.EstimatedAmount, item.EstimatedAmount).Set(x => x.Purpose, item.Purpose).Set(x => x.OtherInfo, item.OtherInfo)
            .Set(x => x.Status, item.Status).Set(x => x.RejectionReason, item.RejectionReason).Set(x => x.SubmittedAt, item.SubmittedAt)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("采购申请不存在或已被删除。");
    }

    private static OaProcurementRequest ToDomain(OaProcurementRequestRecord x)
    {
        var item = new OaProcurementRequest(x.ApplicantUserId, x.ApplicantName, x.DepartmentName, x.LegalEntity, x.DocumentNo,
            x.RequestType, DateOnly.FromDateTime(x.RequestDate), DateOnly.FromDateTime(x.RequiredDate), x.ProjectId, x.BudgetReference,
            x.Purpose, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        item.SetEstimatedAmount(x.EstimatedAmount);
        switch (x.Status)
        {
            case OaProcurementRequestStatus.Submitted: item.Submit(x.SubmittedAt ?? x.CreatedAt); break;
            case OaProcurementRequestStatus.Rejected: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Reject(x.RejectionReason); break;
            case OaProcurementRequestStatus.Approved: item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); break;
            case OaProcurementRequestStatus.Cancelled: item.Cancel(); break;
        }
        return item;
    }

    private static OaProcurementRequestRecord ToRecord(OaProcurementRequest x) => new()
    {
        Id = x.Id, DocumentNo = x.DocumentNo, ApplicantUserId = x.ApplicantUserId, ApplicantName = x.ApplicantName,
        DepartmentName = x.DepartmentName, LegalEntity = x.LegalEntity, RequestType = x.RequestType,
        RequestDate = x.RequestDate.ToDateTime(TimeOnly.MinValue), RequiredDate = x.RequiredDate.ToDateTime(TimeOnly.MinValue),
        ProjectId = x.ProjectId, BudgetReference = x.BudgetReference, EstimatedAmount = x.EstimatedAmount, Purpose = x.Purpose,
        OtherInfo = x.OtherInfo, Status = x.Status, RejectionReason = x.RejectionReason, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt
    };
}

public sealed class FreeSqlProcurementRequestLineRepository(IFreeSql fsql) : IOaProcurementRequestLineRepository
{
    public IReadOnlyList<OaProcurementRequestLine> List(Guid requestId) => fsql.Select<OaProcurementRequestLineRecord>().Where(x => x.RequestId == requestId).ToList().Select(ToDomain).ToArray();
    public OaProcurementRequestLine? Get(Guid id) => fsql.Select<OaProcurementRequestLineRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(OaProcurementRequestLine item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Remove(Guid id) => fsql.Delete<OaProcurementRequestLineRecord>().Where(x => x.Id == id).ExecuteAffrows();

    private static OaProcurementRequestLine ToDomain(OaProcurementRequestLineRecord x) => new(x.RequestId, x.ProductId, x.ItemName, x.MaterialCategory, x.Specification, x.Quantity, x.Unit, x.EstimatedUnitPrice, x.OtherInfo) { Id = x.Id };
    private static OaProcurementRequestLineRecord ToRecord(OaProcurementRequestLine x) => new()
    {
        Id = x.Id, RequestId = x.RequestId, ProductId = x.ProductId, ItemName = x.ItemName, MaterialCategory = x.MaterialCategory,
        Specification = x.Specification, Quantity = x.Quantity, Unit = x.Unit, EstimatedUnitPrice = x.EstimatedUnitPrice, OtherInfo = x.OtherInfo
    };
}
