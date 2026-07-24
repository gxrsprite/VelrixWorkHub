using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Lms;
public sealed class LmsLicenseProductService(ILmsLicenseProductRepository repository)
{
    public IReadOnlyList<LmsLicenseProduct> List(bool includeDisabled = true) => repository.List().Where(x => includeDisabled || x.Status == LmsLicenseProductStatus.Active).OrderBy(x => x.Code).ToArray();
    public LmsLicenseProduct Create(string code, string name, string? description, string? otherInfo) { var item = new LmsLicenseProduct(code, name, description, otherInfo, DateTime.Now); if (repository.List().Any(x => x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("许可证产品编码已存在。"); repository.Add(item); return item; }
    public void Edit(LmsLicenseProduct item, string code, string name, string? description, string? otherInfo) { item.Edit(code, name, description, otherInfo); if (repository.List().Any(x => x.Id != item.Id && x.Code.Equals(item.Code, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("许可证产品编码已存在。"); repository.Update(item); }
    public void SetStatus(LmsLicenseProduct item, LmsLicenseProductStatus status) { item.SetStatus(status); repository.Update(item); }
    public void EnsureActiveProductName(string productName) { var product = repository.List().FirstOrDefault(x => x.Name.Equals(productName.Trim(), StringComparison.OrdinalIgnoreCase)); if (product is null || product.Status != LmsLicenseProductStatus.Active) throw new InvalidOperationException("许可证产品不存在或已停用，不能新建申请。"); }
}
