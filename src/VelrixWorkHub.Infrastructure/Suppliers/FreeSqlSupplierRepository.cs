using FreeSql;
using VelrixWorkHub.Application.Suppliers;
using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Infrastructure.Suppliers;
public sealed class FreeSqlSupplierRepository(IFreeSql fsql) : ISupplierRepository
{
    public IReadOnlyList<Supplier> List() => fsql.Select<SupplierRecord>().OrderByDescending(x => x.CreatedTime).ToList().Select(ToDomain).ToArray();
    public void Add(Supplier item) { var now = DateTime.Now; fsql.Insert(ToRecord(item, now, now)).ExecuteAffrows(); }
    public void Update(Supplier item) { var rows = fsql.Update<SupplierRecord>().Set(x => x.Code, item.Code).Set(x => x.Name, item.Name).Set(x => x.ContactName, item.ContactName).Set(x => x.Phone, item.Phone).Set(x => x.Notes, item.Notes).Set(x => x.Status, item.Status).Set(x => x.QualificationStatus, item.QualificationStatus).Set(x => x.OtherInfo, item.OtherInfo).Set(x => x.ModifiedTime, DateTime.Now).Where(x => x.Id == item.Id).ExecuteAffrows(); if (rows == 0) throw new InvalidOperationException("供应商不存在或已被删除。"); }
    public void Remove(Guid id) => fsql.Delete<SupplierRecord>().Where(x => x.Id == id).ExecuteAffrows();
    private static Supplier ToDomain(SupplierRecord x) { var item = new Supplier(x.Code, x.Name, x.ContactName, x.Phone, x.Notes, x.OtherInfo) { Id = x.Id }; item.SetActive(x.Status == SupplierStatus.Active); item.SetQualification(x.QualificationStatus); return item; }
    private static SupplierRecord ToRecord(Supplier x, DateTime created, DateTime modified) => new() { Id = x.Id, Code = x.Code, Name = x.Name, ContactName = x.ContactName, Phone = x.Phone, Notes = x.Notes, Status = x.Status, QualificationStatus = x.QualificationStatus, OtherInfo = x.OtherInfo, CreatedTime = created, ModifiedTime = modified };
}
