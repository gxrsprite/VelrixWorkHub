using System.Text.Json;
using System.Text.RegularExpressions;

namespace VelrixWorkHub.Domain;

public enum SimpleFormFieldControl { Text, MultiLineText, Select, Radio, MultiSelect, Checkbox, DepartmentPicker, PersonPicker, ReferencePicker }
public enum SimpleFormFieldWidth { Half, Full }
public enum SimpleFormDefinitionVersionStatus { Draft, Published, Archived }
public enum SimpleFormSubmissionStatus { Draft, Submitted, Approved, Rejected, Cancelled }
public enum SimpleFormCompletionEventStatus { Pending, Delivered, Failed }

public sealed record SimpleFormOption(string Value, string Label);
public sealed record SimpleFormFieldSchema(string Key, string Label, string? Description, SimpleFormFieldControl Control, SimpleFormFieldWidth Width, bool Required, IReadOnlyList<SimpleFormOption>? Options = null, string? Source = null);
public sealed record SimpleFormSchema(string Title, IReadOnlyList<SimpleFormFieldSchema> Fields)
{
    private static readonly Regex FieldKey = new("^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant);

    public static SimpleFormSchema Parse(string schemaJson)
    {
        try
        {
            var schema = JsonSerializer.Deserialize<SimpleFormSchema>(schemaJson, JsonSerializationDefaults.CreateWeb()) ?? throw new ArgumentException("表单定义不能为空。", nameof(schemaJson));
            schema.Validate();
            return schema;
        }
        catch (JsonException exception) { throw new ArgumentException("表单定义必须是有效 JSON。", nameof(schemaJson), exception); }
    }

    public static string Normalize(string schemaJson)
    {
        var schema = Parse(schemaJson);
        return JsonSerializer.Serialize(schema, JsonSerializationDefaults.CreateWeb());
    }

    public IReadOnlyList<SimpleFormLayoutRow> GetLayoutRows()
    {
        var rows = new List<SimpleFormLayoutRow>();
        SimpleFormFieldSchema? pending = null;
        foreach (var field in Fields)
        {
            if (field.Width == SimpleFormFieldWidth.Full)
            {
                if (pending is not null) { rows.Add(new SimpleFormLayoutRow([pending], true)); pending = null; }
                rows.Add(new SimpleFormLayoutRow([field], true));
            }
            else if (pending is null) pending = field;
            else { rows.Add(new SimpleFormLayoutRow([pending, field], false)); pending = null; }
        }
        if (pending is not null) rows.Add(new SimpleFormLayoutRow([pending], true));
        return rows;
    }

    public void ValidateData(string dataJson)
    {
        using var document = ParseObject(dataJson, "表单数据");
        var root = document.RootElement;
        var fields = Fields.ToDictionary(x => x.Key, StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject()) if (!fields.ContainsKey(property.Name)) throw new ArgumentException($"表单数据包含未定义字段：{property.Name}。", nameof(dataJson));
        foreach (var field in Fields)
        {
            var hasValue = root.TryGetProperty(field.Key, out var value) && value.ValueKind != JsonValueKind.Null;
            if (field.Required && !hasValue) throw new ArgumentException($"字段“{field.Label}”不能为空。", nameof(dataJson));
            if (!hasValue) continue;
            ValidateValue(field, value, dataJson);
        }
    }

    public IReadOnlyList<SimpleFormDisplayValue> GetDisplayValues(string dataJson)
    {
        ValidateData(dataJson);
        using var document = ParseObject(dataJson, "表单数据");
        var result = new List<SimpleFormDisplayValue>();
        foreach (var field in Fields)
        {
            if (!document.RootElement.TryGetProperty(field.Key, out var value) || value.ValueKind == JsonValueKind.Null) continue;
            result.Add(new SimpleFormDisplayValue(field.Key, field.Label, FormatDisplayValue(field, value)));
        }
        return result;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Title) || Title.Trim().Length > 200) throw new ArgumentException("表单标题不能为空且不能超过 200 个字符。");
        if (Fields is null || Fields.Count == 0) throw new ArgumentException("表单至少需要一个字段。");
        if (Fields.Count > 100) throw new ArgumentException("表单字段不能超过 100 个。");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in Fields)
        {
            if (!FieldKey.IsMatch(field.Key ?? string.Empty)) throw new ArgumentException("字段 key 只能由字母、数字和下划线组成，且必须以字母开头。");
            if (!keys.Add(field.Key!)) throw new ArgumentException($"字段 key 重复：{field.Key}。");
            if (string.IsNullOrWhiteSpace(field.Label) || field.Label.Trim().Length > 100) throw new ArgumentException($"字段“{field.Key}”的名称不能为空且不能超过 100 个字符。");
            if ((field.Description?.Length ?? 0) > 1000) throw new ArgumentException($"字段“{field.Key}”的描述不能超过 1000 个字符。");
            var options = field.Options ?? [];
            if (field.Control is SimpleFormFieldControl.Select or SimpleFormFieldControl.Radio or SimpleFormFieldControl.MultiSelect or SimpleFormFieldControl.ReferencePicker)
            {
                if (options.Count == 0) throw new ArgumentException($"字段“{field.Label}”必须配置选项。");
                if (options.Count > 100) throw new ArgumentException($"字段“{field.Label}”的选项不能超过 100 个。");
                var values = new HashSet<string>(StringComparer.Ordinal);
                foreach (var option in options)
                {
                    if (string.IsNullOrWhiteSpace(option.Value) || string.IsNullOrWhiteSpace(option.Label)) throw new ArgumentException($"字段“{field.Label}”存在空选项。");
                    if (!values.Add(option.Value!)) throw new ArgumentException($"字段“{field.Label}”存在重复选项值：{option.Value}。");
                }
            }
            else if (options.Count > 0) throw new ArgumentException($"字段“{field.Label}”的控件不接受选项。");
            if (field.Control is SimpleFormFieldControl.DepartmentPicker or SimpleFormFieldControl.PersonPicker or SimpleFormFieldControl.ReferencePicker && string.IsNullOrWhiteSpace(field.Source))
                throw new ArgumentException($"字段“{field.Label}”必须指定受控引用来源。");
        }
    }

    private static JsonDocument ParseObject(string json, string label)
    {
        try
        {
            var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException($"{label}必须是 JSON 对象。");
            return document;
        }
        catch (JsonException exception) { throw new ArgumentException($"{label}必须是有效 JSON。", exception); }
    }

    private static void ValidateValue(SimpleFormFieldSchema field, JsonElement value, string parameterName)
    {
        if (field.Control is SimpleFormFieldControl.Text or SimpleFormFieldControl.MultiLineText)
        {
            if (value.ValueKind != JsonValueKind.String) throw new ArgumentException($"字段“{field.Label}”必须是文本。", parameterName);
            if (value.GetString()?.Length > (field.Control == SimpleFormFieldControl.Text ? 1000 : 10000)) throw new ArgumentException($"字段“{field.Label}”长度超出限制。", parameterName);
            return;
        }
        if (field.Control == SimpleFormFieldControl.Checkbox)
        {
            if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new ArgumentException($"字段“{field.Label}”必须是布尔值。", parameterName);
            return;
        }
        if (field.Control is SimpleFormFieldControl.Select or SimpleFormFieldControl.Radio)
        {
            if (value.ValueKind != JsonValueKind.String || !(field.Options ?? []).Any(x => x.Value == value.GetString())) throw new ArgumentException($"字段“{field.Label}”的选项无效。", parameterName);
            return;
        }
        if (field.Control == SimpleFormFieldControl.MultiSelect)
        {
            if (value.ValueKind != JsonValueKind.Array) throw new ArgumentException($"字段“{field.Label}”必须是选项数组。", parameterName);
            var options = (field.Options ?? []).Select(x => x.Value).ToHashSet(StringComparer.Ordinal);
            var values = value.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : null).ToArray();
            if (values.Any(x => string.IsNullOrWhiteSpace(x) || !options.Contains(x!)) || values.Distinct(StringComparer.Ordinal).Count() != values.Length) throw new ArgumentException($"字段“{field.Label}”包含无效或重复选项。", parameterName);
            return;
        }
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()) || !value.TryGetProperty("label", out var label) || label.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"字段“{field.Label}”必须是包含 id 和 label 的引用对象。", parameterName);
        if (field.Control == SimpleFormFieldControl.ReferencePicker)
        {
            var option = (field.Options ?? []).SingleOrDefault(x => x.Value == id.GetString());
            if (option is null || !string.Equals(option.Label, label.GetString(), StringComparison.Ordinal)) throw new ArgumentException($"字段“{field.Label}”引用选项无效。", parameterName);
        }
    }

    private static string FormatDisplayValue(SimpleFormFieldSchema field, JsonElement value)
    {
        if (field.Control == SimpleFormFieldControl.Checkbox) return value.GetBoolean() ? "是" : "否";
        if (field.Control == SimpleFormFieldControl.MultiSelect)
        {
            var labels = (field.Options ?? []).ToDictionary(x => x.Value, x => x.Label, StringComparer.Ordinal);
            return string.Join("、", value.EnumerateArray().Select(x => labels[x.GetString()!]));
        }
        if (field.Control is SimpleFormFieldControl.Select or SimpleFormFieldControl.Radio)
            return (field.Options ?? []).Single(x => x.Value == value.GetString()).Label;
        if (field.Control is SimpleFormFieldControl.DepartmentPicker or SimpleFormFieldControl.PersonPicker or SimpleFormFieldControl.ReferencePicker)
            return value.GetProperty("label").GetString() ?? string.Empty;
        return value.GetString() ?? string.Empty;
    }
}

