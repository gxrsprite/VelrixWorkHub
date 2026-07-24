using FreeSql;
using VelrixWorkHub.Application.Lms;
using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Infrastructure.Lms;

public sealed class FreeSqlLmsCustomerMachineRepository(IFreeSql fsql) : ILmsCustomerMachineRepository
{
    public IReadOnlyList<LmsCustomerMachine> List() => fsql.Select<LmsCustomerMachineRecord>().ToList().Select(x =>
    {
        var item = new LmsCustomerMachine(x.CustomerId, x.MachineCode, x.ProductName, x.Model, x.Environment, x.OtherInfo, x.CreatedAt) { Id = x.Id };
        item.SetStatus(x.Status);
        return item;
    }).ToArray();

    public void Add(LmsCustomerMachine item) => fsql.Insert(new LmsCustomerMachineRecord
    {
        Id = item.Id, CustomerId = item.CustomerId, MachineCode = item.MachineCode, ProductName = item.ProductName,
        Model = item.Model, Environment = item.Environment, Status = item.Status, OtherInfo = item.OtherInfo, CreatedAt = item.CreatedAt
    }).ExecuteAffrows();

    public void Update(LmsCustomerMachine item)
    {
        if (fsql.Update<LmsCustomerMachineRecord>()
            .Set(x => x.MachineCode, item.MachineCode).Set(x => x.ProductName, item.ProductName)
            .Set(x => x.Model, item.Model).Set(x => x.Environment, item.Environment)
            .Set(x => x.Status, item.Status).Set(x => x.OtherInfo, item.OtherInfo)
            .Where(x => x.Id == item.Id).ExecuteAffrows() == 0)
        {
            throw new InvalidOperationException("客户机台不存在。");
        }
    }
}
