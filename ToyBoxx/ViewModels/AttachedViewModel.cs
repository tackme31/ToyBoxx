using CommunityToolkit.Mvvm.ComponentModel;
using Unosquare.FFME.Common;

namespace ToyBoxx.ViewModels;

public abstract class AttachedViewModel : ObservableObject
{
    protected AttachedViewModel(RootViewModel root)
    {
        Root = root;
    }

    /// <summary>
    /// Gets the root VM this object belongs to.
    /// </summary>
    public RootViewModel Root { get; }

    internal virtual void OnApplicationLoaded()
    {
    }
}
