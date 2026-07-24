using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Suppliers;
public interface ISupplierRepository
{
    IReadOnlyList<Supplier> List();
    void Add(Supplier item);
    void Update(Supplier item);
    void Remove(Guid id);
}
