using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Lms;
public interface ILmsFeatureRepository { IReadOnlyList<LmsFeature> List(); void Add(LmsFeature item); void Update(LmsFeature item); }
