using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
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
    private bool _isPauseButtonEnabled;

    [ObservableProperty]
    private bool _isPlayButtonEnabled;

    [ObservableProperty]
    private bool _isStopButtonEnabled;

    [ObservableProperty]
    private bool _isStepForwardEnabled;

    [ObservableProperty]
    private bool _isPlaybackSpeedButtonEnabled;

    [ObservableProperty]
    private TimeSpan? _segmentLoopFrom;

    [ObservableProperty]
    private TimeSpan? _segmentLoopTo;

    [ObservableProperty]
    private bool _isSegmentLoopEnabled;

    [ObservableProperty]
    private BitmapImage? _thumbnail;

    public bool IsLoopingMediaEnabled
    {
        get
        {
            var m = Root.MediaElement;
            return m.LoopingBehavior == MediaPlaybackState.Play;
        }
        set
        {
            var m = Root.MediaElement;
            m.LoopingBehavior = value ? MediaPlaybackState.Play : MediaPlaybackState.Pause;
            OnPropertyChanged(nameof(IsLoopingMediaEnabled));
        }
    }

    internal override void OnApplicationLoaded()
    {
        base.OnApplicationLoaded();
        var m = Root.MediaElement;

        // Load user preference
        IsLoopingMediaEnabled = (MediaPlaybackState)Properties.Settings.Default.LoopingBehavior == MediaPlaybackState.Play;
        m.Volume = Properties.Settings.Default.Volume;
        m.IsMuted = Properties.Settings.Default.IsMuted;

        m.WhenChanged(
            () =>
            {
                PlayButtonVisibility = !m.IsPlaying
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                IsPlayButtonEnabled = m.IsOpen && !m.IsPlaying && !m.IsSeeking && !m.IsChanging;
            },
            nameof(m.IsOpen),
            nameof(m.IsPlaying),
            nameof(m.HasMediaEnded),
            nameof(m.IsSeeking),
            nameof(m.IsChanging));

        m.WhenChanged(
            () =>
            {
                PauseButtonVisibility = m.CanPause && m.IsPlaying
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                IsPauseButtonEnabled = m.IsOpen && m.CanPause && m.IsPlaying;
            },
            nameof(m.CanPause),
            nameof(m.IsPlaying));

        m.WhenChanged(
            () =>
            {
                IsStopButtonEnabled = m.IsOpen && !m.IsChanging && !m.IsSeeking;
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
                IsStepForwardEnabled = m.IsOpen && !m.HasMediaEnded && !m.IsSeeking && m.IsPaused;
            },
            nameof(m.IsOpen),
            nameof(m.IsSeeking),
            nameof(m.HasMediaEnded),
            nameof(m.IsPaused));

        m.WhenChanged(
            () =>
            {
                IsSegmentLoopEnabled = false;
                SegmentLoopFrom = null;
                SegmentLoopTo = null;
            },
            nameof(m.IsOpen));

        m.WhenChanged(() =>
        {
            IsPlaybackSpeedButtonEnabled = m.IsOpen && !m.IsSeeking;
        },
        nameof(m.IsOpen),
        nameof(m.IsSeeking));


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
