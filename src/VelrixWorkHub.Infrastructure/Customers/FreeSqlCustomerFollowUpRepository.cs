using FreeSql;
using VelrixWorkHub.Application.FollowUps;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public sealed class FreeSqlCustomerFollowUpRepository(IFreeSql fsql) : ICustomerFollowUpRepository
{
    public IReadOnlyList<CustomerFollowUp> List() => fsql.Select<CustomerFollowUpRecord>().OrderByDescending(item => item.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(CustomerFollowUp item) { fsql.Insert(new CustomerFollowUpRecord { Id = item.Id, CustomerId = item.CustomerId, ContactId = item.ContactId, Type = item.Type, Content = item.Content, NextFollowUpDate = item.NextFollowUpDate?.ToDateTime(TimeOnly.MinValue), CreatedTime = item.CreatedTime }).ExecuteAffrows(); }
    public void Remove(Guid id) => fsql.Delete<CustomerFollowUpRecord>().Where(item => item.Id == id).ExecuteAffrows();
    private static CustomerFollowUp ToDomain(CustomerFollowUpRecord record) => new(record.CustomerId, record.ContactId, record.Type, record.Content, record.NextFollowUpDate is null ? null : DateOnly.FromDateTime(record.NextFollowUpDate.Value)) { Id = record.Id, CreatedTime = record.CreatedTime };
}
