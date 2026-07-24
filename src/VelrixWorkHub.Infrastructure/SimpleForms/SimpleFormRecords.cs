using FreeSql.DataAnnotations;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.SimpleForms;

[Table(Name = "SimpleFormDefinition")]
[Index("SimpleFormDefinition_uk_Code", nameof(Code), true)]
public sealed class SimpleFormDefinitionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(StringLength = 64, IsNullable = false, Position = 2)] public string Code { get; set; } = string.Empty;
    [Column(StringLength = 200, IsNullable = false, Position = 3)] public string Name { get; set; } = string.Empty;
    [Column(StringLength = 2000, IsNullable = false, Position = 4)] public string Description { get; set; } = string.Empty;
    [Column(StringLength = 64, IsNullable = false, Position = 5)] public string WorkflowDefinitionCode { get; set; } = string.Empty;
    [Column(StringLength = 64, IsNullable = false, Position = 6)] public string CompletionEventCode { get; set; } = "NONE";
    [Column(IsNullable = true, Position = 7)] public int? PublishedVersionNumber { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 8)] public DateTime CreatedAt { get; set; }
}

[Table(Name = "SimpleFormDefinitionVersion")]
[Index("SimpleFormDefinitionVersion_uk_DefinitionId_Version", nameof(DefinitionId) + "," + nameof(VersionNumber), true)]
public sealed class SimpleFormDefinitionVersionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid DefinitionId { get; set; }
    [Column(IsNullable = false, Position = 3)] public int VersionNumber { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 4)] public string SchemaJson { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 5)] public SimpleFormDefinitionVersionStatus Status { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 6)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 7)] public DateTime? PublishedAt { get; set; }
}

[Table(Name = "SimpleFormSubmission")]
[Index("SimpleFormSubmission_ix_Applicant", nameof(ApplicantUserId), false)]
public sealed class SimpleFormSubmissionRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid DefinitionId { get; set; }
    [Column(StringLength = 64, IsNullable = false, Position = 3)] public string DefinitionCode { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 4)] public int FormVersionNumber { get; set; }
    [Column(StringLength = 64, IsNullable = false, Position = 5)] public string WorkflowDefinitionCode { get; set; } = string.Empty;
    [Column(StringLength = 64, IsNullable = false, Position = 6)] public string CompletionEventCode { get; set; } = "NONE";
    [Column(IsNullable = false, Position = 7)] public Guid ApplicantUserId { get; set; }
    [Column(StringLength = 100, IsNullable = false, Position = 8)] public string ApplicantName { get; set; } = string.Empty;
    [Column(StringLength = -1, IsNullable = false, Position = 9)] public string SchemaJson { get; set; } = "{}";
    [Column(StringLength = -1, IsNullable = false, Position = 10)] public string DataJson { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 11)] public SimpleFormSubmissionStatus Status { get; set; }
    [Column(StringLength = 1000, IsNullable = true, Position = 12)] public string? RejectionReason { get; set; }
    [Column(IsNullable = false, ServerTime = DateTimeKind.Local, Position = 13)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 14)] public DateTime? SubmittedAt { get; set; }
}

[Table(Name = "SimpleFormWorkflowSnapshot")]
[Index("SimpleFormWorkflowSnapshot_uk_WorkflowInstanceId", nameof(WorkflowInstanceId), true)]
public sealed class SimpleFormWorkflowSnapshotRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid WorkflowInstanceId { get; set; }
    [Column(IsNullable = false, Position = 3)] public Guid SubmissionId { get; set; }
    [Column(StringLength = 64, IsNullable = false, Position = 4)] public string DefinitionCode { get; set; } = string.Empty;
    [Column(StringLength = 100, IsNullable = false, Position = 5)] public string ApplicantName { get; set; } = string.Empty;
    [Column(IsNullable = false, Position = 6)] public int FormVersionNumber { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 7)] public string SchemaJson { get; set; } = "{}";
    [Column(StringLength = -1, IsNullable = false, Position = 8)] public string DataJson { get; set; } = "{}";
    [Column(IsNullable = false, Position = 9)] public DateTime CreatedAt { get; set; }
}

[Table(Name = "SimpleFormCompletionEvent")]
[Index("SimpleFormCompletionEvent_uk_Submission_Event_Status", nameof(SubmissionId) + "," + nameof(EventCode) + "," + nameof(SubmissionStatus), true)]
public sealed class SimpleFormCompletionEventRecord
{
    [Column(IsPrimary = true, Position = 1)] public Guid Id { get; set; }
    [Column(IsNullable = false, Position = 2)] public Guid SubmissionId { get; set; }
    [Column(StringLength = 128, IsNullable = false, Position = 3)] public string EventCode { get; set; } = string.Empty;
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 4)] public SimpleFormSubmissionStatus SubmissionStatus { get; set; }
    [Column(StringLength = -1, IsNullable = false, Position = 5)] public string ContextJson { get; set; } = "{}";
    [Column(MapType = typeof(string), StringLength = 50, IsNullable = false, Position = 6)] public SimpleFormCompletionEventStatus Status { get; set; }
    [Column(IsNullable = false, Position = 7)] public int RetryCount { get; set; }
    [Column(StringLength = 2000, IsNullable = true, Position = 8)] public string? LastError { get; set; }
    [Column(IsNullable = false, Position = 9)] public DateTime CreatedAt { get; set; }
    [Column(IsNullable = true, Position = 10)] public DateTime? DeliveredAt { get; set; }
}