public sealed record SimpleFormLayoutRow(IReadOnlyList<SimpleFormFieldSchema> Fields, bool IsFullWidth);
public sealed record SimpleFormDisplayValue(string Key, string Label, string Value);

public sealed class SimpleFormDefinition
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string WorkflowDefinitionCode { get; private set; } = string.Empty;
    public string CompletionEventCode { get; private set; } = string.Empty;
    public int? PublishedVersionNumber { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public SimpleFormDefinition(string code, string name, string? description, string workflowDefinitionCode, string? completionEventCode, DateTime createdAt)
    {
        CreatedAt = createdAt;
        Edit(name, description, workflowDefinitionCode, completionEventCode);
        Code = RequiredCode(code, "表单编码");
    }

    public void Edit(string name, string? description, string workflowDefinitionCode, string? completionEventCode)
    {
        Name = Required(name, "表单名称", 200);
        Description = (description ?? string.Empty).Trim();
        if (Description.Length > 2000) throw new ArgumentException("表单说明不能超过 2000 个字符。");
        WorkflowDefinitionCode = RequiredCode(workflowDefinitionCode, "流程编码");
        CompletionEventCode = string.IsNullOrWhiteSpace(completionEventCode) ? "NONE" : RequiredCode(completionEventCode, "完成事件编码");
    }

    public void Publish(int versionNumber) { if (versionNumber <= 0) throw new ArgumentOutOfRangeException(nameof(versionNumber)); PublishedVersionNumber = versionNumber; }
    private static string Required(string? value, string label, int max) => string.IsNullOrWhiteSpace(value) || value.Trim().Length > max ? throw new ArgumentException($"{label}不能为空且不能超过 {max} 个字符。") : value.Trim();
    private static string RequiredCode(string? value, string label) => string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value.Trim(), "^[A-Za-z][A-Za-z0-9_]{0,63}$") ? throw new ArgumentException($"{label}只能由字母、数字和下划线组成，且必须以字母开头。") : value.Trim().ToUpperInvariant();
}

