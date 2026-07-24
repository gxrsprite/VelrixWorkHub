using System.Text.Json;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Domain.Tests;

public sealed class JsonEnumSerializationTests
{
    [Fact]
    public void SharedOptionsSerializeEnumsAsNames()
    {
        var json = JsonSerializer.Serialize(new { Status = AnnouncementStatus.Published, Label = "审批流程" }, JsonSerializationDefaults.CreateWeb());

        Assert.Contains("\"status\":\"Published\"", json);
        Assert.Contains("审批流程", json);
        Assert.DoesNotContain("\\u", json);
        Assert.DoesNotContain("\"status\":1", json);
    }

    [Fact]
    public void WorkflowInstanceSnapshotSerializesNodeTypeAsName()
    {
        var definition = new WorkflowDefinition("JSON-ENUM", "JSON 枚举");
        var start = definition.AddNode(Guid.NewGuid(), WorkflowNodeType.Start, "开始");
        var end = definition.AddNode(Guid.NewGuid(), WorkflowNodeType.End, "结束");
        definition.Connect(start.Id, end.Id);
        definition.Publish();

        var instance = WorkflowInstance.Start(definition, "Test", Guid.NewGuid());

        Assert.Contains("\"type\":\"Start\"", instance.DefinitionSnapshotJson);
        Assert.DoesNotContain("\"type\":0", instance.DefinitionSnapshotJson);
    }
}
