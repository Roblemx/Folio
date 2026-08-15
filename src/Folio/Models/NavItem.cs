using CommunityToolkit.Mvvm.ComponentModel;

namespace Folio.Models;

/// <summary>A sidebar navigation entry.</summary>
public sealed partial class NavItem : ObservableObject
{
    [ObservableProperty]
    private bool _isActive;

    public NavItem(string title, string iconKey)
    {
        Title = title;
        IconKey = iconKey;
    }

    public string Title { get; }

    /// <summary>Key into the icon geometry resources (added in Phase 4).</summary>
    public string IconKey { get; }
}
