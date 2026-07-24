using VelrixWorkHub.Domain;
namespace VelrixWorkHub.Application.Schedules;
public interface IWorkScheduleRepository { IReadOnlyList<WorkSchedule> List(); void Add(WorkSchedule schedule); void Update(WorkSchedule schedule); void Remove(Guid scheduleId); }
