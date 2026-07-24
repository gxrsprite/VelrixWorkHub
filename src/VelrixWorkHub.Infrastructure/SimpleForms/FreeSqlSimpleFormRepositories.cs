using FreeSql;
using VelrixWorkHub.Application.SimpleForms;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.SimpleForms;

public sealed class FreeSqlSimpleFormDefinitionRepository(IFreeSql fsql) : ISimpleFormDefinitionRepository
{
    public IReadOnlyList<SimpleFormDefinition> List() => fsql.Select<SimpleFormDefinitionRecord>().ToList().Select(ToDomain).ToArray();
    public SimpleFormDefinition? Get(Guid id) => fsql.Select<SimpleFormDefinitionRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(SimpleFormDefinition item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(SimpleFormDefinition item) { if (fsql.Update<SimpleFormDefinitionRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows() == 0) throw new InvalidOperationException("表单定义不存在或已被删除。"); }
    private static SimpleFormDefinition ToDomain(SimpleFormDefinitionRecord x) { var item = new SimpleFormDefinition(x.Code, x.Name, x.Description, x.WorkflowDefinitionCode, x.CompletionEventCode, x.CreatedAt) { Id = x.Id }; if (x.PublishedVersionNumber is int version) item.Publish(version); return item; }
    private static SimpleFormDefinitionRecord ToRecord(SimpleFormDefinition x) => new() { Id = x.Id, Code = x.Code, Name = x.Name, Description = x.Description, WorkflowDefinitionCode = x.WorkflowDefinitionCode, CompletionEventCode = x.CompletionEventCode, PublishedVersionNumber = x.PublishedVersionNumber, CreatedAt = x.CreatedAt };
}

public sealed class FreeSqlSimpleFormDefinitionVersionRepository(IFreeSql fsql) : ISimpleFormDefinitionVersionRepository
{
    public IReadOnlyList<SimpleFormDefinitionVersion> List(Guid? definitionId = null) { var query = fsql.Select<SimpleFormDefinitionVersionRecord>(); if (definitionId is Guid id) query = query.Where(x => x.DefinitionId == id); return query.ToList().Select(ToDomain).ToArray(); }
    public SimpleFormDefinitionVersion? Get(Guid id) => fsql.Select<SimpleFormDefinitionVersionRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(SimpleFormDefinitionVersion item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(SimpleFormDefinitionVersion item) { if (fsql.Update<SimpleFormDefinitionVersionRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows() == 0) throw new InvalidOperationException("表单版本不存在或已被删除。"); }
    private static SimpleFormDefinitionVersion ToDomain(SimpleFormDefinitionVersionRecord x) { var item = new SimpleFormDefinitionVersion(x.DefinitionId, x.VersionNumber, x.SchemaJson, x.CreatedAt) { Id = x.Id }; if (x.Status == SimpleFormDefinitionVersionStatus.Published) item.Publish(x.PublishedAt ?? x.CreatedAt); else if (x.Status == SimpleFormDefinitionVersionStatus.Archived) { item.Publish(x.PublishedAt ?? x.CreatedAt); item.Archive(); } return item; }
    private static SimpleFormDefinitionVersionRecord ToRecord(SimpleFormDefinitionVersion x) => new() { Id = x.Id, DefinitionId = x.DefinitionId, VersionNumber = x.VersionNumber, SchemaJson = x.SchemaJson, Status = x.Status, CreatedAt = x.CreatedAt, PublishedAt = x.PublishedAt };
}

public sealed class FreeSqlSimpleFormSubmissionRepository(IFreeSql fsql) : ISimpleFormSubmissionRepository
{
    public IReadOnlyList<SimpleFormSubmission> List(Guid? applicantUserId = null, Guid? definitionId = null) { var query = fsql.Select<SimpleFormSubmissionRecord>(); if (applicantUserId is Guid applicant) query = query.Where(x => x.ApplicantUserId == applicant); if (definitionId is Guid definition) query = query.Where(x => x.DefinitionId == definition); return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray(); }
    public SimpleFormSubmission? Get(Guid id) => fsql.Select<SimpleFormSubmissionRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(SimpleFormSubmission item) => fsql.Insert(ToRecord(item)).ExecuteAffrows();
    public void Update(SimpleFormSubmission item) { if (fsql.Update<SimpleFormSubmissionRecord>().SetSource(ToRecord(item)).Where(x => x.Id == item.Id).ExecuteAffrows() == 0) throw new InvalidOperationException("表单申请不存在或已被删除。"); }
    private static SimpleFormSubmission ToDomain(SimpleFormSubmissionRecord x) { var item = new SimpleFormSubmission(x.DefinitionId, x.DefinitionCode, x.FormVersionNumber, x.WorkflowDefinitionCode, x.CompletionEventCode, x.ApplicantUserId, x.ApplicantName, x.SchemaJson, x.DataJson, x.CreatedAt) { Id = x.Id }; if (x.Status == SimpleFormSubmissionStatus.Submitted) item.Submit(x.SubmittedAt ?? x.CreatedAt); else if (x.Status == SimpleFormSubmissionStatus.Approved) { item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Approve(); } else if (x.Status == SimpleFormSubmissionStatus.Rejected) { item.Submit(x.SubmittedAt ?? x.CreatedAt); item.Reject(x.RejectionReason); } else if (x.Status == SimpleFormSubmissionStatus.Cancelled) item.Cancel(); return item; }
    private static SimpleFormSubmissionRecord ToRecord(SimpleFormSubmission x) => new() { Id = x.Id, DefinitionId = x.DefinitionId, DefinitionCode = x.DefinitionCode, FormVersionNumber = x.FormVersionNumber, WorkflowDefinitionCode = x.WorkflowDefinitionCode, CompletionEventCode = x.CompletionEventCode, ApplicantUserId = x.ApplicantUserId, ApplicantName = x.ApplicantName, SchemaJson = x.SchemaJson, DataJson = x.DataJson, Status = x.Status, RejectionReason = x.RejectionReason, CreatedAt = x.CreatedAt, SubmittedAt = x.SubmittedAt };
}

public sealed class FreeSqlSimpleFormWorkflowSnapshotRepository(IFreeSql fsql) : ISimpleFormWorkflowSnapshotRepository
{
    public SimpleFormWorkflowSnapshot? GetByWorkflowInstanceId(Guid workflowInstanceId) => fsql.Select<SimpleFormWorkflowSnapshotRecord>().Where(x => x.WorkflowInstanceId == workflowInstanceId).ToList().Select(ToDomain).FirstOrDefault();
    public void Add(SimpleFormWorkflowSnapshot item)
    {
        try { fsql.Insert(ToRecord(item)).ExecuteAffrows(); }
        catch (Exception) when (GetByWorkflowInstanceId(item.WorkflowInstanceId) is not null) { }
    }
    private static SimpleFormWorkflowSnapshot ToDomain(SimpleFormWorkflowSnapshotRecord x) => new(x.Id, x.WorkflowInstanceId, x.SubmissionId, x.DefinitionCode, x.ApplicantName, x.FormVersionNumber, x.SchemaJson, x.DataJson, x.CreatedAt);
    private static SimpleFormWorkflowSnapshotRecord ToRecord(SimpleFormWorkflowSnapshot x) => new() { Id = x.Id, WorkflowInstanceId = x.WorkflowInstanceId, SubmissionId = x.SubmissionId, DefinitionCode = x.DefinitionCode, ApplicantName = x.ApplicantName, FormVersionNumber = x.FormVersionNumber, SchemaJson = x.SchemaJson, DataJson = x.DataJson, CreatedAt = x.CreatedAt };
}

public sealed class FreeSqlSimpleFormCompletionEventRepository(IFreeSql fsql) : ISimpleFormCompletionEventRepository
{
    public bool TryAdd(PersistedSimpleFormCompletionEvent item)
    {
        try { return fsql.Insert(ToRecord(item)).ExecuteAffrows() == 1; }
        catch when (fsql.Select<SimpleFormCompletionEventRecord>().Where(x => x.SubmissionId == item.SubmissionId && x.EventCode == item.EventCode && x.SubmissionStatus == item.SubmissionStatus).Any()) { return false; }
    }
    public IReadOnlyList<PersistedSimpleFormCompletionEvent> ListPending(int take) => fsql.Select<SimpleFormCompletionEventRecord>().Where(x => x.Status == SimpleFormCompletionEventStatus.Pending).OrderBy(x => x.CreatedAt).Take(take).ToList().Select(ToApplication).ToArray();
    public void MarkDelivered(Guid id, DateTime deliveredAt) { if (fsql.Update<SimpleFormCompletionEventRecord>().Set(x => x.Status, SimpleFormCompletionEventStatus.Delivered).Set(x => x.DeliveredAt, deliveredAt).Where(x => x.Id == id && x.Status == SimpleFormCompletionEventStatus.Pending).ExecuteAffrows() == 0) throw new InvalidOperationException("简单表单完成事件不存在或已处理。"); }
    public void MarkFailed(Guid id, string error, DateTime attemptedAt) { if (fsql.Update<SimpleFormCompletionEventRecord>().SetRaw("\"RetryCount\" = \"RetryCount\" + 1").Set(x => x.LastError, error).Where(x => x.Id == id && x.Status == SimpleFormCompletionEventStatus.Pending).ExecuteAffrows() == 0) throw new InvalidOperationException("简单表单完成事件不存在或已处理。"); }
    private static PersistedSimpleFormCompletionEvent ToApplication(SimpleFormCompletionEventRecord x) => new(x.Id, x.SubmissionId, x.EventCode, x.SubmissionStatus, x.ContextJson, x.Status, x.RetryCount, x.LastError, x.CreatedAt, x.DeliveredAt);
    private static SimpleFormCompletionEventRecord ToRecord(PersistedSimpleFormCompletionEvent x) => new() { Id = x.Id, SubmissionId = x.SubmissionId, EventCode = x.EventCode, SubmissionStatus = x.SubmissionStatus, ContextJson = x.ContextJson, Status = x.Status, RetryCount = x.RetryCount, LastError = x.LastError, CreatedAt = x.CreatedAt, DeliveredAt = x.DeliveredAt };
}
