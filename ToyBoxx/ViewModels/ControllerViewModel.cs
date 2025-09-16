using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using ToyBoxx.Foundation;
using Unosquare.FFME.Common;

namespace ToyBoxx.ViewModels;

public partial class ControllerViewModel : AttachedViewModel
{
    internal ControllerViewModel(RootViewModel root) : base(root)
    {
    }

    [ObservableProperty]
    private Visibility _pauseButtonVisibility;

    [ObservableProperty]
    private Visibility _playButtonVisibility;

    [ObservableProperty]
    private Visibility _stopButtonVisibility;

    [ObservableProperty]
    private TimeSpan? _segmentLoopFrom;

    [ObservableProperty]
    private TimeSpan? _segmentLoopTo;

    [ObservableProperty]
    private bool _isSegmentLoopEnabled;

    public bool IsLoopingMediaEnabled
    {
        get
        {
            var m = App.ViewModel.MediaElement;
            return m.LoopingBehavior == MediaPlaybackState.Play;
        }
        set
        {
            var m = App.ViewModel.MediaElement;
            m.LoopingBehavior = value ? MediaPlaybackState.Play : MediaPlaybackState.Pause;
            OnPropertyChanged(nameof(IsLoopingMediaEnabled));
        }
    }

    internal override void OnApplicationLoaded()
    {
        base.OnApplicationLoaded();
        var m = App.ViewModel.MediaElement;

        // Load user preference
        IsLoopingMediaEnabled = (MediaPlaybackState)Properties.Settings.Default.LoopingBehavior == MediaPlaybackState.Play;
        m.Volume = Properties.Settings.Default.Volume;
        m.IsMuted = Properties.Settings.Default.IsMuted;

        m.WhenChanged(
            () =>
            {
                PlayButtonVisibility = m.IsOpen && !m.IsPlaying && !m.IsSeeking && !m.IsChanging
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            },
            nameof(m.IsOpen),
            nameof(m.IsPlaying),
            nameof(m.HasMediaEnded),
            nameof(m.IsSeeking),
            nameof(m.IsChanging));

        m.WhenChanged(
            () => PauseButtonVisibility = m.CanPause && m.IsPlaying ? Visibility.Visible : Visibility.Collapsed,
            nameof(m.CanPause),
            nameof(m.IsPlaying));

        m.WhenChanged(
            () =>
            {
                StopButtonVisibility = m.IsOpen && !m.IsChanging && !m.IsSeeking
                ? Visibility.Visible
                : Visibility.Hidden;
            },
            nameof(m.IsOpen),
            nameof(m.HasMediaEnded),
            nameof(m.IsSeekable),
            nameof(m.MediaState),
            nameof(m.IsChanging),
            nameof(m.IsSeeking));

        m.WhenChanged(
            () =>
            {
                IsSegmentLoopEnabled = false;
                SegmentLoopFrom = null;
                SegmentLoopTo = null;
            },
            nameof(m.IsOpen));

        m.PositionChanged += async (sender, args) =>
        {
            if (!IsSegmentLoopEnabled)
            {
                return;
            }

            if (SegmentLoopFrom is null || SegmentLoopTo is null)
            {
                return;
            }

            if (SegmentLoopTo < args.Position || args.Position < SegmentLoopFrom)
            {
                await m.Seek(SegmentLoopFrom.Value);
            }
        };
    }
}
