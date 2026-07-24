using VelrixWorkHub.Domain;

namespace VelrixWorkHub.Application.Lms;

public interface ILmsCustomerMachineRepository
{
    IReadOnlyList<LmsCustomerMachine> List();
    void Add(LmsCustomerMachine item);
    void Update(LmsCustomerMachine item);
}