public sealed class SimpleFormDefinitionVersion
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid DefinitionId { get; init; }
    public int VersionNumber { get; init; }
    public string SchemaJson { get; private set; } = "{}";
    public SimpleFormDefinitionVersionStatus Status { get; private set; } = SimpleFormDefinitionVersionStatus.Draft;
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public SimpleFormDefinitionVersion(Guid definitionId, int versionNumber, string schemaJson, DateTime createdAt) { if (definitionId == Guid.Empty || versionNumber <= 0) throw new ArgumentException("表单定义或版本无效。"); DefinitionId = definitionId; VersionNumber = versionNumber; CreatedAt = createdAt; SaveSchema(schemaJson); }
    public void SaveSchema(string schemaJson) { if (Status != SimpleFormDefinitionVersionStatus.Draft) throw new InvalidOperationException("只有草稿表单版本可以编辑。"); SchemaJson = SimpleFormSchema.Normalize(schemaJson); }
    public void Publish(DateTime publishedAt) { if (Status != SimpleFormDefinitionVersionStatus.Draft) throw new InvalidOperationException("只有草稿表单版本可以发布。"); _ = SimpleFormSchema.Parse(SchemaJson); Status = SimpleFormDefinitionVersionStatus.Published; PublishedAt = publishedAt; }
    public void Archive() { if (Status != SimpleFormDefinitionVersionStatus.Published) throw new InvalidOperationException("只有发布表单版本可以归档。"); Status = SimpleFormDefinitionVersionStatus.Archived; }
}

