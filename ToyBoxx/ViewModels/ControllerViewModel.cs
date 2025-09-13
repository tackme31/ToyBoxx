using System.Windows;
using ToyBoxx.Foundation;
using Unosquare.FFME.Common;

namespace ToyBoxx.ViewModels;

public sealed class ControllerViewModel : AttachedViewModel
{
    internal ControllerViewModel(RootViewModel root) : base(root)
    {
    }

    private Visibility _pauseButtonVisibility;
    public Visibility PauseButtonVisibility
    {
        get => _pauseButtonVisibility;
        set => SetProperty(ref _pauseButtonVisibility, value);
    }

    private Visibility _playButtonVisibility;
    public Visibility PlayButtonVisibility
    {
        get => _playButtonVisibility;
        set => SetProperty(ref _playButtonVisibility, value);
    }

    private Visibility _stopButtonVisibility;
    public Visibility StopButtonVisibility
    {
        get => _stopButtonVisibility;
        set => SetProperty(ref _stopButtonVisibility, value);
    }

    public bool IsLoopingMediaEnabled
    {
        get
        {
            var m = App.ViewModel.MediaElement.Value;
            return m.LoopingBehavior == MediaPlaybackState.Play;
        }
        set
        {
            var m = App.ViewModel.MediaElement.Value;
            m.LoopingBehavior = value ? MediaPlaybackState.Play : MediaPlaybackState.Pause;
            NotifyPropertyChanged(nameof(IsLoopingMediaEnabled));
        }
    }

    internal override void OnApplicationLoaded()
    {
        base.OnApplicationLoaded();
        var m = App.ViewModel.MediaElement.Value;

        m.WhenChanged(
            () => PauseButtonVisibility = m.CanPause && m.IsPlaying ? Visibility.Visible : Visibility.Collapsed,
            nameof(m.CanPause),
            nameof(m.IsPlaying));

        m.WhenChanged(
            () =>
            {
                PlayButtonVisibility = m.IsOpen && !m.IsPlaying && !m.HasMediaEnded && !m.IsSeeking && !m.IsChanging
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            },
            nameof(m.IsOpen),
            nameof(m.IsPlaying),
            nameof(m.HasMediaEnded),
            nameof(m.IsSeeking),
            nameof(m.IsChanging));

        m.WhenChanged(
            () =>
            {
                StopButtonVisibility = m.IsOpen && !m.IsChanging && !m.IsSeeking && (m.HasMediaEnded || (m.IsSeekable && m.MediaState != MediaPlaybackState.Stop))
                ? Visibility.Visible
                : Visibility.Hidden;
            },
            nameof(m.IsOpen),
            nameof(m.HasMediaEnded),
            nameof(m.IsSeekable),
            nameof(m.MediaState),
            nameof(m.IsChanging),
            nameof(m.IsSeeking));
    }

}
