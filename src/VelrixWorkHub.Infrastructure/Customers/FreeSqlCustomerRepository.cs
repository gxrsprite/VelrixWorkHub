using FreeSql;
using VelrixWorkHub.Application.Customers;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Customers;
public sealed class FreeSqlCustomerRepository(IFreeSql fsql) : ICustomerRepository
{
    public IReadOnlyList<Customer> List() => fsql.Select<CustomerRecord>().OrderByDescending(item => item.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(Customer item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(Customer item)
    {
        var rows = fsql.Update<CustomerRecord>().Set(record => record.Name, item.Name).Set(record => record.ContactName, item.ContactName).Set(record => record.Phone, item.Phone).Set(record => record.Email, item.Email).Set(record => record.Notes, item.Notes).Set(record => record.Status, item.Status).Set(record => record.OtherInfo, item.OtherInfo).Set(record => record.ModifiedTime, DateTime.Now).Where(record => record.Id == item.Id).ExecuteAffrows();
        if (rows == 0) throw new InvalidOperationException("客户不存在或已被删除。");
    }
    public void Remove(Guid id) => fsql.Delete<CustomerRecord>().Where(item => item.Id == id).ExecuteAffrows();
    private static Customer ToDomain(CustomerRecord record) { var item = new Customer(record.Name, record.ContactName, record.Phone, record.Email, record.Notes, record.OtherInfo) { Id = record.Id }; item.SetActive(record.Status == CustomerStatus.Active); return item; }
    private static CustomerRecord ToRecord(Customer item, DateTime created, DateTime modified) => new() { Id = item.Id, Name = item.Name, ContactName = item.ContactName, Phone = item.Phone, Email = item.Email, Notes = item.Notes, Status = item.Status, OtherInfo = item.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}
