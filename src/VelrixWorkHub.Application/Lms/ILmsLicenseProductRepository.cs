using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Lms;
public interface ILmsLicenseProductRepository { IReadOnlyList<LmsLicenseProduct> List(); void Add(LmsLicenseProduct item); void Update(LmsLicenseProduct item); }
