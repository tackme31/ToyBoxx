using System.Windows;
using System.Windows.Controls;
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
            var m = App.ViewModel.MediaElement;
            return m.LoopingBehavior == MediaPlaybackState.Play;
        }
        set
        {
            var m = App.ViewModel.MediaElement;
            m.LoopingBehavior = value ? MediaPlaybackState.Play : MediaPlaybackState.Pause;
            NotifyPropertyChanged(nameof(IsLoopingMediaEnabled));
        }
    }

    private TimeSpan? _segmentLoopFrom;
    public TimeSpan? SegmentLoopFrom
    {
        get => _segmentLoopFrom;
        set => SetProperty(ref _segmentLoopFrom, value);
    }

    private TimeSpan? _segmentLoopTo;
    public TimeSpan? SegmentLoopTo
    {
        get => _segmentLoopTo;
        set => SetProperty(ref _segmentLoopTo, value);
    }

    private bool _isSegmentLoopEnabled;
    public bool IsSegmentLoopEnabled
    {
        get => _isSegmentLoopEnabled;
        set => SetProperty(ref _isSegmentLoopEnabled, value);
    }

    public void SetLoopSegment()
    {
        if (IsLoopingMediaEnabled && SegmentLoopFrom is not null && SegmentLoopTo is not null)
        {
            IsSegmentLoopEnabled = false;
            SegmentLoopFrom = null;
            SegmentLoopTo = null;
            return;
        }

        var currentPosition = App.ViewModel.MediaElement.Position;
        if (SegmentLoopFrom is null)
        {
            IsSegmentLoopEnabled = false;
            SegmentLoopFrom = currentPosition;
            SegmentLoopTo = null;
            return;
        }

        if (SegmentLoopFrom >= currentPosition)
        {
            return;
        }

        SegmentLoopTo = currentPosition;
        IsSegmentLoopEnabled = true;
    }

    internal override void OnApplicationLoaded()
    {
        base.OnApplicationLoaded();
        var m = App.ViewModel.MediaElement;

        // Load user preference
        m.LoopingBehavior = (MediaPlaybackState)Properties.Settings.Default.LoopingBehavior;
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

        m.PositionChanged += (sender, args) =>
        {
            if (!IsLoopingMediaEnabled)
            {
                return;
            }

            if (SegmentLoopFrom is null || SegmentLoopTo is null)
            {
                return;
            }

            if (SegmentLoopTo < args.Position || args.Position < SegmentLoopFrom)
            {
                m.Seek(SegmentLoopFrom.Value);
            }
        };
    }
}
