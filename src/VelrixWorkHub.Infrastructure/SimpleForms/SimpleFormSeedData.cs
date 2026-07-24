using FreeSql;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.SimpleForms;

public static class SimpleFormSeedData
{
    public static void Initialize(IFreeSql fsql)
    {
        const string code = "SIMPLE_SEAL_REQUEST";
        if (fsql.Select<SimpleFormDefinitionRecord>().Where(x => x.Code == code).Any()) return;
        var definition = new SimpleFormDefinitionRecord { Id = Guid.CreateVersion7(), Code = code, Name = "印章申请", Description = "简单表单示例：审批通过后通知被申请人。", WorkflowDefinitionCode = WorkflowBindingCodes.SimpleSealRequestApproval, CompletionEventCode = "SEAL_REQUEST_NOTIFY_RECIPIENT", PublishedVersionNumber = 1, CreatedAt = DateTime.Now };
        const string schema = "{\"title\":\"印章申请\",\"fields\":[{\"key\":\"sealType\",\"label\":\"印章类型\",\"description\":\"选择需要使用的印章\",\"control\":\"Select\",\"width\":\"Half\",\"required\":true,\"options\":[{\"value\":\"Company\",\"label\":\"公司公章\"},{\"value\":\"Contract\",\"label\":\"合同章\"},{\"value\":\"Finance\",\"label\":\"财务章\"}]},{\"key\":\"recipient\",\"label\":\"被申请人\",\"description\":\"审批通过后向该人员发送通知\",\"control\":\"PersonPicker\",\"width\":\"Half\",\"required\":true,\"source\":\"Person\"},{\"key\":\"department\",\"label\":\"用印部门\",\"description\":\"可选；组织目录完成初始化后可选择\",\"control\":\"DepartmentPicker\",\"width\":\"Half\",\"required\":false,\"source\":\"Department\"},{\"key\":\"documentName\",\"label\":\"文件名称\",\"description\":\"\",\"control\":\"Text\",\"width\":\"Half\",\"required\":true},{\"key\":\"reason\",\"label\":\"申请事由\",\"description\":\"说明用印用途和必要性\",\"control\":\"MultiLineText\",\"width\":\"Full\",\"required\":true}]}";
        fsql.Insert(definition).ExecuteAffrows();
        fsql.Insert(new SimpleFormDefinitionVersionRecord { Id = Guid.CreateVersion7(), DefinitionId = definition.Id, VersionNumber = 1, SchemaJson = schema, Status = SimpleFormDefinitionVersionStatus.Published, CreatedAt = DateTime.Now, PublishedAt = DateTime.Now }).ExecuteAffrows();
    }
}
