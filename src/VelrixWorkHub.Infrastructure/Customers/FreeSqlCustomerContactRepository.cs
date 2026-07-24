using FreeSql;
using VelrixWorkHub.Application.Contacts;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public sealed class FreeSqlCustomerContactRepository(IFreeSql fsql) : ICustomerContactRepository
{
    public IReadOnlyList<CustomerContact> List() => fsql.Select<CustomerContactRecord>().OrderByDescending(item => item.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(CustomerContact item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(CustomerContact item)
    {
        var rows = fsql.Update<CustomerContactRecord>().Set(record => record.CustomerId, item.CustomerId).Set(record => record.Name, item.Name).Set(record => record.Position, item.Position).Set(record => record.Phone, item.Phone).Set(record => record.Email, item.Email).Set(record => record.IsPrimary, item.IsPrimary).Set(record => record.ModifiedTime, DateTime.Now).Where(record => record.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("联系人不存在或已被删除。");
    }
    public void ClearPrimary(Guid customerId, Guid exceptId) => fsql.Update<CustomerContactRecord>().Set(record => record.IsPrimary, false).Where(record => record.CustomerId == customerId && record.Id != exceptId).ExecuteAffrows();
    public void Remove(Guid id) => fsql.Delete<CustomerContactRecord>().Where(item => item.Id == id).ExecuteAffrows();
    private static CustomerContact ToDomain(CustomerContactRecord record) => new(record.CustomerId, record.Name, record.Position, record.Phone, record.Email, record.IsPrimary) { Id = record.Id };
    private static CustomerContactRecord ToRecord(CustomerContact item, DateTime created, DateTime modified) => new() { Id = item.Id, CustomerId = item.CustomerId, Name = item.Name, Position = item.Position, Phone = item.Phone, Email = item.Email, IsPrimary = item.IsPrimary, CreatedTime = created, ModifiedTime = modified };
}
