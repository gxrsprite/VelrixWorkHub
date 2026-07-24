namespace VelrixWorkHub.Application.Navigation;

public sealed record UserMenuPreference(Guid UserId, Guid MenuId, bool IsVisible, bool IsFavorite, int Sort = 0);

public interface IUserMenuPreferenceRepository
{
    IReadOnlyList<UserMenuPreference> List(Guid userId);
    void Upsert(UserMenuPreference preference);
}

public sealed class UserMenuPreferenceService(IUserMenuPreferenceRepository repository)
{
    public event Action? Changed;

    public IReadOnlyList<UserMenuPreference> List(Guid userId) => repository.List(userId);

    public void SetVisible(Guid userId, Guid menuId, bool visible)
    {
        var current = repository.List(userId).FirstOrDefault(x => x.MenuId == menuId);
        repository.Upsert(new UserMenuPreference(userId, menuId, visible, current?.IsFavorite ?? false, current?.Sort ?? 0));
        Changed?.Invoke();
    }

    public void SetFavorite(Guid userId, Guid menuId, bool favorite)
    {
        var current = repository.List(userId).FirstOrDefault(x => x.MenuId == menuId);
        repository.Upsert(new UserMenuPreference(userId, menuId, current?.IsVisible ?? true, favorite, current?.Sort ?? 0));
        Changed?.Invoke();
    }
}
