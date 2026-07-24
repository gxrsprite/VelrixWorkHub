using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Products;
public interface IProductRepository
{
    IReadOnlyList<Product> List();
    void Add(Product item);
    void Update(Product item);
    void Remove(Guid id);
}