public sealed class SimpleFormSubmission
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid DefinitionId { get; init; }
    public string DefinitionCode { get; init; } = string.Empty;
    public int FormVersionNumber { get; init; }
    public string WorkflowDefinitionCode { get; init; } = string.Empty;
    public string CompletionEventCode { get; init; } = "NONE";
    public Guid ApplicantUserId { get; init; }
    public string ApplicantName { get; private set; } = string.Empty;
    public string SchemaJson { get; init; } = "{}";
    public string DataJson { get; private set; } = "{}";
    public SimpleFormSubmissionStatus Status { get; private set; } = SimpleFormSubmissionStatus.Draft;
    public string? RejectionReason { get; private set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SubmittedAt { get; private set; }
    public SimpleFormSubmission(Guid definitionId, string definitionCode, int formVersionNumber, string workflowDefinitionCode, string completionEventCode, Guid applicantUserId, string applicantName, string schemaJson, string dataJson, DateTime createdAt)
    { if (definitionId == Guid.Empty || applicantUserId == Guid.Empty || formVersionNumber <= 0) throw new ArgumentException("表单申请关联信息无效。"); DefinitionId = definitionId; DefinitionCode = definitionCode; FormVersionNumber = formVersionNumber; WorkflowDefinitionCode = workflowDefinitionCode; CompletionEventCode = completionEventCode; ApplicantUserId = applicantUserId; CreatedAt = createdAt; SchemaJson = SimpleFormSchema.Normalize(schemaJson); ApplicantName = string.IsNullOrWhiteSpace(applicantName) ? throw new ArgumentException("申请人不能为空。") : applicantName.Trim(); Edit(dataJson); }
    public void Edit(string dataJson) { if (Status is not (SimpleFormSubmissionStatus.Draft or SimpleFormSubmissionStatus.Rejected)) throw new InvalidOperationException("只有草稿或已驳回申请可以编辑。"); var schema = SimpleFormSchema.Parse(SchemaJson); schema.ValidateData(dataJson); DataJson = JsonObjectValue.Normalize(dataJson, nameof(dataJson)); }
    public void Submit(DateTime submittedAt) { if (Status is not (SimpleFormSubmissionStatus.Draft or SimpleFormSubmissionStatus.Rejected)) throw new InvalidOperationException("当前状态不能提交表单申请。"); Status = SimpleFormSubmissionStatus.Submitted; RejectionReason = null; SubmittedAt = submittedAt; }
    public void Approve() { if (Status != SimpleFormSubmissionStatus.Submitted) throw new InvalidOperationException("只有已提交申请才能批准。"); Status = SimpleFormSubmissionStatus.Approved; }
    public void Reject(string? reason) { if (Status != SimpleFormSubmissionStatus.Submitted) throw new InvalidOperationException("只有已提交申请才能驳回。"); Status = SimpleFormSubmissionStatus.Rejected; RejectionReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(); }
    public void Cancel() { if (Status is not (SimpleFormSubmissionStatus.Draft or SimpleFormSubmissionStatus.Submitted)) throw new InvalidOperationException("当前状态不能撤回表单申请。"); Status = SimpleFormSubmissionStatus.Cancelled; }
    public void SetStatus(SimpleFormSubmissionStatus status) => Status = status;
}

/// <summary>绑定到单个 Workflow 实例的不可变表单快照，避免重提编辑覆盖历史审批数据。</summary>
public sealed class SimpleFormWorkflowSnapshot
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public Guid WorkflowInstanceId { get; init; }
    public Guid SubmissionId { get; init; }
    public string DefinitionCode { get; init; } = string.Empty;
    public string ApplicantName { get; init; } = string.Empty;
    public int FormVersionNumber { get; init; }
    public string SchemaJson { get; init; } = "{}";
    public string DataJson { get; init; } = "{}";
    public DateTime CreatedAt { get; init; }

    public SimpleFormWorkflowSnapshot(Guid workflowInstanceId, SimpleFormSubmission submission, DateTime createdAt)
    {
        if (workflowInstanceId == Guid.Empty || submission.Id == Guid.Empty) throw new ArgumentException("表单流程快照关联信息无效。");
        WorkflowInstanceId = workflowInstanceId;
        SubmissionId = submission.Id;
        DefinitionCode = submission.DefinitionCode;
        ApplicantName = submission.ApplicantName;
        FormVersionNumber = submission.FormVersionNumber;
        SchemaJson = SimpleFormSchema.Normalize(submission.SchemaJson);
        SimpleFormSchema.Parse(SchemaJson).ValidateData(submission.DataJson);
        DataJson = JsonObjectValue.Normalize(submission.DataJson, nameof(submission));
        CreatedAt = createdAt;
    }

    public SimpleFormWorkflowSnapshot(Guid id, Guid workflowInstanceId, Guid submissionId, string definitionCode, string applicantName, int formVersionNumber, string schemaJson, string dataJson, DateTime createdAt)
    {
        if (id == Guid.Empty || workflowInstanceId == Guid.Empty || submissionId == Guid.Empty || formVersionNumber <= 0) throw new ArgumentException("表单流程快照关联信息无效。");
        Id = id;
        WorkflowInstanceId = workflowInstanceId;
        SubmissionId = submissionId;
        DefinitionCode = string.IsNullOrWhiteSpace(definitionCode) ? throw new ArgumentException("表单编码不能为空。") : definitionCode.Trim().ToUpperInvariant();
        ApplicantName = string.IsNullOrWhiteSpace(applicantName) ? throw new ArgumentException("申请人不能为空。") : applicantName.Trim();
        FormVersionNumber = formVersionNumber;
        SchemaJson = SimpleFormSchema.Normalize(schemaJson);
        SimpleFormSchema.Parse(SchemaJson).ValidateData(dataJson);
        DataJson = JsonObjectValue.Normalize(dataJson, nameof(dataJson));
        CreatedAt = createdAt;
    }
}
