using VelrixWorkHub.Application.Navigation;

namespace VelrixWorkHub.Domain.Tests;

public sealed class UserMenuPreferenceServiceTests
{
    [Fact]
    public void VisibilityAndFavoritePreferencesAreUserScopedAndPreserveEachOther()
    {
        var user = Guid.CreateVersion7();
        var otherUser = Guid.CreateVersion7();
        var menu = Guid.CreateVersion7();
        var repository = new PreferenceRepository();
        var service = new UserMenuPreferenceService(repository);

        service.SetVisible(user, menu, false);
        service.SetFavorite(user, menu, true);
        service.SetFavorite(otherUser, menu, true);

        var preference = Assert.Single(service.List(user));
        Assert.False(preference.IsVisible);
        Assert.True(preference.IsFavorite);
        Assert.True(Assert.Single(service.List(otherUser)).IsFavorite);
    }

    private sealed class PreferenceRepository : IUserMenuPreferenceRepository
    {
        private readonly List<UserMenuPreference> items = [];
        public IReadOnlyList<UserMenuPreference> List(Guid userId) => items.Where(x => x.UserId == userId).ToArray();
        public void Upsert(UserMenuPreference preference)
        {
            var index = items.FindIndex(x => x.UserId == preference.UserId && x.MenuId == preference.MenuId);
            if (index < 0) items.Add(preference); else items[index] = preference;
        }
    }
}
