using CommunityToolkit.Mvvm.ComponentModel;
using VSLoader.Models;

namespace VSLoader.ViewModels;

public sealed class ContextMenuCapabilityListItemViewModel : ObservableObject
{
    public ContextMenuCapabilityListItemViewModel(ContextMenuCapabilityDefinition definition)
    {
        Definition = definition.Clone();
    }

    public ContextMenuCapabilityDefinition Definition { get; }

    public string Name => Definition.Name;

    public string TypeDisplayName => Definition.Kind switch
    {
        ContextMenuCapabilityKinds.BuiltIn => "内建",
        ContextMenuCapabilityKinds.PowerShell => "PowerShell",
        ContextMenuCapabilityKinds.Web => "Web",
        _ => "不支持"
    };

    public bool IsBuiltIn => string.Equals(
        Definition.Kind,
        ContextMenuCapabilityKinds.BuiltIn,
        StringComparison.Ordinal);

    public bool CanDelete => !IsBuiltIn;

    public bool Enabled
    {
        get => Definition.Enabled;
        set
        {
            if (Definition.Enabled == value)
            {
                return;
            }

            Definition.Enabled = value;
            OnPropertyChanged();
        }
    }

    public bool ShowInShortcutList
    {
        get => Definition.ShowInShortcutList;
        set
        {
            if (Definition.ShowInShortcutList == value)
            {
                return;
            }

            Definition.ShowInShortcutList = value;
            OnPropertyChanged();
        }
    }

    public bool ShowInFactoryMap
    {
        get => Definition.ShowInFactoryMap;
        set
        {
            if (Definition.ShowInFactoryMap == value)
            {
                return;
            }

            Definition.ShowInFactoryMap = value;
            OnPropertyChanged();
        }
    }

    public void RefreshDisplay()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(TypeDisplayName));
        OnPropertyChanged(nameof(IsBuiltIn));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(Enabled));
        OnPropertyChanged(nameof(ShowInShortcutList));
        OnPropertyChanged(nameof(ShowInFactoryMap));
    }
}
