using System.Text.Json;
using FreeSql;
using VelrixWorkHub.Application.Workflow;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Workflow;

public sealed class FreeSqlWorkflowDefinitionRepository(IFreeSql fsql) : IWorkflowDefinitionRepository
{
    private static readonly JsonSerializerOptions JsonOptions = JsonSerializationDefaults.CreateWeb();

    public IReadOnlyList<WorkflowDefinition> List(string? code = null, WorkflowDefinitionStatus? status = null)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalizedCode = code.Trim().ToUpperInvariant();
            var records = fsql.Ado.DataType switch
            {
                DataType.PostgreSQL => fsql.Ado.Query<WorkflowDefinitionRecord>("SELECT * FROM \"WorkflowDefinition\" WHERE UPPER(\"Code\") = @Code", new { Code = normalizedCode }),
                DataType.SqlServer => fsql.Ado.Query<WorkflowDefinitionRecord>("SELECT * FROM [WorkflowDefinition] WHERE UPPER([Code]) = @Code", new { Code = normalizedCode }),
                DataType.Sqlite => fsql.Ado.Query<WorkflowDefinitionRecord>("SELECT * FROM \"WorkflowDefinition\" WHERE UPPER(\"Code\") = @Code", new { Code = normalizedCode }),
                _ => null
            };
            if (records is not null)
                return records.Select(ToDomain)
                    .Where(x => status is null || x.Status == status.Value)
                    .OrderBy(x => x.Code)
                    .ThenByDescending(x => x.VersionNumber)
                    .ToArray();
        }

        var query = fsql.Select<WorkflowDefinitionRecord>();
        if (status is not null) query = query.Where(x => x.Status == status.Value);
        return query.OrderBy(x => x.Code).OrderByDescending(x => x.VersionNumber).ToList().Select(ToDomain).ToArray();
    }

    public void Add(WorkflowDefinition definition)
    {
        if (!TryAdd(definition)) throw new WorkflowDefinitionVersionConflictException(definition.Code);
    }

    public bool TryAdd(WorkflowDefinition definition)
    {
        var record = ToRecord(definition);
        var affected = fsql.Ado.DataType switch
        {
            DataType.PostgreSQL => fsql.Ado.ExecuteNonQuery("""
                INSERT INTO "WorkflowDefinition" ("Id", "Code", "Name", "Description", "VersionNumber", "Status", "CreatedAt", "PublishedAt", "NodesJson", "ConnectionsJson")
                VALUES (@Id, @Code, @Name, @Description, @VersionNumber, @Status, @CreatedAt, @PublishedAt, @NodesJson, @ConnectionsJson)
                ON CONFLICT DO NOTHING;
                """, record),
            DataType.SqlServer => fsql.Ado.ExecuteNonQuery("""
                MERGE [WorkflowDefinition] WITH (HOLDLOCK) AS target
                USING (VALUES (@Id, @Code, @Name, @Description, @VersionNumber, @Status, @CreatedAt, @PublishedAt, @NodesJson, @ConnectionsJson))
                    AS source ([Id], [Code], [Name], [Description], [VersionNumber], [Status], [CreatedAt], [PublishedAt], [NodesJson], [ConnectionsJson])
                ON target.[Id] = source.[Id]
                    OR (target.[Code] = source.[Code] AND target.[VersionNumber] = source.[VersionNumber])
                WHEN NOT MATCHED THEN
                    INSERT ([Id], [Code], [Name], [Description], [VersionNumber], [Status], [CreatedAt], [PublishedAt], [NodesJson], [ConnectionsJson])
                    VALUES (source.[Id], source.[Code], source.[Name], source.[Description], source.[VersionNumber], source.[Status], source.[CreatedAt], source.[PublishedAt], source.[NodesJson], source.[ConnectionsJson]);
                """, record),
            // Existing SQLite fixtures rely on FreeSql auto-sync; retain that compatibility path.
            DataType.Sqlite => fsql.InsertOrUpdate<WorkflowDefinitionRecord>()
                .SetSource(record)
                .IfExistsDoNothing()
                .ExecuteAffrows(),
            _ => throw new NotSupportedException($"Workflow 定义 TryAdd 暂不支持数据库类型：{fsql.Ado.DataType}")
        };
        return affected == 1;
    }

    public void Update(WorkflowDefinition definition)
    {
        var record = ToRecord(definition);
        var rows = fsql.Update<WorkflowDefinitionRecord>().SetSource(record).Where(x => x.Id == definition.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("流程定义不存在或已被删除。");
    }

    public void Remove(Guid id)
    {
        var rows = fsql.Delete<WorkflowDefinitionRecord>().Where(x => x.Id == id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("流程定义不存在或已被删除。");
    }

    private static WorkflowDefinitionRecord ToRecord(WorkflowDefinition item) => new()
    {
        Id = item.Id, Code = item.Code, Name = item.Name, Description = item.Description, VersionNumber = item.VersionNumber,
        Status = item.Status, CreatedAt = item.CreatedAt, PublishedAt = item.PublishedAt,
        NodesJson = JsonSerializer.Serialize(item.Nodes.Select(WorkflowNodeDocument.FromDomain), JsonOptions),
        ConnectionsJson = JsonSerializer.Serialize(item.Connections.Select(WorkflowConnectionDocument.FromDomain), JsonOptions)
    };

    private static WorkflowDefinition ToDomain(WorkflowDefinitionRecord record)
    {
        var item = new WorkflowDefinition(record.Code, record.Name, record.VersionNumber, record.Description, record.CreatedAt) { Id = record.Id };
        foreach (var node in JsonSerializer.Deserialize<List<WorkflowNodeDocument>>(record.NodesJson, JsonOptions) ?? []) item.AddNode(node.Id, node.Type, node.Name, node.X, node.Y, node.ConfigJson);
        foreach (var connection in JsonSerializer.Deserialize<List<WorkflowConnectionDocument>>(record.ConnectionsJson, JsonOptions) ?? []) item.Connect(connection.SourceNodeId, connection.TargetNodeId, connection.ConditionKey);
        if (record.Status >= WorkflowDefinitionStatus.Published) item.Publish(record.PublishedAt);
        if (record.Status == WorkflowDefinitionStatus.Archived) item.Archive();
        return item;
    }

}
