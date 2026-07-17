using CommunityToolkit.Mvvm.ComponentModel;

namespace VSLoader.ViewModels;

public sealed partial class SettingsPageItemViewModel : ObservableObject
{
    public SettingsPageItemViewModel(string id, string title, bool isFixed = false)
    {
        Id = id;
        Title = title;
        IsFixed = isFixed;
    }

    public string Id { get; }

    public string Title { get; }

    public bool IsFixed { get; }

    [ObservableProperty]
    private bool canMoveUp;

    [ObservableProperty]
    private bool canMoveDown;
}
