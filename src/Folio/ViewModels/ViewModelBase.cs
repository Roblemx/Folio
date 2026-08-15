using CommunityToolkit.Mvvm.ComponentModel;

namespace Folio.ViewModels;

/// <summary>Base type for all page/screen view models.</summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Called by the navigation service after this page becomes current.</summary>
    public virtual void OnNavigatedTo()
    {
    }
}
