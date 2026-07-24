using FreeSql;
using VelrixWorkHub.Application.Navigation;

namespace VelrixWorkHub.Infrastructure.Navigation;

public sealed class FreeSqlUserMenuPreferenceRepository(IFreeSql fsql) : IUserMenuPreferenceRepository
{
    public IReadOnlyList<UserMenuPreference> List(Guid userId)
        => fsql.Select<UserMenuPreferenceRecord>().Where(x => x.UserId == userId).ToList()
            .Select(x => new UserMenuPreference(x.UserId, x.MenuId, x.IsVisible, x.IsFavorite, x.Sort))
            .OrderBy(x => x.Sort)
            .ToArray();

    public void Upsert(UserMenuPreference preference)
    {
        var existing = fsql.Select<UserMenuPreferenceRecord>()
            .Where(x => x.UserId == preference.UserId && x.MenuId == preference.MenuId)
            .First();
        if (existing is null)
        {
            fsql.Insert(new UserMenuPreferenceRecord
            {
                Id = Guid.CreateVersion7(), UserId = preference.UserId, MenuId = preference.MenuId,
                IsVisible = preference.IsVisible, IsFavorite = preference.IsFavorite,
                Sort = preference.Sort, ModifiedTime = DateTime.Now
            }).ExecuteAffrows();
            return;
        }

        fsql.Update<UserMenuPreferenceRecord>()
            .Set(x => x.IsVisible, preference.IsVisible)
            .Set(x => x.IsFavorite, preference.IsFavorite)
            .Set(x => x.Sort, preference.Sort)
            .Set(x => x.ModifiedTime, DateTime.Now)
            .Where(x => x.Id == existing.Id)
            .ExecuteAffrows();
    }
}
