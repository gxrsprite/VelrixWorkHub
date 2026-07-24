using FreeSql;
using VelrixWorkHub.Application.Overtime;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Overtime;

public sealed class FreeSqlOvertimeConversionRepository(IFreeSql fsql) : IOaOvertimeConversionRepository
{
    public IReadOnlyList<OaOvertimeConversion> List(Guid? userId = null)
    {
        var query = fsql.Select<OaOvertimeConversionRecord>();
        if (userId is Guid id) query = query.Where(x => x.UserId == id);
        return query.OrderByDescending(x => x.CreatedAt).ToList().Select(ToDomain).ToArray();
    }

    public OaOvertimeConversion? GetByOvertimeRequest(Guid overtimeRequestId) => fsql.Select<OaOvertimeConversionRecord>()
        .Where(x => x.OvertimeRequestId == overtimeRequestId).ToList().Select(ToDomain).FirstOrDefault();
    public OaOvertimeConversion? Get(Guid id) => fsql.Select<OaOvertimeConversionRecord>().Where(x => x.Id == id).ToList().Select(ToDomain).FirstOrDefault();

    public void Add(OaOvertimeConversion item) => fsql.Insert(new OaOvertimeConversionRecord
    {
        Id = item.Id, OvertimeRequestId = item.OvertimeRequestId, UserId = item.UserId,
        Type = item.Type, Hours = item.Hours, CreatedAt = item.CreatedAt
    }).ExecuteAffrows();

    public void Update(OaOvertimeConversion item)
    {
        var rows = fsql.Update<OaOvertimeConversionRecord>()
            .Set(x => x.FinanceProcessingStatus, item.FinanceProcessingStatus)
            .Set(x => x.FinanceProcessedBy, item.FinanceProcessedBy)
            .Set(x => x.FinanceProcessedAt, item.FinanceProcessedAt)
            .Set(x => x.FinanceProcessingNote, item.FinanceProcessingNote)
            .Where(x => x.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("加班费财务处理记录不存在或已被删除。");
    }

    private static OaOvertimeConversion ToDomain(OaOvertimeConversionRecord item)
    {
        var conversion = new OaOvertimeConversion(item.OvertimeRequestId, item.UserId, item.Type, item.Hours, item.CreatedAt) { Id = item.Id };
        conversion.RestoreFinanceProcessingForRecovery(item.FinanceProcessingStatus, item.FinanceProcessedBy, item.FinanceProcessedAt, item.FinanceProcessingNote);
        return conversion;
    }
}
